# OpenVINO CPU KV Physical-Allocation Observability Execution Roadmap

> **For agentic workers:** This tracked document is a gated execution roadmap,
> not a substitute for complete implementation bodies. Before each numbered
> task, use `superpowers:writing-plans` to create and commit the corresponding
> task micro-plan at
> `docs/superpowers/plans/2026-07-28-openvino-cpu-kv-allocation-observability-task-NN.md`.
> The micro-plan must contain exact source/test bodies and complete CMake
> content, receive independent spec approval, and cite the roadmap gates it
> satisfies. SDD implementers consume the approved micro-plan, not roadmap
> prose. Use `superpowers:subagent-driven-development` or
> `superpowers:executing-plans` only after that approval.

**Goal:** Add a reproducible, default-off OpenVINO CPU-plugin observer and a strict GenAI/Python reconciliation path that proves post-inference physical KV-cache precision and reserved allocation for native STANDARD/U8/U4 and project TBQ3/TBQ4 state.

**Architecture:** Develop the CPU observer in a derived OpenVINO `2026.2.1` checkout and keep the official checkout immutable. The CPU plugin emits one internal allocation snapshot before public state materialization; GenAI emits exact K/V/component bindings and activation identity; a strict Python reconciler combines both into hash-bound physical-allocation evidence. Public `query_state()` tensor sizes remain logical evidence and never satisfy the physical KV field.

**Tech Stack:** C++17, OpenVINO `2026.2.1`, OpenVINO GenAI `2026.2.1.0`, CMake/MSVC, PowerShell 5.1, Python 3.11, GoogleTest, JSONL, SHA-256.

## Global Constraints

- Preserve clean upstream OpenVINO commit `ede283a88e35465f0d680dabbf1f44080f8fc387` and OpenVINO GenAI commit `7dea0459b2ac7d8dfd877fd9df6737674fd8371d`.
- Compile the observer only when `CPU_DEBUG_CAPS` is defined. Observer builds
  configure both `ENABLE_DEBUG_CAPS=ON` and `ENABLE_CPU_DEBUG_CAPS=ON`; ordinary
  builds configure both options `OFF`. Keep the compiled observer inert unless
  `OV_CPU_STATE_ALLOCATION_DUMP_PATH` names a local `.jsonl` file.
- Do not add or change a public OpenVINO ABI.
- Never record tensor contents, prompts, generated text, raw addresses, or network data.
- Physical bytes are unique retained backing capacities plus owned beam and scale/zero-point capacities; descriptor bytes and process memory are separate fields.
- Trigger the accepted allocation snapshot only after successful inference and before any public `VariableState::get_state()` materialization.
- Never infer U8/U4/TBQ3/TBQ4 physical precision from a request property, file label, runtime enum, public tensor type, payload width, or formula.
- Keep native algorithm identity, runtime element type, active descriptor bytes, retained allocation, logical current-state bytes, and process memory as separate fields.
- Hash a serialized model as SHA-256 of the exact UTF-8 XML bytes, one zero byte,
  and the exact BIN bytes, in that order. Do not prepend a label or byte counts,
  and do not relabel the existing 64-bit FNV value.
- Use only exact CPU plus explicit stateful SDPA, valid output, no fallback, one owned process tree, and zero survivors for an accepted physical observation.
- Emit and validate four separate correlated event types:
  `state_allocation_observer_context`, `turboquant_activation`,
  `kv_logical_state_observation`, and `kv_physical_allocation`.
- The raw CPU record is authoritative only for its `pid`, process-wide
  `observer_request_id`, opaque model `correlation_id`, physical allocations,
  phase, trigger, and `observer_plugin_device="CPU"`. It must not claim the
  caller's requested device or the outer execution-device list.
- The GenAI context record is authoritative for the exact constructor
  `requested_device` and the outer compiled model's
  `actual_execution_devices`. Join it one-to-one to CPU evidence by
  `(pid, correlation_id)`. The accepted reconciled event then carries the
  workbook `request_id` and requires requested `CPU` plus exact `["CPU"]`.
- Close the performance aggregation window and receive the controller
  acknowledgement before `query_state()` triggers observer I/O.
- Preserve the 2,048 MiB available-physical-RAM emergency floor; start core builds at parallelism `2` and reduce to `1` if the floor trend is unsafe.
- Re-run the Task 2 CMake configure command after adding any glob-discovered
  `.cpp` file or CMake target; the OpenVINO CPU source glob is evaluated at
  configure time and an incremental build alone must not be trusted to find it.
- Every implementation task uses TDD, runs focused tests twice, builds its
  production target where one exists, performs diff/marker/process audits,
  commits, and receives independent spec and code-quality approval.
- The design's "derived worktree" is implemented by the existing controller's
  isolated `--local --no-hardlinks` clone. It has an independent working tree,
  branch, index, and object copies; the clean official checkout is never edited.

---

### Task 1: Generalize the Reproducible Patch Workspace for the CPU Observer

**Files:**
- Modify: `scripts/testing/official_openvino/patch_identity.py`
- Modify: `scripts/testing/tests/test_official_openvino_patch_identity.py`
- Create: `scripts/testing/prepare_openvino_cpu_observer_patch.ps1`
- Create: `experiments/patches/openvino-cpu-state-observer/README.md`

**Interfaces:**
- Consumes: a clean pinned upstream Git checkout and tracked ordered patch files.
- Produces:

```python
@dataclass(frozen=True)
class PatchWorkspaceSpec:
    branch: str
    patch_directory: Path
    commit_message: str

def prepare_patch_workspace(
    upstream: Path,
    destination: Path,
    expected_commit: str,
    spec: PatchWorkspaceSpec | None = None,
) -> dict[str, object]:
    """Create or verify one family; None preserves the GenAI default."""
```

- [ ] **Step 1: Write the generic-workspace RED tests**

In `setUp`, create and commit both
`experiments/patches/openvino-turboquant/README.md` and
`experiments/patches/openvino-cpu-state-observer/README.md`. Change the fixture
helper to `prepare(self, spec: PatchWorkspaceSpec)`. Add these tests:

```python
CORE_SPEC = PatchWorkspaceSpec(
    branch="project/cpu-state-allocation-observer",
    patch_directory=Path("experiments/patches/openvino-cpu-state-observer"),
    commit_message="Apply controlled OpenVINO CPU state observer patch set",
)

def test_prepares_core_observer_patch_family_without_genai_constants(self):
    record = self.prepare(spec=CORE_SPEC)
    self.assertEqual(record["branch"], CORE_SPEC.branch)
    self.assertTrue(record["patch_directory"].endswith("openvino-cpu-state-observer"))
    self.assertEqual(record["base_commit"], self.expected)
    self.assertEqual(record["upstream_tree"], git("-C", str(self.upstream), "rev-parse", "HEAD^{tree}"))
    self.assertEqual(len(record["derived_tree"]), 40)
    self.assertEqual(record["applied_patches"], [])

def test_rejects_existing_destination_from_another_patch_family(self):
    self.prepare(spec=CORE_SPEC)
    with self.assertRaisesRegex(ValueError, "branch mismatch"):
        self.prepare(spec=GENAI_TURBOQUANT_SPEC)

def test_rejects_patch_directory_outside_controlling_repository(self):
    unsafe = PatchWorkspaceSpec(
        branch="project/unsafe",
        patch_directory=Path("../outside"),
        commit_message="unsafe",
    )
    with self.assertRaisesRegex(ValueError, "patch directory"):
        self.prepare(spec=unsafe)

def test_cli_rejects_unknown_family(self):
    with self.assertRaisesRegex(ValueError, "unknown patch family"):
        patch_identity.spec_for_family("unknown")
```

- [ ] **Step 2: Run RED**

Run:

```powershell
python -m pytest scripts/testing/tests/test_official_openvino_patch_identity.py -q
```

Expected: the new tests fail because `PatchWorkspaceSpec` and the `spec`
parameter do not exist.

- [ ] **Step 3: Implement the minimal generic controller**

Replace hard-coded branch/patch-directory uses with the immutable spec:

```python
@dataclass(frozen=True)
class PatchWorkspaceSpec:
    branch: str
    patch_directory: Path
    commit_message: str

GENAI_TURBOQUANT_SPEC = PatchWorkspaceSpec(
    branch="project/turboquant-wb04",
    patch_directory=Path("experiments/patches/openvino-turboquant"),
    commit_message="Apply controlled OpenVINO TurboQuant patch set",
)

CORE_OBSERVER_SPEC = PatchWorkspaceSpec(
    branch="project/cpu-state-allocation-observer",
    patch_directory=Path("experiments/patches/openvino-cpu-state-observer"),
    commit_message="Apply controlled OpenVINO CPU state observer patch set",
)

FAMILY_SPECS: Mapping[str, PatchWorkspaceSpec] = {
    "openvino-genai-turboquant": GENAI_TURBOQUANT_SPEC,
    "openvino-cpu-observer": CORE_OBSERVER_SPEC,
}
```

Pass `spec` through `_controlled_patches`, `_create_exact_checkout`, and
`_verify_existing`; use `spec.branch` for checkout and verification and use
`spec.commit_message` as the exact `commit-tree` input. Preserve the existing
record key `applied_patches` and include `branch`, `patch_directory`, ordered
patch names, patch blob IDs, patch SHA-256 values, upstream tree, derived tree,
and clean state in the identity record. Resolve `spec.patch_directory` under
`REPO_ROOT`, then reject it unless
`os.path.commonpath([REPO_ROOT, patch_root]) == str(REPO_ROOT)`.

Resolve `None` to `GENAI_TURBOQUANT_SPEC` at the first line of
`prepare_patch_workspace`, before passing the immutable spec to any helper.
Add:

```python
def spec_for_family(name: str) -> PatchWorkspaceSpec:
    try:
        return FAMILY_SPECS[name]
    except KeyError as exc:
        raise ValueError(f"unknown patch family: {name}") from exc
```

The CLI parser uses
`parser.add_argument("--family", choices=tuple(sorted(FAMILY_SPECS)), default="openvino-genai-turboquant")`
and passes `spec_for_family(args.family)` to `prepare_patch_workspace`.

- [ ] **Step 4: Add the PowerShell entry point**

`prepare_openvino_cpu_observer_patch.ps1` must resolve the shared repository
root exactly as the GenAI controller does, then invoke:

```powershell
python -m scripts.testing.official_openvino.patch_identity `
  --family openvino-cpu-observer `
  --upstream $UpstreamPath `
  --destination $DestinationPath `
  --expected-commit ede283a88e35465f0d680dabbf1f44080f8fc387 `
  --evidence $EvidencePath
```

Default `CampaignDate` is `2026-07-19`. Resolve the upstream from the shared
repository root as
`external/official-openvino/$CampaignDate/openvino`, and resolve the default
destination in the parent worktree as:

```text
external/official-openvino/2026-07-19/openvino-cpu-state-observer
```

- [ ] **Step 5: Run GREEN twice and verify no mutation**

Run twice:

```powershell
python -m pytest scripts/testing/tests/test_official_openvino_patch_identity.py -q
python -m py_compile scripts/testing/official_openvino/patch_identity.py
git diff --check
```

Expected: all tests pass twice; the pinned OpenVINO checkout remains clean.

- [ ] **Step 6: Commit before materializing the derived core**

```powershell
git add scripts/testing/official_openvino/patch_identity.py `
        scripts/testing/tests/test_official_openvino_patch_identity.py `
        scripts/testing/prepare_openvino_cpu_observer_patch.ps1 `
        experiments/patches/openvino-cpu-state-observer/README.md
git commit -m "build(openvino): prepare CPU observer patch workspace"
```

The controller requires the parent worktree to be clean and every patch file to
match parent `HEAD`. Only after this commit, run:

```powershell
& .\scripts\testing\prepare_openvino_cpu_observer_patch.ps1 `
  -CampaignDate 2026-07-19 `
  -ExpectedCommit ede283a88e35465f0d680dabbf1f44080f8fc387
git -C .\external\official-openvino\2026-07-19\openvino-cpu-state-observer `
  rev-parse HEAD
git -C .\external\official-openvino\2026-07-19\openvino-cpu-state-observer `
  status --porcelain --untracked-files=all
```

Expected: the first command reports the deterministic controlled patch commit;
the second output is empty. Record both outputs in the task review.

### Mandatory Pre-Task-2 Guard Bootstrap

**Files in parent repository:**
- Create: `scripts/testing/official_openvino/owned_process_guard.py`
- Create: `scripts/testing/official_openvino/guarded_build.py`
- Create: `scripts/testing/invoke_guarded_command.ps1`
- Create: `scripts/testing/tests/test_official_openvino_guarded_build.py`
- Modify: `scripts/testing/run_openvino_reference_capability.py`
- Modify: `tests/test_openvino_reference_capability.py`

Before Task 2, move without semantic rewriting the existing
`KillOnCloseJob` implementation from
`run_openvino_reference_capability.py` (current lines 271-365), its
`_cleanup_job`/resource-close path (current lines 1135-1215), and the
available-RAM/process-memory sampling primitives used by `run_one` (current
lines 1401-1835) into `owned_process_guard.py`. Import the same definitions
back into the capability runner. `guarded_build.py` exposes:

```python
@dataclass(frozen=True)
class GuardLimits:
    minimum_available_ram_bytes: int = 2_048 * 1024 * 1024
    poll_interval_seconds: float = 0.25
    cleanup_timeout_seconds: float = 15.0

def run_guarded_command(
    command: Sequence[str],
    *,
    cwd: Path,
    log_path: Path,
    evidence_path: Path,
    expected_exit: Literal["zero", "nonzero"],
    limits: GuardLimits = GuardLimits(),
) -> dict[str, object]:
    """Run one owned process tree and atomically persist exit/RAM/cleanup evidence."""
```

The CLI is:

```text
python -m scripts.testing.official_openvino.guarded_build
  --cwd C:\work
  --log C:\evidence\configure.log
  --evidence C:\evidence\configure.json
  --expected-exit zero
  --minimum-available-ram-mib 2048
  -- executable argument
```

