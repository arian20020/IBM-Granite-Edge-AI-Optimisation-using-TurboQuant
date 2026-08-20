import io
import json
import os
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path
from unittest import mock

import numpy as np

from granite_turbovec.cli import EXIT_CODES, LOCKED_VERSIONS, CliDependencies, main, run_cli
from granite_turbovec.contracts import ResearchError
from granite_turbovec.manifest import IndexIdentity


def approved(root: Path, *, model_hash: str = "1" * 64) -> Path:
    cache = root / "cache"
    cache.mkdir()
    (cache / "model.onnx").write_bytes(b"model")
    value = {
        "schema_version": 1,
        "approval_status": "approved",
        "python_version": "3.12.10",
        "turbovec_version": "1.0.0",
        "turbovec_source_commit": "ccab9f325e6ce2a270a87daf01ae4e443bcf2d49",
        "turbovec_wheel_sha256": "2" * 64,
        "embedding_model": "BAAI/bge-small-en-v1.5",
        "embedding_model_manifest_sha256": model_hash,
        "embedding_model_license": "MIT",
        "model_cache_root": str(cache),
    }
    path = root / "approved.json"
    path.write_text(json.dumps(value), encoding="utf-8")
    return path


class FakeEmbedder:
    dimension = 3
    model_identity = "BAAI/bge-small-en-v1.5"
    providers = ["CPUExecutionProvider"]

    def __init__(self):
        self.document_calls = 0
        self.query_calls = 0

    def embed_documents(self, texts):
        self.document_calls += 1
        return np.asarray([[len(x), 1, 1] for x in texts], dtype=np.float32)

    def embed_queries(self, texts):
        self.query_calls += 1
        return np.asarray([[len(x), 1, 1] for x in texts], dtype=np.float32)


class FakeTurbo:
    def __init__(self, dimension, *, bits):
        self.dimension, self.bits = dimension, bits
        self.vectors = self.ids = None

    def add_with_ids(self, vectors, ids):
        self.vectors, self.ids = np.array(vectors), np.array(ids)

    def search(self, queries, k):
        scores = np.asarray(queries) @ self.vectors.T
        order = np.argsort(-scores, axis=1)[:, :k]
        return np.take_along_axis(scores, order, axis=1), self.ids[order]

    def write(self, path):
        with Path(path).open("wb") as stream:
            np.savez(stream, dimension=self.dimension, bits=self.bits,
                     vectors=self.vectors, ids=self.ids)

    @classmethod
    def load(cls, path):
        with np.load(path, allow_pickle=False) as data:
            value = cls(int(data["dimension"]), bits=int(data["bits"]))
            value.vectors, value.ids = data["vectors"], data["ids"]
        return value


def dependencies(root: Path, embedder=None):
    embedder = embedder or FakeEmbedder()
    return CliDependencies(
        python_version=lambda: "3.12.10",
        package_versions=lambda: dict(LOCKED_VERSIONS),
        dependency_lock_sha256=lambda: "438b20b685916055f02e9a207d6bb1466ed30669cb0404bbfa1661e409bd7b35",
        wheel_sha256=lambda: "2" * 64,
        model_manifest_sha256=lambda path: "1" * 64,
        platform_info=lambda: {"platform": "windows", "processor": "test-cpu"},
        make_embedder=lambda cache: embedder,
        make_turbovec=lambda dimension, bits: FakeTurbo(dimension, bits=bits),
        load_turbovec=lambda path: FakeTurbo.load(path),
        now_utc=lambda: "2026-08-20T12:00:00Z",
        model_dimension=lambda: 3,
        perf_counter=__import__("time").perf_counter,
    )


def invoke(args, deps):
    stdout, stderr = io.StringIO(), io.StringIO()
    with redirect_stdout(stdout), redirect_stderr(stderr):
        code = main(args, dependencies=deps)
    return code, stdout.getvalue(), stderr.getvalue()


