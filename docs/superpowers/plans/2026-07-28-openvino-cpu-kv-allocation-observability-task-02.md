# Task 02: CPU State Allocation Snapshot Domain Model Micro-Plan

## Task 1: Execute the CPU State Allocation Snapshot Domain Model

> **For the implementer:** Use `superpowers:test-driven-development` and
> `superpowers:subagent-driven-development`. Apply only the exact changes below
> in the derived OpenVINO core checkout. Stop after the task commit and return
> the complete evidence bundle for independent spec and quality review. Do not
> export or edit the tracked parent patch yet; the roadmap reserves the single
> binary-safe patch export for Task 7.

**Roadmap task:** Task 2 in
`docs/superpowers/plans/2026-07-28-openvino-cpu-kv-allocation-observability.md`
(especially lines 22-60, 260-344, 346-707, 943-1031, and 2085-2212 in the
2026-07-28 revision).

**Goal:** Add a private, `CPU_DEBUG_CAPS`-only allocation snapshot model that
measures physical retained backing from owner capacity, preserves active
descriptor bytes as a separate field, deduplicates aliases by tagged shared
ownership, captures the three concrete CPU variable-state classes, and
serializes deterministic bounded JSON without addresses, prompts, tensor
contents, generated text, or public ABI.

**Acceptance gates satisfied:** immutable official source; exact derived-source
identity; strict RED then GREEN; retained-capacity rather than descriptor-size
physical accounting; global tagged-owner alias deduplication; owned beam and
scale/ZP accounting; fail-closed unknown/external handling; checked arithmetic;
complete bounded JSON; no ordinary-build symbols or marker strings; exact
CMake rediscovery; every Python test, compiled test/probe, configure, and build
under the generic Job Object guard with an exact 2,048 MiB RAM floor and zero
survivors; focused tests twice; production plugin build; diff/process/marker
audits; focused commit; later patch-range/replay identity; independent Spec
PASS and Quality PASS.

## Fixed boundaries and assumptions