`invoke_guarded_command.ps1` accepts `Label`, `WorkingDirectory`,
`EvidenceRoot`, `ExpectedExit` (`Zero` or `NonZero`), and a string-array
`Command`. It creates fresh label-specific log/JSON paths, invokes the CLI with
the fixed 2,048 MiB floor, then parses the JSON and throws unless the child exit
matches the expectation, `minimum_available_ram_bytes >= 2147483648`,
`queried_active_process_count_after_cleanup == 0`, and
`survivor_pids_after_cleanup` is empty.

Write RED tests for normal/nonzero expected exits, unexpected exit, RAM-floor
termination, timeout, child-tree cleanup, atomic log/evidence replacement, and
queried zero survivors. Tests must also prove there is exactly one
`CreateJobObjectW` implementation. Run the new tests plus the existing
79-test capability suite twice, then commit these exact files before Task 2.

```powershell
foreach ($run in 1..2) {
  python -m pytest `
    scripts/testing/tests/test_official_openvino_guarded_build.py `
    tests/test_openvino_reference_capability.py -q
  if ($LASTEXITCODE -ne 0) { throw "Guard bootstrap run $run failed" }
}
git add scripts/testing/official_openvino/owned_process_guard.py `
        scripts/testing/official_openvino/guarded_build.py `
        scripts/testing/invoke_guarded_command.ps1 `
        scripts/testing/tests/test_official_openvino_guarded_build.py `
        scripts/testing/run_openvino_reference_capability.py `
        tests/test_openvino_reference_capability.py
git commit -m "build(testing): guard heavy OpenVINO processes"
```

From Task 2 onward, every CMake configure/build, compiled test/probe, and
Python test process is a `Command` passed to
`invoke_guarded_command.ps1`. A direct heavy child launch is a roadmap failure.
Every invocation uses a unique evidence label, the 2,048 MiB floor, and must
record zero survivors. Git inspection/apply/commit commands are not heavy
children and remain direct.

### Task 2: Add an Allocation Snapshot Domain Model and Retained-Capacity Accessor

**Files in derived OpenVINO core checkout:**
- Modify: `src/plugins/intel_cpu/src/cpu_memory.h`
- Modify: `src/plugins/intel_cpu/src/cpu_memory.cpp`
- Create: `src/plugins/intel_cpu/src/utils/state_allocations_dump.hpp`
- Create: `src/plugins/intel_cpu/src/utils/state_allocations_dump.cpp`
- Create: `src/plugins/intel_cpu/tests/unit/state_allocations_dump_test.cpp`

**Interfaces:**

```cpp
#ifdef CPU_DEBUG_CAPS
std::optional<size_t> DnnlMemoryBlock::getAllocatedSize() const noexcept;

enum class StateMemoryRole { INPUT, OUTPUT, KV, BEAM, SCALE_ZP };

struct SnapshotContext {
    uint32_t pid;
    uint64_t observer_request_id;
    std::string correlation_id;
    std::string trigger = "query_state";
    std::string phase;
    std::string observer_plugin_device = "CPU";
};

enum class AllocationOwnerDomain { DNNL_MEMORY_BLOCK, PLAIN_TENSOR_OWNER };

// Extraction assigns source_owner_ordinal within the tagged owner domain by
// shared-ownership comparison. It is process-local and is never serialized.
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
#endif
```

- [ ] **Step 1: Write retained-capacity and alias RED tests**

Test the pure builder with explicit observations so the RED tests do not depend
on undefined state factories:

```cpp
ObservedAllocation allocation(std::string state,
                              StateMemoryRole role,
                              AllocationOwnerDomain owner_domain,
                              size_t owner,
                              size_t active,
                              std::optional<size_t> reserved) {
    return {std::move(state),
            "VariableStateDoubleBuffer",
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
            false};
}

const SnapshotContext kContext{
    4242, 1, "0123456789abcdef0123456789abcdef",
    "query_state", "post_infer", "CPU"};

TEST(StateAllocationsDump, DeduplicatesAliasedInputAndOutputBacking) {
    auto snapshot = build_state_allocation_snapshot(
        {allocation("past_key", StateMemoryRole::INPUT,
                    AllocationOwnerDomain::DNNL_MEMORY_BLOCK, 40, 192, 192),
         allocation("past_key", StateMemoryRole::OUTPUT,
                    AllocationOwnerDomain::DNNL_MEMORY_BLOCK, 40, 192, 192)},
        1,
        kContext);
    ASSERT_EQ(snapshot.records.size(), 2u);
    EXPECT_EQ(snapshot.records[1].aliases_block_ordinal,
              snapshot.records[0].block_ordinal);
    EXPECT_EQ(snapshot.unique_reserved_bytes, 192u);
    EXPECT_EQ(snapshot.total_physical_state_bytes, 192u);
}

TEST(StateAllocationsDump, ReportsBothDoubleBuffersWithoutAssumingEqualCapacity) {
    auto snapshot = build_state_allocation_snapshot(
        {allocation("past_value", StateMemoryRole::INPUT,
                    AllocationOwnerDomain::DNNL_MEMORY_BLOCK, 11, 64, 64),
         allocation("past_value", StateMemoryRole::OUTPUT,
                    AllocationOwnerDomain::DNNL_MEMORY_BLOCK, 12, 128, 128)},
        2,
        kContext);
    EXPECT_EQ(snapshot.unique_reserved_bytes, 192u);
}

TEST(StateAllocationsDump, UnsupportedBlockIsUnknownNotDescriptorGuess) {
    auto snapshot = build_state_allocation_snapshot(
        {allocation("past_key", StateMemoryRole::KV,
                    AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
                    9, 192, std::nullopt)},
        3,
        kContext);
    EXPECT_FALSE(snapshot.records.front().reserved_backing_bytes.has_value());
    EXPECT_THROW(serialize_state_allocation_snapshot(snapshot), ov::Exception);
}

TEST(StateAllocationsDump, AddsOwnedBeamAndScaleZpExactlyOnce) {
    auto beam = allocation("past_key", StateMemoryRole::BEAM,
                           AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
                           3, 12, 32);
    beam.element_type = "i32";
    auto scale = allocation("past_key", StateMemoryRole::SCALE_ZP,
                            AllocationOwnerDomain::PLAIN_TENSOR_OWNER,
                            4, 16, 64);
    scale.element_type = "f32";
    auto snapshot = build_state_allocation_snapshot(
        {allocation("past_key", StateMemoryRole::KV,
                    AllocationOwnerDomain::DNNL_MEMORY_BLOCK,
                    1, 96, 128), beam, scale},
        4,
        kContext);
    EXPECT_EQ(snapshot.unique_reserved_bytes, 128u);
    EXPECT_EQ(snapshot.beam_reserved_bytes, 32u);
    EXPECT_EQ(snapshot.scale_zp_reserved_bytes, 64u);
    EXPECT_EQ(snapshot.total_physical_state_bytes, 224u);
    ASSERT_EQ(snapshot.state_totals.size(), 1u);
    EXPECT_EQ(snapshot.state_totals[0].total_physical_state_bytes, 224u);
}
```

Add named tests that reject checked-add overflow, an empty `state_name`, a
duplicate logical-state declaration, inconsistent state classes for records
sharing one state name, PID zero, observer request ID zero, an invalid
correlation ID, a trigger other than `query_state`, an empty/unknown phase,
an observer plugin device other than exact `CPU`, invalid UTF-8/control
characters, an alias that changes reserved capacity, an externally backed
DNNL allocation, and an unsupported state subclass. Every rejection must
assert the exact `ov::Exception` message fragment.

Add positive owner-domain tests showing that K/V beam-table aliases deduplicate
globally, two distinct double-buffer owners both count, two scale/ZP
`PlainTensor` copies sharing one control block count once, and a non-owning
scale/ZP view with `m_capacity == 0` emits no allocation.

Deduplicate snapshot-globally with a tagged owner key. For `Memory` payload,
output, hidden, and beam blocks compare `Memory::getMemoryBlock()` owners with
`std::owner_less<MemoryBlockPtr>`. For an owned scale/ZP `PlainTensor`, compare
`m_ptr` owners with `std::owner_less<std::shared_ptr<uint8_t>>` and use exactly
`m_capacity`. The two domains never alias. A null owner or `m_capacity == 0`
is a non-owning view: do not add a scale/ZP allocation because its DNNL owner
is already visited. Assign first-seen temporary owner ordinals; never convert
a pointer to an integer, key by state/role/size, or serialize an address.

- [ ] **Step 2: Run RED**

Configure the short-path derived checkout with CPU unit tests and debug caps,
then build only the unit target:

```powershell
$derivedCore = (Resolve-Path `
  "R:\external\official-openvino\2026-07-19\openvino-cpu-state-observer").Path
$expectedOTarget = [IO.Path]::GetFullPath($derivedCore).TrimEnd('\')
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
    throw "O: maps to '$actualOTarget', not the derived core '$expectedOTarget'"
  }
} else {
  & subst.exe O: $expectedOTarget
  if ($LASTEXITCODE -ne 0) {
    throw "Unable to map the derived core checkout to O:"
  }
}

$cmake = "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"
$configureCommand = @(
  $cmake, "-S", "O:\", "-B", "C:\ov-build\state-observer",
  "-G", "Visual Studio 18 2026", "-A", "x64",
  "-DENABLE_DEBUG_CAPS=ON", "-DENABLE_CPU_DEBUG_CAPS=ON",
  "-DENABLE_TESTS=ON", "-DENABLE_FUNCTIONAL_TESTS=OFF",
  "-DENABLE_SAMPLES=OFF", "-DENABLE_PYTHON=OFF",
  "-DENABLE_INTEL_GPU=OFF", "-DENABLE_INTEL_NPU=OFF",
  "-DENABLE_OV_ONNX_FRONTEND=OFF", "-DENABLE_OV_PADDLE_FRONTEND=OFF",
  "-DENABLE_OV_TF_FRONTEND=OFF", "-DENABLE_LTO=OFF"
)
& .\scripts\testing\invoke_guarded_command.ps1 `
  -Label task2-configure-red -WorkingDirectory R:\ `
  -EvidenceRoot R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards `
  -ExpectedExit Zero -Command $configureCommand
$cache = Get-Content -LiteralPath C:\ov-build\state-observer\CMakeCache.txt
foreach ($required in @(
  "ENABLE_DEBUG_CAPS:BOOL=ON",
  "ENABLE_CPU_DEBUG_CAPS:BOOL=ON"
)) {
  if ($cache -notcontains $required) {
    throw "Observer configure cache is missing '$required'"
  }
}
$buildCommand = @(
  $cmake, "--build", "C:\ov-build\state-observer", "--config", "Release",
  "--target", "ov_cpu_unit_tests", "--parallel", "2"
)
& .\scripts\testing\invoke_guarded_command.ps1 `
  -Label task2-build-red -WorkingDirectory R:\ `
  -EvidenceRoot R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards `
  -ExpectedExit NonZero -Command $buildCommand
```

Expected: compilation fails for absent snapshot types and accessor.

Keep `O:` mapped through Tasks 2-6 because the generated projects retain the
short source path. This block is crash-resumable: a later shell reuses only an
exact matching mapping and rejects every collision. Task 7 removes `O:` only
after all builds/tests stop and after revalidating that it still maps to this
exact derived checkout.

- [ ] **Step 3: Implement the capacity accessor**

Add `<optional>` to `cpu_memory.h`. Under `CPU_DEBUG_CAPS`, return capacity only
for the supported owner:

```cpp
std::optional<size_t> DnnlMemoryBlock::getAllocatedSize() const noexcept {
    const auto* reuse = dynamic_cast<const MemoryBlockWithReuse*>(m_pMemBlock.get());
    if (reuse == nullptr) {
        return std::nullopt;
    }
    return reuse->size();
}
```

Do not fall back to `Memory::getSize()`.

- [ ] **Step 4: Implement snapshot capture and deterministic JSON**

The extractor and pure builder must:

- assign ordinals by first-seen backing-block identity;
- count each owned block once;
- record input/output aliases explicitly;
- classify only the three known CPU state classes;
- include native KV hidden memory and `PlainTensor::m_capacity`;
- emit a `SCALE_ZP` record only for a positive-capacity owned `PlainTensor`,
  using its tagged shared-control-block identity, not a pointer value;
- compute per-state and request totals from the same deduplicated records;
- require positive `pid`/`observer_request_id`, exact opaque correlation ID,
  `query_state`, one allowed phase, and `observer_plugin_device="CPU"`;
- use checked addition;
- produce a complete bounded JSON string before any write.

No raw pointer value may appear in the serialized string.

- [ ] **Step 5: Run GREEN twice**

Repeat the Step 2 configure command so the new production `.cpp` is present in
the generated project, then run twice:

```powershell
foreach ($run in 1..2) {
  & .\scripts\testing\invoke_guarded_command.ps1 `
    -Label "task2-dump-green-$run" -WorkingDirectory R:\ `
    -EvidenceRoot R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards `
    -ExpectedExit Zero -Command @(
      "C:\ov-build\state-observer\bin\intel64\Release\ov_cpu_unit_tests.exe",
      "--gtest_filter=StateAllocationsDump.*"
    )
}
& .\scripts\testing\invoke_guarded_command.ps1 `
  -Label task2-plugin-green -WorkingDirectory R:\ `
  -EvidenceRoot R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards `
  -ExpectedExit Zero -Command @(
    $cmake, "--build", "C:\ov-build\state-observer", "--config", "Release",
    "--target", "openvino_intel_cpu_plugin", "--parallel", "2"
  )
```

Expected: all focused tests pass twice and the production plugin builds.

- [ ] **Step 6: Audit and commit**

```powershell
git diff --check
rg -n "TQDBG|raw_address|reinterpret_cast.*uintptr" `
  src/plugins/intel_cpu/src/utils/state_allocations_dump.* `
  src/plugins/intel_cpu/tests/unit/state_allocations_dump_test.cpp
git add src/plugins/intel_cpu/src/cpu_memory.* `
        src/plugins/intel_cpu/src/utils/state_allocations_dump.* `
        src/plugins/intel_cpu/tests/unit/state_allocations_dump_test.cpp
git commit -m "feat(cpu): capture physical variable-state allocations"
```

