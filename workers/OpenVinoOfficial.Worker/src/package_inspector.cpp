#include "package_inspector.hpp"

#include "protocol.hpp"
#include "runtime_evidence.hpp"

#include <windows.h>
#include <bcrypt.h>

#include <openvino/openvino.hpp>

#include <algorithm>
#include <array>
#include <iomanip>
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
    throw worker_failure(std::move(code), false, std::string(reason));
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

scoped_handle open_leased(const std::filesystem::path& path, bool directory) {
    const DWORD flags = directory
        ? FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT
        : FILE_ATTRIBUTE_NORMAL | FILE_FLAG_SEQUENTIAL_SCAN;
    scoped_handle result(CreateFileW(
        path.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, flags, nullptr));
    if (result.get() == INVALID_HANDLE_VALUE) fail("package unreadable");
    return result;
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
      evidence_(std::move(other.evidence_)),
      handles_(std::move(other.handles_)) {
    other.handles_.clear();
}

package_lease& package_lease::operator=(package_lease&& other) noexcept {
    if (this != &other) {
        close_all(handles_);
        root_ = std::move(other.root_);
        evidence_ = std::move(other.evidence_);
        handles_ = std::move(other.handles_);
        other.handles_.clear();
    }
    return *this;
}

const std::filesystem::path& package_lease::root() const noexcept { return root_; }
const package_evidence& package_lease::evidence() const noexcept { return evidence_; }

package_lease acquire_package(
    const std::filesystem::path& package,
    const std::string& expected_package_digest,
    const std::string& expected_model_digest,
    std::uintmax_t expected_model_length) {
    package_lease lease;
    lease.root_ = std::filesystem::absolute(package).lexically_normal();
    try {
        const DWORD root_attributes = GetFileAttributesW(lease.root_.c_str());
        if (root_attributes == INVALID_FILE_ATTRIBUTES ||
            (root_attributes & FILE_ATTRIBUTE_DIRECTORY) == 0 ||
            (root_attributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0) {
            fail("package unavailable");
        }
        require_no_streams(lease.root_);
        scoped_handle root = open_leased(lease.root_, true);
        require_contained(lease.root_, root.get(), true);
        lease.handles_.push_back(root.release());

        std::vector<snapshot_entry> entries;
        for (const auto& item : std::filesystem::recursive_directory_iterator(lease.root_)) {
            const DWORD attributes = GetFileAttributesW(item.path().c_str());
            if (attributes == INVALID_FILE_ATTRIBUTES ||
                (attributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0 ||
                (!item.is_regular_file() && !item.is_directory())) {
                fail("package unsafe");
            }
            require_no_streams(item.path());
            scoped_handle handle = open_leased(item.path(), item.is_directory());
            require_contained(lease.root_, handle.get(), false);
            if (item.is_regular_file()) {
                const std::string relative = wide_to_utf8(
                    std::filesystem::relative(item.path(), lease.root_).generic_wstring());
                if (relative.empty() || relative.find("..") != std::string::npos ||
                    relative.find(':') != std::string::npos) {
                    fail("package unsafe");
                }
                const auto [length, digest] = sha256_handle(handle.get());
                entries.push_back({relative, length, digest});
            }
            lease.handles_.push_back(handle.release());
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
        lease.evidence_ = {digest, model.digest, model.length};
        return lease;
    } catch (...) {
        close_all(lease.handles_);
        throw;
    }
}

package_evidence inspect_package(
    package_lease& package,
    const native_load_observer& observer) {
    try {
        ov::Core core;
        core.add_extension(executable_directory() / L"openvino_tokenizers.dll");
        if (observer) observer(native_load_stage::main_model);
        (void)core.read_model(package.root() / L"openvino_model.xml");
        if (observer) observer(native_load_stage::tokenizer_model);
        (void)core.read_model(package.root() / L"openvino_tokenizer.xml");
        if (observer) observer(native_load_stage::detokenizer_model);
        (void)core.read_model(package.root() / L"openvino_detokenizer.xml");
        return package.evidence();
    } catch (const worker_failure&) {
        throw;
    } catch (...) {
        throw worker_failure(
            "package_inconsistent_resource", false, "package parse failed");
    }
}

}  // namespace granite::official_worker