1. Run this plan from the parent recovery worktree mounted as `R:\`. Resolve
   the derived core only from the accepted canonical identity at
   `R:\external\official-openvino\2026-07-19\openvino-cpu-state-observer.identity.json`.
   Task 01C supersedes the roadmap's earlier in-tree path: the accepted
   physical destination is exactly
   `C:\ov-wb04\2026-07-19\openvino-cpu-state-observer`, and its identity is the
   organized pointer; do not create a junction. `O:` must map to that exact
   physical directory before any configure. Never edit the shared clean source at
   `<shared-repository-root>\external\official-openvino\2026-07-19\openvino`.
2. The clean OpenVINO base is exactly
   `ede283a88e35465f0d680dabbf1f44080f8fc387`. The derived branch is exactly
   `project/cpu-state-allocation-observer`, and its Task 2 starting tree must
   equal the clean base tree. A later approved Task 1 patch may make HEAD differ
   from the base commit only if its identity JSON names that exact HEAD/tree and
   the five Task 2 preimages below are still base-identical/absent.
3. The guard-bootstrap commit must already contain these exact tracked files:
   `scripts/testing/official_openvino/owned_process_guard.py`,
   `scripts/testing/official_openvino/guarded_build.py`, and
   `scripts/testing/invoke_guarded_command.ps1`. Its independent spec and
   quality reviews must both be PASS before any RED command.
4. The wrapper hard-codes `--minimum-available-ram-mib 2048` and independently
   rejects incomplete memory evidence, a minimum below `2147483648`, nonzero
   active Job PIDs after cleanup, any survivor PID, timeout, low-memory stop,
   emergency action, or forced termination. A wrapper failure is a task
   failure; never bypass it.
5. Git inspection, diff, add, and commit commands are direct because the
   roadmap classifies them as non-heavy. Every Python command, CMake configure,
   CMake build, compiled test, and compiled probe in this plan goes through
   `scripts/testing/invoke_guarded_command.ps1`. Do not launch `python`,
   `cmake`, `MSBuild`, `ctest`, or a test executable directly.
6. The physical total is the sum of unique positive retained capacities:
   ordinary input/output/KV owners in `unique_reserved_bytes`, hidden beam
   owners in `beam_reserved_bytes`, and owned `PlainTensor` scale/ZP owners in
   `scale_zp_reserved_bytes`. `active_descriptor_bytes` is descriptive only and
   is never substituted for missing capacity.
7. Alias identity is `(AllocationOwnerDomain, shared-owner control block)`.
   Extraction converts first-seen shared owners to process-local positive
   ordinals; builder/JSON code never sees or serializes a pointer. DNNL and
   `PlainTensor` domains can never alias.
8. Shared owners are globally charged to the first emitted canonical record.
   Every later logical record retains its active descriptor and identical
   capacity but has `aliases_block_ordinal` set to the earlier canonical
   ordinal and contributes zero physical bytes. Per-state physical totals use
   the same canonical-first rule, so their sum equals request physical total.
9. An allocation with unknown retained capacity may exist in the in-memory
   snapshot for diagnosis, but serialization fails closed. A positive external
   DNNL allocation, zero-capacity emitted owner, ownership inconsistency, or
   capacity-changing alias is rejected before a snapshot is returned.
10. The only allowed phases are exact `fresh`, `seeded_no_infer`, and
    `post_infer`. Correlation IDs are exactly 32 lowercase hexadecimal
    characters. Trigger is exact `query_state`; plugin device is exact `CPU`.
11. The JSON limit is exactly 1 MiB (`1,048,576` bytes before the Task 3
    writer adds its newline). Field order is fixed by the implementation below.
12. This plan intentionally makes no CMake-file edit. The production target
    uses `file(GLOB_RECURSE ... src/*.cpp)` and the unit target uses
    `ov_add_test_target(ROOT ...)`; therefore a full reconfigure after adding
    each new `.cpp` is the exact integration action. Generated-project audits
    prove both files were discovered.

## Files in the derived OpenVINO core

- Modify: `src/plugins/intel_cpu/src/cpu_memory.h`
- Modify: `src/plugins/intel_cpu/src/cpu_memory.cpp`
- Create: `src/plugins/intel_cpu/src/utils/state_allocations_dump.hpp`
- Create: `src/plugins/intel_cpu/src/utils/state_allocations_dump.cpp`
- Create: `src/plugins/intel_cpu/tests/unit/state_allocations_dump_test.cpp`

No parent-repository implementation file and no CMake file belongs to this
task commit.

## Step 1: Freeze immutable source, patch blobs, and guard provenance

Run these direct inspection commands from `R:\` before editing:

```powershell
$ErrorActionPreference = "Stop"
$roadmap = "docs\superpowers\plans\2026-07-28-openvino-cpu-kv-allocation-observability.md"
$roadmapHash = (Get-FileHash -LiteralPath $roadmap -Algorithm SHA256).Hash.ToLowerInvariant()
if ($roadmapHash -ne "9497cba857247af42bc7f889c17fc2e248edd6a02cf5703906aec23434504a3e") {
  throw "Task 2 roadmap changed; regenerate and re-review this micro-plan"
}

$commonDirRaw = (& git rev-parse --git-common-dir).Trim()
if (-not $commonDirRaw) { throw "git-common-dir is empty" }
$commonDir = if ([IO.Path]::IsPathRooted($commonDirRaw)) {
  [IO.Path]::GetFullPath($commonDirRaw)
} else {
  (Resolve-Path -LiteralPath $commonDirRaw).Path
}
$sharedRepoRoot = Split-Path -Parent $commonDir
$sharedTop = (& git -C $sharedRepoRoot rev-parse --show-toplevel).Trim()
if (-not [IO.Path]::GetFullPath($sharedTop).Equals(
    [IO.Path]::GetFullPath($sharedRepoRoot),
    [StringComparison]::OrdinalIgnoreCase)) {
  throw "Shared repository root resolution failed"
}

$cleanCore = Join-Path $sharedRepoRoot "external\official-openvino\2026-07-19\openvino"
$identityPath = "R:\external\official-openvino\2026-07-19\openvino-cpu-state-observer.identity.json"
$base = "ede283a88e35465f0d680dabbf1f44080f8fc387"
$identity = Get-Content -LiteralPath $identityPath -Raw | ConvertFrom-Json
$expectedDerivedCore =
  "C:\ov-wb04\2026-07-19\openvino-cpu-state-observer"
if (-not [IO.Path]::GetFullPath([string]$identity.destination_path).Equals(
    [IO.Path]::GetFullPath($expectedDerivedCore),
    [StringComparison]::OrdinalIgnoreCase)) {
  throw "Canonical identity does not name the accepted Task 01C destination"
}
$derivedCore = (Resolve-Path -LiteralPath $identity.destination_path).Path

if ((& git -C $cleanCore rev-parse HEAD).Trim() -ne $base) {
  throw "Clean OpenVINO HEAD is not the exact pin"
}
if (& git -C $cleanCore status --porcelain --untracked-files=all) {
  throw "Clean OpenVINO source is dirty"
}
if ((& git -C $derivedCore branch --show-current).Trim() -ne
    "project/cpu-state-allocation-observer") {
  throw "Derived core is on the wrong branch"
}
if (& git -C $derivedCore status --porcelain --untracked-files=all) {
  throw "Derived core must be clean at the Task 2 boundary"
}
& git -C $derivedCore merge-base --is-ancestor $base HEAD
if ($LASTEXITCODE -ne 0) { throw "Derived core is not based on the exact pin" }

$derivedHead = (& git -C $derivedCore rev-parse HEAD).Trim()
$derivedTree = (& git -C $derivedCore rev-parse "HEAD^{tree}").Trim()
if ($identity.base_commit -ne $base -or
    $identity.patch_commit -ne $derivedHead -or
    $identity.derived_tree -ne $derivedTree -or
    $identity.dirty -ne $false) {
  throw "Derived identity JSON does not match the exact clean checkout"
}

$taskPaths = @(
  "src/plugins/intel_cpu/src/cpu_memory.h",
  "src/plugins/intel_cpu/src/cpu_memory.cpp",
  "src/plugins/intel_cpu/src/utils/state_allocations_dump.hpp",
  "src/plugins/intel_cpu/src/utils/state_allocations_dump.cpp",
  "src/plugins/intel_cpu/tests/unit/state_allocations_dump_test.cpp"
)
$preimages = [ordered]@{}
foreach ($path in $taskPaths) {
  & git -C $derivedCore cat-file -e "HEAD:$path" 2>$null
  if ($LASTEXITCODE -eq 0) {
    $preimages[$path] = (& git -C $derivedCore rev-parse "HEAD:$path").Trim()
  } elseif ($LASTEXITCODE -eq 128) {
    $preimages[$path] = "ABSENT"
  } else {
    throw "Unable to resolve preimage for $path"
  }
}
$cleanPreimages = [ordered]@{}
foreach ($path in $taskPaths) {
  & git -C $cleanCore cat-file -e "$base`:$path" 2>$null
  if ($LASTEXITCODE -eq 0) {
    $cleanPreimages[$path] =
      (& git -C $cleanCore rev-parse "$base`:$path").Trim()
  } elseif ($LASTEXITCODE -eq 128) {
    $cleanPreimages[$path] = "ABSENT"
  } else {
    throw "Unable to resolve clean preimage for $path"
  }
  if ($cleanPreimages[$path] -ne $preimages[$path]) {
    throw "Derived Task 2 preimage differs from immutable base: $path"
  }
}
if ($preimages["src/plugins/intel_cpu/src/utils/state_allocations_dump.hpp"] -ne "ABSENT" -or
    $preimages["src/plugins/intel_cpu/src/utils/state_allocations_dump.cpp"] -ne "ABSENT" -or
    $preimages["src/plugins/intel_cpu/tests/unit/state_allocations_dump_test.cpp"] -ne "ABSENT") {
  throw "A supposedly new Task 2 path already exists"
}

$guardPaths = @(
  "scripts/testing/official_openvino/owned_process_guard.py",
  "scripts/testing/official_openvino/guarded_build.py",
  "scripts/testing/invoke_guarded_command.ps1"
)
$guardBlobs = [ordered]@{}
foreach ($path in $guardPaths) {
  & git cat-file -e "HEAD:$path"
  if ($LASTEXITCODE -ne 0) { throw "Missing committed guard prerequisite: $path" }
  $guardBlobs[$path] = (& git rev-parse "HEAD:$path").Trim()
}

$boundary = [ordered]@{
  schema = "openvino-cpu-observer-task02-boundary/v1"
  roadmap_sha256 = $roadmapHash
  clean_core_commit = (& git -C $cleanCore rev-parse HEAD).Trim()
  clean_core_tree = (& git -C $cleanCore rev-parse "HEAD^{tree}").Trim()
  derived_start_commit = $derivedHead
  derived_start_tree = $derivedTree
  derived_identity_sha256 = (
    Get-FileHash -LiteralPath $identityPath -Algorithm SHA256
  ).Hash.ToLowerInvariant()
  task_preimage_blobs = $preimages
  clean_preimage_blobs = $cleanPreimages
  guard_blobs = $guardBlobs
}
$boundary | ConvertTo-Json -Depth 8 |
  Set-Content -LiteralPath "R:\.superpowers\sdd\task02-boundary.json" -Encoding UTF8
```

The `.superpowers/sdd` boundary record is ignored scratch, not a task change.
Immediately re-read it and keep it in the review evidence. Do not proceed if
any precondition fails.

Establish or validate `O:` with a crash-resumable collision check. Task 01C may
have left `O:` mapped to the now-retired in-tree campaign directory. That one
known mapping may be retired only after proving the accepted identity and
absence of the rejected checkout/replacement identity; every other collision
is rejected:

```powershell
$expectedOTarget = [IO.Path]::GetFullPath($derivedCore).TrimEnd('\')
$parentWorktreePhysical = [IO.Path]::GetFullPath(
  ((& git rev-parse --show-toplevel).Trim() -replace '/', '\')
)
$retiredCampaignTarget = [IO.Path]::GetFullPath(
  (Join-Path $parentWorktreePhysical `
    "external\official-openvino\2026-07-19")
).TrimEnd('\')
$existingOLines = @(& subst.exe) | Where-Object { $_ -match '^O:\\: => ' }
if ($existingOLines.Count -gt 1) {
  throw "More than one O: substitution was reported"
}
if ($existingOLines.Count -eq 1) {
  if ($existingOLines[0] -notmatch '^O:\\: => (?<target>.+)$') {
    throw "Unable to parse the existing O: substitution"
  }
  $actualOTarget = [IO.Path]::GetFullPath($Matches.target).TrimEnd('\')
  if (-not $actualOTarget.Equals(
      $expectedOTarget, [StringComparison]::OrdinalIgnoreCase)) {
    if (-not $actualOTarget.Equals(
        $retiredCampaignTarget,
        [StringComparison]::OrdinalIgnoreCase)) {
      throw "O: has an unknown collision at '$actualOTarget'"
    }
    if ((Test-Path -LiteralPath "O:\openvino-cpu-state-observer") -or
        (Test-Path -LiteralPath `
          "O:\openvino-cpu-state-observer.replacement.identity.json") -or
        $identity.destination_path -ne $expectedDerivedCore) {
      throw "Task 01C retirement is incomplete; refusing to remap O:"
    }
    & subst.exe O: /D
    if ($LASTEXITCODE -ne 0) {
      throw "Unable to retire the exact Task 01C campaign mapping"
    }
    & subst.exe O: $expectedOTarget
    if ($LASTEXITCODE -ne 0) {
      throw "Unable to map accepted derived core to O:"
    }
  }
} else {
  & subst.exe O: $expectedOTarget
  if ($LASTEXITCODE -ne 0) { throw "Unable to map derived core to O:" }
}
```

Keep `O:` mapped through Tasks 2-6. Task 7 owns exact-map revalidation against
the canonical identity destination and removal after every build/test process
has stopped. Its older literal in-tree comparison must be amended to this
accepted Task 01C physical destination before Task 7 executes.

## Step 2: Write the complete RED test file

Create
`O:\src\plugins\intel_cpu\tests\unit\state_allocations_dump_test.cpp` with this
exact body:

```cpp
// Copyright (C) 2018-2026 Intel Corporation
// SPDX-License-Identifier: Apache-2.0
//

#include <gtest/gtest.h>

#include <cstdint>
#include <functional>
#include <limits>
#include <memory>
#include <optional>
#include <string>
#include <utility>
#include <vector>

#include "cpu_memory.h"
#include "memory_desc/cpu_blocked_memory_desc.h"
#include "memory_state.h"
#include "openvino/core/except.hpp"
#include "openvino/core/partial_shape.hpp"
#include "utils/plain_tensor.hpp"
#include "utils/state_allocations_dump.hpp"

#ifdef CPU_DEBUG_CAPS

namespace ov::intel_cpu {
namespace {

const SnapshotContext kContext{
    4242,
    1,
    "0123456789abcdef0123456789abcdef",
    "query_state",
    "post_infer",
    "CPU",
};

ObservedAllocation allocation(std::string state,
                              StateMemoryRole role,
                              AllocationOwnerDomain owner_domain,
                              size_t owner,
                              size_t active,
                              std::optional<size_t> reserved) {
    return {
        std::move(state),
        role == StateMemoryRole::KV || role == StateMemoryRole::BEAM ||
                role == StateMemoryRole::SCALE_ZP
            ? "VariableStateKVcache"
            : "VariableStateDoubleBuffer",
        role,
        owner_domain,
        owner,
        "f16",
        {1, 2, 3, 16},
        {0, 1, 2, 3},
        {96, 48, 16, 1},
        active,
        reserved,
        true,
        false,
    };
}

template <typename Callable>
void expect_ov_exception(Callable&& callable, const std::string& fragment) {
    try {
        callable();
        FAIL() << "Expected ov::Exception containing: " << fragment;
    } catch (const ov::Exception& error) {
        EXPECT_NE(std::string(error.what()).find(fragment), std::string::npos)
            << error.what();
    } catch (...) {
        FAIL() << "Expected ov::Exception containing: " << fragment;
    }
}

MemoryPtr make_memory(ov::element::Type precision, const Shape& shape) {
    dnnl::engine engine(dnnl::engine::kind::cpu, 0);
    auto descriptor = std::make_shared<CpuBlockedMemoryDesc>(precision, shape);
    return std::make_shared<Memory>(engine, descriptor);
}

std::shared_ptr<VariableStateKVcache> make_kv_state(const std::string& name) {
    const Shape dynamic_shape(ov::PartialShape{-1, -1, -1, -1});
    auto external = std::make_shared<CpuBlockedMemoryDesc>(
        ov::element::f16,
        dynamic_shape);
    auto internal = std::make_shared<CpuBlockedMemoryDesc>(
        ov::element::f16,
        dynamic_shape);
    return std::make_shared<VariableStateKVcache>(
        name,
        external,
        internal,
        false,
        0);
}

class UnsupportedMemoryBlock final : public IMemoryBlock {
public:
    void* getRawPtr() const noexcept override {
        return nullptr;
    }
    void setExtBuff(void*, size_t) override {}
    bool resize(size_t) override {
        return false;
    }
    bool hasExtBuffer() const noexcept override {
        return false;
    }
};

class UnsupportedVariableState final : public IVariableState {
public:
    UnsupportedVariableState() : IVariableState("unsupported") {}
    void commit() override {}
    MemoryPtr input_mem() override {
        return nullptr;
    }
    MemoryPtr output_mem() override {
        return nullptr;
    }
    MemoryDescPtr internal_desc() const override {
        return nullptr;
    }
    bool is_reset_state() const override {
        return false;
    }
};

TEST(StateAllocationsDump, ReportsRetainedCapacityOnlyForReuseOwner) {
    DnnlMemoryBlock supported(std::make_unique<MemoryBlockWithReuse>());
    ASSERT_TRUE(supported.getAllocatedSize().has_value());
    EXPECT_EQ(*supported.getAllocatedSize(), 0u);
    EXPECT_TRUE(supported.resize(192));
    ASSERT_TRUE(supported.getAllocatedSize().has_value());
    EXPECT_EQ(*supported.getAllocatedSize(), 192u);

    DnnlMemoryBlock unsupported(std::make_unique<UnsupportedMemoryBlock>());
    EXPECT_FALSE(unsupported.getAllocatedSize().has_value());
}

TEST(StateAllocationsDump, DeduplicatesAliasedInputAndOutputBacking) {
    const auto snapshot = build_state_allocation_snapshot(
        {
            allocation(
                "past_key",
                StateMemoryRole::INPUT,
                AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
                40,
                192,
                192),
            allocation(
                "past_key",
                StateMemoryRole::OUTPUT,
                AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
                40,
                192,
                192),
        },
        1,
        kContext);
    ASSERT_EQ(snapshot.records.size(), 2u);
    ASSERT_TRUE(snapshot.records[1].aliases_block_ordinal.has_value());
    EXPECT_EQ(
        *snapshot.records[1].aliases_block_ordinal,
        snapshot.records[0].block_ordinal);
    EXPECT_EQ(snapshot.unique_reserved_bytes, 192u);
    EXPECT_EQ(snapshot.total_physical_state_bytes, 192u);
    ASSERT_EQ(snapshot.state_totals.size(), 1u);
    EXPECT_EQ(snapshot.state_totals[0].active_descriptor_bytes, 384u);
    EXPECT_EQ(snapshot.state_totals[0].total_physical_state_bytes, 192u);
}

TEST(StateAllocationsDump, ReportsBothDoubleBuffersWithoutAssumingEqualCapacity) {
    const auto snapshot = build_state_allocation_snapshot(
        {
            allocation(
                "past_value",
                StateMemoryRole::INPUT,
                AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
                11,
                64,
                64),
            allocation(
                "past_value",
                StateMemoryRole::OUTPUT,
                AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
                12,
                128,
                128),
        },
        2,
        kContext);
    EXPECT_EQ(snapshot.unique_reserved_bytes, 192u);
    EXPECT_EQ(snapshot.total_physical_state_bytes, 192u);
    EXPECT_FALSE(snapshot.records[0].aliases_block_ordinal.has_value());
    EXPECT_FALSE(snapshot.records[1].aliases_block_ordinal.has_value());
}

TEST(StateAllocationsDump, UnsupportedBlockIsUnknownNotDescriptorGuess) {
    const auto snapshot = build_state_allocation_snapshot(
        {
            allocation(
                "past_key",
                StateMemoryRole::KV,
                AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
                9,
                192,
                std::nullopt),
        },
        3,
        kContext);
    ASSERT_EQ(snapshot.records.size(), 1u);
    EXPECT_FALSE(snapshot.records.front().reserved_backing_bytes.has_value());
    expect_ov_exception(
        [&] {
            static_cast<void>(serialize_state_allocation_snapshot(snapshot));
        },
        "unknown retained capacity");
}

TEST(StateAllocationsDump, AddsOwnedBeamAndScaleZpExactlyOnce) {
    auto beam = allocation(
        "past_key",
        StateMemoryRole::BEAM,
        AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
        3,
        12,
        32);
    beam.element_type = "i32";
    auto scale = allocation(
        "past_key",
        StateMemoryRole::SCALE_ZP,
        AllocationOwnerDomain::PLAIN_TENSOR_OWNER,
        4,
        16,
        64);
    scale.element_type = "f32";
    const auto snapshot = build_state_allocation_snapshot(
        {
            allocation(
                "past_key",
                StateMemoryRole::KV,
                AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
                1,
                96,
                128),
            beam,
            scale,
        },
        4,
        kContext);
    EXPECT_EQ(snapshot.unique_reserved_bytes, 128u);
    EXPECT_EQ(snapshot.beam_reserved_bytes, 32u);
    EXPECT_EQ(snapshot.scale_zp_reserved_bytes, 64u);
    EXPECT_EQ(snapshot.total_physical_state_bytes, 224u);
    ASSERT_EQ(snapshot.state_totals.size(), 1u);
    EXPECT_EQ(snapshot.state_totals[0].unique_reserved_backing_bytes, 128u);
    EXPECT_EQ(snapshot.state_totals[0].beam_reserved_bytes, 32u);
    EXPECT_EQ(snapshot.state_totals[0].scale_zp_reserved_bytes, 64u);
    EXPECT_EQ(snapshot.state_totals[0].total_physical_state_bytes, 224u);
}

TEST(StateAllocationsDump, RejectsCheckedAdditionOverflow) {
    expect_ov_exception(
        [] {
            static_cast<void>(build_state_allocation_snapshot(
                {
                    allocation(
                        "past_key",
                        StateMemoryRole::INPUT,
                        AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
                        1,
                        std::numeric_limits<size_t>::max(),
                        std::numeric_limits<size_t>::max()),
                    allocation(
                        "past_key",
                        StateMemoryRole::OUTPUT,
                        AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
                        2,
                        1,
                        1),
                },
                5,
                kContext));
        },
        "checked addition overflow");
}

TEST(StateAllocationsDump, RejectsEmptyStateName) {
    expect_ov_exception(
        [] {
            static_cast<void>(build_state_allocation_snapshot(
                {
                    allocation(
                        "",
                        StateMemoryRole::INPUT,
                        AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
                        1,
                        1,
                        1),
                },
                6,
                kContext));
        },
        "state_name must be non-empty");
}

TEST(StateAllocationsDump, RejectsDuplicateLogicalStateRole) {
    expect_ov_exception(
        [] {
            static_cast<void>(build_state_allocation_snapshot(
                {
                    allocation(
                        "past_key",
                        StateMemoryRole::INPUT,
                        AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
                        1,
                        1,
                        1),
                    allocation(
                        "past_key",
                        StateMemoryRole::INPUT,
                        AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
                        2,
                        1,
                        1),
                },
                7,
                kContext));
        },
        "duplicate logical-state declaration");
}

TEST(StateAllocationsDump, RejectsInconsistentStateClass) {
    auto second = allocation(
        "past_key",
        StateMemoryRole::OUTPUT,
        AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
        2,
        1,
        1);
    second.state_class = "VariableStateSingleBuffer";
    expect_ov_exception(
        [&] {
            static_cast<void>(build_state_allocation_snapshot(
                {
                    allocation(
                        "past_key",
                        StateMemoryRole::INPUT,
                        AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
                        1,
                        1,
                        1),
                    second,
                },
                8,
                kContext));
        },
        "inconsistent state class");
}

TEST(StateAllocationsDump, RejectsZeroPid) {
    auto context = kContext;
    context.pid = 0;
    expect_ov_exception(
        [&] {
            static_cast<void>(build_state_allocation_snapshot({}, 9, context));
        },
        "pid must be positive");
}

TEST(StateAllocationsDump, RejectsZeroObserverRequestId) {
    auto context = kContext;
    context.observer_request_id = 0;
    expect_ov_exception(
        [&] {
            static_cast<void>(build_state_allocation_snapshot({}, 10, context));
        },
        "observer_request_id must be positive");
}

TEST(StateAllocationsDump, RejectsInvalidCorrelationId) {
    auto context = kContext;
    context.correlation_id = "ABC";
    expect_ov_exception(
        [&] {
            static_cast<void>(build_state_allocation_snapshot({}, 11, context));
        },
        "correlation_id must be 32 lowercase hexadecimal characters");
}

TEST(StateAllocationsDump, RejectsUnknownTrigger) {
    auto context = kContext;
    context.trigger = "infer";
    expect_ov_exception(
        [&] {
            static_cast<void>(build_state_allocation_snapshot({}, 12, context));
        },
        "trigger must be query_state");
}

TEST(StateAllocationsDump, RejectsEmptyAndUnknownPhase) {
    for (const std::string phase : {"", "failed"}) {
        auto context = kContext;
        context.phase = phase;
        expect_ov_exception(
            [&] {
                static_cast<void>(
                    build_state_allocation_snapshot({}, 13, context));
            },
            "phase is not allowed");
    }
}

TEST(StateAllocationsDump, RejectsNonCpuObserverPluginDevice) {
    auto context = kContext;
    context.observer_plugin_device = "AUTO:CPU";
    expect_ov_exception(
        [&] {
            static_cast<void>(build_state_allocation_snapshot({}, 14, context));
        },
        "observer_plugin_device must be CPU");
}

TEST(StateAllocationsDump, RejectsInvalidUtf8AndControlCharacters) {
    auto invalid_utf8 = allocation(
        std::string("past_") + static_cast<char>(0xC3) + static_cast<char>(0x28),
        StateMemoryRole::INPUT,
        AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
        1,
        1,
        1);
    expect_ov_exception(
        [&] {
            static_cast<void>(
                build_state_allocation_snapshot({invalid_utf8}, 15, kContext));
        },
        "state_name must be valid UTF-8 without controls");

    auto control = allocation(
        "past_\nkey",
        StateMemoryRole::INPUT,
        AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
        1,
        1,
        1);
    expect_ov_exception(
        [&] {
            static_cast<void>(
                build_state_allocation_snapshot({control}, 16, kContext));
        },
        "state_name must be valid UTF-8 without controls");
}

TEST(StateAllocationsDump, RejectsAliasCapacityChange) {
    expect_ov_exception(
        [] {
            static_cast<void>(build_state_allocation_snapshot(
                {
                    allocation(
                        "past_key",
                        StateMemoryRole::INPUT,
                        AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
                        8,
                        64,
                        64),
                    allocation(
                        "past_key",
                        StateMemoryRole::OUTPUT,
                        AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
                        8,
                        64,
                        128),
                },
                17,
                kContext));
        },
        "alias changes retained capacity");
}

TEST(StateAllocationsDump, RejectsExternallyBackedDnnlAllocation) {
    auto external = allocation(
        "past_key",
        StateMemoryRole::INPUT,
        AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
        1,
        64,
        64);
    external.owned_backing = false;
    external.external_backing = true;
    expect_ov_exception(
        [&] {
            static_cast<void>(
                build_state_allocation_snapshot({external}, 18, kContext));
        },
        "external DNNL backing is not physical observer evidence");
}

TEST(StateAllocationsDump, RejectsUnsupportedStateSubclass) {
    std::vector<MemStatePtr> states{
        std::make_shared<UnsupportedVariableState>()};
    expect_ov_exception(
        [&] {
            static_cast<void>(capture_state_allocations(states, 19, kContext));
        },
        "unsupported CPU variable state subclass");
}

TEST(StateAllocationsDump, BeamAliasesDeduplicateGloballyAcrossKeyAndValue) {
    auto key = allocation(
        "past_key",
        StateMemoryRole::BEAM,
        AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
        50,
        32,
        32);
    key.element_type = "i32";
    auto value = key;
    value.state_name = "past_value";
    const auto snapshot =
        build_state_allocation_snapshot({key, value}, 20, kContext);
    EXPECT_EQ(snapshot.beam_reserved_bytes, 32u);
    EXPECT_EQ(snapshot.total_physical_state_bytes, 32u);
    ASSERT_TRUE(snapshot.records[1].aliases_block_ordinal.has_value());
    EXPECT_EQ(
        *snapshot.records[1].aliases_block_ordinal,
        snapshot.records[0].block_ordinal);
}

TEST(StateAllocationsDump, CaptureCoversDoubleAndSingleBufferOwnership) {
    auto first = make_memory(ov::element::f16, Shape{1, 2, 2, 4});
    auto second = make_memory(ov::element::f16, Shape{1, 2, 2, 8});
    auto state = std::make_shared<VariableStateDoubleBuffer>(
        "past_key",
        first,
        second,
        first->getDescPtr());
    const auto snapshot =
        capture_state_allocations({state}, 21, kContext);
    ASSERT_EQ(snapshot.records.size(), 2u);
    EXPECT_FALSE(snapshot.records[0].aliases_block_ordinal.has_value());
    EXPECT_FALSE(snapshot.records[1].aliases_block_ordinal.has_value());
    EXPECT_EQ(snapshot.unique_reserved_bytes, 96u);

    auto single_memory =
        make_memory(ov::element::f16, Shape{1, 2, 2, 4});
    auto single = std::make_shared<VariableStateSingleBuffer>(
        "past_value",
        single_memory,
        single_memory->getDescPtr());
    const auto single_snapshot =
        capture_state_allocations({single}, 210, kContext);
    ASSERT_EQ(single_snapshot.records.size(), 2u);
    EXPECT_FALSE(
        single_snapshot.records[0].aliases_block_ordinal.has_value());
    ASSERT_TRUE(
        single_snapshot.records[1].aliases_block_ordinal.has_value());
    EXPECT_EQ(
        *single_snapshot.records[1].aliases_block_ordinal,
        single_snapshot.records[0].block_ordinal);
    EXPECT_EQ(single_snapshot.unique_reserved_bytes, 32u);
    EXPECT_EQ(single_snapshot.total_physical_state_bytes, 32u);
}

TEST(StateAllocationsDump, CaptureDeduplicatesSharedBeamAndScaleOwners) {
    auto key = make_kv_state("past_key");
    auto value = make_kv_state("past_value");
    key->assign_internal_state(
        make_memory(ov::element::f16, Shape{1, 2, 2, 4}));
    value->assign_internal_state(
        make_memory(ov::element::f16, Shape{1, 2, 2, 4}));

    auto shared_beam = make_memory(ov::element::i32, Shape{1, 8});
    key->assign_hidden_state(shared_beam);
    value->assign_hidden_state(shared_beam);

    PlainTensor shared_scale;
    shared_scale.resize<float>({2, 2});
    key->set_scale_zp(shared_scale);
    value->set_scale_zp(shared_scale);

    const auto snapshot =
        capture_state_allocations({key, value}, 22, kContext);
    EXPECT_EQ(snapshot.unique_reserved_bytes, 64u);
    EXPECT_EQ(snapshot.beam_reserved_bytes, 32u);
    EXPECT_EQ(snapshot.scale_zp_reserved_bytes, 16u);
    EXPECT_EQ(snapshot.total_physical_state_bytes, 112u);

    size_t beam_aliases = 0;
    size_t scale_aliases = 0;
    for (const auto& record : snapshot.records) {
        if (record.role == StateMemoryRole::BEAM &&
            record.aliases_block_ordinal.has_value()) {
            ++beam_aliases;
        }
        if (record.role == StateMemoryRole::SCALE_ZP &&
            record.aliases_block_ordinal.has_value()) {
            ++scale_aliases;
        }
    }
    EXPECT_EQ(beam_aliases, 1u);
    EXPECT_EQ(scale_aliases, 1u);
}

TEST(StateAllocationsDump, CaptureSkipsNonOwningScaleView) {
    auto state = make_kv_state("past_key");
    state->assign_internal_state(
        make_memory(ov::element::f16, Shape{1, 2, 2, 4}));
    state->assign_hidden_state(
        make_memory(ov::element::i32, Shape{1, 8}));

    float external_scale[4] = {};
    PlainTensor view;
    view.resize<float>({2, 2}, external_scale);
    ASSERT_EQ(view.m_capacity, 0u);
    state->set_scale_zp(view);

    const auto snapshot =
        capture_state_allocations({state}, 23, kContext);
    for (const auto& record : snapshot.records) {
        EXPECT_NE(record.role, StateMemoryRole::SCALE_ZP);
    }
    EXPECT_EQ(snapshot.scale_zp_reserved_bytes, 0u);
}

TEST(StateAllocationsDump, SerializesDeterministicCompleteBoundedJson) {
    const auto snapshot = build_state_allocation_snapshot(
        {
            allocation(
                "past_key",
                StateMemoryRole::INPUT,
                AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
                1,
                192,
                192),
        },
        24,
        kContext);
    const auto first = serialize_state_allocation_snapshot(snapshot);
    const auto second = serialize_state_allocation_snapshot(snapshot);
    EXPECT_EQ(first, second);
    EXPECT_LE(first.size(), kMaxStateAllocationSnapshotBytes);
    EXPECT_NE(first.find("\"schema_version\":1"), std::string::npos);
    EXPECT_NE(first.find("\"sequence\":24"), std::string::npos);
    EXPECT_NE(first.find("\"owner_domain\":\"dnnl_memory_block\""),
              std::string::npos);
    EXPECT_NE(first.find("\"reserved_backing_bytes\":192"),
              std::string::npos);
    EXPECT_NE(first.find("\"total_physical_state_bytes\":192"),
              std::string::npos);
    EXPECT_EQ(first.find("0x"), std::string::npos);
    EXPECT_EQ(first.find("raw_address"), std::string::npos);
    ASSERT_FALSE(first.empty());
    EXPECT_EQ(first.front(), '{');
    EXPECT_EQ(first.back(), '}');

    std::vector<ObservedAllocation> oversized;
    oversized.reserve(10000);
    for (size_t index = 0; index < 10000; ++index) {
        oversized.push_back(allocation(
            "state_" + std::to_string(index),
            StateMemoryRole::INPUT,
            AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
            index + 1,
            1,
            1));
    }
    const auto oversized_snapshot = build_state_allocation_snapshot(
        std::move(oversized),
        240,
        kContext);
    expect_ov_exception(
        [&] {
            static_cast<void>(
                serialize_state_allocation_snapshot(oversized_snapshot));
        },
        "state allocation JSON exceeds 1048576 bytes");
}

TEST(StateAllocationsDump, AcceptsEveryAllowedPhase) {
    for (const std::string phase :
         {"fresh", "seeded_no_infer", "post_infer"}) {
        auto context = kContext;
        context.phase = phase;
        const auto snapshot =
            build_state_allocation_snapshot({}, 25, context);
        EXPECT_EQ(snapshot.phase, phase);
    }
}

}  // namespace
}  // namespace ov::intel_cpu

#endif  // CPU_DEBUG_CAPS
```

This is the only test source for Task 2. Do not weaken an assertion to make
GREEN; implementation must satisfy the exact contract.

## Step 3: Reconfigure and prove RED under the process guard

Use the roadmap's exact configure route. A prior label is never overwritten.
Use a fresh attempt directory for a full execution or retry and retain every
earlier attempt; within one attempt the labels below remain exact. An
interrupted attempt is audited as incomplete and a full retry uses the next
numbered directory.

```powershell
$guardEvidence =
  "R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards\task02-attempt-001"
if ((Test-Path -LiteralPath $guardEvidence) -and
    @(Get-ChildItem -LiteralPath $guardEvidence -Force).Count -ne 0) {
  throw "Task 2 attempt evidence already exists; audit it and select a new numbered attempt directory"
}
$wrapper = "R:\scripts\testing\invoke_guarded_command.ps1"
$cmake = "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"
$configureCommand = @(
  $cmake, "-S", "O:\", "-B", "C:\ov-build\state-observer",
  "-G", "Visual Studio 18 2026", "-A", "x64",
  "-DENABLE_DEBUG_CAPS=ON", "-DENABLE_CPU_DEBUG_CAPS=ON",
  "-DENABLE_TESTS=ON", "-DENABLE_FUNCTIONAL_TESTS=OFF",
  "-DENABLE_SAMPLES=OFF", "-DENABLE_PYTHON=OFF",
  "-DENABLE_INTEL_GPU=OFF", "-DENABLE_INTEL_NPU=OFF",
  "-DENABLE_OV_ONNX_FRONTEND=OFF", "-DENABLE_OV_PADDLE_FRONTEND=OFF",
  "-DENABLE_OV_TF_FRONTEND=OFF", "-DENABLE_LTO=OFF",
  "-DBUILD_SHARED_LIBS=ON"
)
$buildUnitCommand = @(
  $cmake, "--build", "C:\ov-build\state-observer", "--config", "Release",
  "--target", "ov_cpu_unit_tests", "--parallel", "2"
)

& $wrapper `
  -Label task2-configure-red `
  -WorkingDirectory R:\ `
  -EvidenceRoot $guardEvidence `
  -ExpectedExit Zero `
  -Command $configureCommand

$cache = Get-Content -LiteralPath "C:\ov-build\state-observer\CMakeCache.txt"
foreach ($required in @(
  "ENABLE_DEBUG_CAPS:BOOL=ON",
  "ENABLE_CPU_DEBUG_CAPS:BOOL=ON",
  "ENABLE_TESTS:BOOL=ON",
  "ENABLE_FUNCTIONAL_TESTS:BOOL=OFF",
  "BUILD_SHARED_LIBS:BOOL=ON"
)) {
  if ($cache -notcontains $required) {
    throw "Observer configure cache is missing '$required'"
  }
}

& $wrapper `
  -Label task2-build-red `
  -WorkingDirectory R:\ `
  -EvidenceRoot $guardEvidence `
  -ExpectedExit NonZero `
  -Command $buildUnitCommand

$redLog = Get-Content -LiteralPath "$guardEvidence\task2-build-red.log" -Raw
if ($redLog -notmatch "state_allocations_dump.hpp|SnapshotContext|ObservedAllocation") {
  throw "RED did not fail for the absent Task 2 API"
}
```

Expected:

- configure exits zero;
- the cache proves both debug options and unit tests are enabled;
- the guarded build exits nonzero because the exact test references the absent
  header/domain/accessor;
- both guard JSON records show
  `configured_minimum_available_ram_bytes == 2147483648`,
  observed minimum RAM at or above that floor,
  `queried_active_process_count_after_cleanup == 0`, empty
  `survivor_pids_after_cleanup`, and `valid == true`;
- no compiler, linker, MSBuild, or test child survives.

Do not proceed if the RED reason is environmental, a path collision, OOM,
timeout, or any failure other than the absent Task 2 contract.

## Step 4: Add the retained-capacity accessor exactly

In `O:\src\plugins\intel_cpu\src\cpu_memory.h`, add this include immediately
after `#include <memory>`:

```cpp
#ifdef CPU_DEBUG_CAPS
#    include <optional>
#endif
```

In the public section of `DnnlMemoryBlock`, immediately after
`hasExtBuffer() const noexcept`, add:

```cpp
#ifdef CPU_DEBUG_CAPS
    [[nodiscard]] std::optional<size_t> getAllocatedSize() const noexcept;
#endif
```

In `O:\src\plugins\intel_cpu\src\cpu_memory.cpp`, immediately after
`DnnlMemoryBlock::hasExtBuffer()`, add:

```cpp
#ifdef CPU_DEBUG_CAPS
std::optional<size_t> DnnlMemoryBlock::getAllocatedSize() const noexcept {
    const auto* reuse =
        dynamic_cast<const MemoryBlockWithReuse*>(m_pMemBlock.get());
    if (reuse == nullptr) {
        return std::nullopt;
    }
    return reuse->size();
}
#endif
```

Do not add an accessor to `IMemoryBlock`, export a symbol, expose a public
runtime property, or fall back to `Memory::getSize()`. A non-reuse owner is
unknown, not descriptor-sized physical evidence.

## Step 5: Create the exact private domain-model header

Create `O:\src\plugins\intel_cpu\src\utils\state_allocations_dump.hpp` with
this exact body:

```cpp
// Copyright (C) 2018-2026 Intel Corporation
// SPDX-License-Identifier: Apache-2.0
//

#pragma once

#ifdef CPU_DEBUG_CAPS

#include <cstddef>
#include <cstdint>
#include <optional>
#include <string>
#include <vector>

#include "cpu_types.h"
#include "memory_state.h"

namespace ov::intel_cpu {

inline constexpr size_t kMaxStateAllocationSnapshotBytes = 1024U * 1024U;

enum class StateMemoryRole {
    INPUT,
    OUTPUT,
    KV,
    BEAM,
    SCALE_ZP,
};

struct SnapshotContext {
    uint32_t pid;
    uint64_t observer_request_id;
    std::string correlation_id;
    std::string trigger = "query_state";
    std::string phase;
    std::string observer_plugin_device = "CPU";
};

enum class AllocationOwnerDomain {
    DNNL_MEMORY_BLOCK,
    PLAIN_TENSOR_OWNER,
};

struct ObservedAllocation {
    std::string state_name;
    std::string state_class;
    StateMemoryRole role;
    AllocationOwnerDomain source_owner_domain;
    size_t source_owner_ordinal;
    std::string element_type;
    VectorDims shape;
    VectorDims order;
    VectorDims strides;
    size_t active_descriptor_bytes;
    std::optional<size_t> reserved_backing_bytes;
    bool owned_backing;
    bool external_backing;
};

struct StateAllocationRecord {
    std::string state_name;
    std::string state_class;
    StateMemoryRole role;
    AllocationOwnerDomain owner_domain;
    size_t block_ordinal;
    std::optional<size_t> aliases_block_ordinal;
    std::string element_type;
    VectorDims shape;
    VectorDims order;
    VectorDims strides;
    size_t active_descriptor_bytes;
    std::optional<size_t> reserved_backing_bytes;
    bool owned_backing;
    bool external_backing;
};

struct StateAllocationTotal {
    std::string state_name;
    size_t active_descriptor_bytes = 0;
    size_t unique_reserved_backing_bytes = 0;
    size_t beam_reserved_bytes = 0;
    size_t scale_zp_reserved_bytes = 0;
    size_t total_physical_state_bytes = 0;
};

struct StateAllocationSnapshot {
    uint32_t schema_version = 1;
    uint64_t sequence = 0;
    uint32_t pid = 0;
    uint64_t observer_request_id = 0;
    std::string correlation_id;
    std::string trigger;
    std::string phase;
    std::string observer_plugin_device;
    std::vector<StateAllocationRecord> records;
    std::vector<StateAllocationTotal> state_totals;
    size_t unique_reserved_bytes = 0;
    size_t beam_reserved_bytes = 0;
    size_t scale_zp_reserved_bytes = 0;
    size_t total_physical_state_bytes = 0;
};

StateAllocationSnapshot build_state_allocation_snapshot(
    std::vector<ObservedAllocation> observed,
    uint64_t sequence,
    const SnapshotContext& context);

StateAllocationSnapshot capture_state_allocations(
    const std::vector<MemStatePtr>& states,
    uint64_t sequence,
    const SnapshotContext& context);

std::string serialize_state_allocation_snapshot(
    const StateAllocationSnapshot& snapshot);

}  // namespace ov::intel_cpu

#endif  // CPU_DEBUG_CAPS
```

Every declaration and marker is lexically inside `CPU_DEBUG_CAPS`; the ordinary
translation unit sees no observer declaration.

## Step 6: Create the exact extraction, builder, and serializer

Create `O:\src\plugins\intel_cpu\src\utils\state_allocations_dump.cpp` with
this exact body:

```cpp
// Copyright (C) 2018-2026 Intel Corporation
// SPDX-License-Identifier: Apache-2.0
//

#include "utils/state_allocations_dump.hpp"

#ifdef CPU_DEBUG_CAPS

#include <algorithm>
#include <array>
#include <cstdint>
#include <limits>
#include <map>
#include <memory>
#include <numeric>
#include <optional>
#include <set>
#include <sstream>
#include <string>
#include <tuple>
#include <utility>
#include <vector>

#include "memory_desc/blocked_memory_desc.h"
#include "openvino/core/except.hpp"
#include "utils/plain_tensor.hpp"

namespace ov::intel_cpu {
namespace {

using OwnerKey = std::pair<AllocationOwnerDomain, size_t>;

struct CanonicalOwner {
    size_t block_ordinal;
    std::optional<size_t> reserved_backing_bytes;
    bool owned_backing;
    bool external_backing;
};

size_t checked_add(size_t left, size_t right, const char* field) {
    if (right > std::numeric_limits<size_t>::max() - left) {
        OPENVINO_THROW("state allocation checked addition overflow: ", field);
    }
    return left + right;
}

size_t checked_multiply(size_t left, size_t right, const char* field) {
    if (left != 0 && right > std::numeric_limits<size_t>::max() / left) {
        OPENVINO_THROW(
            "state allocation checked multiplication overflow: ",
            field);
    }
    return left * right;
}

bool is_continuation(uint8_t byte) {
    return (byte & 0xC0U) == 0x80U;
}

bool is_valid_utf8_without_controls(const std::string& value) {
    size_t index = 0;
    while (index < value.size()) {
        const auto first = static_cast<uint8_t>(value[index]);
        uint32_t code_point = 0;
        size_t width = 0;

        if (first <= 0x7FU) {
            code_point = first;
            width = 1;
        } else if (first >= 0xC2U && first <= 0xDFU) {
            code_point = first & 0x1FU;
            width = 2;
        } else if (first >= 0xE0U && first <= 0xEFU) {
            code_point = first & 0x0FU;
            width = 3;
        } else if (first >= 0xF0U && first <= 0xF4U) {
            code_point = first & 0x07U;
            width = 4;
        } else {
            return false;
        }

        if (index + width > value.size()) {
            return false;
        }
        for (size_t offset = 1; offset < width; ++offset) {
            const auto next = static_cast<uint8_t>(value[index + offset]);
            if (!is_continuation(next)) {
                return false;
            }
            code_point = (code_point << 6U) | (next & 0x3FU);
        }

        if ((width == 2 && code_point < 0x80U) ||
            (width == 3 && code_point < 0x800U) ||
            (width == 4 && code_point < 0x10000U) ||
            (code_point >= 0xD800U && code_point <= 0xDFFFU) ||
            code_point > 0x10FFFFU) {
            return false;
        }
        if ((code_point <= 0x1FU) ||
            (code_point >= 0x7FU && code_point <= 0x9FU)) {
            return false;
        }
        index += width;
    }
    return true;
}

void require_text(
    const std::string& value,
    const char* field,
    bool require_non_empty = true) {
    if (require_non_empty && value.empty()) {
        OPENVINO_THROW(field, " must be non-empty");
    }
    if (!is_valid_utf8_without_controls(value)) {
        OPENVINO_THROW(field, " must be valid UTF-8 without controls");
    }
}

bool is_lower_hex_correlation(const std::string& value) {
    if (value.size() != 32) {
        return false;
    }
    return std::all_of(value.begin(), value.end(), [](char character) {
        return (character >= '0' && character <= '9') ||
               (character >= 'a' && character <= 'f');
    });
}

bool is_allowed_phase(const std::string& phase) {
    return phase == "fresh" || phase == "seeded_no_infer" ||
           phase == "post_infer";
}

void validate_context(
    uint64_t sequence,
    uint32_t pid,
    uint64_t observer_request_id,
    const std::string& correlation_id,
    const std::string& trigger,
    const std::string& phase,
    const std::string& observer_plugin_device) {
    if (sequence == 0) {
        OPENVINO_THROW("sequence must be positive");
    }
    if (pid == 0) {
        OPENVINO_THROW("pid must be positive");
    }
    if (observer_request_id == 0) {
        OPENVINO_THROW("observer_request_id must be positive");
    }
    if (!is_lower_hex_correlation(correlation_id)) {
        OPENVINO_THROW(
            "correlation_id must be 32 lowercase hexadecimal characters");
    }
    if (trigger != "query_state") {
        OPENVINO_THROW("trigger must be query_state");
    }
    if (!is_allowed_phase(phase)) {
        OPENVINO_THROW("phase is not allowed");
    }
    if (observer_plugin_device != "CPU") {
        OPENVINO_THROW("observer_plugin_device must be CPU");
    }
}

bool is_known_state_class(const std::string& state_class) {
    return state_class == "VariableStateDoubleBuffer" ||
           state_class == "VariableStateSingleBuffer" ||
           state_class == "VariableStateKVcache";
}

bool role_is_valid_for_class(
    const std::string& state_class,
    StateMemoryRole role) {
    if (state_class == "VariableStateDoubleBuffer" ||
        state_class == "VariableStateSingleBuffer") {
        return role == StateMemoryRole::INPUT ||
               role == StateMemoryRole::OUTPUT;
    }
    if (state_class == "VariableStateKVcache") {
        return role == StateMemoryRole::KV ||
               role == StateMemoryRole::BEAM ||
               role == StateMemoryRole::SCALE_ZP;
    }
    return false;
}

const char* role_name(StateMemoryRole role) {
    switch (role) {
    case StateMemoryRole::INPUT:
        return "input";
    case StateMemoryRole::OUTPUT:
        return "output";
    case StateMemoryRole::KV:
        return "kv";
    case StateMemoryRole::BEAM:
        return "beam";
    case StateMemoryRole::SCALE_ZP:
        return "scale_zp";
    }
    OPENVINO_THROW("unknown state memory role");
}

const char* owner_domain_name(AllocationOwnerDomain domain) {
    switch (domain) {
    case AllocationOwnerDomain::DNNL_MEMORY_BLOCK:
        return "dnnl_memory_block";
    case AllocationOwnerDomain::PLAIN_TENSOR_OWNER:
        return "plain_tensor_owner";
    }
    OPENVINO_THROW("unknown allocation owner domain");
}

void validate_layout(const ObservedAllocation& item) {
    if (item.shape.size() != item.order.size() ||
        item.shape.size() != item.strides.size()) {
        OPENVINO_THROW("shape/order/strides rank mismatch");
    }
    std::vector<size_t> sorted_order = item.order;
    std::sort(sorted_order.begin(), sorted_order.end());
    for (size_t index = 0; index < sorted_order.size(); ++index) {
        if (sorted_order[index] != index) {
            OPENVINO_THROW("order is not a rank permutation");
        }
    }
}

void validate_observed(const ObservedAllocation& item) {
    require_text(item.state_name, "state_name");
    require_text(item.state_class, "state_class");
    require_text(item.element_type, "element_type");
    if (!is_known_state_class(item.state_class)) {
        OPENVINO_THROW("unsupported CPU variable state subclass: ",
                       item.state_class);
    }
    if (!role_is_valid_for_class(item.state_class, item.role)) {
        OPENVINO_THROW("state role is invalid for concrete state class");
    }
    if (item.source_owner_ordinal == 0) {
        OPENVINO_THROW("source owner ordinal must be positive");
    }
    if (item.external_backing &&
        item.source_owner_domain ==
            AllocationOwnerDomain::DNNL_MEMORY_BLOCK) {
        OPENVINO_THROW(
            "external DNNL backing is not physical observer evidence");
    }
    if (!item.owned_backing || item.external_backing) {
        OPENVINO_THROW("emitted allocation must have owned backing");
    }
    if (item.role == StateMemoryRole::SCALE_ZP &&
        item.source_owner_domain !=
            AllocationOwnerDomain::PLAIN_TENSOR_OWNER) {
        OPENVINO_THROW("scale/ZP allocation must use PlainTensor owner domain");
    }
    if (item.role != StateMemoryRole::SCALE_ZP &&
        item.source_owner_domain ==
            AllocationOwnerDomain::PLAIN_TENSOR_OWNER) {
        OPENVINO_THROW("PlainTensor owner domain is reserved for scale/ZP");
    }
    if (item.reserved_backing_bytes.has_value()) {
        if (*item.reserved_backing_bytes == 0) {
            OPENVINO_THROW("retained capacity must be positive");
        }
        if (item.active_descriptor_bytes >
            *item.reserved_backing_bytes) {
            OPENVINO_THROW(
                "active descriptor bytes exceed retained capacity");
        }
    }
    validate_layout(item);
}

template <typename Pointer>
size_t shared_owner_ordinal(
    const Pointer& owner,
    std::vector<Pointer>& first_seen) {
    if (!owner) {
        return 0;
    }
    const std::owner_less<Pointer> less;
    for (size_t index = 0; index < first_seen.size(); ++index) {
        if (!less(owner, first_seen[index]) &&
            !less(first_seen[index], owner)) {
            return index + 1;
        }
    }
    first_seen.push_back(owner);
    return first_seen.size();
}

size_t plain_tensor_active_bytes(const PlainTensor& tensor) {
    if (tensor.m_rank == 0) {
        return 0;
    }
    size_t greatest_element_offset = 0;
    for (size_t index = 0; index < tensor.m_rank; ++index) {
        if (tensor.m_dims[index] == 0) {
            return 0;
        }
        const auto extent = checked_multiply(
            tensor.m_dims[index] - 1,
            tensor.m_strides[index],
            "PlainTensor active extent");
        greatest_element_offset = checked_add(
            greatest_element_offset,
            extent,
            "PlainTensor active extent");
    }
    const auto element_count = checked_add(
        greatest_element_offset,
        1,
        "PlainTensor active elements");
    const auto unpacked_bytes = checked_multiply(
        element_count,
        tensor.m_element_size,
        "PlainTensor active bytes");
    return checked_add(
               unpacked_bytes,
               tensor.m_sub_byte_multiplier - 1,
               "PlainTensor packed rounding") /
           tensor.m_sub_byte_multiplier;
}

ObservedAllocation observe_memory(
    const std::string& state_name,
    const std::string& state_class,
    StateMemoryRole role,
    const MemoryPtr& memory,
    size_t owner_ordinal) {
    if (!memory) {
        OPENVINO_THROW("observe_memory requires a non-null memory");
    }
    const auto blocked =
        memory->getDescWithType<BlockedMemoryDesc>();
    if (!blocked) {
        OPENVINO_THROW("state memory descriptor is not blocked");
    }
    const auto owner = memory->getMemoryBlock();
    if (!owner || owner_ordinal == 0) {
        OPENVINO_THROW("state memory has no shared backing owner");
    }
    const auto dnnl_owner =
        std::dynamic_pointer_cast<DnnlMemoryBlock>(owner);
    const auto retained = dnnl_owner
                              ? dnnl_owner->getAllocatedSize()
                              : std::optional<size_t>{};
    return {
        state_name,
        state_class,
        role,
        AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
        owner_ordinal,
        memory->getPrecision().get_type_name(),
        memory->getStaticDims(),
        blocked->getOrder(),
        blocked->getStrides(),
        memory->getSize(),
        retained,
        !owner->hasExtBuffer(),
        owner->hasExtBuffer(),
    };
}

void append_memory(
    std::vector<ObservedAllocation>& observed,
    std::vector<MemoryBlockPtr>& owners,
    const std::string& state_name,
    const std::string& state_class,
    StateMemoryRole role,
    const MemoryPtr& memory) {
    if (!memory) {
        return;
    }
    const auto owner = memory->getMemoryBlock();
    const auto ordinal = shared_owner_ordinal(owner, owners);
    auto item = observe_memory(
        state_name,
        state_class,
        role,
        memory,
        ordinal);
    if (!item.external_backing &&
        item.reserved_backing_bytes.has_value() &&
        *item.reserved_backing_bytes == 0) {
        return;
    }
    observed.push_back(std::move(item));
}

void append_scale_zp(
    std::vector<ObservedAllocation>& observed,
    std::vector<std::shared_ptr<uint8_t>>& owners,
    const std::string& state_name,
    const PlainTensor& tensor) {
    if (!tensor.m_ptr || tensor.m_capacity == 0) {
        return;
    }
    const auto ordinal = shared_owner_ordinal(tensor.m_ptr, owners);
    VectorDims order(tensor.m_rank);
    std::iota(order.begin(), order.end(), size_t{0});
    observed.push_back({
        state_name,
        "VariableStateKVcache",
        StateMemoryRole::SCALE_ZP,
        AllocationOwnerDomain::PLAIN_TENSOR_OWNER,
        ordinal,
        tensor.get_precision().get_type_name(),
        tensor.shape(),
        std::move(order),
        tensor.get_strides<size_t>(),
        plain_tensor_active_bytes(tensor),
        tensor.m_capacity,
        true,
        false,
    });
}

StateAllocationTotal& state_total(
    StateAllocationSnapshot& snapshot,
    std::map<std::string, size_t>& indexes,
    const std::string& state_name) {
    const auto found = indexes.find(state_name);
    if (found != indexes.end()) {
        return snapshot.state_totals[found->second];
    }
    const auto index = snapshot.state_totals.size();
    indexes.emplace(state_name, index);
    snapshot.state_totals.push_back({state_name});
    return snapshot.state_totals.back();
}

void add_physical_capacity(
    StateAllocationSnapshot& snapshot,
    StateAllocationTotal& total,
    StateMemoryRole role,
    size_t capacity) {
    if (role == StateMemoryRole::BEAM) {
        snapshot.beam_reserved_bytes = checked_add(
            snapshot.beam_reserved_bytes,
            capacity,
            "beam_reserved_bytes");
        total.beam_reserved_bytes = checked_add(
            total.beam_reserved_bytes,
            capacity,
            "state beam_reserved_bytes");
    } else if (role == StateMemoryRole::SCALE_ZP) {
        snapshot.scale_zp_reserved_bytes = checked_add(
            snapshot.scale_zp_reserved_bytes,
            capacity,
            "scale_zp_reserved_bytes");
        total.scale_zp_reserved_bytes = checked_add(
            total.scale_zp_reserved_bytes,
            capacity,
            "state scale_zp_reserved_bytes");
    } else {
        snapshot.unique_reserved_bytes = checked_add(
            snapshot.unique_reserved_bytes,
            capacity,
            "unique_reserved_bytes");
        total.unique_reserved_backing_bytes = checked_add(
            total.unique_reserved_backing_bytes,
            capacity,
            "state unique_reserved_backing_bytes");
    }
    snapshot.total_physical_state_bytes = checked_add(
        snapshot.total_physical_state_bytes,
        capacity,
        "total_physical_state_bytes");
    total.total_physical_state_bytes = checked_add(
        total.total_physical_state_bytes,
        capacity,
        "state total_physical_state_bytes");
}

class BoundedJson {
public:
    void append(const std::string& value) {
        if (value.size() > kMaxStateAllocationSnapshotBytes - text.size()) {
            OPENVINO_THROW(
                "state allocation JSON exceeds 1048576 bytes");
        }
        text += value;
    }

    void append(char value) {
        if (text.size() == kMaxStateAllocationSnapshotBytes) {
            OPENVINO_THROW(
                "state allocation JSON exceeds 1048576 bytes");
        }
        text.push_back(value);
    }

    template <typename Integer>
    void append_number(Integer value) {
        append(std::to_string(value));
    }

    void append_string(const std::string& value) {
        append('"');
        for (const auto raw : value) {
            const auto byte = static_cast<uint8_t>(raw);
            switch (byte) {
            case '"':
                append("\\\"");
                break;
            case '\\':
                append("\\\\");
                break;
            default:
                append(static_cast<char>(byte));
                break;
            }
        }
        append('"');
    }

    std::string take() {
        return std::move(text);
    }

private:
    std::string text;
};

void append_vector(BoundedJson& json, const VectorDims& values) {
    json.append('[');
    for (size_t index = 0; index < values.size(); ++index) {
        if (index != 0) {
            json.append(',');
        }
        json.append_number(values[index]);
    }
    json.append(']');
}

void validate_serializable_record(const StateAllocationRecord& record) {
    require_text(record.state_name, "state_name");
    require_text(record.state_class, "state_class");
    require_text(record.element_type, "element_type");
    if (!record.reserved_backing_bytes.has_value()) {
        OPENVINO_THROW(
            "cannot serialize allocation with unknown retained capacity");
    }
    if (*record.reserved_backing_bytes == 0) {
        OPENVINO_THROW("cannot serialize zero retained capacity");
    }
    if (!record.owned_backing || record.external_backing) {
        OPENVINO_THROW("cannot serialize non-owned allocation");
    }
}

void append_record(BoundedJson& json, const StateAllocationRecord& record) {
    validate_serializable_record(record);
    json.append("{\"state_name\":");
    json.append_string(record.state_name);
    json.append(",\"state_class\":");
    json.append_string(record.state_class);
    json.append(",\"role\":");
    json.append_string(role_name(record.role));
    json.append(",\"owner_domain\":");
    json.append_string(owner_domain_name(record.owner_domain));
    json.append(",\"block_ordinal\":");
    json.append_number(record.block_ordinal);
    json.append(",\"aliases_block_ordinal\":");
    if (record.aliases_block_ordinal.has_value()) {
        json.append_number(*record.aliases_block_ordinal);
    } else {
        json.append("null");
    }
    json.append(",\"element_type\":");
    json.append_string(record.element_type);
    json.append(",\"shape\":");
    append_vector(json, record.shape);
    json.append(",\"order\":");
    append_vector(json, record.order);
    json.append(",\"strides\":");
    append_vector(json, record.strides);
    json.append(",\"active_descriptor_bytes\":");
    json.append_number(record.active_descriptor_bytes);
    json.append(",\"reserved_backing_bytes\":");
    json.append_number(*record.reserved_backing_bytes);
    json.append(",\"owned_backing\":true,\"external_backing\":false}");
}

void append_total(BoundedJson& json, const StateAllocationTotal& total) {
    json.append("{\"state_name\":");
    json.append_string(total.state_name);
    json.append(",\"active_descriptor_bytes\":");
    json.append_number(total.active_descriptor_bytes);
    json.append(",\"unique_reserved_backing_bytes\":");
    json.append_number(total.unique_reserved_backing_bytes);
    json.append(",\"beam_reserved_bytes\":");
    json.append_number(total.beam_reserved_bytes);
    json.append(",\"scale_zp_reserved_bytes\":");
    json.append_number(total.scale_zp_reserved_bytes);
    json.append(",\"total_physical_state_bytes\":");
    json.append_number(total.total_physical_state_bytes);
    json.append('}');
}

}  // namespace

StateAllocationSnapshot build_state_allocation_snapshot(
    std::vector<ObservedAllocation> observed,
    uint64_t sequence,
    const SnapshotContext& context) {
    validate_context(
        sequence,
        context.pid,
        context.observer_request_id,
        context.correlation_id,
        context.trigger,
        context.phase,
        context.observer_plugin_device);

    StateAllocationSnapshot snapshot;
    snapshot.sequence = sequence;
    snapshot.pid = context.pid;
    snapshot.observer_request_id = context.observer_request_id;
    snapshot.correlation_id = context.correlation_id;
    snapshot.trigger = context.trigger;
    snapshot.phase = context.phase;
    snapshot.observer_plugin_device = context.observer_plugin_device;
    snapshot.records.reserve(observed.size());

    std::set<std::pair<std::string, StateMemoryRole>> declarations;
    std::map<std::string, std::string> state_classes;
    std::map<std::string, size_t> state_indexes;
    std::map<OwnerKey, CanonicalOwner> owners;
    size_t next_block_ordinal = 1;

    for (auto& item : observed) {
        validate_observed(item);
        if (!declarations.emplace(item.state_name, item.role).second) {
            OPENVINO_THROW("duplicate logical-state declaration");
        }
        const auto class_entry =
            state_classes.emplace(item.state_name, item.state_class);
        if (!class_entry.second &&
            class_entry.first->second != item.state_class) {
            OPENVINO_THROW("inconsistent state class for state_name");
        }

        auto& total =
            state_total(snapshot, state_indexes, item.state_name);
        total.active_descriptor_bytes = checked_add(
            total.active_descriptor_bytes,
            item.active_descriptor_bytes,
            "state active_descriptor_bytes");

        const OwnerKey key{
            item.source_owner_domain,
            item.source_owner_ordinal,
        };
        const auto found = owners.find(key);
        std::optional<size_t> alias;
        size_t block_ordinal = 0;
        if (found == owners.end()) {
            block_ordinal = next_block_ordinal++;
            owners.emplace(
                key,
                CanonicalOwner{
                    block_ordinal,
                    item.reserved_backing_bytes,
                    item.owned_backing,
                    item.external_backing,
                });
            if (item.reserved_backing_bytes.has_value()) {
                add_physical_capacity(
                    snapshot,
                    total,
                    item.role,
                    *item.reserved_backing_bytes);
            }
        } else {
            block_ordinal = found->second.block_ordinal;
            alias = block_ordinal;
            if (found->second.reserved_backing_bytes !=
                item.reserved_backing_bytes) {
                OPENVINO_THROW("alias changes retained capacity");
            }
            if (found->second.owned_backing != item.owned_backing ||
                found->second.external_backing != item.external_backing) {
                OPENVINO_THROW("alias changes backing ownership");
            }
        }

        snapshot.records.push_back({
            std::move(item.state_name),
            std::move(item.state_class),
            item.role,
            item.source_owner_domain,
            block_ordinal,
            alias,
            std::move(item.element_type),
            std::move(item.shape),
            std::move(item.order),
            std::move(item.strides),
            item.active_descriptor_bytes,
            item.reserved_backing_bytes,
            item.owned_backing,
            item.external_backing,
        });
    }
    return snapshot;
}

StateAllocationSnapshot capture_state_allocations(
    const std::vector<MemStatePtr>& states,
    uint64_t sequence,
    const SnapshotContext& context) {
    std::vector<ObservedAllocation> observed;
    std::vector<MemoryBlockPtr> memory_owners;
    std::vector<std::shared_ptr<uint8_t>> plain_owners;

    for (const auto& state : states) {
        if (!state) {
            OPENVINO_THROW("state list contains a null state");
        }
        const auto& name = state->get_name();
        if (const auto kv =
                std::dynamic_pointer_cast<VariableStateKVcache>(state)) {
            append_memory(
                observed,
                memory_owners,
                name,
                "VariableStateKVcache",
                StateMemoryRole::KV,
                kv->input_mem());
            append_memory(
                observed,
                memory_owners,
                name,
                "VariableStateKVcache",
                StateMemoryRole::BEAM,
                kv->hidden_state_mem());
            append_scale_zp(
                observed,
                plain_owners,
                name,
                kv->get_scale_zp());
        } else if (const auto double_buffer =
                       std::dynamic_pointer_cast<
                           VariableStateDoubleBuffer>(state)) {
            append_memory(
                observed,
                memory_owners,
                name,
                "VariableStateDoubleBuffer",
                StateMemoryRole::INPUT,
                double_buffer->input_mem());
            append_memory(
                observed,
                memory_owners,
                name,
                "VariableStateDoubleBuffer",
                StateMemoryRole::OUTPUT,
                double_buffer->output_mem());
        } else if (const auto single_buffer =
                       std::dynamic_pointer_cast<
                           VariableStateSingleBuffer>(state)) {
            append_memory(
                observed,
                memory_owners,
                name,
                "VariableStateSingleBuffer",
                StateMemoryRole::INPUT,
                single_buffer->input_mem());
            append_memory(
                observed,
                memory_owners,
                name,
                "VariableStateSingleBuffer",
                StateMemoryRole::OUTPUT,
                single_buffer->output_mem());
        } else {
            OPENVINO_THROW(
                "unsupported CPU variable state subclass: ",
                name);
        }
    }

    return build_state_allocation_snapshot(
        std::move(observed),
        sequence,
        context);
}

std::string serialize_state_allocation_snapshot(
    const StateAllocationSnapshot& snapshot) {
    if (snapshot.schema_version != 1) {
        OPENVINO_THROW("unsupported state allocation schema_version");
    }
    validate_context(
        snapshot.sequence,
        snapshot.pid,
        snapshot.observer_request_id,
        snapshot.correlation_id,
        snapshot.trigger,
        snapshot.phase,
        snapshot.observer_plugin_device);

    const auto recomputed_total = checked_add(
        checked_add(
            snapshot.unique_reserved_bytes,
            snapshot.beam_reserved_bytes,
            "serialized physical total"),
        snapshot.scale_zp_reserved_bytes,
        "serialized physical total");
    if (recomputed_total != snapshot.total_physical_state_bytes) {
        OPENVINO_THROW("snapshot physical totals are inconsistent");
    }
    for (const auto& total : snapshot.state_totals) {
        require_text(total.state_name, "state_name");
        const auto state_recomputed = checked_add(
            checked_add(
                total.unique_reserved_backing_bytes,
                total.beam_reserved_bytes,
                "serialized state physical total"),
            total.scale_zp_reserved_bytes,
            "serialized state physical total");
        if (state_recomputed != total.total_physical_state_bytes) {
            OPENVINO_THROW("state physical totals are inconsistent");
        }
    }

    BoundedJson json;
    json.append("{\"schema_version\":");
    json.append_number(snapshot.schema_version);
    json.append(",\"sequence\":");
    json.append_number(snapshot.sequence);
    json.append(",\"pid\":");
    json.append_number(snapshot.pid);
    json.append(",\"observer_request_id\":");
    json.append_number(snapshot.observer_request_id);
    json.append(",\"correlation_id\":");
    json.append_string(snapshot.correlation_id);
    json.append(",\"trigger\":");
    json.append_string(snapshot.trigger);
    json.append(",\"phase\":");
    json.append_string(snapshot.phase);
    json.append(",\"observer_plugin_device\":");
    json.append_string(snapshot.observer_plugin_device);
    json.append(",\"records\":[");
    for (size_t index = 0; index < snapshot.records.size(); ++index) {
        if (index != 0) {
            json.append(',');
        }
        append_record(json, snapshot.records[index]);
    }
    json.append("],\"state_totals\":[");
    for (size_t index = 0; index < snapshot.state_totals.size(); ++index) {
        if (index != 0) {
            json.append(',');
        }
        append_total(json, snapshot.state_totals[index]);
    }
    json.append("],\"unique_reserved_bytes\":");
    json.append_number(snapshot.unique_reserved_bytes);
    json.append(",\"beam_reserved_bytes\":");
    json.append_number(snapshot.beam_reserved_bytes);
    json.append(",\"scale_zp_reserved_bytes\":");
    json.append_number(snapshot.scale_zp_reserved_bytes);
    json.append(",\"total_physical_state_bytes\":");
    json.append_number(snapshot.total_physical_state_bytes);
    json.append('}');
    return json.take();
}

}  // namespace ov::intel_cpu

#endif  // CPU_DEBUG_CAPS
```

The implementation deliberately never calls `VariableStateKVcache::get_state()`
and never uses public tensor materialization. It observes only live private
owners: input/output/KV `MemoryBlockPtr`, hidden beam `MemoryBlockPtr`, and
positive-capacity owned scale/ZP `PlainTensor::m_ptr` control blocks.

## Step 7: Reconfigure, build the unit target, and prove GREEN twice

The production source and test source are glob-discovered, so reconfiguration
is mandatory after both `.cpp` files exist. Do not reuse the RED-generated
project without this configure.

```powershell
& $wrapper `
  -Label task2-configure-green `
  -WorkingDirectory R:\ `
  -EvidenceRoot $guardEvidence `
  -ExpectedExit Zero `
  -Command $configureCommand

& $wrapper `
  -Label task2-build-unit-green `
  -WorkingDirectory R:\ `
  -EvidenceRoot $guardEvidence `
  -ExpectedExit Zero `
  -Command $buildUnitCommand

$unitExecutable =
  "C:\ov-build\state-observer\bin\intel64\Release\ov_cpu_unit_tests.exe"
& $wrapper `
  -Label task2-list-green `
  -WorkingDirectory R:\ `
  -EvidenceRoot $guardEvidence `
  -ExpectedExit Zero `
  -Command @(
    $unitExecutable,
    "--gtest_filter=StateAllocationsDump.*",
    "--gtest_list_tests"
  )

$listLog = Get-Content -LiteralPath "$guardEvidence\task2-list-green.log" -Raw
$listedTests = @(
  $listLog -split "`r?`n" |
    Where-Object { $_ -match '^\s{2}[A-Za-z0-9_]+$' }
)
if ($listLog -notmatch '(?m)^StateAllocationsDump\.\r?$' -or
    $listedTests.Count -ne 25) {
  throw "Expected exactly 25 StateAllocationsDump tests, found $($listedTests.Count)"
}

foreach ($run in 1..2) {
  & $wrapper `
    -Label "task2-dump-green-$run" `
    -WorkingDirectory R:\ `
    -EvidenceRoot $guardEvidence `
    -ExpectedExit Zero `
    -Command @(
      $unitExecutable,
      "--gtest_filter=StateAllocationsDump.*"
    )
  $runLog = Get-Content -LiteralPath `
    "$guardEvidence\task2-dump-green-$run.log" -Raw
  if ($runLog -notmatch '\[\s*PASSED\s*\]\s+25 tests?\.') {
    throw "Task 2 focused run $run did not report exactly 25 passed"
  }
  if ($runLog -match '\[\s*(FAILED|SKIPPED|DISABLED)\s*\]') {
    throw "Task 2 focused run $run contains a non-passing result"
  }
}
```

Expected: guarded configure and build exit zero; enumeration proves exactly 25
selected tests; each independent fresh test process reports exactly 25 passed,
zero failed, skipped, or disabled; every guard record proves the 2,048 MiB
floor and zero survivors.

## Step 8: Build the production plugin and prove CMake integration

Build the actual production target through the same guard:

```powershell
$buildPluginCommand = @(
  $cmake, "--build", "C:\ov-build\state-observer", "--config", "Release",
  "--target", "openvino_intel_cpu_plugin", "--parallel", "2"
)
& $wrapper `
  -Label task2-plugin-green `
  -WorkingDirectory R:\ `
  -EvidenceRoot $guardEvidence `
  -ExpectedExit Zero `
  -Command $buildPluginCommand
```

Then inspect the generated Visual Studio projects directly:

```powershell
$productionProjects = @(
  & rg -l --fixed-strings "state_allocations_dump.cpp" `
    "C:\ov-build\state-observer" -g "*.vcxproj"
)
if ($LASTEXITCODE -ne 0 -or $productionProjects.Count -lt 1) {
  throw "Generated projects did not discover state_allocations_dump.cpp"
}
$testProjects = @(
  & rg -l --fixed-strings "state_allocations_dump_test.cpp" `
    "C:\ov-build\state-observer" -g "*.vcxproj"
)
if ($LASTEXITCODE -ne 0 -or $testProjects.Count -ne 1 -or
    [IO.Path]::GetFileName($testProjects[0]) -ne "ov_cpu_unit_tests.vcxproj") {
  throw "Unit source was not discovered exactly once by ov_cpu_unit_tests"
}
$expectedPluginProjects = @(
  "openvino_intel_cpu_plugin.vcxproj",
  "openvino_intel_cpu_plugin_obj.vcxproj"
) | Sort-Object
$actualPluginProjects = @(
  $productionProjects |
    ForEach-Object { [IO.Path]::GetFileName($_) }
) | Sort-Object -Unique
if (Compare-Object $expectedPluginProjects $actualPluginProjects) {
  throw "Production source was not discovered in the exact shared plugin and object projects"
}

$pluginDll =
  "C:\ov-build\state-observer\bin\intel64\Release\openvino_intel_cpu_plugin.dll"
if (-not (Test-Path -LiteralPath $pluginDll)) {
  throw "Observer CPU plugin DLL is missing"
}
$observerObjects = @(
  Get-ChildItem -LiteralPath "C:\ov-build\state-observer" `
    -Recurse -File -Filter "state_allocations_dump.obj"
)
if ($observerObjects.Count -lt 1 -or
    @($observerObjects | Where-Object { $_.Length -le 0 }).Count -ne 0) {
  throw "Task 2 production source did not emit a non-empty object file"
}
$observerObjectSha256 = [ordered]@{}
foreach ($object in $observerObjects) {
  $observerObjectSha256[$object.FullName] = (
    Get-FileHash -LiteralPath $object.FullName -Algorithm SHA256
  ).Hash.ToLowerInvariant()
}
$pluginDllSha256 = (
  Get-FileHash -LiteralPath $pluginDll -Algorithm SHA256
).Hash.ToLowerInvariant()
```

There is intentionally no CMake file change. The fresh configure plus the two
generated-project membership checks are the complete CMake integration proof.
The non-empty object and its hash prove the Task 2 translation unit compiled.
Task 2 has no production caller yet, so Release `/Gy` plus `/OPT:REF` may
legitimately remove its private functions and literals from the final DLL.
Task 3 owns the first production reference and the positive DLL marker proof.
The ordinary-build absence proof is not faked here: roadmap Task 7 builds a
separate both-options-OFF route and requires a no-match binary scan, `dumpbin`
symbol scan, and lexical preprocessor audit.

## Step 9: Run exact static, diff, marker, immutable-source, and process audits

All commands in this step are direct read-only Git/text/JSON inspections; no
Python, build, test, or compiled probe is launched.

First require only the five Task 2 paths:

```powershell
$expectedChanged = @(
  "src/plugins/intel_cpu/src/cpu_memory.cpp",
  "src/plugins/intel_cpu/src/cpu_memory.h",
  "src/plugins/intel_cpu/src/utils/state_allocations_dump.cpp",
  "src/plugins/intel_cpu/src/utils/state_allocations_dump.hpp",
  "src/plugins/intel_cpu/tests/unit/state_allocations_dump_test.cpp"
) | Sort-Object
$actualChanged = @(
  (& git -C $derivedCore diff --name-only) +
  (& git -C $derivedCore ls-files --others --exclude-standard)
) | Where-Object { $_ } | Sort-Object -Unique
if (Compare-Object $expectedChanged $actualChanged) {
  throw "Derived Task 2 diff contains an unexpected or missing path"
}
& git -C $derivedCore diff --check
if ($LASTEXITCODE -ne 0) { throw "Task 2 whitespace audit failed" }
& git -C $derivedCore diff --stat
& git -C $derivedCore diff -- @expectedChanged
```

Require the new implementation and test bodies to be lexically guarded and
free of forbidden markers/address conversion:

```powershell
$headerText = Get-Content -LiteralPath `
  "$derivedCore\src\plugins\intel_cpu\src\utils\state_allocations_dump.hpp" -Raw
$sourceText = Get-Content -LiteralPath `
  "$derivedCore\src\plugins\intel_cpu\src\utils\state_allocations_dump.cpp" -Raw
$memoryHeaderText = Get-Content -LiteralPath `
  "$derivedCore\src\plugins\intel_cpu\src\cpu_memory.h" -Raw
$memorySourceText = Get-Content -LiteralPath `
  "$derivedCore\src\plugins\intel_cpu\src\cpu_memory.cpp" -Raw

if ($headerText -notmatch '(?s)#ifdef CPU_DEBUG_CAPS.*StateAllocationSnapshot.*#endif\s+// CPU_DEBUG_CAPS') {
  throw "Observer header is not wholly guarded by CPU_DEBUG_CAPS"
}
if ($sourceText -notmatch '(?s)#ifdef CPU_DEBUG_CAPS.*build_state_allocation_snapshot.*serialize_state_allocation_snapshot.*#endif\s+// CPU_DEBUG_CAPS') {
  throw "Observer definitions are not wholly guarded by CPU_DEBUG_CAPS"
}
if ($memoryHeaderText -notmatch '(?s)#ifdef CPU_DEBUG_CAPS\s+\[\[nodiscard\]\]\s+std::optional<size_t>\s+getAllocatedSize\(\)\s+const noexcept;\s+#endif') {
  throw "Capacity declaration is not guarded by CPU_DEBUG_CAPS"
}
if ($memorySourceText -notmatch '(?s)#ifdef CPU_DEBUG_CAPS\s+std::optional<size_t>\s+DnnlMemoryBlock::getAllocatedSize\(\)\s+const noexcept.*?#endif') {
  throw "Capacity definition is not guarded by CPU_DEBUG_CAPS"
}
if ($sourceText -match 'get_state\s*\(') {
  throw "Physical capture must not materialize public variable-state tensors"
}
if ($sourceText -match 'Memory::getSize\(\).*reserved|reserved.*Memory::getSize\(') {
  throw "Descriptor size appears to be used as retained capacity"
}

& rg -n "TQDBG|raw_address|uintptr_t|reinterpret_cast.*(long|int|size_t)" `
  "$derivedCore\src\plugins\intel_cpu\src\utils\state_allocations_dump.hpp" `
  "$derivedCore\src\plugins\intel_cpu\src\utils\state_allocations_dump.cpp" `
  "$derivedCore\src\plugins\intel_cpu\tests\unit\state_allocations_dump_test.cpp"
$forbiddenExit = $LASTEXITCODE
if ($forbiddenExit -eq 0) { throw "Forbidden marker or address conversion found" }
if ($forbiddenExit -ne 1) { throw "Forbidden marker scan failed" }

& rg -n "OPENVINO_API|OPENVINO_RUNTIME_API|OPENVINO_C_API" `
  "$derivedCore\src\plugins\intel_cpu\src\utils\state_allocations_dump.hpp" `
  "$derivedCore\src\plugins\intel_cpu\src\utils\state_allocations_dump.cpp"
$apiExit = $LASTEXITCODE
if ($apiExit -eq 0) { throw "Task 2 introduced an exported/public API marker" }
if ($apiExit -ne 1) { throw "Public API marker scan failed" }
```

Revalidate immutable clean source against the Step 1 boundary:

```powershell
$boundary = Get-Content -LiteralPath `
  "R:\.superpowers\sdd\task02-boundary.json" -Raw | ConvertFrom-Json
if ((& git -C $cleanCore rev-parse HEAD).Trim() -ne
    $boundary.clean_core_commit) {
  throw "Clean core commit changed during Task 2"
}
if ((& git -C $cleanCore rev-parse "HEAD^{tree}").Trim() -ne
    $boundary.clean_core_tree) {
  throw "Clean core tree changed during Task 2"
}
if (& git -C $cleanCore status --porcelain --untracked-files=all) {
  throw "Clean core became dirty during Task 2"
}
foreach ($path in $taskPaths) {
  $expected = [string](
    $boundary.clean_preimage_blobs.PSObject.Properties[$path].Value
  )
  & git -C $cleanCore cat-file -e "$base`:$path" 2>$null
  $catFileExit = $LASTEXITCODE
  $actual = if ($catFileExit -eq 0) {
    (& git -C $cleanCore rev-parse "$base`:$path").Trim()
  } elseif ($catFileExit -eq 128) {
    "ABSENT"
  } else {
    throw "Unable to revalidate clean-source blob for $path"
  }
  if ($actual -ne $expected) {
    throw "Clean-source blob boundary changed for $path"
  }
}
```

Finally audit every Task 2 guard receipt, including RED:

```powershell
$guardExpectations = [ordered]@{
  "task2-configure-red" = 0
  "task2-build-red" = "nonzero"
  "task2-configure-green" = 0
  "task2-build-unit-green" = 0
  "task2-list-green" = 0
  "task2-dump-green-1" = 0
  "task2-dump-green-2" = 0
  "task2-plugin-green" = 0
}
foreach ($entry in $guardExpectations.GetEnumerator()) {
  $path = Join-Path $guardEvidence ($entry.Key + ".json")
  if (-not (Test-Path -LiteralPath $path)) {
    throw "Missing guard receipt: $($entry.Key)"
  }
  $record = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
  if ($record.schema -ne "official-openvino-owned-process-guard/v1" -or
      $record.valid -ne $true -or
      [int64]$record.configured_minimum_available_ram_bytes -ne 2147483648 -or
      [int64]$record.observed_available_ram_bytes.minimum -lt 2147483648 -or
      $record.launch_governance.created_suspended -ne $true -or
      $record.launch_governance.assigned_before_resume -ne $true -or
      $record.job_object.query_ok -ne $true -or
      [int]$record.job_object.queried_active_process_count_after_cleanup -ne 0 -or
      @($record.job_object.survivor_pids_after_cleanup).Count -ne 0 -or
      $record.timed_out -ne $false -or
      $record.low_memory_stop -ne $false -or
      $null -ne $record.termination_reason -or
      @($record.emergency_actions).Count -ne 0 -or
      @($record.validation_errors).Count -ne 0) {
    throw "Guard receipt failed zero-survivor/RAM validation: $($entry.Key)"
  }
  if ($entry.Value -eq "nonzero") {
    if ([int]$record.exit_code -eq 0 -or $record.expected_exit -ne "nonzero") {
      throw "RED guard receipt did not contain the expected nonzero exit"
    }
  } elseif ([int]$record.exit_code -ne 0 -or
            $record.expected_exit -ne "zero") {
    throw "GREEN guard receipt did not contain exit zero"
  }
  $logPath = Join-Path $guardEvidence ($entry.Key + ".log")
  $observedLogHash = (
    Get-FileHash -LiteralPath $logPath -Algorithm SHA256
  ).Hash.ToLowerInvariant()
  if ($record.log_sha256 -ne $observedLogHash) {
    throw "Guard log hash mismatch: $($entry.Key)"
  }
}
```

No audit may be waived. A missing receipt, ambiguous `rg` exit, unexpected
path, changed clean blob, survivor, or RAM-floor breach keeps Task 2 failed.

## Step 10: Commit the derived-core task boundary

Stage only the five derived-core files and recheck the index:

```powershell
& git -C $derivedCore add `
  src/plugins/intel_cpu/src/cpu_memory.h `
  src/plugins/intel_cpu/src/cpu_memory.cpp `
  src/plugins/intel_cpu/src/utils/state_allocations_dump.hpp `
  src/plugins/intel_cpu/src/utils/state_allocations_dump.cpp `
  src/plugins/intel_cpu/tests/unit/state_allocations_dump_test.cpp
if ($LASTEXITCODE -ne 0) { throw "Unable to stage Task 2 files" }

$staged = @(& git -C $derivedCore diff --cached --name-only) |
  Where-Object { $_ } | Sort-Object
if (Compare-Object $expectedChanged $staged) {
  throw "Staged Task 2 path set is not exact"
}
& git -C $derivedCore diff --cached --check
if ($LASTEXITCODE -ne 0) { throw "Staged whitespace audit failed" }
& git -C $derivedCore diff --cached --stat
& git -C $derivedCore commit -m `
  "feat(cpu): capture physical variable-state allocations"
if ($LASTEXITCODE -ne 0) { throw "Task 2 commit failed" }

$task2Head = (& git -C $derivedCore rev-parse HEAD).Trim()
$task2Tree = (& git -C $derivedCore rev-parse "HEAD^{tree}").Trim()
if ($task2Head.Length -ne 40 -or $task2Tree.Length -ne 40) {
  throw "Task 2 commit/tree identity is not full length"
}
if (& git -C $derivedCore status --porcelain --untracked-files=all) {
  throw "Derived core is not clean after the Task 2 commit"
}
```

Expected: one focused local commit with the exact message and five paths. Do
not push it, merge it, rerun the parent patch controller, or edit the
materialization identity JSON. That JSON remains the immutable Task 1
materialization receipt; the Task 2 development commit/tree are recorded
separately below until Task 7 exports and replays the full patch.

## Step 11: Create a scratch patch/replay identity receipt without publishing

This step proves that the current development tree is expressible as a
binary-safe base-to-head patch while preserving the roadmap's Task 7 ownership
of the tracked patch. It writes only ignored `.superpowers/sdd` scratch.

```powershell
$previewPatch = "R:\.superpowers\sdd\task02-core-preview.patch"
& git -C $derivedCore diff --binary $base $task2Head `
  --output=$previewPatch
if ($LASTEXITCODE -ne 0 -or
    -not (Test-Path -LiteralPath $previewPatch) -or
    (Get-Item -LiteralPath $previewPatch).Length -eq 0) {
  throw "Task 2 binary-safe preview patch is empty"
}

$scratch = "R:\.superpowers\sdd\task02-patch-replay"
if (Test-Path -LiteralPath $scratch) {
  $resolvedScratch = (Resolve-Path -LiteralPath $scratch).Path
  $allowedRoot = [IO.Path]::GetFullPath(
    "R:\.superpowers\sdd\task02-patch-replay"
  )
  if (-not $resolvedScratch.Equals(
      $allowedRoot,
      [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsafe Task 2 replay cleanup path"
  }
  Remove-Item -LiteralPath $resolvedScratch -Recurse -Force
}
& git clone --local --no-hardlinks --no-checkout $cleanCore $scratch
if ($LASTEXITCODE -ne 0) { throw "Task 2 replay clone failed" }
& git -C $scratch checkout --detach $base
if ($LASTEXITCODE -ne 0) { throw "Task 2 replay base checkout failed" }
& git -C $scratch apply --cached --check $previewPatch
if ($LASTEXITCODE -ne 0) { throw "Task 2 preview patch check failed" }
& git -C $scratch apply --cached $previewPatch
if ($LASTEXITCODE -ne 0) { throw "Task 2 preview patch apply failed" }
$replayedTree = (& git -C $scratch write-tree).Trim()
if ($replayedTree -ne $task2Tree) {
  throw "Task 2 preview replay tree differs from development tree"
}

$receipt = [ordered]@{
  schema = "openvino-cpu-observer-task02-receipt/v1"
  roadmap_sha256 = $roadmapHash
  clean_core_commit = $base
  clean_core_tree = (& git -C $cleanCore rev-parse "$base^{tree}").Trim()
  derived_start_commit = $boundary.derived_start_commit
  derived_start_tree = $boundary.derived_start_tree
  task2_commit = $task2Head
  task2_tree = $task2Tree
  task2_commit_subject = (
    & git -C $derivedCore show -s --format=%s $task2Head
  ).Trim()
  changed_paths = @(
    & git -C $derivedCore diff --name-only $base $task2Head
  )
  preview_patch_path = $previewPatch
  preview_patch_bytes = (Get-Item -LiteralPath $previewPatch).Length
  preview_patch_sha256 = (
    Get-FileHash -LiteralPath $previewPatch -Algorithm SHA256
  ).Hash.ToLowerInvariant()
  replayed_tree = $replayedTree
  guard_evidence_root = $guardEvidence
  guard_labels = @($guardExpectations.Keys)
  guard_receipt_sha256 = [ordered]@{}
  production_plugin_path = $pluginDll
  production_plugin_sha256 = $pluginDllSha256
  observer_object_sha256 = $observerObjectSha256
}
foreach ($label in $guardExpectations.Keys) {
  $receipt.guard_receipt_sha256[$label] = (
    Get-FileHash -LiteralPath `
      (Join-Path $guardEvidence ($label + ".json")) -Algorithm SHA256
  ).Hash.ToLowerInvariant()
}
$receipt | ConvertTo-Json -Depth 10 |
  Set-Content -LiteralPath `
    "R:\.superpowers\sdd\task02-receipt.json" -Encoding UTF8
```

Review the receipt and require its `changed_paths` to be the exact five-file
set. The scratch replay can remain for review; it is never a source checkout.

At roadmap Task 7, require:

```powershell
& git -C $core merge-base --is-ancestor $task2Head $coreDevelopmentHead
if ($LASTEXITCODE -ne 0) {
  throw "Final core development head does not contain approved Task 2"
}
& git -C $core diff --binary $base $coreDevelopmentHead `
  --output="R:\experiments\patches\openvino-cpu-state-observer\0001-cpu-state-allocation-observer.patch"
```

Task 7 must then commit that one tracked patch in the parent, replay it from the
immutable base with the controller, and prove
`replay write-tree == coreDevelopmentHead^{tree}` plus exact patch tracked blob,
SHA-256, source commit/tree, derived commit/tree, clean status, ordinary
both-options-OFF absence, and observer both-options-ON presence. The Task 2
preview is evidence only and must never be copied into the tracked patch path.

## Step 12: Independent spec and quality review gates

The implementer records the boundary JSON, exact RED reason, all eight guard
JSON/log hashes, test-list count, both 25-pass outputs, production DLL hash,
observer object paths/hashes, generated-project membership,
static/diff/marker/process audits, commit/tree, preview patch hash, and replayed
tree in `.superpowers/sdd/progress.md`.

Then request two independent reviews in order:

1. **Spec reviewer (independent agent, read-only):**
   - read the complete roadmap and this complete micro-plan;
   - inspect exact commit `$task2Head`, all five blobs, Task 2 receipt, and every
     guard receipt;
   - verify every roadmap Task 2 requirement is present, no requirement was
     silently deferred except the explicitly Task 7-owned ordinary/replay
     gates, and no public tensor materialization or capacity inference exists;
   - verify the clean base remained immutable and the commit/patch replay tree
     identities are exact;
   - return `SPEC PASS` or `SPEC FAIL` with concrete file/line/evidence
     references. Silence, conditional approval, or `looks good` is not PASS.
2. **Quality reviewer (different independent agent, read-only, only after
   `SPEC PASS`):**
   - review overflow/UTF-8/JSON-bound correctness, alias-domain identity,
     canonical-first total accounting, external/unknown fail-closed behavior,
     supported state extraction, `CPU_DEBUG_CAPS` lexical containment,
     test strength, CMake membership, guard completeness, and absence of raw
     addresses/content/public ABI;
   - independently recompute the expected example totals
     (`192`, `192`, `224`, and shared-owner capture `112`);
   - verify exact 25-test selection and two clean runs, production target build,
     non-empty observer object paths/hashes, the explicit Task 3 positive-DLL
     marker deferral, 2,048 MiB minimum on every command, and zero survivors;
   - return `QUALITY PASS` or `QUALITY FAIL` with concrete references.

Task 2 is accepted only when both verdicts are explicit PASS against the same
40-hex `$task2Head`. If either reviewer reports a defect, do not continue to
Task 3. Return the issue to the implementer, add a regression first, reproduce
RED through a new unique guard label, implement the minimum fix, rerun the
fresh configure/build/list plus the full focused suite twice and production
build, rerun every audit, and update the commit. If the original commit has not
left this private derived branch, amend it and regenerate the receipt; otherwise
use a separately reviewed fix commit and make Task 7 include both. Both
independent reviews restart against the new HEAD.

## Completion checklist

- [ ] Roadmap SHA-256 is exact and the guard bootstrap has independent PASS.
- [ ] Clean core commit/tree/status and all five preimage blobs are frozen.
- [ ] `O:` maps only to the exact no-hardlink derived core.
- [ ] Exact 25-test file exists; RED fails only for the absent Task 2 API.
- [ ] Capacity accessor is supported-owner-only and debug-capability-only.
- [ ] Header/source bodies match this plan and contain no public ABI/address.
- [ ] Fresh CMake configure discovers both new `.cpp` files.
- [ ] Unit target builds under guard.
- [ ] Exactly 25 tests are listed and pass twice in fresh guarded processes.
- [ ] Production CPU plugin target builds under guard; generated projects name
      the source and at least one non-empty observer object is hash-bound.
- [ ] Every guard receipt proves 2,048 MiB minimum and zero survivors.
- [ ] Diff/path/whitespace/source-marker/API/immutable-source audits pass.
- [ ] Exact five-file derived commit is clean and has the required subject.
- [ ] Scratch binary patch applies to the immutable base and writes the exact
      Task 2 tree.
- [ ] Task 7 export/replay expectations are recorded; no tracked patch was
      prematurely written.
- [ ] Independent `SPEC PASS` and `QUALITY PASS` name the same Task 2 commit.
