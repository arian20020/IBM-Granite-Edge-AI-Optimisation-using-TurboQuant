#include "runtime_evidence.hpp"

#include "protocol.hpp"

#include <windows.h>
#include <bcrypt.h>
#include <psapi.h>
#include <winver.h>

#include <openvino/openvino.hpp>
#include <openvino/genai/version.hpp>

#include <algorithm>
#include <array>
#include <cwctype>
#include <cstring>
#include <fstream>
#include <iomanip>
#include <set>
#include <sstream>
#include <utility>
#include <vector>

namespace granite::official_worker {
namespace {

using json = nlohmann::json;

class algorithm_handle final {
public:
    algorithm_handle() {
        if (BCryptOpenAlgorithmProvider(&value_, BCRYPT_SHA256_ALGORITHM, nullptr, 0) < 0) {
            throw protocol_error("hash provider failed");
        }
    }
    ~algorithm_handle() { if (value_ != nullptr) BCryptCloseAlgorithmProvider(value_, 0); }
    operator BCRYPT_ALG_HANDLE() const noexcept { return value_; }
private:
    BCRYPT_ALG_HANDLE value_{};
};

class hash_handle final {
public:
    explicit hash_handle(BCRYPT_ALG_HANDLE algorithm) {
        if (BCryptCreateHash(algorithm, &value_, nullptr, 0, nullptr, 0, 0) < 0) {
            throw protocol_error("hash initialization failed");
        }
    }
    ~hash_handle() { if (value_ != nullptr) BCryptDestroyHash(value_); }
    operator BCRYPT_HASH_HANDLE() const noexcept { return value_; }
private:
    BCRYPT_HASH_HANDLE value_{};
};

std::wstring lower_path(std::filesystem::path path) {
    std::wstring value = std::filesystem::weakly_canonical(std::move(path)).wstring();
    std::transform(value.begin(), value.end(), value.begin(), [](wchar_t item) {
        return static_cast<wchar_t>(std::towlower(item));
    });
    if (!value.empty() && value.back() != L'\\') value.push_back(L'\\');
    return value;
}

bool starts_with_path(const std::filesystem::path& path, const std::wstring& root) {
    std::wstring value = std::filesystem::weakly_canonical(path).wstring();
    std::transform(value.begin(), value.end(), value.begin(), [](wchar_t item) {
        return static_cast<wchar_t>(std::towlower(item));
    });
    return value.size() >= root.size() && value.compare(0, root.size(), root) == 0;
}

void require_regular_no_reparse(const std::filesystem::path& path) {
    const DWORD attributes = GetFileAttributesW(path.c_str());
    if (attributes == INVALID_FILE_ATTRIBUTES ||
        (attributes & FILE_ATTRIBUTE_DIRECTORY) != 0 ||
        (attributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0) {
        throw protocol_error("runtime integrity failed");
    }
}

[[noreturn]] void integrity_failure() {
    throw protocol_error("runtime integrity failed");
}

HANDLE open_runtime_path(const std::filesystem::path& path, bool directory) {
    const DWORD flags = directory
        ? FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT
        : FILE_ATTRIBUTE_NORMAL | FILE_FLAG_RANDOM_ACCESS;
    const HANDLE handle = CreateFileW(
        path.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, flags, nullptr);
    if (handle == INVALID_HANDLE_VALUE) integrity_failure();
    return handle;
}

std::wstring final_path(HANDLE handle) {
    std::wstring value(512U, L'\0');
    for (;;) {
        const DWORD length = GetFinalPathNameByHandleW(
            handle, value.data(), static_cast<DWORD>(value.size()), FILE_NAME_NORMALIZED);
        if (length == 0U) integrity_failure();
        if (length < value.size()) {
            value.resize(length);
            constexpr std::wstring_view prefix = LR"(\\?\)";
            if (value.starts_with(prefix)) value.erase(0U, prefix.size());
            return std::filesystem::absolute(value).lexically_normal().wstring();
        }
        value.resize(static_cast<std::size_t>(length) + 1U);
    }
}

void require_final_contained(
    const std::filesystem::path& root,
    HANDLE handle,
    bool allow_root = false) {
    std::wstring approved = std::filesystem::absolute(root).lexically_normal().wstring();
    std::wstring actual = final_path(handle);
    std::transform(approved.begin(), approved.end(), approved.begin(), towlower);
    std::transform(actual.begin(), actual.end(), actual.begin(), towlower);
    if (allow_root && actual == approved) return;
    if (!approved.ends_with(L'\\')) approved.push_back(L'\\');
    if (!actual.starts_with(approved)) integrity_failure();
}

void require_no_alternate_streams(const std::filesystem::path& path) {
    WIN32_FIND_STREAM_DATA data{};
    const HANDLE find = FindFirstStreamW(path.c_str(), FindStreamInfoStandard, &data, 0);
    if (find == INVALID_HANDLE_VALUE) {
        const DWORD error = GetLastError();
        if (error == ERROR_HANDLE_EOF || error == ERROR_NO_MORE_FILES) return;
        integrity_failure();
    }
    bool invalid = false;
    do {
        if (std::wstring_view(data.cStreamName) != L"::$DATA") invalid = true;
    } while (!invalid && FindNextStreamW(find, &data));
    const DWORD error = GetLastError();
    FindClose(find);
    if (invalid || (error != ERROR_HANDLE_EOF && error != ERROR_NO_MORE_FILES)) {
        integrity_failure();
    }
}

void require_amd64(const std::filesystem::path& path) {
    std::ifstream stream(path, std::ios::binary);
    std::array<unsigned char, 64> header{};
    stream.read(reinterpret_cast<char*>(header.data()), header.size());
    if (stream.gcount() != static_cast<std::streamsize>(header.size()) ||
        header[0] != 'M' || header[1] != 'Z') {
        integrity_failure();
    }
    std::uint32_t offset = 0;
    std::memcpy(&offset, header.data() + 0x3cU, sizeof(offset));
    stream.seekg(offset);
    std::array<unsigned char, 6> pe{};
    stream.read(reinterpret_cast<char*>(pe.data()), pe.size());
    std::uint32_t signature = 0;
    std::uint16_t machine = 0;
    std::memcpy(&signature, pe.data(), sizeof(signature));
    std::memcpy(&machine, pe.data() + 4U, sizeof(machine));
    if (!stream || signature != 0x00004550U || machine != 0x8664U) integrity_failure();
}

std::string product_version(const std::filesystem::path& path) {
    DWORD ignored = 0;
    const DWORD length = GetFileVersionInfoSizeW(path.c_str(), &ignored);
    if (length == 0) throw protocol_error("runtime identity missing");
    std::vector<std::byte> data(length);
    if (!GetFileVersionInfoW(path.c_str(), 0, length, data.data())) {
        throw protocol_error("runtime identity unavailable");
    }
    struct language_and_codepage { WORD language; WORD codepage; };
    language_and_codepage* translations = nullptr;
    UINT translation_bytes = 0;
    if (!VerQueryValueW(data.data(), L"\\VarFileInfo\\Translation",
                        reinterpret_cast<void**>(&translations), &translation_bytes) ||
        translation_bytes < sizeof(language_and_codepage)) {
        throw protocol_error("runtime identity unavailable");
    }
    wchar_t query[64]{};
    swprintf_s(query, L"\\StringFileInfo\\%04x%04x\\ProductVersion",
               translations[0].language, translations[0].codepage);
    wchar_t* value = nullptr;
    UINT value_length = 0;
    if (!VerQueryValueW(data.data(), query, reinterpret_cast<void**>(&value), &value_length) ||
        value == nullptr || value_length <= 1U) {
        throw protocol_error("runtime identity unavailable");
    }
    return wide_to_utf8(std::wstring(value, value_length - 1U));
}

void verify_manifest(
    const std::filesystem::path& root,
    const std::filesystem::path& manifest,
    std::vector<void*>& handles) {
    require_regular_no_reparse(manifest);
    require_no_alternate_streams(manifest);
    const HANDLE manifest_handle = open_runtime_path(manifest, false);
    handles.push_back(manifest_handle);
    require_final_contained(root, manifest_handle);
    std::ifstream input(manifest, std::ios::binary);
    json document;
    try {
        input >> document;
    } catch (const json::exception&) {
        throw protocol_error("runtime integrity failed");
    }
    if (!document.is_object() || document.size() != 2U ||
        document.value("schemaVersion", 0) != 1 || !document.contains("files") ||
        !document["files"].is_array()) {
        throw protocol_error("runtime integrity failed");
    }

    std::set<std::string, std::less<>> expected;
    std::set<std::string, std::less<>> expected_directories;
    std::string previous;
    for (const json& entry : document["files"]) {
        if (!entry.is_object() || entry.size() != 3U ||
            !entry.contains("path") || !entry["path"].is_string() ||
            !entry.contains("length") || !entry["length"].is_number_unsigned() ||
            !entry.contains("sha256") || !entry["sha256"].is_string()) {
            throw protocol_error("runtime integrity failed");
        }
        const std::string relative = entry["path"].get<std::string>();
        const std::uintmax_t length = entry["length"].get<std::uintmax_t>();
        const std::string digest = entry["sha256"].get<std::string>();
        if (relative.empty() || relative == "worker-manifest.json" ||
            relative.find("..") != std::string::npos || relative.find(':') != std::string::npos ||
            relative.front() == '/' || relative.front() == '\\' || length == 0 ||
            !is_lower_sha256(digest) || (!previous.empty() && previous >= relative) ||
            !expected.insert(relative).second) {
            throw protocol_error("runtime integrity failed");
        }
        previous = relative;
        const std::filesystem::path file = root / utf8_to_wide(relative);
        require_regular_no_reparse(file);
        require_no_alternate_streams(file);
        const HANDLE handle = open_runtime_path(file, false);
        handles.push_back(handle);
        require_final_contained(root, handle);
        if (std::filesystem::file_size(file) != length || sha256_file(file) != digest) {
            throw protocol_error("runtime integrity failed");
        }
        if (file.extension() == L".exe" || file.extension() == L".dll") require_amd64(file);
        std::filesystem::path parent = std::filesystem::path(utf8_to_wide(relative)).parent_path();
        while (!parent.empty()) {
            expected_directories.insert(wide_to_utf8(parent.generic_wstring()));
            parent = parent.parent_path();
        }
    }
    if (expected.empty()) throw protocol_error("runtime integrity failed");

    std::set<std::string, std::less<>> actual;
    std::set<std::string, std::less<>> actual_directories;
    for (const auto& item : std::filesystem::recursive_directory_iterator(root)) {
        const DWORD attributes = GetFileAttributesW(item.path().c_str());
        if (attributes == INVALID_FILE_ATTRIBUTES || (attributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0) {
            throw protocol_error("runtime integrity failed");
        }
        if (item.is_regular_file()) {
            std::string relative = wide_to_utf8(std::filesystem::relative(item.path(), root).generic_wstring());
            if (relative != "worker-manifest.json") actual.insert(relative);
        } else if (item.is_directory()) {
            const std::string relative = wide_to_utf8(
                std::filesystem::relative(item.path(), root).generic_wstring());
            actual_directories.insert(relative);
            require_no_alternate_streams(item.path());
            const HANDLE handle = open_runtime_path(item.path(), true);
            handles.push_back(handle);
            require_final_contained(root, handle);
        } else {
            throw protocol_error("runtime integrity failed");
        }
    }
    if (actual != expected || actual_directories != expected_directories) {
        throw protocol_error("runtime integrity failed");
    }
}

void close_runtime_handles(std::vector<void*>& handles) noexcept {
    for (auto iterator = handles.rbegin(); iterator != handles.rend(); ++iterator) {
        if (*iterator != nullptr && *iterator != INVALID_HANDLE_VALUE) {
            CloseHandle(static_cast<HANDLE>(*iterator));
        }
    }
    handles.clear();
}

}  // namespace

nlohmann::ordered_json runtime_evidence::to_json() const {
    return {{"runtimeBuild", runtime_build},
            {"genAiBuild", genai_build},
            {"tokenizersBuild", tokenizers_build},
            {"workerManifestDigest", worker_manifest_digest}};
}

std::filesystem::path executable_directory() {
    std::wstring buffer(32768U, L'\0');
    const DWORD length = GetModuleFileNameW(nullptr, buffer.data(), static_cast<DWORD>(buffer.size()));
    if (length == 0 || length >= buffer.size()) throw protocol_error("worker root unavailable");
    buffer.resize(length);
    return std::filesystem::path(buffer).parent_path();
}

std::string sha256_file(const std::filesystem::path& path) {
    require_regular_no_reparse(path);
    algorithm_handle algorithm;
    hash_handle hash(algorithm);
    std::ifstream input(path, std::ios::binary);
    if (!input) throw protocol_error("file unreadable");
    std::array<char, 64U * 1024U> buffer{};
    while (input) {
        input.read(buffer.data(), static_cast<std::streamsize>(buffer.size()));
        const std::streamsize count = input.gcount();
        if (count > 0 && BCryptHashData(hash,
                reinterpret_cast<PUCHAR>(buffer.data()), static_cast<ULONG>(count), 0) < 0) {
            throw protocol_error("hash failed");
        }
    }
    if (!input.eof()) throw protocol_error("file unreadable");
    std::array<UCHAR, 32> digest{};
    if (BCryptFinishHash(hash, digest.data(), static_cast<ULONG>(digest.size()), 0) < 0) {
        throw protocol_error("hash failed");
    }
    std::ostringstream encoded;
    encoded << std::hex << std::setfill('0');
    for (UCHAR byte : digest) encoded << std::setw(2) << static_cast<unsigned>(byte);
    return encoded.str();
}

runtime_context::~runtime_context() { close_runtime_handles(handles_); }

runtime_context::runtime_context(runtime_context&& other) noexcept
    : evidence_(std::move(other.evidence_)),
      handles_(std::move(other.handles_)) {
    other.handles_.clear();
}

runtime_context& runtime_context::operator=(runtime_context&& other) noexcept {
    if (this != &other) {
        close_runtime_handles(handles_);
        evidence_ = std::move(other.evidence_);
        handles_ = std::move(other.handles_);
        other.handles_.clear();
    }
    return *this;
}

const runtime_evidence& runtime_context::evidence() const noexcept { return evidence_; }

runtime_context initialize_verified_runtime_at(
    const std::filesystem::path& worker_root,
    const std::function<void()>& after_handles_acquired) {
    runtime_context context;
    const std::filesystem::path root =
        std::filesystem::absolute(worker_root).lexically_normal();
    try {
        if (!SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_SYSTEM32 | LOAD_LIBRARY_SEARCH_USER_DIRS) ||
            !SetDllDirectoryW(L"") || AddDllDirectory(root.c_str()) == nullptr) {
            throw protocol_error("runtime loader hardening failed");
        }
        const DWORD root_attributes = GetFileAttributesW(root.c_str());
        if (root_attributes == INVALID_FILE_ATTRIBUTES ||
            (root_attributes & FILE_ATTRIBUTE_DIRECTORY) == 0 ||
            (root_attributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0) {
            integrity_failure();
        }
        require_no_alternate_streams(root);
        const HANDLE root_handle = open_runtime_path(root, true);
        context.handles_.push_back(root_handle);
        require_final_contained(root, root_handle, true);

        const std::filesystem::path manifest = root / L"worker-manifest.json";
        verify_manifest(root, manifest, context.handles_);
        if (after_handles_acquired) after_handles_acquired();

        const ov::Version runtime = ov::get_openvino_version();
        const ov::Version genai = ov::genai::get_version();
        context.evidence_ = {
            runtime.buildNumber == nullptr ? "" : runtime.buildNumber,
            genai.buildNumber == nullptr ? "" : genai.buildNumber,
            product_version(root / L"openvino_tokenizers.dll"),
            sha256_file(manifest)};
        if (context.evidence_.runtime_build !=
                "2026.3.0-22451-8a17657b995-releases/2026/3" ||
            context.evidence_.genai_build != "2026.3.0.0-3277-bd8d6542e3c" ||
            context.evidence_.tokenizers_build != "2026.3.0.0-703-183c6f25cda") {
            throw protocol_error("runtime identity mismatch");
        }
        return context;
    } catch (...) {
        close_runtime_handles(context.handles_);
        throw;
    }
}

runtime_context initialize_verified_runtime() {
    return initialize_verified_runtime_at(executable_directory(), {});
}

void verify_loaded_module_closure(const std::filesystem::path& worker_root) {
    const auto fail = []() -> void {
        throw worker_failure(
            "runtime_integrity_failed", true, "module escaped verified closure");
    };
    std::array<HMODULE, 2048> modules{};
    DWORD required = 0;
    if (!EnumProcessModules(GetCurrentProcess(), modules.data(),
                            static_cast<DWORD>(sizeof(modules)), &required) ||
        required > sizeof(modules)) {
        fail();
    }
    wchar_t windows_buffer[MAX_PATH]{};
    const UINT windows_length = GetWindowsDirectoryW(windows_buffer, MAX_PATH);
    if (windows_length == 0 || windows_length >= MAX_PATH) fail();
    const std::wstring windows_root = lower_path(std::filesystem::path(windows_buffer));
    const std::wstring closure_root = lower_path(worker_root);
    const std::size_t count = required / sizeof(HMODULE);
    std::wstring path_buffer(32768U, L'\0');
    for (std::size_t index = 0; index < count; ++index) {
        const DWORD length = GetModuleFileNameExW(GetCurrentProcess(), modules[index], path_buffer.data(),
                                                  static_cast<DWORD>(path_buffer.size()));
        if (length == 0 || length >= path_buffer.size()) fail();
        path_buffer.resize(length);
        const std::filesystem::path module(path_buffer);
        path_buffer.resize(32768U);
        if (!starts_with_path(module, windows_root) && !starts_with_path(module, closure_root)) {
            fail();
        }
    }
}

}  // namespace granite::official_worker