class CliTests(unittest.TestCase):
    def test_dependency_noise_is_contained_to_preserve_json_streams(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); approval = approved(root)
            deps = dependencies(root)
            original = deps.model_manifest_sha256
            def noisy(path):
                print("dependency-progress")
                return original(path)
            deps.model_manifest_sha256 = noisy
            code, stdout, stderr = invoke(["doctor", "--approved-input", str(approval)], deps)
        self.assertEqual(0, code)
        self.assertEqual("", stderr)
        json.loads(stdout)
        self.assertNotIn("dependency-progress", stdout)

    def test_doctor_emits_one_safe_json_document_with_version(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            approval = approved(root)
            code, stdout, stderr = invoke(["doctor", "--approved-input", str(approval)], dependencies(root))
        self.assertEqual(0, code)
        self.assertEqual("", stderr)
        payload = json.loads(stdout)
        self.assertEqual("1.0.0", payload["turbovec"]["version"])
        self.assertEqual("3.12.10", payload["python"]["version"])
        self.assertNotIn(str(Path.home()), stdout)
        self.assertEqual(1, stdout.count("\n"))

    def test_argument_errors_are_one_json_diagnostic_without_usage(self):
        code, stdout, stderr = invoke(["query"], dependencies(Path(".")))
        self.assertEqual(2, code)
        self.assertEqual("", stdout)
        self.assertEqual({"schema_version": 1, "code": "research-argument-error"}, json.loads(stderr))
        self.assertNotIn("usage", stderr.casefold())

    def test_placeholder_approval_and_unknown_codes_fail_closed(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            approval = approved(root)
            payload = json.loads(approval.read_text())
            payload["approval_status"] = "example_only_replace_hashes_before_use"
            payload["turbovec_wheel_sha256"] = "0" * 64
            approval.write_text(json.dumps(payload))
            code, _, stderr = invoke(["doctor", "--approved-input", str(approval)], dependencies(root))
        self.assertEqual(30, code)
        self.assertEqual("approval-mismatch", json.loads(stderr)["code"])
        self.assertNotEqual(0, EXIT_CODES.get("brand-new-fixed-code", 30))

    def test_existing_index_destination_returns_22_without_overwrite(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            approval = approved(root)
            source = root / "source.txt"; source.write_text("hello world")
            output = root / "index"; output.mkdir()
            marker = output / "keep"; marker.write_text("safe")
            code, _, stderr = invoke(["index", "--approved-input", str(approval), "--input", str(source), "--output", str(output)], dependencies(root))
            self.assertEqual("safe", marker.read_text())
        self.assertEqual(22, code)
        self.assertEqual("index-destination-exists", json.loads(stderr)["code"])

    def test_index_query_round_trip_has_coherent_artifacts_and_bounded_excerpt(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            approval = approved(root)
            source = root / "knowledge"; source.mkdir()
            (source / "a.txt").write_text("Granite retrieval " * 30)
            index = root / "index"
            deps = dependencies(root)
            build = run_cli(["index", "--approved-input", str(approval), "--input", str(source), "--output", str(index)], dependencies=deps)
            self.assertEqual(0, build.exit_code)
            names = {p.name for p in index.iterdir()}
            self.assertTrue({"manifest.json", "chunks.jsonl", "vectors-float32.npy", "ids.npy", "baseline-results.json", "index-2bit.tvim", "index-4bit.tvim"} <= names)
            answer = run_cli(["query", "--approved-input", str(approval), "--index", str(index), "--text", "Granite", "--top-k", "1"], dependencies=deps)
        self.assertEqual(0, answer.exit_code)
        self.assertEqual(1, len(answer.payload["results"]))
        self.assertLessEqual(len(answer.payload["results"][0]["excerpt"]), 240)
        self.assertEqual("4bit", answer.payload["route"])

    def test_manifest_identity_mismatch_precedes_embedding_or_index_load(self):
        class Spies:
            embeds = loads = 0
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); approval = approved(root)
            source = root / "a.txt"; source.write_text("granite")
            index = root / "index"
            deps = dependencies(root)
            self.assertEqual(0, run_cli(["index", "--approved-input", str(approval), "--input", str(source), "--output", str(index)], dependencies=deps).exit_code)
            payload = json.loads(approval.read_text()); payload["embedding_model_manifest_sha256"] = "4" * 64
            approval.write_text(json.dumps(payload))
            mismatch_deps = dependencies(root)
            mismatch_deps.model_manifest_sha256 = lambda path: "4" * 64
            mismatch_deps.make_embedder = lambda cache: (_ for _ in ()).throw(AssertionError("embedder loaded"))
            mismatch_deps.load_turbovec = lambda path: (_ for _ in ()).throw(AssertionError("index loaded"))
            result = run_cli(["query", "--approved-input", str(approval), "--index", str(index), "--text", "x"], dependencies=mismatch_deps)
        self.assertEqual(31, result.exit_code)
        self.assertEqual("index-embedding-mismatch", result.payload["code"])

    def test_expected_failures_have_minimal_private_safe_diagnostic_and_fixed_mapping(self):
        deps = dependencies(Path("."))
        deps.python_version = lambda: (_ for _ in ()).throw(ResearchError("embedding-failed"))
        code, stdout, stderr = invoke(["doctor", "--approved-input", "C:/private/customer/approval.json"], deps)
        self.assertEqual(20, code)  # unreadable approved input is an input error
        self.assertEqual("", stdout)
        self.assertEqual({"schema_version": 1, "code": "approved-input-invalid"}, json.loads(stderr))
        self.assertNotIn("customer", stderr)

    def test_keyboard_interrupt_is_single_safe_failure(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); approval = approved(root)
            deps = dependencies(root); deps.model_manifest_sha256 = lambda _: (_ for _ in ()).throw(KeyboardInterrupt())
            code, stdout, stderr = invoke(["doctor", "--approved-input", str(approval)], deps)
        self.assertEqual(130, code)
        self.assertEqual("", stdout)
        self.assertEqual("research-cancelled", json.loads(stderr)["code"])

    def test_unexpected_failure_hides_stack_and_exception_text_by_default(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); approval = approved(root); deps = dependencies(root)
            deps.platform_info = lambda: (_ for _ in ()).throw(RuntimeError("private-secret"))
            with mock.patch.dict(os.environ, {"GRANITE_TURBOVEC_DEBUG": "0"}):
                code, stdout, stderr = invoke(["doctor", "--approved-input", str(approval)], deps)
        self.assertEqual(70, code)
        self.assertEqual("", stdout)
        self.assertEqual({"schema_version": 1, "code": "research-unexpected-failure"}, json.loads(stderr))
        self.assertNotIn("private-secret", stderr)

    def test_environment_and_hash_mismatches_are_exit_30_without_paths(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); approval = approved(root)
            deps = dependencies(root); deps.wheel_sha256 = lambda: "9" * 64
            result = run_cli(["doctor", "--approved-input", str(approval)], dependencies=deps)
            self.assertEqual(30, result.exit_code)
            self.assertEqual("approval-mismatch", result.payload["code"])
            with mock.patch.dict(os.environ, {"HF_HUB_OFFLINE": "0"}):
                env_result = run_cli(["doctor", "--approved-input", str(approval)], dependencies=dependencies(root))
        self.assertEqual(30, env_result.exit_code)
        self.assertEqual("environment-mismatch", env_result.payload["code"])

    def test_any_pinned_dependency_version_mismatch_fails_approval(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); approval = approved(root); deps = dependencies(root)
            versions = dict(LOCKED_VERSIONS); versions["fastembed"] = "999.0"
            deps.package_versions = lambda: versions
            result = run_cli(["doctor", "--approved-input", str(approval)], dependencies=deps)
        self.assertEqual(30, result.exit_code)
        self.assertEqual("dependency-mismatch", result.payload["code"])

    def test_all_query_routes_return_stable_ids_and_corruption_fails_closed(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); approval = approved(root)
            source = root / "a.txt"; source.write_text("one two three " * 200)
            index = root / "index"; deps = dependencies(root)
            self.assertEqual(0, run_cli(["index", "--approved-input", str(approval), "--input", str(source), "--output", str(index)], dependencies=deps).exit_code)
            found = []
            for route in ("float32", "2bit", "4bit"):
                result = run_cli(["query", "--approved-input", str(approval), "--index", str(index), "--text", "three", "--route", route, "--top-k", "1"], dependencies=deps)
                self.assertEqual(0, result.exit_code, route)
                found.append(result.payload["results"][0]["chunk_id"])
            self.assertEqual(1, len(set(found)))
            with (index / "ids.npy").open("ab") as stream:
                stream.write(b"corrupt")
            corrupt = run_cli(["query", "--approved-input", str(approval), "--index", str(index), "--text", "three"], dependencies=deps)
        self.assertEqual(31, corrupt.exit_code)
        self.assertEqual("index-artifact-mismatch", corrupt.payload["code"])

    def test_duplicate_chunk_keys_are_index_corruption_not_approved_input_errors(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); approval = approved(root)
            source = root / "a.txt"; source.write_text("granite")
            index = root / "index"; deps = dependencies(root)
            self.assertEqual(0, run_cli(["index", "--approved-input", str(approval), "--input", str(source), "--output", str(index)], dependencies=deps).exit_code)
            chunks = index / "chunks.jsonl"
            value = json.loads(chunks.read_text())
            chunks.write_text('{"chunk_id":%d,"chunk_id":%d,"relative_path":"a.txt","start":0,"end":7,"text":"granite"}\n' % (value["chunk_id"], value["chunk_id"]))
            manifest = json.loads((index / "manifest.json").read_text())
            data = chunks.read_bytes()
            import hashlib
            for artifact in manifest["artifacts"]:
                if artifact["filename"] == "chunks.jsonl":
                    artifact["sha256"] = hashlib.sha256(data).hexdigest(); artifact["size"] = len(data)
            (index / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, allow_nan=False, separators=(",", ":"), sort_keys=True))
            result = run_cli(["query", "--approved-input", str(approval), "--index", str(index), "--text", "x"], dependencies=deps)
        self.assertEqual(31, result.exit_code)
        self.assertEqual("chunks-artifact-invalid", result.payload["code"])

    def test_benchmark_runs_matched_routes_five_warm_times_and_persists_gate_failure(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); approval = approved(root); deps = dependencies(root)
            fixture = Path(__file__).parent / "fixtures" / "evaluation.json"
            output = root / "evidence"
            result = run_cli(["benchmark", "--approved-input", str(approval), "--fixture", str(fixture), "--input", str(fixture.parent / "knowledge"), "--output", str(output)], dependencies=deps)
            self.assertEqual(40, result.exit_code)
            self.assertEqual("benchmark-gate-failed", result.payload["code"])
            evidence = json.loads((output / "results.json").read_text())
            self.assertEqual({"float32", "turbovec-2bit", "turbovec-4bit"}, {evidence["baseline"]["route"], *(item["route"] for item in evidence["candidates"])})
            self.assertTrue(all(route["timings"]["warm"]["count"] >= 5 for route in [evidence["baseline"], *evidence["candidates"]]))
            self.assertTrue((output / "summary.md").is_file())
            summary = (output / "summary.md").read_text()
            for phase in ("document_embedding_seconds", "query_embedding_seconds", "2bit_index_build_seconds", "4bit_index_load_seconds", "end_to_end_seconds"):
                self.assertIn(phase, summary)
            again = run_cli(["benchmark", "--approved-input", str(approval), "--fixture", str(fixture), "--input", str(fixture.parent / "knowledge"), "--output", str(output)], dependencies=deps)
        self.assertEqual(22, again.exit_code)

    def test_benchmark_pass_returns_safe_success_after_persisting_evidence(self):
        class TinyTurbo(FakeTurbo):
            stored = {}
            def write(self, path):
                Path(path).write_bytes(b"T")
                self.stored[str(path)] = self
            @classmethod
            def load(cls, path):
                return cls.stored[str(path)]

        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); approval = approved(root); deps = dependencies(root)
            deps.make_turbovec = lambda dimension, bits: TinyTurbo(dimension, bits=bits)
            deps.load_turbovec = TinyTurbo.load
            ticks = iter(range(10_000))
            deps.perf_counter = lambda: next(ticks)
            fixture = Path(__file__).parent / "fixtures" / "evaluation.json"
            output = root / "pass-evidence"
            result = run_cli(["benchmark", "--approved-input", str(approval), "--fixture", str(fixture), "--input", str(fixture.parent / "knowledge"), "--output", str(output)], dependencies=deps)
        self.assertEqual(0, result.exit_code)
        self.assertEqual({"schema_version": 1, "evidence": "pass-evidence", "gate_passed": True}, result.payload)


@unittest.skipUnless(os.environ.get("GRANITE_TURBOVEC_RUN_CONTROLLED") == "1", "controlled offline runtime not configured")
class ControlledOfflineIntegrationTests(unittest.TestCase):
    def test_real_doctor_uses_explicit_approved_input(self):
        approval = os.environ["GRANITE_TURBOVEC_APPROVED_INPUT"]
        result = run_cli(["doctor", "--approved-input", approval])
        self.assertEqual(0, result.exit_code)


if __name__ == "__main__":
    unittest.main()