### Task 3: Add the Default-Off JSONL Trigger at CPU `query_state()`

**Files in derived OpenVINO core checkout:**
- Modify: `src/plugins/intel_cpu/src/compiled_model.h`
- Modify: `src/plugins/intel_cpu/src/compiled_model.cpp`
- Modify: `src/plugins/intel_cpu/src/utils/debug_caps_config.h`
- Modify: `src/plugins/intel_cpu/src/utils/debug_caps_config.cpp`
- Modify: `src/plugins/intel_cpu/src/infer_request.h`
- Modify: `src/plugins/intel_cpu/src/infer_request.cpp`
- Create: `src/plugins/intel_cpu/tests/unit/state_allocations_writer_test.cpp`

**Interfaces:**

```cpp
struct StateAllocationDumpConfig {
    std::filesystem::path path;
    bool enabled() const noexcept;
};

struct StateAllocationObserverContext {
    uint32_t schema_version;
    std::string correlation_id;
};

using EnvironmentLookup =
    std::function<std::optional<std::string>(std::string_view)>;
StateAllocationDumpConfig parse_state_allocation_dump_config(
    const EnvironmentLookup& lookup);
StateAllocationObserverContext parse_state_allocation_observer_context(
    const ov::RTMap& model_rt_info);
uint64_t next_observer_request_id() noexcept;
void append_state_allocation_snapshot(
    const StateAllocationDumpConfig& config,
    const std::vector<MemStatePtr>& states,
    const SnapshotContext& context);
```

- [ ] **Step 1: Write default-off, path, and concurrency RED tests**

Use a pure environment lookup in parser tests and a unique existing temporary
directory owned by the test fixture in writer tests. The fixture's `SetUp`
combines `std::filesystem::temp_directory_path()`, the current PID, and a
monotonic test counter; it creates that directory. `TearDown` uses
`remove_all` only after verifying the resolved path is a child of the system
temporary directory. Add these exact tests:

```cpp
using Environment = std::map<std::string, std::string>;

StateAllocationDumpConfig parse(const Environment& environment) {
    return parse_state_allocation_dump_config(
        [&](std::string_view name) -> std::optional<std::string> {
            auto found = environment.find(std::string{name});
            return found == environment.end()
                       ? std::nullopt
                       : std::optional<std::string>{found->second};
        });
}

TEST(StateAllocationsWriter, AbsentEnvironmentDisablesWriter) {
    EXPECT_FALSE(parse({}).enabled());
}

TEST(StateAllocationsWriter, RejectsDirectoryAndNonJsonlPath) {
    for (const auto& path : {temporary_directory().string(),
                             (temporary_directory() / "snapshot.json").string()}) {
        EXPECT_THROW(
            parse({{"OV_CPU_STATE_ALLOCATION_DUMP_PATH", path}}),
            ov::Exception);
    }
}

TEST(StateAllocationsWriter, RejectsMissingOutputParent) {
    EXPECT_THROW(
        parse({{"OV_CPU_STATE_ALLOCATION_DUMP_PATH",
                (temporary_directory() / "missing" / "snapshot.jsonl").string()}}),
        ov::Exception);
}

TEST(StateAllocationsWriter, ConcurrentWritesRemainWholeJsonLines) {
    const auto path = temporary_directory() / "snapshot.jsonl";
    const auto config =
        parse({{"OV_CPU_STATE_ALLOCATION_DUMP_PATH", path.string()}});
    std::vector<std::thread> writers;
    for (size_t index = 0; index < 8; ++index) {
        writers.emplace_back([&, index] {
            append_serialized_snapshot(
                config,
                serialized_fixture(4242, index + 1));
        });
    }
    for (auto& writer : writers) {
        writer.join();
    }
    const auto lines = read_nonempty_lines(path);
    ASSERT_EQ(lines.size(), 8u);
    for (const auto& line : lines) {
        EXPECT_NO_THROW(parse_json_object_with_unique_keys(line));
    }
}

TEST(StateAllocationsWriter, RecordsNoTensorContentsOrRawAddresses) {
    const auto serialized = serialized_fixture(4242, 1);
    EXPECT_EQ(serialized.find("tensor_contents"), std::string::npos);
    EXPECT_EQ(serialized.find("prompt"), std::string::npos);
    EXPECT_EQ(serialized.find("response"), std::string::npos);
    EXPECT_EQ(serialized.find("raw_address"), std::string::npos);
    EXPECT_EQ(serialized.find("0x"), std::string::npos);
}
```

Implement `temporary_directory`, `serialized_fixture`,
`append_serialized_snapshot`, `read_nonempty_lines`, and
`parse_json_object_with_unique_keys` as private fixture helpers in this same
test file. The serializer fixture contains the full Task 2 schema; the JSON
helper invokes the production duplicate-key-rejecting parser, so no second
schema parser exists only for tests.

Also add named tests for: valid/malformed/missing private model context;
request-ID uniqueness across two compiled models and after destruction;
`fresh`, `seeded_no_infer`, and `post_infer`; failed/cancelled inference not
advancing the phase; a delegating request not emitting a duplicate aggregate
record; and a process-wide sequence remaining monotonic across threads.

- [ ] **Step 2: Run RED**

Run the new focused tests and confirm failure because the dump config/writer do
not exist.

- [ ] **Step 3: Parse strict opt-in configuration**

Read exactly one environment variable:

```text
OV_CPU_STATE_ALLOCATION_DUMP_PATH
```

Reject a configured path unless it has a `.jsonl` extension and its parent
already exists. Do not create arbitrary directories. When the path is absent,
do not parse model metadata, allocate an observer request ID, inspect states,
or perform observer I/O.

When enabled, parse the private
`openvino_genai.cpu_state_allocation_observer` model `rt_info` once in the
`CompiledModel` constructor. Require schema `1` and one 32-lowercase-hex
`correlation_id`, then expose that immutable private context only through
`CompiledModelHolder`; add no public property or supported-property entry.
Never read requested-device, phase, request ID, or execution-device truth from
the environment.

- [ ] **Step 4: Integrate before public materialization**

Use one function-static `std::atomic<uint64_t>` in the debug observer DLL.
Assign a never-reused observer request ID in each enabled
`SyncInferRequest` constructor; do not use the decrementing/reusable
`CompiledModelHolder::id()`. Set `latest_infer_succeeded=false` at the start of
every inference attempt and set it true only after both `graph.Infer(this)` and
`graph.PullOutputData(m_outputs)` complete. A failure/cancellation therefore
clears prior success.

At `query_state()`, first inspect all private states. If all are reset, clear
`latest_infer_succeeded` and return phase `fresh`; otherwise return
`post_infer` only when the latest-attempt flag is true, and
`seeded_no_infer` when it is false. This also resets the flag after a public
state reset. Add explicit `success -> failure` and `success -> state reset`
tests; neither may remain `post_infer`. In the delegating branch, let each real
CPU subrequest emit and do not write an aggregate record. At the
non-delegating leaf, before constructing public state wrappers:

```cpp
#ifdef CPU_DEBUG_CAPS
const auto& dump_config =
    m_compiled_model.graph().getConfig().debugCaps.stateAllocationDump;
if (dump_config.enabled()) {
    append_state_allocation_snapshot(
        dump_config,
        m_memory_states,
        SnapshotContext{current_process_id(),
                        m_observer_request_id,
                        m_compiled_model.state_allocation_observer_context()
                            .correlation_id,
                        "query_state",
                        observer_phase(),
                        "CPU"});
}
#endif
return {m_memory_states.begin(), m_memory_states.end()};
```

Fail closed if enabled context parsing, serialization, or writing cannot
complete. The raw record field is `observer_plugin_device`, never
`requested_device`, `actual_devices`, or `actual_execution_devices`.

- [ ] **Step 5: Run GREEN twice and production build**

Repeat the Task 2 configure command so the new unit source is present in the
generated project, then run twice:

```powershell
foreach ($run in 1..2) {
  & .\scripts\testing\invoke_guarded_command.ps1 `
    -Label "task3-writer-green-$run" -WorkingDirectory R:\ `
    -EvidenceRoot R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards `
    -ExpectedExit Zero -Command @(
      "C:\ov-build\state-observer\bin\intel64\Release\ov_cpu_unit_tests.exe",
      "--gtest_filter=StateAllocationsWriter.*:StateAllocationsDump.*"
    )
}
& .\scripts\testing\invoke_guarded_command.ps1 `
  -Label task3-plugin-green -WorkingDirectory R:\ `
  -EvidenceRoot R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards `
  -ExpectedExit Zero -Command @(
    $cmake, "--build", "C:\ov-build\state-observer", "--config", "Release",
    "--target", "openvino_intel_cpu_plugin", "--parallel", "2"
  )
```

Then unset `OV_CPU_STATE_ALLOCATION_DUMP_PATH`, run a stock state-query
fixture, and prove no JSONL file is created and the observer request counter
does not advance. Also run once with a non-JSONL path and require a nonzero
fixture exit plus no new file.

- [ ] **Step 6: Commit**

```powershell
git add src/plugins/intel_cpu/src/utils/debug_caps_config.* `
        src/plugins/intel_cpu/src/compiled_model.* `
        src/plugins/intel_cpu/src/infer_request.h `
        src/plugins/intel_cpu/src/infer_request.cpp `
        src/plugins/intel_cpu/src/utils/state_allocations_dump.* `
        src/plugins/intel_cpu/tests/unit/state_allocations_writer_test.cpp
git commit -m "feat(cpu): emit opt-in state allocation snapshots"
```

### Task 4: Prove Native F16/U8/U4 Physical Allocation with a Synthetic Probe

**Files in derived OpenVINO core checkout:**
- Create: `src/plugins/intel_cpu/tests/state_alloc_probe/CMakeLists.txt`
- Create: `src/plugins/intel_cpu/tests/state_alloc_probe/main.cpp`
- Modify: `src/plugins/intel_cpu/tests/CMakeLists.txt`
- Create in parent repository: `scripts/testing/official_openvino/kv_physical.py`
- Create in parent repository: `scripts/testing/tests/test_official_openvino_kv_physical.py`

**Interfaces:**

```python
def reject_duplicate_pairs(pairs: list[tuple[str, object]]) -> dict[str, object]:
    result: dict[str, object] = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"duplicate JSON key: {key}")
        result[key] = value
    return result

def load_cpu_state_snapshots(path: Path) -> list[dict[str, object]]:
    records: list[dict[str, object]] = []
    with path.open("r", encoding="utf-8", newline="") as stream:
        for line_number, line in enumerate(stream, 1):
            if not line.endswith("\n") or not line.strip():
                raise ValueError(f"incomplete JSONL line: {line_number}")
            record = json.loads(line, object_pairs_hook=reject_duplicate_pairs)
            if not isinstance(record, dict):
                raise ValueError(f"snapshot is not an object: {line_number}")
            records.append(record)
    if not records:
        raise ValueError("snapshot file is empty")
    return records

def recompute_and_validate_native_snapshot(
    snapshot: Mapping[str, object],
    expected_precision: str,
) -> dict[str, object]:
    """Validate every allocation record and return independently recomputed totals."""

def validate_native_probe(
    snapshots: Sequence[Mapping[str, object]],
    *,
    expected_precision: str,
    expected_phase: str,
    expected_correlation_id: str,
) -> dict[str, object]:
    if len(snapshots) != 1:
        raise ValueError("native probe requires exactly one snapshot")
    snapshot = dict(snapshots[0])
    if snapshot.get("phase") != expected_phase:
        raise ValueError("native probe phase mismatch")
    if snapshot.get("trigger") != "query_state":
        raise ValueError("native probe trigger mismatch")
    if snapshot.get("observer_plugin_device") != "CPU":
        raise ValueError("native probe observer plugin mismatch")
    if snapshot.get("correlation_id") != expected_correlation_id:
        raise ValueError("native probe correlation mismatch")
    if not isinstance(snapshot.get("pid"), int) or snapshot["pid"] <= 0:
        raise ValueError("native probe PID is invalid")
    if (
        not isinstance(snapshot.get("observer_request_id"), int)
        or snapshot["observer_request_id"] <= 0
    ):
        raise ValueError("native probe observer request ID is invalid")
    return recompute_and_validate_native_snapshot(snapshot, expected_precision)
```

`load_cpu_state_snapshots` also passes a `parse_constant` callback that rejects
`NaN`, positive infinity, and negative infinity. Implement
`recompute_and_validate_native_snapshot` before `validate_native_probe`; it
requires a nonempty `records` list and performs these operations in order:

1. validate every integer as a non-Boolean value in `[0, 2**64 - 1]`;
2. validate each tagged `(owner_domain, block_ordinal)` and require every
   alias to reference an earlier matching owner with identical capacity;
3. count each non-aliased owned DNNL or PlainTensor owner exactly once across
   the whole snapshot, never per state;
4. reject null/zero retained capacity for an emitted owned record, unknown
   owners, external backing, unknown roles/classes, and U4 arithmetic based on
   `element_type.size()`;
5. recompute generic, beam, scale/ZP, per-state, and request totals with
   checked addition and compare every serialized total;
6. require exactly the native K and V states, concrete class
   `VariableStateKVcache`, expected internal KV precision, a beam owner for
   each state, and positive scale/ZP owners for U8/U4 only.

Return a new normalized dictionary containing only the recomputed totals and
validated state identities; never return a shallowly trusted serialized total.
Expose this as:

```text
python -m scripts.testing.official_openvino.kv_physical validate-native
  --snapshot C:\ov-evidence\native-f16-run1.jsonl
  --precision f16
  --phase post_infer
  --correlation-id 0123456789abcdef0123456789abcdef
```

- [ ] **Step 1: Write Python validator RED tests**

Use frozen fake JSONL for F16/U8/U4 and reject:

- public logical type substituted for internal type;
- missing K or V state;
- wrong concrete CPU state class;
- input/output alias double-counting;
- absent beam allocation;
- absent U8/U4 scale/ZP capacity;
- U4 calculated with `element_type.size()`;
- descriptor bytes substituted for reserved bytes;
- `seeded_no_infer` represented as steady state;
- malformed/duplicate JSON keys, overflow, null, NaN, and unsourced zero.

- [ ] **Step 2: Run Python RED**

```powershell
& .\scripts\testing\invoke_guarded_command.ps1 `
  -Label task4-python-red -WorkingDirectory R:\ `
  -EvidenceRoot R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards `
  -ExpectedExit NonZero -Command @(
    "python", "-m", "pytest",
    "scripts/testing/tests/test_official_openvino_kv_physical.py", "-q"
  )
```

