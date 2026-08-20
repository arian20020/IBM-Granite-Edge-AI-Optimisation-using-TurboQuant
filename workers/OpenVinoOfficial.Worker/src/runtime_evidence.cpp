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
#include <fstream>
#include <iomanip>
#include <set>
#include <sstream>
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

void verify_manifest(const std::filesystem::path& root, const std::filesystem::path& manifest) {
    require_regular_no_reparse(manifest);
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
        if (std::filesystem::file_size(file) != length || sha256_file(file) != digest) {
            throw protocol_error("runtime integrity failed");
        }
    }
    if (expected.empty()) throw protocol_error("runtime integrity failed");

    std::set<std::string, std::less<>> actual;
    for (const auto& item : std::filesystem::recursive_directory_iterator(root)) {
        const DWORD attributes = GetFileAttributesW(item.path().c_str());
        if (attributes == INVALID_FILE_ATTRIBUTES || (attributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0) {
            throw protocol_error("runtime integrity failed");
        }
        if (item.is_regular_file()) {
            std::string relative = wide_to_utf8(std::filesystem::relative(item.path(), root).generic_wstring());
            if (relative != "worker-manifest.json") actual.insert(relative);
        } else if (!item.is_directory()) {
            throw protocol_error("runtime integrity failed");
        }
    }
    if (actual != expected) throw protocol_error("runtime integrity failed");
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

runtime_evidence initialize_verified_runtime() {
    const std::filesystem::path root = executable_directory();
    if (!SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_SYSTEM32 | LOAD_LIBRARY_SEARCH_USER_DIRS) ||
        !SetDllDirectoryW(L"") || AddDllDirectory(root.c_str()) == nullptr) {
        throw protocol_error("runtime loader hardening failed");
    }
    const std::filesystem::path manifest = root / L"worker-manifest.json";
    verify_manifest(root, manifest);

    const ov::Version runtime = ov::get_openvino_version();
    const ov::Version genai = ov::genai::get_version();
    runtime_evidence evidence{
        runtime.buildNumber == nullptr ? "" : runtime.buildNumber,
        genai.buildNumber == nullptr ? "" : genai.buildNumber,
        product_version(root / L"openvino_tokenizers.dll"),
        sha256_file(manifest)};
    if (evidence.runtime_build != "2026.3.0-22451-8a17657b995-releases/2026/3" ||
        evidence.genai_build != "2026.3.0.0-3277-bd8d6542e3c" ||
        evidence.tokenizers_build != "2026.3.0.0-703-183c6f25cda") {
        throw protocol_error("runtime identity mismatch");
    }
    return evidence;
}

void verify_loaded_module_closure(const std::filesystem::path& worker_root) {
    std::array<HMODULE, 2048> modules{};
    DWORD required = 0;
    if (!EnumProcessModules(GetCurrentProcess(), modules.data(),
                            static_cast<DWORD>(sizeof(modules)), &required) ||
        required > sizeof(modules)) {
        throw protocol_error("module inventory failed");
    }
    wchar_t windows_buffer[MAX_PATH]{};
    const UINT windows_length = GetWindowsDirectoryW(windows_buffer, MAX_PATH);
    if (windows_length == 0 || windows_length >= MAX_PATH) throw protocol_error("module inventory failed");
    const std::wstring windows_root = lower_path(std::filesystem::path(windows_buffer));
    const std::wstring closure_root = lower_path(worker_root);
    const std::size_t count = required / sizeof(HMODULE);
    std::wstring path_buffer(32768U, L'\0');
    for (std::size_t index = 0; index < count; ++index) {
        const DWORD length = GetModuleFileNameExW(GetCurrentProcess(), modules[index], path_buffer.data(),
                                                  static_cast<DWORD>(path_buffer.size()));
        if (length == 0 || length >= path_buffer.size()) throw protocol_error("module inventory failed");
        path_buffer.resize(length);
        const std::filesystem::path module(path_buffer);
        path_buffer.resize(32768U);
        if (!starts_with_path(module, windows_root) && !starts_with_path(module, closure_root)) {
            throw protocol_error("module escaped verified closure");
        }
    }
}

}  // namespace granite::official_worker
