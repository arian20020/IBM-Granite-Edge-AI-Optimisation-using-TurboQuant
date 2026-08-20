#include "package_inspector.hpp"

#include "protocol.hpp"
#include "runtime_evidence.hpp"

#include <windows.h>
#include <bcrypt.h>

#include <openvino/openvino.hpp>

#include <algorithm>
#include <array>
#include <fstream>
#include <iomanip>
#include <sstream>
#include <vector>

namespace granite::official_worker {
namespace {

struct snapshot_entry final {
    std::string relative;
    std::uintmax_t length{};
    std::string digest;
};

std::string sha256_bytes(const std::string& value) {
    BCRYPT_ALG_HANDLE algorithm = nullptr;
    BCRYPT_HASH_HANDLE hash = nullptr;
    if (BCryptOpenAlgorithmProvider(&algorithm, BCRYPT_SHA256_ALGORITHM, nullptr, 0) < 0 ||
        BCryptCreateHash(algorithm, &hash, nullptr, 0, nullptr, 0, 0) < 0) {
        if (hash != nullptr) BCryptDestroyHash(hash);
        if (algorithm != nullptr) BCryptCloseAlgorithmProvider(algorithm, 0);
        throw protocol_error("hash initialization failed");
    }
    std::array<UCHAR, 32> digest{};
    const NTSTATUS update = BCryptHashData(hash,
        reinterpret_cast<PUCHAR>(const_cast<char*>(value.data())),
        static_cast<ULONG>(value.size()), 0);
    const NTSTATUS finish = update < 0 ? update :
        BCryptFinishHash(hash, digest.data(), static_cast<ULONG>(digest.size()), 0);
    BCryptDestroyHash(hash);
    BCryptCloseAlgorithmProvider(algorithm, 0);
    if (finish < 0) throw protocol_error("hash failed");
    std::ostringstream result;
    result << std::hex << std::setfill('0');
    for (UCHAR byte : digest) result << std::setw(2) << static_cast<unsigned>(byte);
    return result.str();
}

std::vector<snapshot_entry> snapshot(const std::filesystem::path& package) {
    const DWORD root_attributes = GetFileAttributesW(package.c_str());
    if (root_attributes == INVALID_FILE_ATTRIBUTES ||
        (root_attributes & FILE_ATTRIBUTE_DIRECTORY) == 0 ||
        (root_attributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0) {
        throw protocol_error("package unavailable");
    }
    std::vector<snapshot_entry> entries;
    for (const auto& item : std::filesystem::recursive_directory_iterator(package)) {
        const DWORD attributes = GetFileAttributesW(item.path().c_str());
        if (attributes == INVALID_FILE_ATTRIBUTES || (attributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0 ||
            (!item.is_regular_file() && !item.is_directory())) {
            throw protocol_error("package unsafe");
        }
        if (item.is_regular_file()) {
            const std::string relative = wide_to_utf8(
                std::filesystem::relative(item.path(), package).generic_wstring());
            if (relative.empty() || relative.find("..") != std::string::npos ||
                relative.find(':') != std::string::npos) {
                throw protocol_error("package unsafe");
            }
            const std::uintmax_t length = item.file_size();
            if (length == 0) throw protocol_error("package inconsistent");
            entries.push_back({relative, length, sha256_file(item.path())});
        }
    }
    std::sort(entries.begin(), entries.end(), [](const auto& left, const auto& right) {
        return left.relative < right.relative;
    });
    if (entries.empty()) throw protocol_error("package inconsistent");
    return entries;
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

const snapshot_entry& find_entry(const std::vector<snapshot_entry>& entries, std::string_view name) {
    const auto found = std::find_if(entries.begin(), entries.end(), [&](const snapshot_entry& entry) {
        return entry.relative == name;
    });
    if (found == entries.end()) throw protocol_error("package resource missing");
    return *found;
}

}  // namespace

package_evidence inspect_package(
    const std::filesystem::path& package,
    const std::string& expected_package_digest,
    const std::string& expected_model_digest,
    std::uintmax_t expected_model_length) {
    const std::vector<snapshot_entry> before = snapshot(package);
    const std::string digest = manifest_digest(before);
    const snapshot_entry& model = find_entry(before, "openvino_model.bin");
    if (digest != expected_package_digest || model.digest != expected_model_digest ||
        model.length != expected_model_length) {
        throw protocol_error("package identity mismatch");
    }

    ov::Core core;
    core.add_extension(executable_directory() / L"openvino_tokenizers.dll");
    (void)core.read_model(package / L"openvino_model.xml");
    (void)core.read_model(package / L"openvino_tokenizer.xml");
    (void)core.read_model(package / L"openvino_detokenizer.xml");

    const std::vector<snapshot_entry> after = snapshot(package);
    if (before.size() != after.size() || manifest_digest(after) != digest) {
        throw protocol_error("package changed");
    }
    return {digest, model.digest, model.length};
}

}  // namespace granite::official_worker