Expected: import failure for absent `kv_physical.py`.

- [ ] **Step 3: Add the public-API synthetic probe**

The executable must build a paired stateful-SDPA model with
`B=1,H=2,L=3,D=16`, compile CPU with one stream and one cache precision,
perform one state update/inference, and call `query_state()` exactly once
afterward. It accepts:

```text
--precision f16|u8|u4
--request-id native-f16-run1
--plugins-xml C:\ov-build\state-observer\bin\intel64\Release\plugins.xml
--output C:\ov-evidence\native-f16-run1.jsonl
--phase post_infer
--correlation-id 0123456789abcdef0123456789abcdef
--expect-observer present|absent
```

Before constructing `ov::Core`, the probe validates the output parent, attaches
private model `rt_info` schema `1` plus the exact correlation ID, and calls
`_putenv_s` on Windows (or `setenv` elsewhere) only for the dump path. It
constructs
`ov::Core(arguments.plugins_xml.string())`; no ambient plugin registry is
accepted. It then verifies:

```cpp
OPENVINO_ASSERT(compiled.get_property(ov::execution_devices) ==
                    std::vector<std::string>{"CPU"},
                "probe did not execute on exact CPU");
OPENVINO_ASSERT(compiled.get_property(ov::hint::kv_cache_precision) ==
                    requested_precision,
                "probe cache precision differs from request");
OPENVINO_ASSERT(count_runtime_layers(compiled, "ScaledDotProductAttention") == 1,
                "probe did not compile exactly one fused stateful SDPA");
```

After one successful inference it calls `query_state()` exactly once, verifies
the returned state names are exactly the fixture's K and V IDs, never calls
`get_state()`, and checks the selected observer expectation. `present` requires
exactly one complete JSONL line; `absent` requires that the output path does
not exist. Any other value is rejected by CLI parsing.

- [ ] **Step 4: Add the strict Python validator**

The validator parses JSON with duplicate-key rejection, recomputes all unique
block totals, and requires:

```python
assert snapshot["phase"] == "post_infer"
assert snapshot["observer_plugin_device"] == "CPU"
assert {state["state_class"] for state in states} == {"VariableStateKVcache"}
assert observed_internal_types == {expected_precision}
assert request_total == sum(unique_reserved_blocks) + beam_total + scale_zp_total
```

Formulas for `192/96/48` bytes are independent fixture expectations only; the
accepted measured values come from observer fields.

- [ ] **Step 5: Build and run all three modes twice**

Add `add_subdirectory(state_alloc_probe)` explicitly to
`src/plugins/intel_cpu/tests/CMakeLists.txt`; sibling test directories are not
auto-discovered. Repeat the Task 2 configure command so this target and all
glob-discovered sources are present, then:

```powershell
& .\scripts\testing\invoke_guarded_command.ps1 `
  -Label task4-probe-build -WorkingDirectory R:\ `
  -EvidenceRoot R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards `
  -ExpectedExit Zero -Command @(
    $cmake, "--build", "C:\ov-build\state-observer", "--config", "Release",
    "--target", "ov_cpu_state_alloc_probe", "--parallel", "2"
  )
$nativeEvidence = "R:\experiments\raw-results\openvino-turboquant\2026-07-28\native-probe"
New-Item -ItemType Directory -Force -Path $nativeEvidence | Out-Null
foreach ($precision in @("f16","u8","u4")) {
  1..2 | ForEach-Object {
    $run = $_
    $requestId = "native-$precision-run$run"
    $output = Join-Path $nativeEvidence "$requestId.jsonl"
    if (Test-Path -LiteralPath $output) {
      throw "Refusing stale native-probe evidence: $output"
    }
    & .\scripts\testing\invoke_guarded_command.ps1 `
      -Label "task4-$requestId" -WorkingDirectory R:\ `
      -EvidenceRoot R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards `
      -ExpectedExit Zero -Command @(
        "C:\ov-build\state-observer\bin\intel64\Release\ov_cpu_state_alloc_probe.exe",
        "--precision", $precision, "--request-id", $requestId,
        "--plugins-xml",
        "C:\ov-build\state-observer\bin\intel64\Release\plugins.xml",
        "--phase", "post_infer", "--correlation-id",
        "0123456789abcdef0123456789abcdef",
        "--expect-observer", "present", "--output", $output
      )
    & .\scripts\testing\invoke_guarded_command.ps1 `
      -Label "task4-validate-$requestId" -WorkingDirectory R:\ `
      -EvidenceRoot R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards `
      -ExpectedExit Zero -Command @(
        "python", "-m", "scripts.testing.official_openvino.kv_physical",
        "validate-native", "--snapshot", $output, "--precision", $precision,
        "--phase", "post_infer", "--correlation-id",
        "0123456789abcdef0123456789abcdef"
      )
  }
}
& .\scripts\testing\invoke_guarded_command.ps1 `
  -Label task4-python-green -WorkingDirectory R:\ `
  -EvidenceRoot R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards `
  -ExpectedExit Zero -Command @(
    "python", "-m", "pytest",
    "scripts/testing/tests/test_official_openvino_kv_physical.py", "-q"
  )
```

Expected: all six fresh processes pass; normalized types, shapes, active
bytes, reserved bytes, state classes, and totals match across repetitions.

- [ ] **Step 6: Commit both checkouts**

Derived core:

```powershell
git add src/plugins/intel_cpu/tests/state_alloc_probe/CMakeLists.txt `
        src/plugins/intel_cpu/tests/state_alloc_probe/main.cpp `
        src/plugins/intel_cpu/tests/CMakeLists.txt
git commit -m "test(cpu): prove native KV physical allocation"
```

Parent:

```powershell
git add scripts/testing/official_openvino/kv_physical.py `
        scripts/testing/tests/test_official_openvino_kv_physical.py
git commit -m "test(openvino): validate CPU KV physical snapshots"
```

### Task 5: Add Exact GenAI K/V Bindings and SHA-256 Model Identity

**Files in derived OpenVINO GenAI checkout:**
- Create: `src/cpp/src/llm/sha256.hpp`
- Create: `src/cpp/src/llm/sha256.cpp`
- Modify: `src/cpp/src/llm/turboquant_stateful_graph.hpp`
- Modify: `src/cpp/src/llm/turboquant_stateful_graph.cpp`
- Create: `src/cpp/src/llm/kv_state_binding_manifest.hpp`
- Create: `src/cpp/src/llm/kv_state_binding_manifest.cpp`
- Modify: `src/cpp/src/llm/pipeline.cpp`
- Modify: `src/cpp/src/llm/pipeline_stateful.cpp`
- Modify: `src/cpp/src/continuous_batching/cache/turboquant_config.cpp`
- Modify: `src/cpp/include/openvino/genai/turboquant_config.hpp`
- Create: `tests/cpp/turboquant_kv_state_binding.cpp`
- Modify: `tests/cpp/CMakeLists.txt`
- Modify: `tests/python/test_turboquant_activation_schema.py`

**Interfaces:**

```cpp
enum class RuntimeStateComponent {
    STANDARD_STATE,
    PAYLOAD,
    NORM,
    METADATA,
};

struct RuntimeStateBinding {
    std::string variable_id;
    KVKind kind;
    RuntimeStateComponent component;
    ov::element::Type graph_element_type;
    ov::PartialShape graph_shape;
};

struct LogicalKVStateBinding {
    std::string source_variable_id;
    KVKind kind;
    CacheAlgorithm requested_algorithm;
    ov::element::Type source_element_type;
    ov::PartialShape source_shape;
    std::vector<RuntimeStateBinding> runtime_bindings;
};

struct KVStateBindingManifest {
    uint32_t schema_version = 1;
    std::string request_id;
    std::string source_model_sha256;
    std::string transformed_model_sha256;
    std::vector<LogicalKVStateBinding> states;
};

class Sha256 {
public:
    Sha256();
    void update(const void* bytes, size_t size);
    std::array<uint8_t, 32> finish();
};

std::string sha256_hex(std::initializer_list<std::string_view> chunks);
std::string sha256_openvino_model(const std::shared_ptr<ov::Model>& model);
std::string standard_transform_sha256(std::string_view source_model_sha256);
std::string kv_state_binding_manifest_json(
    const KVStateBindingManifest& manifest);
```

- [ ] **Step 1: Write binding/SHA RED tests**

Required tests:

- deceptive variable names are classified only by SDPA K/V port;
- STANDARD creates exactly one `STANDARD_STATE` binding;
- TBQ creates exact payload/norm/metadata IDs at construction time;
- mixed routes retain both binding forms;
- no manifest byte field exists;
- duplicate/empty binding IDs fail;
- source and transformed SHA-256 are lowercase 64-hex;
- `sha256_hex({"abc"})` equals
  `ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad`;
- XML bytes `<net/>`, one zero byte, and BIN bytes `00 01 02 ff` hash to
  `1f30877ca2abf7c8478d54fec9f871878998abaff1aaa79e912924be49a9ee68`;
- two serializations of the same model hash identically;
- changing one weight byte changes the hash;
- STANDARD transform identity for a source digest of 64 lowercase `a` bytes is
  `8115a092563d54f4d5703479a7661eba9374a79a28e537a3efa0dd179bdde186`;
- the old 16-hex FNV value cannot satisfy the schema.
- serialized manifests have exactly one K or V role per source state, exact
  runtime component IDs, no byte fields, no duplicate keys, and a 1 MiB bound.
- activation schema v2 and all current Python fixtures require 64-lowercase-hex
  source/transformed identities and reject the historical 16-hex FNV form.

- [ ] **Step 2: Run RED**

Build/run:

```powershell
& .\scripts\testing\invoke_guarded_command.ps1 `
  -Label task5-binding-red -WorkingDirectory R:\ `
  -EvidenceRoot R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards `
  -ExpectedExit NonZero -Command @(
    "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe",
    "--build",
    "R:\external\official-openvino\2026-07-19\build-genai-turboquant",
    "--config", "Release", "--target",
    "turboquant_kv_state_binding_tests", "--parallel", "1"
  )
```

Expected: compilation fails because the manifest and SHA-256 functions do not
exist.

- [ ] **Step 3: Extract immutable graph discovery**

Reuse the already strict candidate discovery without mutating the original
model. Persist exact created component IDs inside `build_replacement()`.
For STANDARD, persist the original variable ID as one `STANDARD_STATE`
binding. For TBQ, `build_replacement()` returns the exact IDs it created for
`PAYLOAD`, `NORM`, and `METADATA`; no later code may append or parse suffixes.
Validate one KEY and one VALUE candidate per layer from the SDPA input port.

- [ ] **Step 4: Implement portable SHA-256 and exact model domains**

Implement SHA-256 locally in `sha256.cpp` with the FIPS 180-4 round constants,
big-endian message schedule, checked bit-length arithmetic, and no platform
crypto dependency. The NIST and binary-domain RED vectors above are mandatory.

Serialize with `ov::pass::Serialize` into binary-capable XML and BIN streams.
Feed exactly:

```text
xml_bytes || 0x00 || bin_bytes
```

There is no prefix, length word, newline, filename, or temporary path in the
domain. Require lowercase 64-hex output. At the start of the model-taking
`StatefulLLMPipeline` constructor, before
`apply_slice_before_matmul_transformation()` or any other mutation, compute the
source digest. Compute the transformed digest only after the exact graph
replacement is complete.

For an untransformed STANDARD route, define the still-64-hex transform identity
as:

```text
SHA256(UTF8("standard-untransformed") || 0x00 ||
       UTF8(source_model_sha256))
```

Rename the implementation helpers to make their historical status explicit,
but retain forwarding definitions for the existing public
`turboquant_model_hash(path|string)` symbols so this change does not break ABI.
No accepted producer may call those forwarders. Replace every source-model
call site in `pipeline.cpp` and `pipeline_stateful.cpp`, the transformed-model
assignment in `pipeline_stateful.cpp`, and the identity validation in
`turboquant_config.cpp` with the SHA-256 API. Update the identity fields and
all current activation/Python fixtures to require 64 lowercase hex. The
unrelated lifetime-result and vision-tensor FNV hashes remain untouched.

- [ ] **Step 5: Run GREEN twice and object build**

In `tests/cpp/CMakeLists.txt`, create the standalone target
`turboquant_kv_state_binding_tests`, set
`RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"`, and name every authored
test `TurboQuantKVStateBinding.*`. Reconfigure the existing GenAI build once
so the new target and glob-discovered sources are present. Then build the exact
targets and run their exact filters twice:

```powershell
$cmake = "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"
$build = "R:\external\official-openvino\2026-07-19\build-genai-turboquant"
$source = "R:\external\official-openvino\2026-07-19\openvino.genai-turboquant"
& .\scripts\testing\invoke_guarded_command.ps1 `
  -Label task5-configure -WorkingDirectory R:\ `
  -EvidenceRoot R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards `
  -ExpectedExit Zero -Command @($cmake, "-S", $source, "-B", $build)
