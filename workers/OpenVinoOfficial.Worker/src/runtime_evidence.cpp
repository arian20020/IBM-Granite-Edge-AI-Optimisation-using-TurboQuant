#include "runtime_evidence.hpp"

#include "namespace_monitor.hpp"
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
#include <exception>
#include <fstream>
#include <iomanip>
#include <set>
#include <sstream>
#include <utility>
#include <vector>

namespace granite::official_worker {
namespace {

using json = nlohmann::json;

bool is_gpu_device(std::string_view device) noexcept {
    return device == "GPU" || device.starts_with("GPU.");
}

void verify_loaded_module_membership_only(const runtime_context& runtime);

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

[[noreturn]] void integrity_failure() {
    throw protocol_error("runtime integrity failed");
}

class scoped_runtime_handle final {
public:
    explicit scoped_runtime_handle(HANDLE value = INVALID_HANDLE_VALUE) noexcept
        : value_(value) {}
    ~scoped_runtime_handle() { reset(); }
    scoped_runtime_handle(scoped_runtime_handle&& other) noexcept
        : value_(other.release()) {}
    scoped_runtime_handle& operator=(scoped_runtime_handle&& other) noexcept {
        if (this != &other) {
            reset();
            value_ = other.release();
        }
        return *this;
    }
    scoped_runtime_handle(const scoped_runtime_handle&) = delete;
    scoped_runtime_handle& operator=(const scoped_runtime_handle&) = delete;
    [[nodiscard]] HANDLE get() const noexcept { return value_; }
    [[nodiscard]] HANDLE release() noexcept {
        const HANDLE result = value_;
        value_ = INVALID_HANDLE_VALUE;
        return result;
    }
private:
    void reset() noexcept {
        if (value_ != INVALID_HANDLE_VALUE && value_ != nullptr) CloseHandle(value_);
        value_ = INVALID_HANDLE_VALUE;
    }
    HANDLE value_;
};

HANDLE open_runtime_path(
    const std::filesystem::path& path,
    bool directory) {
    const DWORD flags = FILE_FLAG_OPEN_REPARSE_POINT |
        (directory ? FILE_FLAG_BACKUP_SEMANTICS : FILE_FLAG_RANDOM_ACCESS);
    const HANDLE handle = CreateFileW(
        path.c_str(), directory ? FILE_READ_ATTRIBUTES : GENERIC_READ,
        FILE_SHARE_READ, nullptr, OPEN_EXISTING, flags, nullptr);
    if (handle == INVALID_HANDLE_VALUE) integrity_failure();
    return handle;
}

HANDLE open_runtime_monitor_path(const std::filesystem::path& path) {
    const HANDLE handle = CreateFileW(
        path.c_str(),
        FILE_READ_ATTRIBUTES | FILE_LIST_DIRECTORY,
        FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
        nullptr,
        OPEN_EXISTING,
        FILE_FLAG_OPEN_REPARSE_POINT | FILE_FLAG_BACKUP_SEMANTICS |
            FILE_FLAG_OVERLAPPED,
        nullptr);
    if (handle == INVALID_HANDLE_VALUE) integrity_failure();
    return handle;
}

void require_handle_kind(HANDLE handle, bool directory) {
    FILE_ATTRIBUTE_TAG_INFO info{};
    if (!GetFileInformationByHandleEx(
            handle, FileAttributeTagInfo, &info, sizeof(info)) ||
        ((info.FileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0) != directory ||
        (info.FileAttributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0 ||
        info.ReparseTag != 0U) {
        integrity_failure();
    }
}

native_file_identity file_identity(HANDLE handle) {
    FILE_ID_INFO info{};
    if (!GetFileInformationByHandleEx(handle, FileIdInfo, &info, sizeof(info))) {
        integrity_failure();
    }
    native_file_identity result{};
    result.volume_serial = info.VolumeSerialNumber;
    std::copy(
        std::begin(info.FileId.Identifier),
        std::end(info.FileId.Identifier),
        result.file_id.begin());
    return result;
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

void require_amd64(HANDLE handle) {
    std::array<unsigned char, 64> header{};
    LARGE_INTEGER origin{};
    DWORD read = 0U;
    if (!SetFilePointerEx(handle, origin, nullptr, FILE_BEGIN) ||
        !ReadFile(handle, header.data(), static_cast<DWORD>(header.size()), &read, nullptr) ||
        read != header.size() ||
        header[0] != 'M' || header[1] != 'Z') {
        integrity_failure();
    }
    std::uint32_t offset = 0;
    std::memcpy(&offset, header.data() + 0x3cU, sizeof(offset));
    std::array<unsigned char, 6> pe{};
    LARGE_INTEGER pe_offset{};
    pe_offset.QuadPart = offset;
    read = 0U;
    if (!SetFilePointerEx(handle, pe_offset, nullptr, FILE_BEGIN) ||
        !ReadFile(handle, pe.data(), static_cast<DWORD>(pe.size()), &read, nullptr) ||
        read != pe.size()) {
        integrity_failure();
    }
    std::uint32_t signature = 0;
    std::uint16_t machine = 0;
    std::memcpy(&signature, pe.data(), sizeof(signature));
    std::memcpy(&machine, pe.data() + 4U, sizeof(machine));
    if (signature != 0x00004550U || machine != 0x8664U) integrity_failure();
}

std::pair<std::uintmax_t, std::string> digest_handle(HANDLE file) {
    LARGE_INTEGER size{};
    if (!GetFileSizeEx(file, &size) || size.QuadPart <= 0) integrity_failure();
    algorithm_handle algorithm;
    hash_handle hash(algorithm);
    std::array<unsigned char, 64U * 1024U> buffer{};
    LARGE_INTEGER origin{};
    if (!SetFilePointerEx(file, origin, nullptr, FILE_BEGIN)) integrity_failure();
    std::uintmax_t total = 0U;
    for (;;) {
        DWORD read = 0U;
        if (!ReadFile(file, buffer.data(), static_cast<DWORD>(buffer.size()), &read, nullptr)) {
            integrity_failure();
        }
        if (read == 0U) break;
        if (BCryptHashData(hash, buffer.data(), read, 0) < 0) integrity_failure();
        total += read;
    }
    std::array<UCHAR, 32> digest{};
    if (BCryptFinishHash(hash, digest.data(), static_cast<ULONG>(digest.size()), 0) < 0 ||
        total != static_cast<std::uintmax_t>(size.QuadPart)) {
        integrity_failure();
    }
    std::ostringstream encoded;
    encoded << std::hex << std::setfill('0');
    for (UCHAR byte : digest) encoded << std::setw(2) << static_cast<unsigned>(byte);
    return {total, encoded.str()};
}

std::string read_bounded_text(HANDLE handle, std::uintmax_t maximum) {
    LARGE_INTEGER size{};
    if (!GetFileSizeEx(handle, &size) || size.QuadPart <= 0 ||
        static_cast<std::uintmax_t>(size.QuadPart) > maximum) {
        integrity_failure();
    }
    std::string result(static_cast<std::size_t>(size.QuadPart), '\0');
    LARGE_INTEGER origin{};
    if (!SetFilePointerEx(handle, origin, nullptr, FILE_BEGIN)) integrity_failure();
    std::size_t total = 0U;
    while (total < result.size()) {
        DWORD read = 0U;
        if (!ReadFile(
                handle,
                result.data() + total,
                static_cast<DWORD>(result.size() - total),
                &read,
                nullptr) || read == 0U) {
            integrity_failure();
        }
        total += read;
    }
    return result;
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

struct verified_manifest final {
    std::string digest;
    native_file_identity identity;
    std::vector<retained_runtime_entry> entries;
};

std::filesystem::path relative_path(
    const std::filesystem::path& root,
    const std::filesystem::path& path) {
    const std::filesystem::path relative = path.lexically_relative(root);
    if (relative.empty() || relative.is_absolute()) integrity_failure();
    return relative.lexically_normal();
}

void require_exact_topology(
    const std::filesystem::path& root,
    const native_file_identity& root_identity,
    const native_file_identity& manifest_identity,
    const std::vector<retained_runtime_entry>& entries) {
    scoped_runtime_handle current_root(open_runtime_path(root, true));
    require_handle_kind(current_root.get(), true);
    require_final_contained(root, current_root.get(), true);
    if (file_identity(current_root.get()) != root_identity) integrity_failure();

    std::set<std::string, std::less<>> expected_files;
    std::set<std::string, std::less<>> expected_directories;
    for (const auto& entry : entries) {
        const std::string relative = wide_to_utf8(entry.relative.generic_wstring());
        (entry.directory ? expected_directories : expected_files).insert(relative);
    }
    std::set<std::string, std::less<>> actual_files;
    std::set<std::string, std::less<>> actual_directories;
    std::vector<std::filesystem::path> parents{std::filesystem::path{}};
    for (const auto& entry : entries) {
        if (entry.directory) parents.push_back(entry.relative);
    }
    for (const auto& parent : parents) {
        const std::filesystem::path full_parent = root / parent;
        for (const auto& item : std::filesystem::directory_iterator(full_parent)) {
            const DWORD attributes = GetFileAttributesW(item.path().c_str());
            if (attributes == INVALID_FILE_ATTRIBUTES ||
                (attributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0) {
                integrity_failure();
            }
            const std::string relative = wide_to_utf8(
                relative_path(root, item.path()).generic_wstring());
            if ((attributes & FILE_ATTRIBUTE_DIRECTORY) != 0) {
                actual_directories.insert(relative);
            } else if (relative != "worker-manifest.json") {
                actual_files.insert(relative);
            }
        }
    }
    if (actual_files != expected_files || actual_directories != expected_directories) {
        integrity_failure();
    }

    scoped_runtime_handle manifest(open_runtime_path(root / L"worker-manifest.json", false));
    require_handle_kind(manifest.get(), false);
    require_final_contained(root, manifest.get());
    if (file_identity(manifest.get()) != manifest_identity) integrity_failure();
    require_no_alternate_streams(root / L"worker-manifest.json");

    for (const auto& entry : entries) {
        const std::filesystem::path full = root / entry.relative;
        scoped_runtime_handle handle(open_runtime_path(full, entry.directory));
        require_handle_kind(handle.get(), entry.directory);
        require_final_contained(root, handle.get());
        if (file_identity(handle.get()) != entry.identity) integrity_failure();
        require_no_alternate_streams(full);
    }
}

verified_manifest verify_manifest(
    const std::filesystem::path& root,
    const std::filesystem::path& manifest,
    std::vector<void*>& handles,
    const native_path_open_observer& before_path_open) {
    if (before_path_open) before_path_open(L"worker-manifest.json", false);
    scoped_runtime_handle manifest_handle(open_runtime_path(manifest, false));
    require_handle_kind(manifest_handle.get(), false);
    require_final_contained(root, manifest_handle.get());
    require_no_alternate_streams(manifest);
    const std::string manifest_text = read_bounded_text(manifest_handle.get(), 1024U * 1024U);
    const auto [manifest_length, manifest_digest] = digest_handle(manifest_handle.get());
    (void)manifest_length;
    json document;
    try { document = json::parse(manifest_text); }
    catch (const json::exception&) { integrity_failure(); }
    if (!document.is_object() || document.size() != 2U ||
        document.value("schemaVersion", 0) != 1 || !document.contains("files") ||
        !document["files"].is_array()) {
        throw protocol_error("runtime integrity failed");
    }

    struct manifest_entry final {
        std::string relative;
        std::uintmax_t length{};
        std::string digest;
    };
    std::vector<manifest_entry> manifest_entries;
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
            !is_lower_sha256(digest) || (!previous.empty() && previous >= relative)) {
            throw protocol_error("runtime integrity failed");
        }
        previous = relative;
        manifest_entries.push_back({relative, length, digest});
        std::filesystem::path parent = std::filesystem::path(utf8_to_wide(relative)).parent_path();
        while (!parent.empty()) {
            expected_directories.insert(wide_to_utf8(parent.generic_wstring()));
            parent = parent.parent_path();
        }
    }
    if (manifest_entries.empty()) integrity_failure();

    verified_manifest result{};
    result.digest = manifest_digest;
    result.identity = file_identity(manifest_handle.get());
    handles.push_back(manifest_handle.release());

    std::vector<std::string> ordered_directories(
        expected_directories.begin(), expected_directories.end());
    std::sort(ordered_directories.begin(), ordered_directories.end(), [](const auto& left, const auto& right) {
        const auto left_depth = std::count(left.begin(), left.end(), '/');
        const auto right_depth = std::count(right.begin(), right.end(), '/');
        return left_depth == right_depth ? left < right : left_depth < right_depth;
    });
    for (const std::string& relative : ordered_directories) {
        const std::filesystem::path relative_path_value = utf8_to_wide(relative);
        if (before_path_open) before_path_open(relative_path_value, true);
        const std::filesystem::path full = root / relative_path_value;
        scoped_runtime_handle handle(open_runtime_path(full, true));
        require_handle_kind(handle.get(), true);
        require_final_contained(root, handle.get());
        require_no_alternate_streams(full);
        result.entries.push_back({relative_path_value, true, file_identity(handle.get())});
        handles.push_back(handle.release());
    }
    for (const manifest_entry& entry : manifest_entries) {
        const std::filesystem::path relative = utf8_to_wide(entry.relative);
        if (before_path_open) before_path_open(relative, false);
        const std::filesystem::path full = root / relative;
        scoped_runtime_handle handle(open_runtime_path(full, false));
        require_handle_kind(handle.get(), false);
        require_final_contained(root, handle.get());
        require_no_alternate_streams(full);
        const auto [length, digest] = digest_handle(handle.get());
        if (length != entry.length || digest != entry.digest) integrity_failure();
        if (full.extension() == L".exe" || full.extension() == L".dll") {
            require_amd64(handle.get());
        }
        result.entries.push_back({relative, false, file_identity(handle.get())});
        handles.push_back(handle.release());
    }
    return result;
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

bool is_explicit_execution_device(std::string_view device) noexcept {
    if (device == "CPU" || device == "GPU") return true;
    constexpr std::string_view prefix = "GPU.";
    if (!device.starts_with(prefix) || device.size() == prefix.size()) return false;
    const std::string_view index = device.substr(prefix.size());
    if (index.size() > 1U && index.front() == '0') return false;
    return std::all_of(index.begin(), index.end(), [](unsigned char value) {
        return value >= static_cast<unsigned char>('0') &&
            value <= static_cast<unsigned char>('9');
    });
}

void require_execution_device_match(
    std::string_view requested,
    const std::vector<std::string>& actual) {
    if (actual.size() != 1U || actual.front() != requested) {
        throw worker_failure(
            "runtime_device_mismatch", true, "execution device mismatch");
    }
}

verified_execution_device verify_execution_device(
    const std::filesystem::path& model_path,
    const std::string& requested,
    const std::function<void()>& module_verifier) {
    if (!is_explicit_execution_device(requested)) {
        throw protocol_error("device rejected");
    }

    try {
        ov::Core core;
        const std::vector<std::string> available = core.get_available_devices();
        if (module_verifier) module_verifier();
        const bool present = std::find(
            available.begin(), available.end(), requested) != available.end();
        const bool unqualified_gpu_present = requested == "GPU" &&
            std::any_of(available.begin(), available.end(), [](const std::string& value) {
                return value == "GPU" || value.starts_with("GPU.");
            });
        if (!present && !unqualified_gpu_present) {
            throw worker_failure(
                "runtime_device_unavailable", true, "requested device unavailable");
        }

        std::shared_ptr<ov::Model> model = core.read_model(model_path);
        ov::CompiledModel compiled = core.compile_model(model, requested);
        if (module_verifier) module_verifier();
        std::vector<std::string> actual =
            compiled.get_property(ov::execution_devices);
        require_execution_device_match(requested, actual);
        return {requested, std::move(actual)};
    } catch (const worker_failure&) {
        throw;
    } catch (const protocol_error&) {
        throw;
    } catch (...) {
        if (is_gpu_device(requested)) {
            throw worker_failure(
                "runtime_device_unavailable", true, "requested device unavailable");
        }
        throw;
    }
}

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
    scoped_runtime_handle handle(open_runtime_path(path, false));
    require_handle_kind(handle.get(), false);
    return digest_handle(handle.get()).second;
}

runtime_context::~runtime_context() { close_runtime_handles(handles_); }

runtime_context::runtime_context(runtime_context&& other) noexcept
    : evidence_(std::move(other.evidence_)),
      root_(std::move(other.root_)),
      root_identity_(other.root_identity_),
      manifest_identity_(other.manifest_identity_),
      entries_(std::move(other.entries_)),
      handles_(std::move(other.handles_)),
      monitor_(std::move(other.monitor_)) {
    other.handles_.clear();
}

runtime_context& runtime_context::operator=(runtime_context&& other) noexcept {
    if (this != &other) {
        close_runtime_handles(handles_);
        evidence_ = std::move(other.evidence_);
        root_ = std::move(other.root_);
        root_identity_ = other.root_identity_;
        manifest_identity_ = other.manifest_identity_;
        entries_ = std::move(other.entries_);
        handles_ = std::move(other.handles_);
        monitor_ = std::move(other.monitor_);
        other.handles_.clear();
    }
    return *this;
}

const runtime_evidence& runtime_context::evidence() const noexcept { return evidence_; }
const std::filesystem::path& runtime_context::root() const noexcept { return root_; }

void runtime_context::verify_topology(bool drain_notifications) const {
    if (drain_notifications) {
        verify_terminal_topology();
        return;
    }
    try {
        if (monitor_ == nullptr || monitor_->changed()) {
            throw worker_failure(
                "runtime_integrity_failed", true, "runtime namespace changed");
        }
        require_exact_topology(
            root_, root_identity_, manifest_identity_, entries_);
        if (monitor_->changed()) {
            throw worker_failure(
                "runtime_integrity_failed", true, "runtime namespace changed");
        }
    } catch (const worker_failure&) {
        throw;
    } catch (...) {
        throw worker_failure(
            "runtime_integrity_failed", true, "runtime topology changed");
    }
}

void runtime_context::verify_terminal_topology(
    const std::function<void()>& during_rescan) const {
    try {
        if (monitor_ == nullptr || monitor_->terminal_barrier()) {
            throw worker_failure(
                "runtime_integrity_failed", true, "runtime namespace changed");
        }
        if (during_rescan) during_rescan();
        require_exact_topology(root_, root_identity_, manifest_identity_, entries_);
        if (monitor_->terminal_barrier()) {
            throw worker_failure(
                "runtime_integrity_failed", true, "runtime namespace changed");
        }
    } catch (const worker_failure&) {
        throw;
    } catch (...) {
        throw worker_failure(
            "runtime_integrity_failed", true, "runtime topology changed");
    }
}

bool runtime_context::contains_approved_file(
    const native_file_identity& identity) const noexcept {
    return std::any_of(entries_.begin(), entries_.end(), [&](const auto& entry) {
        return !entry.directory && entry.identity == identity;
    });
}

runtime_context runtime_context::initialize(
    const std::filesystem::path& worker_root,
    const native_path_open_observer& before_path_open,
    const std::function<void()>& after_handles_acquired,
    const runtime_load_observer& load_observer,
    bool verify_modules) {
    runtime_context context;
    const std::filesystem::path root =
        std::filesystem::absolute(worker_root).lexically_normal();
    try {
        scoped_runtime_handle root_handle(open_runtime_path(root, true));
        require_handle_kind(root_handle.get(), true);
        require_final_contained(root, root_handle.get(), true);
        require_no_alternate_streams(root);
        context.root_ = root;
        context.root_identity_ = file_identity(root_handle.get());
        context.handles_.push_back(root_handle.release());
        scoped_runtime_handle monitor_handle(open_runtime_monitor_path(root));
        require_handle_kind(monitor_handle.get(), true);
        require_final_contained(root, monitor_handle.get(), true);
        if (file_identity(monitor_handle.get()) != context.root_identity_) {
            integrity_failure();
        }
        context.monitor_ =
            std::make_unique<namespace_monitor>(monitor_handle.release());

        const std::filesystem::path manifest = root / L"worker-manifest.json";
        verified_manifest verified = verify_manifest(
            root, manifest, context.handles_, before_path_open);
        context.manifest_identity_ = verified.identity;
        context.entries_ = std::move(verified.entries);
        context.verify_topology(true);
        if (after_handles_acquired) after_handles_acquired();
        context.verify_topology(true);

        if (!SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_SYSTEM32 | LOAD_LIBRARY_SEARCH_USER_DIRS) ||
            !SetDllDirectoryW(L"") || AddDllDirectory(root.c_str()) == nullptr) {
            throw protocol_error("runtime loader hardening failed");
        }
        const auto verify_integrity = [&](bool drain_notifications) {
            std::exception_ptr first_failure;
            const auto capture = [&](const auto& check) {
                try {
                    check();
                } catch (...) {
                    if (first_failure == nullptr) first_failure = std::current_exception();
                }
            };
            capture([&] { context.verify_topology(drain_notifications); });
            if (verify_modules) {
                capture([&] { verify_loaded_module_membership_only(context); });
            }
            if (first_failure != nullptr) std::rethrow_exception(first_failure);
        };
        ov::Version runtime{};
        try {
            verify_integrity(false);
            if (load_observer) load_observer(runtime_load_stage::runtime_version);
            runtime = ov::get_openvino_version();
            verify_integrity(true);
        } catch (...) {
            verify_integrity(true);
            throw;
        }
        ov::Version genai{};
        try {
            verify_integrity(false);
            if (load_observer) load_observer(runtime_load_stage::genai_version);
            genai = ov::genai::get_version();
            verify_integrity(true);
        } catch (...) {
            verify_integrity(true);
            throw;
        }
        std::string tokenizers;
        try {
            verify_integrity(false);
            if (load_observer) load_observer(runtime_load_stage::tokenizers_version);
            tokenizers = product_version(root / L"openvino_tokenizers.dll");
            verify_integrity(true);
        } catch (...) {
            verify_integrity(true);
            throw;
        }
        context.evidence_ = {
            runtime.buildNumber == nullptr ? "" : runtime.buildNumber,
            genai.buildNumber == nullptr ? "" : genai.buildNumber,
            tokenizers,
            verified.digest};
        if (context.evidence_.runtime_build !=
                "2026.3.0-22451-8a17657b995-releases/2026/3" ||
            context.evidence_.genai_build != "2026.3.0.0-3277-bd8d6542e3c" ||
            context.evidence_.tokenizers_build != "2026.3.0.0-703-183c6f25cda") {
            throw protocol_error("runtime identity mismatch");
        }
        verify_integrity(true);
        return context;
    } catch (...) {
        const std::exception_ptr original_failure = std::current_exception();
        std::exception_ptr integrity_check_failure;
        const auto capture_integrity = [&](const auto& check) {
            try {
                check();
            } catch (...) {
                if (integrity_check_failure == nullptr) {
                    integrity_check_failure = std::current_exception();
                }
            }
        };
        if (context.monitor_ != nullptr) {
            capture_integrity([&] { context.verify_topology(true); });
            if (verify_modules) {
                capture_integrity(
                    [&] { verify_loaded_module_membership_only(context); });
            }
        }
        close_runtime_handles(context.handles_);
        if (integrity_check_failure != nullptr) {
            std::rethrow_exception(integrity_check_failure);
        }
        std::rethrow_exception(original_failure);
    }
}

runtime_context initialize_verified_runtime_at(
    const std::filesystem::path& worker_root,
    const native_path_open_observer& before_path_open,
    const std::function<void()>& after_handles_acquired) {
    return runtime_context::initialize(
        worker_root, before_path_open, after_handles_acquired, {}, false);
}

runtime_context initialize_verified_runtime_at(
    const std::filesystem::path& worker_root,
    const native_path_open_observer& before_path_open,
    const std::function<void()>& after_handles_acquired,
    const runtime_load_observer& load_observer) {
    return runtime_context::initialize(
        worker_root, before_path_open, after_handles_acquired, load_observer, false);
}

runtime_context initialize_verified_runtime() {
    return runtime_context::initialize(executable_directory(), {}, {}, {}, true);
}

namespace {

void verify_loaded_module_membership_only(const runtime_context& runtime) {
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
    wchar_t system_buffer[MAX_PATH]{};
    wchar_t windows_buffer[MAX_PATH]{};
    const UINT system_length = GetSystemDirectoryW(system_buffer, MAX_PATH);
    const UINT windows_length = GetWindowsDirectoryW(windows_buffer, MAX_PATH);
    if (system_length == 0 || system_length >= MAX_PATH ||
        windows_length == 0 || windows_length >= MAX_PATH) {
        fail();
    }
    const std::vector<std::filesystem::path> os_roots{
        std::filesystem::path(system_buffer),
        std::filesystem::path(windows_buffer) / L"WinSxS"};
    try {
        const std::size_t count = required / sizeof(HMODULE);
        std::wstring path_buffer(32768U, L'\0');
        for (std::size_t index = 0; index < count; ++index) {
            const DWORD length = GetModuleFileNameExW(
                GetCurrentProcess(), modules[index], path_buffer.data(),
                static_cast<DWORD>(path_buffer.size()));
            if (length == 0 || length >= path_buffer.size()) fail();
            path_buffer.resize(length);
            const std::filesystem::path module(path_buffer);
            path_buffer.resize(32768U);
            if (is_module_in_validated_os_roots(module, os_roots)) continue;
            verify_module_file_membership(runtime, module);
        }
    } catch (const worker_failure&) {
        throw;
    } catch (...) {
        fail();
    }
}

}  // namespace

void verify_loaded_module_closure(const runtime_context& runtime) {
    std::exception_ptr first_failure;
    const auto capture = [&](const auto& check) {
        try {
            check();
        } catch (...) {
            if (first_failure == nullptr) first_failure = std::current_exception();
        }
    };
    capture([&] { runtime.verify_topology(); });
    capture([&] { verify_loaded_module_membership_only(runtime); });
    capture([&] { runtime.verify_topology(); });
    if (first_failure != nullptr) std::rethrow_exception(first_failure);
}

bool is_module_in_validated_os_roots(
    const std::filesystem::path& module,
    const std::vector<std::filesystem::path>& roots) {
    try {
        scoped_runtime_handle module_handle(open_runtime_path(module, false));
        require_handle_kind(module_handle.get(), false);
        std::wstring module_final = final_path(module_handle.get());
        std::transform(
            module_final.begin(), module_final.end(), module_final.begin(), towlower);
        for (const auto& root : roots) {
            scoped_runtime_handle root_handle(open_runtime_path(root, true));
            require_handle_kind(root_handle.get(), true);
            std::wstring root_final = final_path(root_handle.get());
            std::transform(
                root_final.begin(), root_final.end(), root_final.begin(), towlower);
            if (!root_final.ends_with(L'\\')) root_final.push_back(L'\\');
            if (module_final.size() > root_final.size() &&
                module_final.compare(0U, root_final.size(), root_final) == 0) {
                return true;
            }
        }
        return false;
    } catch (...) {
        return false;
    }
}

void verify_module_file_membership(
    const runtime_context& runtime,
    const std::filesystem::path& module) {
    try {
        scoped_runtime_handle handle(open_runtime_path(module, false));
        require_handle_kind(handle.get(), false);
        require_final_contained(runtime.root(), handle.get());
        if (!runtime.contains_approved_file(file_identity(handle.get()))) {
            throw worker_failure(
                "runtime_integrity_failed", true, "module escaped verified closure");
        }
    } catch (const worker_failure&) {
        throw;
    } catch (...) {
        throw worker_failure(
            "runtime_integrity_failed", true, "module escaped verified closure");
    }
}

}  // namespace granite::official_worker
