#include "package_inspector.hpp"

#include "namespace_monitor.hpp"
#include "protocol.hpp"
#include "runtime_evidence.hpp"

#include <windows.h>
#include <bcrypt.h>

#include <openvino/openvino.hpp>

#include <algorithm>
#include <array>
#include <exception>
#include <iomanip>
#include <set>
#include <sstream>
#include <utility>

namespace granite::official_worker {
namespace {

struct snapshot_entry final {
    std::string relative;
    std::uintmax_t length{};
    std::string digest;
};

class scoped_handle final {
public:
    explicit scoped_handle(HANDLE value = INVALID_HANDLE_VALUE) noexcept : value_(value) {}
    ~scoped_handle() { reset(); }
    scoped_handle(scoped_handle&& other) noexcept : value_(other.release()) {}
    scoped_handle& operator=(scoped_handle&& other) noexcept {
        if (this != &other) {
            reset();
            value_ = other.release();
        }
        return *this;
    }
    scoped_handle(const scoped_handle&) = delete;
    scoped_handle& operator=(const scoped_handle&) = delete;
    [[nodiscard]] HANDLE get() const noexcept { return value_; }
    [[nodiscard]] HANDLE release() noexcept {
        const HANDLE value = value_;
        value_ = INVALID_HANDLE_VALUE;
        return value;
    }
    void reset() noexcept {
        if (value_ != INVALID_HANDLE_VALUE && value_ != nullptr) CloseHandle(value_);
        value_ = INVALID_HANDLE_VALUE;
    }
private:
    HANDLE value_;
};

[[noreturn]] void fail(std::string_view reason) {
    std::string code = "package_inconsistent_resource";
    if (reason.find("unsafe") != std::string_view::npos) {
        code = "package_unsafe_path";
    } else if (reason.find("missing") != std::string_view::npos) {
        code = "package_missing_resource";
    } else if (reason.find("changed") != std::string_view::npos) {
        code = "package_changed";
    } else if (reason.find("unavailable") != std::string_view::npos ||
               reason.find("unreadable") != std::string_view::npos ||
               reason.find("hash") != std::string_view::npos) {
        code = "package_unreadable";
    }
    const bool fatal = code == "package_changed";
    throw worker_failure(std::move(code), fatal, std::string(reason));
}

std::string digest_hex(const std::array<UCHAR, 32>& digest) {
    std::ostringstream result;
    result << std::hex << std::setfill('0');
    for (UCHAR byte : digest) result << std::setw(2) << static_cast<unsigned>(byte);
    return result.str();
}

std::string sha256_bytes(const std::string& value) {
    BCRYPT_ALG_HANDLE algorithm = nullptr;
    BCRYPT_HASH_HANDLE hash = nullptr;
    if (BCryptOpenAlgorithmProvider(&algorithm, BCRYPT_SHA256_ALGORITHM, nullptr, 0) < 0 ||
        BCryptCreateHash(algorithm, &hash, nullptr, 0, nullptr, 0, 0) < 0) {
        if (hash != nullptr) BCryptDestroyHash(hash);
        if (algorithm != nullptr) BCryptCloseAlgorithmProvider(algorithm, 0);
        fail("hash initialization failed");
    }
    std::array<UCHAR, 32> digest{};
    const NTSTATUS update = BCryptHashData(hash,
        reinterpret_cast<PUCHAR>(const_cast<char*>(value.data())),
        static_cast<ULONG>(value.size()), 0);
    const NTSTATUS finish = update < 0 ? update :
        BCryptFinishHash(hash, digest.data(), static_cast<ULONG>(digest.size()), 0);
    BCryptDestroyHash(hash);
    BCryptCloseAlgorithmProvider(algorithm, 0);
    if (finish < 0) fail("hash failed");
    return digest_hex(digest);
}

std::pair<std::uintmax_t, std::string> sha256_handle(HANDLE file) {
    LARGE_INTEGER size{};
    if (!GetFileSizeEx(file, &size) || size.QuadPart <= 0) fail("package inconsistent");
    BCRYPT_ALG_HANDLE algorithm = nullptr;
    BCRYPT_HASH_HANDLE hash = nullptr;
    if (BCryptOpenAlgorithmProvider(&algorithm, BCRYPT_SHA256_ALGORITHM, nullptr, 0) < 0 ||
        BCryptCreateHash(algorithm, &hash, nullptr, 0, nullptr, 0, 0) < 0) {
        if (hash != nullptr) BCryptDestroyHash(hash);
        if (algorithm != nullptr) BCryptCloseAlgorithmProvider(algorithm, 0);
        fail("hash initialization failed");
    }
    std::array<unsigned char, 64U * 1024U> buffer{};
    LARGE_INTEGER origin{};
    if (!SetFilePointerEx(file, origin, nullptr, FILE_BEGIN)) fail("package unreadable");
    std::uintmax_t total = 0U;
    for (;;) {
        DWORD read = 0U;
        if (!ReadFile(file, buffer.data(), static_cast<DWORD>(buffer.size()), &read, nullptr)) {
            BCryptDestroyHash(hash);
            BCryptCloseAlgorithmProvider(algorithm, 0);
            fail("package unreadable");
        }
        if (read == 0U) break;
        if (BCryptHashData(hash, buffer.data(), read, 0) < 0) {
            BCryptDestroyHash(hash);
            BCryptCloseAlgorithmProvider(algorithm, 0);
            fail("hash failed");
        }
        total += read;
    }
    std::array<UCHAR, 32> digest{};
    const NTSTATUS finish = BCryptFinishHash(
        hash, digest.data(), static_cast<ULONG>(digest.size()), 0);
    BCryptDestroyHash(hash);
    BCryptCloseAlgorithmProvider(algorithm, 0);
    if (finish < 0 || total != static_cast<std::uintmax_t>(size.QuadPart)) {
        fail("package changed");
    }
    return {total, digest_hex(digest)};
}

std::wstring final_path(HANDLE handle) {
    std::wstring value(512U, L'\0');
    for (;;) {
        const DWORD length = GetFinalPathNameByHandleW(
            handle, value.data(), static_cast<DWORD>(value.size()), FILE_NAME_NORMALIZED);
        if (length == 0U) fail("package unsafe");
        if (length < value.size()) {
            value.resize(length);
            return value;
        }
        value.resize(static_cast<std::size_t>(length) + 1U);
    }
}

std::wstring normalize_final(std::wstring value) {
    constexpr std::wstring_view prefix = LR"(\\?\)";
    constexpr std::wstring_view unc = LR"(\\?\UNC\)";
    if (value.starts_with(unc)) value = LR"(\\)" + value.substr(unc.size());
    else if (value.starts_with(prefix)) value.erase(0U, prefix.size());
    return std::filesystem::absolute(value).lexically_normal().wstring();
}

void require_contained(const std::filesystem::path& root, HANDLE handle, bool allow_root) {
    std::wstring approved = std::filesystem::absolute(root).lexically_normal().wstring();
    std::wstring actual = normalize_final(final_path(handle));
    std::transform(approved.begin(), approved.end(), approved.begin(), towlower);
    std::transform(actual.begin(), actual.end(), actual.begin(), towlower);
    if (allow_root && actual == approved) return;
    if (!approved.ends_with(L'\\')) approved.push_back(L'\\');
    if (!actual.starts_with(approved)) fail("package unsafe");
}

void require_no_streams(const std::filesystem::path& path) {
    WIN32_FIND_STREAM_DATA data{};
    const HANDLE find = FindFirstStreamW(path.c_str(), FindStreamInfoStandard, &data, 0);
    if (find == INVALID_HANDLE_VALUE) {
        const DWORD error = GetLastError();
        if (error == ERROR_HANDLE_EOF || error == ERROR_NO_MORE_FILES) return;
        fail("package unsafe");
    }
    bool invalid = false;
    do {
        if (std::wstring_view(data.cStreamName) != L"::$DATA") invalid = true;
    } while (!invalid && FindNextStreamW(find, &data));
    const DWORD error = GetLastError();
    FindClose(find);
    if (invalid || (error != ERROR_HANDLE_EOF && error != ERROR_NO_MORE_FILES)) {
        fail("package unsafe");
    }
}

scoped_handle open_leased(
    const std::filesystem::path& path,
    bool directory) {
    const DWORD flags = FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT |
        (directory ? 0U : FILE_FLAG_SEQUENTIAL_SCAN);
    scoped_handle result(CreateFileW(
        path.c_str(), directory ? FILE_READ_ATTRIBUTES : GENERIC_READ,
        FILE_SHARE_READ, nullptr, OPEN_EXISTING, flags, nullptr));
    if (result.get() == INVALID_HANDLE_VALUE) fail("package unreadable");
    return result;
}

scoped_handle open_package_monitor(const std::filesystem::path& path) {
    scoped_handle result(CreateFileW(
        path.c_str(),
        FILE_READ_ATTRIBUTES | FILE_LIST_DIRECTORY,
        FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
        nullptr,
        OPEN_EXISTING,
        FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT |
            FILE_FLAG_OVERLAPPED,
        nullptr));
    if (result.get() == INVALID_HANDLE_VALUE) fail("package unavailable");
    return result;
}

bool handle_is_directory(HANDLE handle) {
    FILE_ATTRIBUTE_TAG_INFO info{};
    if (!GetFileInformationByHandleEx(
            handle, FileAttributeTagInfo, &info, sizeof(info)) ||
        (info.FileAttributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0 ||
        info.ReparseTag != 0U) {
        fail("package unsafe");
    }
    return (info.FileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0;
}

native_file_identity identity_of(HANDLE handle) {
    FILE_ID_INFO info{};
    if (!GetFileInformationByHandleEx(handle, FileIdInfo, &info, sizeof(info))) {
        fail("package unavailable");
    }
    native_file_identity result{};
    result.volume_serial = info.VolumeSerialNumber;
    std::copy(
        std::begin(info.FileId.Identifier),
        std::end(info.FileId.Identifier),
        result.file_id.begin());
    return result;
}

std::filesystem::path safe_relative(
    const std::filesystem::path& root,
    const std::filesystem::path& path) {
    const std::filesystem::path relative = path.lexically_relative(root).lexically_normal();
    if (relative.empty() || relative.is_absolute()) fail("package unsafe");
    for (const auto& component : relative) {
        if (component == L".." || component == L".") fail("package unsafe");
    }
    return relative;
}

void verify_package_topology(
    const std::filesystem::path& root,
    const native_file_identity& root_identity,
    const std::vector<retained_package_entry>& entries) {
    scoped_handle current_root = open_leased(root, true);
    if (!handle_is_directory(current_root.get()) ||
        identity_of(current_root.get()) != root_identity) {
        fail("package changed");
    }
    require_contained(root, current_root.get(), true);

    std::set<std::wstring, std::less<>> expected_files;
    std::set<std::wstring, std::less<>> expected_directories;
    std::vector<std::filesystem::path> parents{std::filesystem::path{}};
    for (const auto& entry : entries) {
        const std::wstring relative = entry.relative.generic_wstring();
        (entry.directory ? expected_directories : expected_files).insert(relative);
        if (entry.directory) parents.push_back(entry.relative);
    }
    std::set<std::wstring, std::less<>> actual_files;
    std::set<std::wstring, std::less<>> actual_directories;
    for (const auto& parent : parents) {
        for (const auto& item : std::filesystem::directory_iterator(root / parent)) {
            const DWORD attributes = GetFileAttributesW(item.path().c_str());
            if (attributes == INVALID_FILE_ATTRIBUTES ||
                (attributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0) {
                fail("package changed");
            }
            const std::wstring relative = safe_relative(root, item.path()).generic_wstring();
            ((attributes & FILE_ATTRIBUTE_DIRECTORY) != 0
                ? actual_directories
                : actual_files).insert(relative);
        }
    }
    if (actual_files != expected_files || actual_directories != expected_directories) {
        fail("package changed");
    }
    for (const auto& entry : entries) {
        const std::filesystem::path full = root / entry.relative;
        scoped_handle handle = open_leased(full, entry.directory);
        if (handle_is_directory(handle.get()) != entry.directory ||
            identity_of(handle.get()) != entry.identity) {
            fail("package changed");
        }
        require_contained(root, handle.get(), false);
        require_no_streams(full);
    }
}

std::string manifest_digest(const std::vector<snapshot_entry>& entries) {
    std::string tuples;
    for (const snapshot_entry& entry : entries) {
        tuples.append(entry.relative);
        tuples.push_back('\0');
        tuples.append(std::to_string(entry.length));
        tuples.push_back('\0');
        tuples.append(entry.digest);
        tuples.push_back('\n');
    }
    return sha256_bytes(tuples);
}

const snapshot_entry& find_entry(
    const std::vector<snapshot_entry>& entries,
    std::string_view name) {
    const auto found = std::find_if(entries.begin(), entries.end(), [&](const auto& entry) {
        return entry.relative == name;
    });
    if (found == entries.end()) fail("package resource missing");
    return *found;
}

void close_all(std::vector<void*>& handles) noexcept {
    for (auto iterator = handles.rbegin(); iterator != handles.rend(); ++iterator) {
        if (*iterator != nullptr && *iterator != INVALID_HANDLE_VALUE) {
            CloseHandle(static_cast<HANDLE>(*iterator));
        }
    }
    handles.clear();
}

}  // namespace

package_lease::~package_lease() { close_all(handles_); }

package_lease::package_lease(package_lease&& other) noexcept
    : root_(std::move(other.root_)),
      root_identity_(other.root_identity_),
      evidence_(std::move(other.evidence_)),
      entries_(std::move(other.entries_)),
      handles_(std::move(other.handles_)),
      monitor_(std::move(other.monitor_)) {
    other.handles_.clear();
}

package_lease& package_lease::operator=(package_lease&& other) noexcept {
    if (this != &other) {
        close_all(handles_);
        root_ = std::move(other.root_);
        root_identity_ = other.root_identity_;
        evidence_ = std::move(other.evidence_);
        entries_ = std::move(other.entries_);
        handles_ = std::move(other.handles_);
        monitor_ = std::move(other.monitor_);
        other.handles_.clear();
    }
    return *this;
}

const std::filesystem::path& package_lease::root() const noexcept { return root_; }
const package_evidence& package_lease::evidence() const noexcept { return evidence_; }

void package_lease::verify_topology(bool drain_notifications) const {
    if (drain_notifications) {
        verify_terminal_topology();
        return;
    }
    if (monitor_ == nullptr || monitor_->changed()) fail("package changed");
    verify_package_topology(root_, root_identity_, entries_);
    if (monitor_->changed()) fail("package changed");
}

void package_lease::verify_terminal_topology(
    const std::function<void()>& during_rescan) const {
    if (monitor_ == nullptr || monitor_->terminal_barrier()) fail("package changed");
    if (during_rescan) during_rescan();
    verify_package_topology(root_, root_identity_, entries_);
    if (monitor_->terminal_barrier()) fail("package changed");
}

package_lease acquire_package(
    const std::filesystem::path& package,
    const std::string& expected_package_digest,
    const std::string& expected_model_digest,
    std::uintmax_t expected_model_length,
    const package_path_open_observer& before_path_open) {
    package_lease lease;
    lease.root_ = std::filesystem::absolute(package).lexically_normal();
    try {
        scoped_handle root = open_leased(lease.root_, true);
        if (!handle_is_directory(root.get())) fail("package unavailable");
        require_contained(lease.root_, root.get(), true);
        require_no_streams(lease.root_);
        lease.root_identity_ = identity_of(root.get());
        lease.handles_.push_back(root.release());
        scoped_handle monitor = open_package_monitor(lease.root_);
        if (!handle_is_directory(monitor.get()) ||
            identity_of(monitor.get()) != lease.root_identity_) {
            fail("package changed");
        }
        require_contained(lease.root_, monitor.get(), true);
        try {
            lease.monitor_ = std::make_unique<namespace_monitor>(monitor.release());
        } catch (...) {
            fail("package changed");
        }

        std::vector<snapshot_entry> entries;
        std::vector<std::filesystem::path> pending{std::filesystem::path{}};
        for (std::size_t index = 0U; index < pending.size(); ++index) {
            const std::filesystem::path parent = pending[index];
            for (const auto& item : std::filesystem::directory_iterator(lease.root_ / parent)) {
                const std::filesystem::path relative_path =
                    safe_relative(lease.root_, item.path());
                const DWORD observed_attributes = GetFileAttributesW(item.path().c_str());
                const bool observed_directory = observed_attributes != INVALID_FILE_ATTRIBUTES &&
                    (observed_attributes & FILE_ATTRIBUTE_DIRECTORY) != 0;
                if (before_path_open) before_path_open(relative_path, observed_directory);
                scoped_handle handle = open_leased(item.path(), observed_directory);
                const bool directory = handle_is_directory(handle.get());
                require_contained(lease.root_, handle.get(), false);
                require_no_streams(item.path());
                lease.entries_.push_back(
                    {relative_path, directory, identity_of(handle.get())});
                if (directory) {
                    pending.push_back(relative_path);
                } else {
                    const std::string relative = wide_to_utf8(relative_path.generic_wstring());
                if (relative.empty() || relative.find("..") != std::string::npos ||
                    relative.find(':') != std::string::npos) {
                    fail("package unsafe");
                }
                const auto [length, digest] = sha256_handle(handle.get());
                entries.push_back({relative, length, digest});
                }
                lease.handles_.push_back(handle.release());
            }
        }
        std::sort(entries.begin(), entries.end(), [](const auto& left, const auto& right) {
            return left.relative < right.relative;
        });
        if (entries.empty()) fail("package inconsistent");
        const std::string digest = manifest_digest(entries);
        const snapshot_entry& model = find_entry(entries, "openvino_model.bin");
        if (digest != expected_package_digest || model.digest != expected_model_digest ||
            model.length != expected_model_length) {
            fail("package identity mismatch");
        }
        const bool has_chat_template = std::any_of(
            entries.begin(), entries.end(), [](const snapshot_entry& entry) {
                return entry.relative == "chat_template.jinja" ||
                    entry.relative == "chat_template.json";
            });
        lease.evidence_ = {digest, model.digest, model.length, has_chat_template};
        lease.verify_topology(true);
        return lease;
    } catch (...) {
        close_all(lease.handles_);
        throw;
    }
}

package_evidence inspect_package(
    package_lease& package,
    const runtime_context& runtime,
    const native_load_observer& observer,
    const native_module_verifier& module_verifier) {
    const auto verify_integrity = [&](bool drain_notifications) {
        std::exception_ptr first_failure;
        const auto capture = [&](const auto& check) {
            try {
                check();
            } catch (...) {
                if (first_failure == nullptr) first_failure = std::current_exception();
            }
        };
        capture([&] { package.verify_topology(drain_notifications); });
        capture([&] { runtime.verify_topology(drain_notifications); });
        if (module_verifier) capture(module_verifier);
        if (first_failure != nullptr) std::rethrow_exception(first_failure);
    };
    try {
        ov::Core core;
        const auto boundary = [&](native_load_stage stage, const auto& load) {
            if (observer) observer(stage);
            verify_integrity(false);
            try {
                load();
            } catch (...) {
                verify_integrity(true);
                throw;
            }
            verify_integrity(true);
        };
        boundary(native_load_stage::tokenizer_extension, [&] {
            core.add_extension(runtime.root() / L"openvino_tokenizers.dll");
        });
        boundary(native_load_stage::main_model, [&] {
            (void)core.read_model(package.root() / L"openvino_model.xml");
        });
        boundary(native_load_stage::tokenizer_model, [&] {
            (void)core.read_model(package.root() / L"openvino_tokenizer.xml");
        });
        boundary(native_load_stage::detokenizer_model, [&] {
            (void)core.read_model(package.root() / L"openvino_detokenizer.xml");
        });
        return package.evidence();
    } catch (const worker_failure&) {
        verify_integrity(true);
        throw;
    } catch (...) {
        verify_integrity(true);
        throw worker_failure(
            "package_inconsistent_resource", false, "package parse failed");
    }
}

}  // namespace granite::official_worker