$filters = [ordered]@{
  turboquant_kv_state_binding_tests = "TurboQuantKVStateBinding.*"
  turboquant_stateful_graph_tests = "TurboQuantStatefulGraph.*"
  turboquant_state_update_decode_tests = "TurboQuantStateUpdateDecode.*"
  turboquant_codec_tests = "TurboQuantCodec.*"
  turboquant_config_tests = "TurboQuantConfig.*:TurboQuantPipeline.*"
  turboquant_pipeline_activation_tests = "TurboQuantPipelineActivation.*"
}
foreach ($name in $filters.Keys) {
  & .\scripts\testing\invoke_guarded_command.ps1 `
    -Label "task5-build-$name" -WorkingDirectory R:\ `
    -EvidenceRoot R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards `
    -ExpectedExit Zero -Command @(
      $cmake, "--build", $build, "--config", "Release",
      "--target", $name, "--parallel", "1"
    )
}
foreach ($run in 1..2) {
  foreach ($name in $filters.Keys) {
    $exe = Get-ChildItem $build -Recurse -Filter "$name.exe" |
      Select-Object -ExpandProperty FullName -Unique
    if (@($exe).Count -ne 1) { throw "Expected one executable for $name" }
    & .\scripts\testing\invoke_guarded_command.ps1 `
      -Label "task5-$name-$run" -WorkingDirectory R:\ `
      -EvidenceRoot R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards `
      -ExpectedExit Zero -Command @(
        $exe, "--gtest_filter=$($filters[$name])"
      )
  }
  & .\scripts\testing\invoke_guarded_command.ps1 `
    -Label "task5-python-$run" -WorkingDirectory R:\ `
    -EvidenceRoot R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards `
    -ExpectedExit Zero -Command @(
      "python", "-m", "pytest",
      "R:\external\official-openvino\2026-07-19\openvino.genai-turboquant\tests\python\test_turboquant_activation_schema.py",
      "-q"
    )
}
& .\scripts\testing\invoke_guarded_command.ps1 `
  -Label task5-genai-object -WorkingDirectory R:\ `
  -EvidenceRoot R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards `
  -ExpectedExit Zero -Command @(
    $cmake, "--build", $build, "--config", "Release",
    "--target", "openvino_genai_obj", "--parallel", "1"
  )
```

- [ ] **Step 6: Commit**

```powershell
git add src/cpp/src/llm/kv_state_binding_manifest.* `
        src/cpp/src/llm/sha256.* `
        src/cpp/src/llm/turboquant_stateful_graph.* `
        src/cpp/src/llm/pipeline.cpp `
        src/cpp/src/llm/pipeline_stateful.cpp `
        src/cpp/src/continuous_batching/cache/turboquant_config.cpp `
        src/cpp/include/openvino/genai/turboquant_config.hpp `
        tests/cpp/turboquant_kv_state_binding.cpp `
        tests/cpp/CMakeLists.txt `
        tests/python/test_turboquant_activation_schema.py
git commit -m "feat(openvino): bind runtime KV states to model SHA256"
```

### Task 6: Reconcile CPU Physical Snapshots with GenAI Activation

**Files in derived OpenVINO GenAI checkout:**
- Modify: `src/cpp/src/llm/pipeline_base.hpp`
- Modify: `src/cpp/src/llm/pipeline.cpp`
- Modify: `src/cpp/src/llm/pipeline_stateful.hpp`
- Modify: `src/cpp/src/llm/pipeline_stateful.cpp`
- Create: `src/cpp/src/llm/state_allocation_observer_context.hpp`
- Create: `src/cpp/src/llm/state_allocation_observer_context.cpp`
- Create: `src/cpp/src/llm/kv_observation.hpp`
- Create: `src/cpp/src/llm/kv_observation.cpp`
- Modify: `src/cpp/src/continuous_batching/cache/turboquant_config.cpp`
- Modify: `src/cpp/include/openvino/genai/turboquant_config.hpp`
- Modify: `tests/cpp/turboquant_pipeline_activation.cpp`
- Create: `tests/cpp/turboquant_kv_observation.cpp`
- Modify: `tests/cpp/CMakeLists.txt`
- Modify: `tests/python/test_turboquant_activation_schema.py`

**Files in parent repository:**
- Modify: `scripts/testing/official_openvino/runner.py`
- Modify: `scripts/testing/official_openvino/kv_physical.py`
- Create: `scripts/testing/official_openvino/kv_observation_barrier.py`
- Modify: `scripts/testing/tests/test_official_openvino_runner.py`
- Modify: `scripts/testing/tests/test_official_openvino_kv_physical.py`
- Create: `scripts/testing/tests/test_official_openvino_kv_observation_barrier.py`
- Create: `scripts/testing/tests/fixtures/fake_openvino_two_phase_child.py`

**Interfaces:**

```cpp
struct RuntimeStateExtent {
    size_t batch;
    size_t sequence_length;
};

struct KVObservationContext {
    std::string request_id;
    uint32_t pid;
    std::string correlation_id;
    std::string requested_device;
    std::vector<std::string> actual_execution_devices;
    std::string requested_attention_path;
    bool fallback_permitted = false;
    KVStateBindingManifest binding_manifest;
};

enum class KVObservationState {
    NOT_STARTED,
    WAITING_FOR_RELEASE,
    COMPLETE,
    FAILED,
};

struct ObservationBarrierConfig {
    std::filesystem::path ready_path;
    std::filesystem::path release_path;
    std::string request_id;
    std::string nonce;
    std::chrono::milliseconds timeout{30000};
};

std::vector<ov::SoPtr<ov::IVariableState>>
query_and_validate_state_names_once(
    ov::InferRequest& request,
    const KVStateBindingManifest& manifest);
RuntimeStateExtent live_runtime_state_extent(const ov::InferRequest& request);
std::string logical_state_observation_json(
    const KVObservationContext& context,
    const RuntimeStateExtent& extent,
    const std::vector<ov::SoPtr<ov::IVariableState>>& states);
void wait_for_observer_release(
    const ObservationBarrierConfig& barrier,
    std::string_view request_id);
void write_observation_event_jsonl(std::string_view complete_json_object);

// Declaration inside the private LLMPipelineImplBase class. It adds no public
// OpenVINO GenAI ABI; the default supports other private pipeline subclasses.
virtual void finalize_kv_observation(bool output_valid) {}
```

```python
def reconcile_physical_kv_allocation(
    *,
    snapshot: Mapping[str, object],
    observer_context: Mapping[str, object],
    binding_manifest: Mapping[str, object],
    activation: Mapping[str, object],
    logical_observation: Mapping[str, object],
    expected_runtime: Mapping[str, object],
) -> dict[str, object]:
    """Return one accepted kv_physical_allocation v1 record or raise."""

def wait_for_ready_and_release(
    *,
    ready_path: Path,
    release_path: Path,
    snapshot_path: Path,
    request_id: str,
    nonce: str,
    close_performance_window: Callable[[], None],
    signal_sampler_stop: Callable[[], None],
    wait_sampler_exit: Callable[[float], int],
    sampler_active_pids: Callable[[], Sequence[int]],
    timeout_seconds: float = 30.0,
) -> dict[str, object]:
    """Prove sampler exit/cleanup and snapshot absence, then release observation."""
```

- [ ] **Step 1: Write end-to-end RED fixtures**

Parameterize the real CPU synthetic pipeline test over exactly 11 accepted
configurations: the Cartesian product of `STANDARD`, `TBQ3`, and `TBQ4` for K
and V with every STANDARD side using native F16 (nine cases), plus
STANDARD/STANDARD native U8 and native U4 controls. Each case owns a unique
request ID, CPU JSONL path, ready path, release path, and 32-lowercase-hex nonce. A
controller thread waits for the ready record, marks a test sampler closed,
waits for its sampler thread to exit successfully, proves zero active sampler
processes, checks that the allocation JSONL is absent, then atomically writes
the exact request ID and nonce to the release file. It proves the sampler
receives no post-close samples.

Required positive assertions:

- native STANDARD F16 uses observed `VariableStateKVcache`;
- native scalar U8/U4 uses the exact internal type and auxiliary capacity;
- TBQ components use observed `VariableStateDoubleBuffer`;
- both retained generic buffers contribute after inference;
- mixed routes compare activation only with the TBQ logical subtotal;
- all routes report separate key/value and active/reserved totals;
- every accepted case emits one GenAI observer-context v1 record, one
  activation v2 record, one logical-state v1 record, one raw CPU snapshot, and
  one reconciled physical-allocation v1 record; GenAI records share the same
  request ID, while the CPU record joins exactly once by `(pid,
  correlation_id)`;
- exact CPU records requested `CPU` and outer
  `actual_execution_devices=["CPU"]`; an AUTO diagnostic remains requested
  `AUTO` even when its outer execution list is `["CPU"]` and is rejected for a
  formal CPU-only row;
- a multi-entry outer execution-device list remains a list and is rejected,
  never flattened or truncated;
- the single public state query calls `get_name()` only. A
`CountingVariableState` test double increments counters in `get_state()` and
proves `query_count == 1` and `get_state_count == 0`.

The controller is the sole request-ID creator. It generates one value matching
`[A-Za-z0-9._-]{1,128}` and injects it as
`OV_GENAI_KV_OBSERVATION_REQUEST_ID` before child launch. The GenAI constructor
parses the complete private barrier configuration once, before model
compilation, and adopts those exact bytes into `ObservationBarrierConfig`,
`KVObservationContext`, `KVStateBindingManifest`, ready/release validation,
context, activation, and logical events. No producer creates a replacement ID.
Test the disabled case with all four GenAI variables absent and all 14
nonempty proper subsets of the four-variable set; every partial subset fails
before compile/generation and creates no ready, CPU, or event output.

Required negative assertions:

- default/explicit PagedAttention, GPU, NPU, AUTO, fallback, invalid output;
- missing/extra/duplicate/ambiguous state IDs;
- unknown backing capacity;
- wrong internal type or concrete class;
- invalid alias graph or arithmetic overflow;
- snapshot captured before inference;
- activation/manifest/snapshot/build/model hash mismatch;
- TBQ U8 payload relabelled U3/U4;
- total physical bytes equated to mixed-route activation bytes;
- public logical bytes substituted for reserved allocation;
- any observer file written after a failed generation;
- missing, late, duplicate, wrong-request, or timed-out barrier
  release;
- outer actual execution devices represented as a scalar instead of exact
  `["CPU"]`;
- missing/duplicate/stale/wrong-PID/wrong-correlation GenAI context records;
- any raw CPU record that claims requested or outer execution-device fields;
- more than one CPU JSONL line for the joined `(pid, correlation_id)`.

Create the standalone CMake target `turboquant_kv_observation_tests`, set its
`RUNTIME_OUTPUT_DIRECTORY` to `${CMAKE_BINARY_DIR}/bin`, and name the suite
`TurboQuantKVObservation.*`. Its minimum ten C++ cases prove decoded-order,
encoded-order, no-release timeout, wrong nonce, one valid release/one query,
empty-output suppression, STANDARD/scalar eligibility, virtual/non-CPU/path
ineligibility, terminal second-finalization behavior, and a source audit with
no `VariableState::get_state()` call. The parent real-process suite has at
least eight cases covering full transition order, early snapshot, duplicate
snapshot, malformed/wrong nonce, sampler failure, timeout/RAM/crash cleanup,
after-release aggregate exclusion, and failure-artifact preservation.

- [ ] **Step 2: Run RED**

Run:

```powershell
& .\scripts\testing\invoke_guarded_command.ps1 `
  -Label task6-python-red -WorkingDirectory R:\ `
  -EvidenceRoot R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards `
  -ExpectedExit NonZero -Command @(
    "python", "-m", "pytest",
    "scripts/testing/tests/test_official_openvino_kv_physical.py",
    "scripts/testing/tests/test_official_openvino_kv_observation_barrier.py",
    "-q"
  )
```

Expected: collection or assertions fail because the barrier, context/logical
events, and five-record reconciliation are absent. Build the new C++ target and expect
compilation to fail for the absent observation interfaces:

```powershell
$cmake = "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"
& .\scripts\testing\invoke_guarded_command.ps1 `
  -Label task6-cpp-red -WorkingDirectory R:\ `
  -EvidenceRoot R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards `
  -ExpectedExit NonZero -Command @(
    $cmake, "--build",
    "R:\external\official-openvino\2026-07-19\build-genai-turboquant",
    "--config", "Release", "--target",
    "turboquant_kv_observation_tests", "--parallel", "1"
  )
```

- [ ] **Step 3: Carry exact context for STANDARD and TBQ routes**

Construct the immutable context for explicitly observer-enabled stateful-SDPA
runs. Mark it formally acceptable only when all of these are true:

- requested device is the literal `CPU`;
- explicit attention route is stateful SDPA;
- fallback is forbidden;
- request ID matches `[A-Za-z0-9._-]{1,128}`;
- source and transformed identities are lowercase 64-hex.

Create a binding manifest even for `STANDARD/STANDARD`. Do not emit its
historical `not_requested` event in the constructor. The precompiled
`InferRequest` constructor, default or explicit PagedAttention, GPU, NPU, and
draft-device paths remain observation-ineligible. AUTO/multi-device diagnostic
contexts retain their exact requested/outer device data but cannot pass the
formal exact-CPU acceptance gate.

Retain the constructor's exact device string without normalization. Generate
one opaque 32-lowercase-hex correlation ID for the final compile model. Compute
the canonical transformed-model SHA-256 before adding this run-specific value,
then attach this private `rt_info` only to a compile-only clone:

```text
openvino_genai.cpu_state_allocation_observer.schema = 1
openvino_genai.cpu_state_allocation_observer.correlation_id =
    0123456789abcdef0123456789abcdef
```

Compile that exact clone. Immediately read the outer
`compiled_model.get_property(ov::execution_devices)` as a vector and retain it
without flattening. The bounded `state_allocation_observer_context` record
contains PID, correlation ID, exact constructor `requested_device`, outer
`actual_execution_devices`, source/transformed hashes, and build identity.
The CPU child may copy only schema/correlation from model metadata and must
never serialize the requested device from it as CPU-side truth. Add C++ tests
that the marker survives compilation while neither canonical model hash
changes.

Compiled-model caching is forbidden for every observer-enabled route because a
cache hit could bypass the run-specific marker. Before compilation, reject a
nonempty `ov::cache_dir`/`CACHE_DIR` supplied through any property layer, then
set `ov::cache_dir("")` on the exact private `ov::Core` used for compilation.
The GenAI context and build manifest require exact fields
`cache_dir=""` and `loaded_from_cache=false`. Tests prove nonempty cache
rejection occurs before compile, the effective cache directory is empty before
and after compile, no cache artifact is created, and marker survival is
demonstrated only by a direct non-cached compile. The final validator rejects
missing/nonempty `CACHE_DIR`, `loaded_from_cache` other than Boolean false, or
any cache artifact.

- [ ] **Step 4: Move finalization after complete public results**

Remove the current activation block from
`StatefulLLMPipeline::generate(EncodedInputs)`. Add the default no-op private
hook to `LLMPipelineImplBase` and override it in `StatefulLLMPipeline`.
Add overloads of `has_generated_output`: encoded output is valid only when
`tokens` is nonempty and at least one token sequence is nonempty; decoded
output is valid only when `texts` is nonempty and at least one text is
nonempty. Restructure the decoded wrapper as:

```cpp
auto result = run_generate_with_parsers(
    generation_config,
    streamer,
    [&]() -> DecodedResults {
        return m_pimpl->generate(inputs, generation_config, streamer);
    });
m_pimpl->finalize_kv_observation(has_generated_output(result));
return result;
```

Apply that same result/finalize/return sequence to all four decoded overloads,
retaining each overload's current complete `run_generate_with_parsers` call.
For each of the two encoded overloads, bind the current
private-generate return to `result`, finalize it, then return it.
Thus decoded finalization occurs only after parser work, detokenization, final
decoded output, and final decoded performance metrics exist; encoded
finalization occurs only after the returned result and its metrics exist. One
accepted process performs one fresh non-chat generation; it does not call
`finish_chat()`, `reset_state()`, or a second generation.

- [ ] **Step 5: Remove public state materialization and emit separate events**

Replace the current loop that calls
`actual_states.emplace(state.get_name(), state.get_state())`. After valid
generation, derive `RuntimeStateExtent` from the live `attention_mask` tensor:
require rank two, positive batch/sequence, and exact agreement with
`beam_idx`/generated output batch. Call `query_state()` once, validate its name
set against the exact manifest, and retain only names. Never call
`VariableState::get_state()`.

The logical observation reports, for every exact binding, its graph element
type, concrete current shape obtained by replacing only manifest batch and
sequence dimensions with `RuntimeStateExtent`, and checked logical current
bytes. Its event header is exactly:

```json
{"event":"kv_logical_state_observation","schema_version":1,
 "request_id":"request-0001","pid":4242,
 "correlation_id":"0123456789abcdef0123456789abcdef",
 "requested_device":"CPU","actual_execution_devices":["CPU"],
 "attention_path":"stateful_sdpa",
 "calculation_basis":"manifest_shape_and_live_request_extent"}
```

Activation schema v2 retains algorithm/operation/no-fallback proof and renames
all byte fields to:

```text
logical_current_payload_bytes
logical_current_norm_bytes
logical_current_metadata_bytes
logical_current_tbq_bytes
```

Do not call them physical or reserved. Retain v1 readers only for historical
raw evidence; new WB-04 acceptance requires v2. A STANDARD/STANDARD activation
has status `not_requested`, all four logical TBQ byte fields zero, and is
emitted only after valid output.

Use the existing GenAI telemetry channel: one UTF-8 JSON object per line on
`std::clog` (child stderr), never stdout or an additional C++ event file.
`write_observation_event_jsonl` rejects empty, invalid, duplicate-key, or
larger-than-1-MiB objects, appends exactly one newline, flushes `std::clog`,
and throws on stream failure. Context, activation, and logical events all use
this writer. The controller captures child stderr directly, extracts only
those three exact event names with the duplicate-key-rejecting JSON parser,
requires one of each, atomically persists their original lines in order to
`genai-events.jsonl`, and records its byte length and SHA-256. Non-event
diagnostics remain in raw stderr and cannot become evidence.

- [ ] **Step 6: Implement the audited two-phase single-process gate**

The private barrier is default-off and has no public ABI. Invalid partial
configuration fails before generation. Accepted measurement supplies:

```text
OV_GENAI_KV_OBSERVATION_READY_PATH=C:\ov-attempts\request-0001\performance-complete.json
OV_GENAI_KV_OBSERVATION_RELEASE_PATH=C:\ov-attempts\request-0001\observer-release.json
OV_GENAI_KV_OBSERVATION_REQUEST_ID=request-0001
OV_GENAI_KV_OBSERVATION_NONCE=0123456789abcdef0123456789abcdef
OV_CPU_STATE_ALLOCATION_DUMP_PATH=C:\ov-attempts\request-0001\kv-allocation.jsonl
```

The attempt directory is freshly created by the controller and empty before
launch. CPU phase, PID, observer request ID, correlation ID, and plugin device
come from private runtime state, never environment variables.

The finalizer transitions `NOT_STARTED -> WAITING_FOR_RELEASE -> COMPLETE`; any
error transitions terminally to `FAILED`, and every later finalization attempt
fails without querying state. It atomically writes a bounded ready record
containing request ID, nonce, PID, correlation ID, and
`performance_window_end_monotonic_ns`, then waits at most 30 seconds.

The parent controller extends `official_openvino/runner.py`, imports the
pre-Task-2 owned-process guard, and reuses
`collect_process_utilization.ps1` without changing its production gate. The
two-phase controller:

1. stops appending performance memory/CPU/GPU rows;
2. creates `utilization.stop`;
3. waits for the sampler process to exit code zero;
4. queries the sampler Job Object and requires zero active PIDs;
5. proves the allocation JSONL is absent or empty;
6. atomically writes the exact request ID and nonce to the release file.

Only then may the still-live child call `query_state()` once. A missing,
mismatched, duplicate, stale, malformed, or late release throws before query
and creates no CPU snapshot. Creating the sampler stop file alone is never
treated as sampler completion.

Emit the activation and logical records as complete bounded JSON lines only
after the synchronous CPU snapshot has returned. The controller requires the
raw CPU file to contain exactly one complete line before reconciliation.

- [ ] **Step 7: Implement strict physical reconciliation**

First select exactly one GenAI context record and one CPU snapshot with the
same `(pid, correlation_id)`. Reject missing, duplicate, foreign, or stale
records. Map exact observer state names through exact manifest bindings.
Recompute:

```python
key_reserved = sum_unique(KEY blocks and auxiliaries)
value_reserved = sum_unique(VALUE blocks and auxiliaries)
total_reserved = key_reserved + value_reserved
tbq_logical = payload_active + norm_active + metadata_active
```

Require `tbq_logical == activation["logical_current_tbq_bytes"]`; never require
`total_reserved == tbq_logical`. Require:

```python
assert snapshot["pid"] == observer_context["pid"]
assert snapshot["correlation_id"] == observer_context["correlation_id"]
assert snapshot["observer_plugin_device"] == "CPU"
assert "requested_device" not in snapshot
assert "actual_execution_devices" not in snapshot
assert observer_context["requested_device"] == "CPU"
assert observer_context["actual_execution_devices"] == ["CPU"]
assert activation["request_id"] == logical_observation["request_id"]
assert activation["actual_execution_devices"] == ["CPU"]
assert logical_observation["actual_execution_devices"] == ["CPU"]
assert snapshot["phase"] == "post_infer"
assert snapshot["trigger"] == "query_state"
```

Map state names only through `binding_manifest`; reject suffix reconstruction.
Require exact expected concrete class per component, observed internal element
type, owned backing, non-null reserved capacity, valid alias graph, and exact
model/build/plugin hashes from `expected_runtime`. U8 TBQ payload containers
remain element type U8 while algorithm identity remains TBQ3 or TBQ4.
Only the resulting `kv_physical_allocation` event carries the workbook
requested/actual device fields; they are copied from the validated GenAI
context, never synthesized from the CPU child.

Serialize that reconciled event with the same duplicate-key and 1-MiB bound,
atomically write exactly one newline-terminated record to
`kv-physical-allocation.jsonl`, then record its byte length and SHA-256. The
accepted manifest includes hashes for raw stderr, `genai-events.jsonl`, raw CPU
JSONL, and reconciled physical JSONL; validation rereads and rehashes all four.

- [ ] **Step 8: Prove controller ordering and crash gates**

The real-process fixture supports named modes:
`success`, `early_snapshot`, `duplicate_snapshot`, `wrong_nonce`,
`malformed_ready`, `no_ready`, `sampler_failure`, `child_crash_after_release`,
`ram_floor`, and `timeout`. Parent tests require zero survivors and preserved
raw failure files in every invalid mode, with no accepted summary.

Record controller `perf_counter_ns()` transition timestamps:

```text
performance_ready_observed
performance_sampling_closed
sampler_stop_signaled
sampler_exit_observed
pre_release_snapshot_absence_checked
observer_release_written
observer_snapshot_observed
workload_exit_observed
```

Require strict monotonic order in exactly that sequence. Add an after-release
CPU burn and memory allocation in the fake child and prove neither affects
CPU/GPU means nor peak process memory. Continue timeout and RAM-floor safety
polling after performance sampling closes, but never append those safety-only
observations to performance series.

- [ ] **Step 9: Run GREEN twice and production builds**

Reconfigure a separate observer-linked GenAI development build. Locate exactly
one `OpenVINOConfig.cmake` in the core build tree and use its parent:

```powershell
$cmake = "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"
$configs = @(Get-ChildItem C:\ov-build\state-observer -Recurse -Filter OpenVINOConfig.cmake)
if ($configs.Count -ne 1) { throw "Expected exactly one observer OpenVINOConfig.cmake" }
$ovDir = $configs[0].Directory.FullName
$genaiSource = "R:\external\official-openvino\2026-07-19\openvino.genai-turboquant"
$genaiBuild = "C:\ov-build\genai-observer-dev"
$genaiConfigure = @(
  $cmake, "-S", $genaiSource, "-B", $genaiBuild,
  "-G", "Visual Studio 18 2026", "-A", "x64",
  "-DOpenVINO_DIR=$ovDir", "-DENABLE_TESTS=ON",
  "-DENABLE_SAMPLES=OFF", "-DENABLE_TOOLS=OFF",
  "-DENABLE_PYTHON=OFF", "-DENABLE_JS=OFF"
)
& .\scripts\testing\invoke_guarded_command.ps1 `
  -Label task6-configure -WorkingDirectory R:\ `
  -EvidenceRoot R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards `
  -ExpectedExit Zero -Command $genaiConfigure
$genaiCache = Get-Content -LiteralPath "$genaiBuild\CMakeCache.txt"
foreach ($required in @(
  "ENABLE_TESTS:BOOL=ON",
  "ENABLE_SAMPLES:BOOL=OFF",
  "ENABLE_TOOLS:BOOL=OFF",
  "ENABLE_PYTHON:BOOL=OFF",
  "ENABLE_JS:BOOL=OFF"
)) {
  if ($genaiCache -notcontains $required) {
    throw "Observer-linked GenAI cache is missing '$required'"
  }
}
& .\scripts\testing\invoke_guarded_command.ps1 `
  -Label task6-core-plugin -WorkingDirectory R:\ `
  -EvidenceRoot R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards `
  -ExpectedExit Zero -Command @(
    $cmake, "--build", "C:\ov-build\state-observer", "--config", "Release",
    "--target", "openvino_intel_cpu_plugin", "--parallel", "2"
  )
& .\scripts\testing\invoke_guarded_command.ps1 `
  -Label task6-genai-build -WorkingDirectory R:\ `
  -EvidenceRoot R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards `
  -ExpectedExit Zero -Command @(
    $cmake, "--build", $genaiBuild, "--config", "Release", "--target",
    "turboquant_kv_observation_tests", "turboquant_pipeline_activation_tests",
    "turboquant_config_tests", "openvino_genai_obj", "--parallel", "1"
  )

foreach ($run in 1..2) {
  $observationExe = @(Get-ChildItem $genaiBuild -Recurse `
    -Filter turboquant_kv_observation_tests.exe)
  $activationExe = @(Get-ChildItem $genaiBuild -Recurse `
    -Filter turboquant_pipeline_activation_tests.exe)
  if ($observationExe.Count -ne 1 -or $activationExe.Count -ne 1) {
    throw "Focused GenAI executable discovery was ambiguous"
  }
  & .\scripts\testing\invoke_guarded_command.ps1 `
    -Label "task6-observation-$run" -WorkingDirectory R:\ `
    -EvidenceRoot R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards `
    -ExpectedExit Zero -Command @(
      $observationExe[0].FullName,
      "--gtest_filter=TurboQuantKVObservation.*"
    )
  & .\scripts\testing\invoke_guarded_command.ps1 `
    -Label "task6-activation-$run" -WorkingDirectory R:\ `
    -EvidenceRoot R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards `
    -ExpectedExit Zero -Command @(
      $activationExe[0].FullName,
      "--gtest_filter=TurboQuantPipelineActivation.*"
    )
  & .\scripts\testing\invoke_guarded_command.ps1 `
    -Label "task6-python-$run" -WorkingDirectory R:\ `
    -EvidenceRoot R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards `
    -ExpectedExit Zero -Command @(
      "python", "-m", "pytest",
      "scripts/testing/tests/test_official_openvino_kv_physical.py",
      "scripts/testing/tests/test_official_openvino_kv_observation_barrier.py",
      "R:\external\official-openvino\2026-07-19\openvino.genai-turboquant\tests\python\test_turboquant_activation_schema.py",
      "-q"
    )
}
```

Expected: all 11 real CPU K/V configurations pass in each C++ run; all
Python tests pass twice with zero failures/skips. Query the owned Job Object
after every executable and require zero active PIDs. Record available physical
RAM before/after and require it never crossed 2,048 MiB.

- [ ] **Step 10: Commit both checkouts**

Derived GenAI:

```powershell
git add src/cpp/src/llm/pipeline.cpp `
        src/cpp/src/llm/pipeline_base.hpp `
        src/cpp/src/llm/pipeline_stateful.hpp `
        src/cpp/src/llm/pipeline_stateful.cpp `
        src/cpp/src/llm/state_allocation_observer_context.hpp `
        src/cpp/src/llm/state_allocation_observer_context.cpp `
        src/cpp/src/llm/kv_observation.hpp `
        src/cpp/src/llm/kv_observation.cpp `
        src/cpp/src/continuous_batching/cache/turboquant_config.cpp `
        src/cpp/include/openvino/genai/turboquant_config.hpp `
        tests/cpp/turboquant_pipeline_activation.cpp `
        tests/cpp/turboquant_kv_observation.cpp `
        tests/cpp/CMakeLists.txt `
        tests/python/test_turboquant_activation_schema.py
git commit -m "feat(openvino): observe physical CPU KV allocation"
```

Parent:

```powershell
git add scripts/testing/official_openvino/kv_physical.py `
        scripts/testing/official_openvino/kv_observation_barrier.py `
        scripts/testing/official_openvino/runner.py `
        scripts/testing/tests/test_official_openvino_runner.py `
        scripts/testing/tests/test_official_openvino_kv_physical.py `
        scripts/testing/tests/test_official_openvino_kv_observation_barrier.py `
        scripts/testing/tests/fixtures/fake_openvino_two_phase_child.py
git commit -m "test(openvino): reconcile physical KV allocation"
```

### Task 7: Export, Replay, Build, and Freeze Both Patch Families

**Files in parent repository:**
- Create: `experiments/patches/openvino-cpu-state-observer/0001-cpu-state-allocation-observer.patch`
- Create: `experiments/patches/openvino-turboquant/0003-fused-stateful-runtime.patch`
- Create: `experiments/patches/openvino-turboquant/0004-physical-kv-observation.patch`
- Modify: `experiments/patches/openvino-cpu-state-observer/README.md`
- Modify: `experiments/patches/openvino-turboquant/README.md`
- Create: `scripts/testing/build_openvino_cpu_observer.ps1`
- Create: `scripts/testing/build_openvino_turboquant.ps1`
- Create: `scripts/testing/official_openvino/stateful_sdpa_probe/CMakeLists.txt`
- Create: `scripts/testing/official_openvino/stateful_sdpa_probe/main.cpp`
- Modify: `scripts/testing/official_openvino/owned_process_guard.py`
- Modify: `scripts/testing/official_openvino/guarded_build.py`
- Modify: `scripts/testing/verify_openvino_turboquant_build.py`
- Modify: `scripts/testing/run_openvino_reference_capability.py`
- Modify: `scripts/testing/tests/test_official_openvino_guarded_build.py`
- Modify: `scripts/testing/tests/test_official_openvino_patched_build.py`
- Modify: `scripts/testing/tests/test_official_openvino_source_audit.py`
- Modify: `tests/test_openvino_reference_capability.py`

**Interfaces:**

```powershell
.\scripts\testing\build_openvino_cpu_observer.ps1 `
  -SourcePath R:\external\official-openvino\2026-07-19\openvino-cpu-state-observer-replay `
  -BuildPath C:\ov-build\observer-replay-on `
  -ObserverMode Enabled `
  -Parallelism 2 `
  -EvidenceRoot R:\experiments\raw-results\openvino-turboquant\2026-07-28\build\core

$ovConfigs = @(Get-ChildItem C:\ov-build\observer-replay-on -Recurse `
  -Filter OpenVINOConfig.cmake)
if ($ovConfigs.Count -ne 1) {
  throw "Expected one observer OpenVINOConfig.cmake"
}
.\scripts\testing\build_openvino_turboquant.ps1 `
  -SourcePath R:\external\official-openvino\2026-07-19\openvino.genai-turboquant-replay `
  -OpenVINODir $ovConfigs[0].Directory.FullName `
  -BuildPath C:\ov-build\genai-observer-replay `
  -Parallelism 1 `
  -EvidenceRoot R:\experiments\raw-results\openvino-turboquant\2026-07-28\build\genai
```

- [ ] **Step 1: Export exact non-overlapping patches**

Let `$coreDevelopmentHead` and `$genaiDevelopmentHead` be the independently
approved derived checkout HEADs from Tasks 4 and 6. Require both working trees
clean. Export with Git's binary-safe output option:

```powershell
$parent = "R:\"
$commonDirRaw = (& git rev-parse --git-common-dir).Trim()
if (-not $commonDirRaw) { throw "git-common-dir is empty" }
$commonDir = if ([IO.Path]::IsPathRooted($commonDirRaw)) {
  [IO.Path]::GetFullPath($commonDirRaw)
} else {
  (Resolve-Path -LiteralPath $commonDirRaw).Path
}
$sharedRepoRoot = Split-Path -Parent $commonDir
$sharedTopLevel = (& git -C $sharedRepoRoot rev-parse --show-toplevel).Trim()
if (-not [IO.Path]::GetFullPath($sharedTopLevel).Equals(
    [IO.Path]::GetFullPath($sharedRepoRoot),
    [StringComparison]::OrdinalIgnoreCase)) {
  throw "git-common-dir did not resolve the shared repository root"
}
$cleanCore = Join-Path $sharedRepoRoot `
  "external\official-openvino\2026-07-19\openvino"
$cleanGenAI = Join-Path $sharedRepoRoot `
  "external\official-openvino\2026-07-19\openvino.genai"
foreach ($cleanSource in @($cleanCore, $cleanGenAI)) {
  if (-not (Test-Path -LiteralPath (Join-Path $cleanSource ".git"))) {
    throw "Shared clean checkout is missing: $cleanSource"
  }
}
$core = "R:\external\official-openvino\2026-07-19\openvino-cpu-state-observer"
$genai = "R:\external\official-openvino\2026-07-19\openvino.genai-turboquant"
$coreBase = "ede283a88e35465f0d680dabbf1f44080f8fc387"
$genaiBase = "7dea0459b2ac7d8dfd877fd9df6737674fd8371d"
$genaiReplay = "122f50ebf9bcfff8835633b42ef1df0499ae834a"
$activationHead = "981153f6a01856945fe669db1c287d3abf935ab0"
$coreDevelopmentHead = (& git -C $core rev-parse HEAD).Trim()
$genaiDevelopmentHead = (& git -C $genai rev-parse HEAD).Trim()
if ((& git -C $core status --porcelain --untracked-files=all) -or
    (& git -C $genai status --porcelain --untracked-files=all)) {
  throw "Development checkout is dirty"
}
& git -C $core merge-base --is-ancestor $coreBase $coreDevelopmentHead
if ($LASTEXITCODE -ne 0) { throw "Core development HEAD is not based on the pin" }
& git -C $genai merge-base --is-ancestor $activationHead $genaiDevelopmentHead
if ($LASTEXITCODE -ne 0) { throw "GenAI observation HEAD is not based on activation HEAD" }

& git -C $core diff --binary $coreBase $coreDevelopmentHead `
  --output="$parent\experiments\patches\openvino-cpu-state-observer\0001-cpu-state-allocation-observer.patch"
& git -C $genai diff --binary $genaiReplay $activationHead `
  --output="$parent\experiments\patches\openvino-turboquant\0003-fused-stateful-runtime.patch"
& git -C $genai diff --binary $activationHead $genaiDevelopmentHead `
  --output="$parent\experiments\patches\openvino-turboquant\0004-physical-kv-observation.patch"
foreach ($patch in @(
  "$parent\experiments\patches\openvino-cpu-state-observer\0001-cpu-state-allocation-observer.patch",
  "$parent\experiments\patches\openvino-turboquant\0003-fused-stateful-runtime.patch",
  "$parent\experiments\patches\openvino-turboquant\0004-physical-kv-observation.patch"
)) {
  if (-not (Test-Path $patch) -or (Get-Item $patch).Length -eq 0) {
    throw "Patch export is empty: $patch"
  }
}
```

Update both READMEs with exact base/range semantics, lexical application order,
and the project-added observer/TurboQuant provenance boundary.

- [ ] **Step 2: Verify patches in throwaway indexes and commit them**

Create fresh no-hardlink local clones in the task scratch directory. Apply each
series with `git apply --cached --check`, then `git apply --cached`, and compare
the resulting `git write-tree` with the corresponding development tree:

```powershell
$scratch = "R:\.superpowers\sdd\patch-replay-check"
if (Test-Path $scratch) {
  $resolved = (Resolve-Path $scratch).Path
  if (-not $resolved.StartsWith("R:\.superpowers\sdd\", [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unsafe scratch cleanup path"
  }
  Remove-Item -LiteralPath $resolved -Recurse -Force
}
New-Item -ItemType Directory -Path $scratch | Out-Null
& git clone --local --no-hardlinks --no-checkout `
  $cleanCore `
  "$scratch\core"
& git -C "$scratch\core" checkout --detach $coreBase
& git -C "$scratch\core" apply --cached --check `
  R:\experiments\patches\openvino-cpu-state-observer\0001-cpu-state-allocation-observer.patch
& git -C "$scratch\core" apply --cached `
  R:\experiments\patches\openvino-cpu-state-observer\0001-cpu-state-allocation-observer.patch
if ((& git -C "$scratch\core" write-tree).Trim() -ne
    (& git -C $core rev-parse "$coreDevelopmentHead^{tree}").Trim()) {
  throw "Core patch tree differs from development tree"
}

& git clone --local --no-hardlinks --no-checkout `
  $cleanGenAI `
  "$scratch\genai"
& git -C "$scratch\genai" checkout --detach $genaiBase
foreach ($patch in @(
  "R:\experiments\patches\openvino-turboquant\0001-tbq-codec.patch",
  "R:\experiments\patches\openvino-turboquant\0002-kv-config-telemetry.patch",
  "R:\experiments\patches\openvino-turboquant\0003-fused-stateful-runtime.patch",
  "R:\experiments\patches\openvino-turboquant\0004-physical-kv-observation.patch"
)) {
  & git -C "$scratch\genai" apply --cached --check $patch
  if ($LASTEXITCODE -ne 0) { throw "GenAI patch check failed: $patch" }
  & git -C "$scratch\genai" apply --cached $patch
  if ($LASTEXITCODE -ne 0) { throw "GenAI patch apply failed: $patch" }
}
if ((& git -C "$scratch\genai" write-tree).Trim() -ne
    (& git -C $genai rev-parse "$genaiDevelopmentHead^{tree}").Trim()) {
  throw "GenAI patch tree differs from development tree"
}
```

Commit the patch files and READMEs now, before calling either controller:

```powershell
git add experiments/patches/openvino-cpu-state-observer/README.md `
        experiments/patches/openvino-cpu-state-observer/0001-cpu-state-allocation-observer.patch `
        experiments/patches/openvino-turboquant/README.md `
        experiments/patches/openvino-turboquant/0003-fused-stateful-runtime.patch `
        experiments/patches/openvino-turboquant/0004-physical-kv-observation.patch
git commit -m "build(openvino): export physical KV patch families"
```

- [ ] **Step 3: Replay from the clean parent HEAD**

The patch controller refuses dirty or non-HEAD patches. Confirm the parent is
clean, then run both controllers. Use a new GenAI replay destination so the
accepted development checkout is not mistaken for replay evidence:

```powershell
if (git status --porcelain --untracked-files=all) {
  throw "Parent must be clean before controlled replay"
}
& .\scripts\testing\prepare_openvino_cpu_observer_patch.ps1 `
  -CampaignDate 2026-07-19 `
  -ExpectedCommit ede283a88e35465f0d680dabbf1f44080f8fc387 `
  -DestinationPath R:\external\official-openvino\2026-07-19\openvino-cpu-state-observer-replay
& .\scripts\testing\prepare_openvino_turboquant_patch.ps1 `
  -CampaignDate 2026-07-19 `
  -ExpectedCommit 7dea0459b2ac7d8dfd877fd9df6737674fd8371d `
  -DestinationPath R:\external\official-openvino\2026-07-19\openvino.genai-turboquant-replay
foreach ($checkout in @(
  "R:\external\official-openvino\2026-07-19\openvino-cpu-state-observer-replay",
  "R:\external\official-openvino\2026-07-19\openvino.genai-turboquant-replay"
)) {
  if (& git -C $checkout status --porcelain --untracked-files=all) {
    throw "Replayed checkout is dirty: $checkout"
  }
}
```

- [ ] **Step 4: Write guard/build/validator RED tests**

Reuse the exact `KillOnCloseJob`, cleanup, and RAM/process-memory primitives
extracted in the pre-Task-2 bootstrap. `guarded_build.py`, the capability runner, and the
two-phase runner must all import that module; tests scan for and reject a
second `CreateJobObjectW` call, cleanup loop, or RAM-floor implementation.
Preserve the original capability caller and run
`tests/test_openvino_reference_capability.py`.

`guarded_build.py` retains the exact pre-Task-2 interface and active-PID/RAM
behavior. Task 7 may add manifest fields but must not add a second execution
path or weaken the fixed guard limits.

Write RED tests for normal exit, a simulated floor breach that kills the whole
owned tree, timeout, nonzero child exit, atomic log/manifest replacement, and
queried zero survivors. Extend the build-manifest tests to reject:

- stale patch lists or identity files;
- dirty source;
- wrong upstream/derived commits or trees;
- either `ENABLE_DEBUG_CAPS` or `ENABLE_CPU_DEBUG_CAPS` not enabled for observer
  builds;
- either debug option enabled for an ordinary build;
- an observer variable active during default-off equivalence;
- GenAI configured against the official wheel rather than the observer build;
- observer-enabled GenAI with missing/nonempty effective `cache_dir` or any
  compiled-model cache artifact;
- missing DLL/LIB/executable hashes;
- non-current embedded GenAI commit;
- failed/skipped focused tests;
- missing no-env/wrong-env/live-HEAD provenance gates;
- missing behavior-equivalence evidence;
- missing cleanup query or survivors;
- build parallelism above `2` for core or above `1` for GenAI.

Replace the validator's single-source identity arguments with the four exact
options used in Step 9:
`--expected-core-upstream-commit`, `--expected-core-patch-commit`,
`--expected-genai-upstream-commit`, and
`--expected-genai-patch-commit`. Each requires a full 40-hex commit and checks
the corresponding source tree, patch list, and binary provenance.

- [ ] **Step 5: Run RED, implement tooling, run GREEN twice, and commit**

```powershell
& .\scripts\testing\invoke_guarded_command.ps1 `
  -Label task7-tooling-red -WorkingDirectory R:\ `
  -EvidenceRoot R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards `
  -ExpectedExit NonZero -Command @(
    "python", "-m", "pytest",
    "scripts/testing/tests/test_official_openvino_guarded_build.py",
    "scripts/testing/tests/test_official_openvino_patched_build.py",
    "scripts/testing/tests/test_official_openvino_source_audit.py",
    "tests/test_openvino_reference_capability.py", "-q"
  )
```

Expected RED: new build-manifest assertions fail for missing fields. Implement the
minimal guarded-build wrapper, validator fields, atomic JSON writes, and
PowerShell entry points. Repeat the exact guarded command twice with
`ExpectedExit Zero` and unique labels `task7-tooling-green-1` and
`task7-tooling-green-2`; require the current
79-test reference-capability suite plus all authored guard/build tests to pass
with zero skips. Then commit exact files:

```powershell
git add scripts/testing/build_openvino_cpu_observer.ps1 `
        scripts/testing/build_openvino_turboquant.ps1 `
        scripts/testing/official_openvino/stateful_sdpa_probe/CMakeLists.txt `
        scripts/testing/official_openvino/stateful_sdpa_probe/main.cpp `
        scripts/testing/official_openvino/owned_process_guard.py `
        scripts/testing/official_openvino/guarded_build.py `
        scripts/testing/verify_openvino_turboquant_build.py `
        scripts/testing/run_openvino_reference_capability.py `
        scripts/testing/tests/test_official_openvino_guarded_build.py `
        scripts/testing/tests/test_official_openvino_patched_build.py `
        scripts/testing/tests/test_official_openvino_source_audit.py `
        tests/test_openvino_reference_capability.py
git commit -m "build(openvino): guard observer replay builds"
```

- [ ] **Step 6: Build clean, ordinary, observer, and GenAI routes serially**

Use fresh build directories or fail if an existing `CMakeCache.txt` names a
different source/options. Run only one guarded command at a time:

1. clean upstream core, both debug options OFF, production runtime/plugin;
2. replayed observer core, both debug options OFF, production runtime/plugin
   plus `ENABLE_TESTS=ON`, `ENABLE_FUNCTIONAL_TESTS=ON`, and
   `ov_cpu_func_tests` for the named stock state-query gate;
3. replayed observer core, both debug options ON, unit/probe/runtime/plugin;
4. replayed GenAI against route 3, tests plus object/library.

Run these exact wrappers and stop on every nonzero exit:

```powershell
$evidence = "R:\experiments\raw-results\openvino-turboquant\2026-07-28\build"
$commonDirRaw = (& git rev-parse --git-common-dir).Trim()
$commonDir = if ([IO.Path]::IsPathRooted($commonDirRaw)) {
  [IO.Path]::GetFullPath($commonDirRaw)
} else {
  (Resolve-Path -LiteralPath $commonDirRaw).Path
}
$sharedRepoRoot = Split-Path -Parent $commonDir
$cleanCore = Join-Path $sharedRepoRoot `
  "external\official-openvino\2026-07-19\openvino"
& .\scripts\testing\build_openvino_cpu_observer.ps1 `
  -SourcePath $cleanCore `
  -BuildPath C:\ov-build\clean-core-off `
  -ObserverMode Disabled -Parallelism 2 `
  -EvidenceRoot "$evidence\clean-core"
if ($LASTEXITCODE -ne 0) { throw "Clean core build failed" }
& .\scripts\testing\build_openvino_cpu_observer.ps1 `
  -SourcePath R:\external\official-openvino\2026-07-19\openvino-cpu-state-observer-replay `
  -BuildPath C:\ov-build\observer-replay-off `
  -ObserverMode Disabled -Parallelism 2 `
  -EvidenceRoot "$evidence\ordinary-core"
if ($LASTEXITCODE -ne 0) { throw "Ordinary replay build failed" }
& .\scripts\testing\build_openvino_cpu_observer.ps1 `
  -SourcePath R:\external\official-openvino\2026-07-19\openvino-cpu-state-observer-replay `
  -BuildPath C:\ov-build\observer-replay-on `
  -ObserverMode Enabled -Parallelism 2 `
  -EvidenceRoot "$evidence\observer-core"
if ($LASTEXITCODE -ne 0) { throw "Observer replay build failed" }
$ovConfigs = @(Get-ChildItem C:\ov-build\observer-replay-on -Recurse `
  -Filter OpenVINOConfig.cmake)
if ($ovConfigs.Count -ne 1) {
  throw "Expected one observer OpenVINOConfig.cmake"
}
& .\scripts\testing\build_openvino_turboquant.ps1 `
  -SourcePath R:\external\official-openvino\2026-07-19\openvino.genai-turboquant-replay `
  -OpenVINODir $ovConfigs[0].Directory.FullName `
  -BuildPath C:\ov-build\genai-observer-replay `
  -Parallelism 1 -EvidenceRoot "$evidence\genai"
if ($LASTEXITCODE -ne 0) { throw "Observer-linked GenAI build failed" }
```

The core wrapper always passes both debug options explicitly and checks both
cache values. It begins at `--parallel 2`; on an unsafe downward RAM trend it
stops the owned tree, records the interrupted attempt, and resumes with
parallelism `1`. It never retries a compiler process outside a new Job Object.
The GenAI wrapper always uses parallelism `1`. Every command records its exact
argv, cwd, exit code, minimum RAM, log SHA-256, Job Object cleanup query, and
survivor list.
It configures only actual GenAI options
`ENABLE_TESTS=ON`, `ENABLE_SAMPLES=OFF`, `ENABLE_TOOLS=OFF`,
`ENABLE_PYTHON=OFF`, and `ENABLE_JS=OFF`, then asserts those exact
`CMakeCache.txt` entries.

- [ ] **Step 7: Prove ordinary absence and three-route equivalence**

Add a source-audit test that tracks preprocessor nesting and requires every
observer declaration, definition, request-ID/phase mutation, model-context
parse, serializer/writer call, and `query_state()` hook to be lexically inside
`CPU_DEBUG_CAPS`. It also requires the ordinary compile command to omit
`CPU_DEBUG_CAPS`. Run that test through the guard.

For the ordinary replay DLL, require an explicit no-match exit (`1`); exit `0`
means a leak and exit `2` or greater means the scan failed:

```powershell
$ordinaryDll = "C:\ov-build\observer-replay-off\bin\intel64\Release\openvino_intel_cpu_plugin.dll"
& rg -a -n `
  "OV_CPU_STATE_ALLOCATION_DUMP_PATH|StateAllocationObserver|state_allocation_snapshot" `
  $ordinaryDll
$markerExit = $LASTEXITCODE
if ($markerExit -eq 0) {
  throw "Observer marker is present in ordinary CPU plugin"
}
if ($markerExit -ge 2) { throw "Ordinary DLL marker scan failed" }
```

Run `dumpbin /symbols` over every ordinary
`openvino_intel_cpu_plugin` object and its import library; require no symbol
matching
`StateAllocation|state_allocation|observer_request_id|observer_phase`.
Require `dumpbin` exit zero and persist/hash its output. This symbol gate and
the source/preprocessor audit are mandatory in addition to the binary marker
gate.

Build the public-API neutral probe source three separate times, each through
the guard and against its route's own unique `OpenVINOConfig.cmake`:
`probe-clean`, `probe-ordinary`, and `probe-observer`. Never copy one probe
binary between routes. Each run uses that route's probe executable, runtime
DLL directory, `plugins.xml`, and CPU plugin, and records all four binary
hashes. Run clean/ordinary with `--expect-observer absent`; run the observer
probe once with `absent` and once with `present`. Use identical STANDARD F16
stateful-SDPA input bytes and seed. Require identical output SHA-256, runtime
graph layers, execution devices `["CPU"]`, selected precision, and no fallback
across the three route-specific binaries. Require no JSONL in disabled routes
and exactly one valid JSONL only when enabled.

As the named stock state-query regression, build
`ov_cpu_func_tests` for the ordinary route and run:

```powershell
Remove-Item Env:OV_CPU_STATE_ALLOCATION_DUMP_PATH -ErrorAction SilentlyContinue
& .\scripts\testing\invoke_guarded_command.ps1 `
  -Label ordinary-stock-query-state `
  -WorkingDirectory R:\ `
  -EvidenceRoot R:\experiments\raw-results\openvino-turboquant\2026-07-28\gates `
  -ExpectedExit Zero `
  -Command @(
    "C:\ov-build\observer-replay-off\bin\intel64\Release\ov_cpu_func_tests.exe",
    "--gtest_filter=smoke_ReadValue_Assign/ReadValueAssignTest.CompareWithRefs/*"
  )
```

Launch with `OV_CPU_STATE_ALLOCATION_DUMP_PATH` unset; require a positive selected count,
zero failures/skips, no JSONL, 2,048 MiB floor evidence, and zero survivors.
Timing is recorded but not compared.

- [ ] **Step 8: Run the complete focused gates twice**

For each run:

1. list and run `StateAllocationsDump.*:StateAllocationsWriter.*` from
   `ov_cpu_unit_tests.exe`; selected count must be positive, stable across the
   two runs, with zero failures/skips;
2. run the F16/U8/U4 native probe modes; six total fresh-process repetitions
   across the two runs;
3. run the existing standalone GenAI filters with their pre-change baseline
   counts: codec 9, config/pipeline 14, graph 33, state-update 17, activation
   16, for a total of 89; then run the authored binding and observation suites
   with positive stable counts;
4. run the four-test GenAI Python activation schema plus all authored
   physical/barrier Python tests;
5. run the complete 178-test parent baseline plus the new physical,
   barrier, guarded-build, and extended patched-build tests.

Build `openvino_intel_cpu_plugin`, `openvino_genai_obj`, and
`openvino_genai` after the second test run. Do not use the stale
`tests_continuous_batching` executable as focused evidence.

The exact parent baseline command is:

```powershell
$parentTests = @(
  "scripts/testing/tests/test_official_openvino_acquisition.py",
  "scripts/testing/tests/test_official_openvino_conversion.py",
  "scripts/testing/tests/test_official_openvino_diagnostics.py",
  "scripts/testing/tests/test_official_openvino_docx.py",
  "scripts/testing/tests/test_official_openvino_matrix.py",
  "scripts/testing/tests/test_official_openvino_metrics.py",
  "scripts/testing/tests/test_official_openvino_patch_identity.py",
  "scripts/testing/tests/test_official_openvino_patched_build.py",
  "scripts/testing/tests/test_official_openvino_quality.py",
  "scripts/testing/tests/test_official_openvino_reconcile.py",
  "scripts/testing/tests/test_official_openvino_runner.py",
  "scripts/testing/tests/test_official_openvino_source_audit.py",
  "tests/test_openvino_reference_capability.py",
  "scripts/testing/tests/test_official_openvino_kv_physical.py",
  "scripts/testing/tests/test_official_openvino_kv_observation_barrier.py",
  "scripts/testing/tests/test_official_openvino_guarded_build.py"
)
foreach ($run in 1..2) {
  $parentCommand = @("python", "-m", "pytest") + $parentTests + @("-q")
  & .\scripts\testing\invoke_guarded_command.ps1 `
    -Label "task7-parent-gate-$run" -WorkingDirectory R:\ `
    -EvidenceRoot R:\experiments\raw-results\openvino-turboquant\2026-07-28\guards `
    -ExpectedExit Zero -Command $parentCommand
}
```

- [ ] **Step 9: Freeze and validate immutable evidence**

Write immutable manifests containing:

- upstream/derived commits and trees;
- ordered patch names, Git blob IDs, and SHA-256 values;
- configure/build commands and log hashes;
- toolchain/options/parallelism;
- CPU plugin, GenAI library, and probe executable sizes/SHA-256;
- exact test totals with zero failures/skips;
- behavior-equivalence output hashes;
- raw stderr, extracted GenAI-event JSONL, CPU snapshot JSONL, and reconciled
  physical-event JSONL sizes/SHA-256 values;
- final RAM/survivor audits.

Also record the ordinary DLL's negative marker scan, disabled/enabled
environment values, behavior-equivalence identities, request IDs, exact
selected/passed counts for every authored suite, and the existing 178/4/89
baselines. A count is never left unspecified: collect it after authoring and
freeze the observed integer.

Run the validator from clean parent `HEAD`:

```powershell
$coreReplayHead = (& git -C `
  R:\external\official-openvino\2026-07-19\openvino-cpu-state-observer-replay `
  rev-parse HEAD).Trim()
$genaiReplayHead = (& git -C `
  R:\external\official-openvino\2026-07-19\openvino.genai-turboquant-replay `
  rev-parse HEAD).Trim()
python -m scripts.testing.verify_openvino_turboquant_build `
  --manifest R:\experiments\raw-results\openvino-turboquant\2026-07-28\build\combined-manifest.json `
  --expected-core-upstream-commit ede283a88e35465f0d680dabbf1f44080f8fc387 `
  --expected-core-patch-commit $coreReplayHead `
  --expected-genai-upstream-commit 7dea0459b2ac7d8dfd877fd9df6737674fd8371d `
  --expected-genai-patch-commit $genaiReplayHead `
  --required-binary openvino_intel_cpu_plugin.dll `
  --required-binary openvino_genai.dll `
  --required-binary ov_cpu_state_alloc_probe.exe `
  --required-test-suite StateAllocationsDump `
  --required-test-suite StateAllocationsWriter `
  --required-test-suite TurboQuantKVStateBinding `
  --required-test-suite TurboQuantKVObservation `
  --required-test-suite TurboQuantPipelineActivation
git diff --check
```

After the validator and every zero-survivor Job Object check pass, clean up the
Task 2 short-path mapping. Re-read `subst` output, require exactly one `O:`
line, parse it with `^O:\\: => (?<target>.+)$`, and require the normalized
target to equal
`R:\external\official-openvino\2026-07-19\openvino-cpu-state-observer`.
Only then run `subst.exe O: /D` and require exit zero. A missing or different
mapping is reported and never removed.

Expected terminal state: clean official checkouts, clean replayed derived
checkouts, independently approved core and GenAI patches, exact physical
allocation evidence for all 11 synthetic K/V configurations, complete manifests,
both ordinary and observer build gates, the complete test inventories passing
twice, and zero surviving owned processes. Evidence stays in the campaign raw
results tree; do not create a final tracked commit merely to claim a build
passed.
