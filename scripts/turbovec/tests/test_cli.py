import io
import ast
import json
import os
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path
from unittest import mock

import numpy as np

from granite_turbovec.cli import FIXED_RESEARCH_ERROR_CODES, EXIT_CODES, LOCKED_VERSIONS, CliDependencies, _raw_float32_bytes, main, run_cli
from granite_turbovec.contracts import ResearchError
from granite_turbovec.manifest import MANIFEST_MISMATCH_CODES, IndexIdentity


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
    dimension = 384
    model_identity = "BAAI/bge-small-en-v1.5"
    providers = ["CPUExecutionProvider"]

    def __init__(self):
        self.document_calls = 0
        self.query_calls = 0

    def embed_documents(self, texts):
        self.document_calls += 1
        result = np.zeros((len(texts), self.dimension), dtype=np.float32)
        for index, text in enumerate(texts): result[index, :3] = (len(text), 1, 1)
        return result

    def embed_queries(self, texts):
        self.query_calls += 1
        result = np.zeros((len(texts), self.dimension), dtype=np.float32)
        for index, text in enumerate(texts): result[index, :3] = (len(text), 1, 1)
        return result


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
        platform_info=lambda: {"platform": "windows", "architecture": "AMD64", "processor": "test-cpu"},
        make_embedder=lambda cache: embedder,
        make_turbovec=lambda dimension, bits: FakeTurbo(dimension, bits=bits),
        load_turbovec=lambda path: FakeTurbo.load(path),
        now_utc=lambda: "2026-08-20T12:00:00Z",
        perf_counter=__import__("time").perf_counter,
        peak_working_set=lambda: 123456,
    )


def invoke(args, deps):
    stdout, stderr = io.StringIO(), io.StringIO()
    with redirect_stdout(stdout), redirect_stderr(stderr):
        code = main(args, dependencies=deps)
    return code, stdout.getvalue(), stderr.getvalue()


class CliTests(unittest.TestCase):
    def setUp(self):
        self.offline = mock.patch.dict(os.environ, {"HF_HUB_OFFLINE": "1", "TRANSFORMERS_OFFLINE": "1", "HF_HUB_DISABLE_TELEMETRY": "1"})
        self.offline.start()

    def tearDown(self):
        self.offline.stop()

    def _build_benchmark_index(self, root, approval, deps):
        fixture = Path(__file__).parent / "fixtures" / "evaluation.json"
        index = root / "benchmark-index"
        result = run_cli(["index", "--approved-input", str(approval), "--input", str(fixture.parent / "knowledge"), "--output", str(index), "--bits", "2", "4"], dependencies=deps)
        self.assertEqual(0, result.exit_code, result.payload)
        return fixture, index

    def test_dependency_noise_is_contained_to_preserve_json_streams(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); approval = approved(root)
            deps = dependencies(root)
            original = deps.model_manifest_sha256
            def noisy(path):
                print("dependency-progress")
                os.write(1, b"native-dependency-progress\n")
                os.write(2, b"native-dependency-error\n")
                return original(path)
            deps.model_manifest_sha256 = noisy
            code, stdout, stderr = invoke(["doctor", "--approved-input", str(approval)], deps)
        self.assertEqual(0, code)
        self.assertEqual("", stderr)
        json.loads(stdout)
        self.assertNotIn("dependency-progress", stdout)
        self.assertNotIn("native-dependency", stdout + stderr)

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

    def test_every_fixed_research_error_has_one_reviewed_exit_mapping(self):
        package = Path(__file__).parents[1] / "granite_turbovec"
        emitted = set()
        for source in package.glob("*.py"):
            tree = ast.parse(source.read_text(encoding="utf-8"))
            for node in ast.walk(tree):
                if isinstance(node, ast.Call) and isinstance(node.func, ast.Name) and node.func.id == "ResearchError" and node.args and isinstance(node.args[0], ast.Constant) and isinstance(node.args[0].value, str):
                    emitted.add(node.args[0].value)
        self.assertEqual(set(), emitted - set(EXIT_CODES) - {"research-argument-error"})
        self.assertEqual(21, EXIT_CODES["input-file-size-limit"])
        self.assertEqual(32, EXIT_CODES["embedding-failed"])
        self.assertEqual(32, EXIT_CODES["vectors-nonfinite"])
        self.assertEqual(33, EXIT_CODES["index-search-failed"])
        self.assertEqual(31, EXIT_CODES["index-embedding-mismatch"])
        self.assertEqual(31, EXIT_CODES["index-package-mismatch"])
        self.assertTrue(MANIFEST_MISMATCH_CODES <= set(EXIT_CODES))
        self.assertEqual(set(EXIT_CODES) | {"research-argument-error"}, set(FIXED_RESEARCH_ERROR_CODES))
        source = (package / "cli.py").read_text(encoding="utf-8")
        tree = ast.parse(source)
        mapping = next(node.value for node in tree.body if isinstance(node, ast.Assign) and any(isinstance(target, ast.Name) and target.id == "ERROR_EXIT" for target in node.targets))
        keys = [key.value for key in mapping.keys if isinstance(key, ast.Constant)]
        self.assertEqual(len(keys), len(set(keys)), "ERROR_EXIT must not contain duplicate literal codes")

    def test_stored_index_package_mismatch_returns_exact_exit_31(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); approval = approved(root); source = root / "a.txt"; source.write_text("granite")
            index = root / "index"; deps = dependencies(root)
            self.assertEqual(0, run_cli(["index", "--approved-input", str(approval), "--input", str(source), "--output", str(index)], dependencies=deps).exit_code)
            manifest_path = index / "manifest.json"; payload = json.loads(manifest_path.read_text())
            payload["identity"]["dependency_lock_sha256"] = "9" * 64
            manifest_path.write_text(json.dumps(payload, ensure_ascii=False, allow_nan=False, separators=(",", ":"), sort_keys=True))
            result = run_cli(["query", "--approved-input", str(approval), "--index", str(index), "--text", "x", "--top-k", "1"], dependencies=deps)
        self.assertEqual(31, result.exit_code)
        self.assertEqual("index-package-mismatch", result.payload["code"])

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

    def test_bits_contract_accepts_two_values_and_rejects_duplicates(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); approval = approved(root); source = root / "a.txt"; source.write_text("granite")
            result = run_cli(["index", "--approved-input", str(approval), "--input", str(source), "--output", str(root / "index"), "--bits", "4", "2"], dependencies=dependencies(root))
            duplicate = run_cli(["index", "--approved-input", str(approval), "--input", str(source), "--output", str(root / "other"), "--bits", "2", "2"], dependencies=dependencies(root))
        self.assertEqual(0, result.exit_code)
        self.assertEqual(["float32", "2bit", "4bit"], result.payload["routes"])
        self.assertEqual(2, duplicate.exit_code)

    def test_benchmark_requires_existing_index_and_rejects_input_rebuild_argument(self):
        fixture = Path(__file__).parent / "fixtures" / "evaluation.json"
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); approval = approved(root); deps = dependencies(root)
            missing = run_cli(["benchmark", "--approved-input", str(approval), "--fixture", str(fixture), "--output", str(root / "e")], dependencies=deps)
            rebuild = run_cli(["benchmark", "--approved-input", str(approval), "--fixture", str(fixture), "--index", str(root / "index"), "--input", str(fixture.parent / "knowledge"), "--output", str(root / "e")], dependencies=deps)
        self.assertEqual(2, missing.exit_code)
        self.assertEqual(2, rebuild.exit_code)

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
            self.assertEqual({"manifest.json", "chunks.jsonl", "vectors-float32.npy", "ids.npy", "baseline-results.json", "index-2bit.tvim", "index-4bit.tvim"}, names)
            manifest = json.loads((index / "manifest.json").read_text())
            self.assertEqual("matched-suite", manifest["identity"]["actual_backend"])
            self.assertEqual("1.0.0", manifest["identity"]["turbovec_version"])
            routes = {item["route"]: item for item in manifest["artifacts"] if item["route"] != "metadata"}
            self.assertEqual({"float32", "2bit", "4bit"}, set(routes))
            self.assertEqual(("turbovec", "gtvi-turbovec-v1", 4), (routes["4bit"]["backend"], routes["4bit"]["index_format"], routes["4bit"]["bit_width"]))
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

    def test_invalid_query_text_and_top_k_precede_artifact_or_runtime_loading(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); approval = approved(root); source = root / "a.txt"; source.write_text("granite")
            index = root / "index"; deps = dependencies(root)
            self.assertEqual(0, run_cli(["index", "--approved-input", str(approval), "--input", str(source), "--output", str(index)], dependencies=deps).exit_code)
            spies = dependencies(root)
            spies.make_embedder = lambda cache: (_ for _ in ()).throw(AssertionError("embedder reached"))
            spies.load_turbovec = lambda path: (_ for _ in ()).throw(AssertionError("loader reached"))
            empty = run_cli(["query", "--approved-input", str(approval), "--index", str(index), "--text", "   "], dependencies=spies)
            topk = run_cli(["query", "--approved-input", str(approval), "--index", str(index), "--text", "x", "--top-k", "2"], dependencies=spies)
        self.assertEqual(20, empty.exit_code)
        self.assertEqual(20, topk.exit_code)
        self.assertEqual("query-top-k-invalid", topk.payload["code"])

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

    def test_runtime_platform_model_dimension_and_provider_are_strict(self):
        cases = (
            ({"platform": "linux", "architecture": "x86_64", "processor": "cpu"}, None, "environment-mismatch"),
            ({"platform": "windows", "architecture": "arm64", "processor": "cpu"}, None, "environment-mismatch"),
            (None, {"model_identity": "other/model"}, "approval-mismatch"),
            (None, {"dimension": 3}, "approval-mismatch"),
            (None, {"providers": ["CUDAExecutionProvider"]}, "environment-mismatch"),
            (None, {"providers": []}, "environment-mismatch"),
        )
        for platform_value, embedder_changes, expected in cases:
            with self.subTest(expected=expected, changes=embedder_changes), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary); approval = approved(root); deps = dependencies(root)
                if platform_value is not None: deps.platform_info = lambda value=platform_value: value
                if embedder_changes is not None:
                    embedder = FakeEmbedder()
                    for name, value in embedder_changes.items(): setattr(embedder, name, value)
                    deps.make_embedder = lambda cache, value=embedder: value
                result = run_cli(["doctor", "--approved-input", str(approval)], dependencies=deps)
                self.assertEqual(30, result.exit_code)
                self.assertEqual(expected, result.payload["code"])

    def test_missing_offline_flag_fails_closed(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); approval = approved(root)
            with mock.patch.dict(os.environ, {}, clear=True):
                result = run_cli(["doctor", "--approved-input", str(approval)], dependencies=dependencies(root))
        self.assertEqual(30, result.exit_code)
        self.assertEqual("environment-mismatch", result.payload["code"])

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
            corrupt = run_cli(["query", "--approved-input", str(approval), "--index", str(index), "--text", "three", "--top-k", "1"], dependencies=deps)
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
            result = run_cli(["query", "--approved-input", str(approval), "--index", str(index), "--text", "x", "--top-k", "1"], dependencies=deps)
        self.assertEqual(31, result.exit_code)
        self.assertEqual("chunks-artifact-invalid", result.payload["code"])

    def test_bad_tvim_container_is_rejected_before_turbovec_loader(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); approval = approved(root); source = root / "a.txt"; source.write_text("granite")
            index = root / "index"; deps = dependencies(root)
            self.assertEqual(0, run_cli(["index", "--approved-input", str(approval), "--input", str(source), "--output", str(index)], dependencies=deps).exit_code)
            container = index / "index-2bit.tvim"
            payload = container.read_bytes(); container.write_bytes(b"BAD!" + payload[4:])
            manifest = json.loads((index / "manifest.json").read_text())
            import hashlib
            for artifact in manifest["artifacts"]:
                if artifact["filename"] == container.name:
                    artifact["sha256"] = hashlib.sha256(container.read_bytes()).hexdigest()
            (index / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, allow_nan=False, separators=(",", ":"), sort_keys=True))
            spies = dependencies(root)
            spies.load_turbovec = lambda path: (_ for _ in ()).throw(AssertionError("unsafe loader reached"))
            result = run_cli(["query", "--approved-input", str(approval), "--index", str(index), "--text", "x", "--top-k", "1", "--route", "2bit"], dependencies=spies)
        self.assertEqual(31, result.exit_code)
        self.assertEqual("index-artifact-invalid", result.payload["code"])

    def test_benchmark_runs_matched_routes_five_warm_times_and_persists_gate_failure(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); approval = approved(root); embedder = FakeEmbedder(); deps = dependencies(root, embedder)
            fixture, index = self._build_benchmark_index(root, approval, deps)
            document_calls = embedder.document_calls
            output = root / "evidence"
            result = run_cli(["benchmark", "--approved-input", str(approval), "--fixture", str(fixture), "--index", str(index), "--output", str(output)], dependencies=deps)
            self.assertEqual(40, result.exit_code)
            self.assertEqual("benchmark-gate-failed", result.payload["code"])
            results_text = (output / "results.json").read_text()
            self.assertNotIn(str(root), results_text)
            document = json.loads(results_text)
            evidence = document["benchmark"]
            self.assertEqual(64, len(document["index_manifest_sha256"]))
            self.assertEqual(64, len(document["evaluation_fixture_sha256"]))
            self.assertIn("environment", document)
            self.assertIn("phase_timings", document)
            self.assertGreater(document["peak_working_set_bytes"], 0)
            self.assertEqual({"float32", "turbovec-2bit", "turbovec-4bit"}, {evidence["baseline"]["route"], *(item["route"] for item in evidence["candidates"])})
            self.assertTrue(all(route["timings"]["warm"]["count"] >= 5 for route in [evidence["baseline"], *evidence["candidates"]]))
            self.assertTrue((output / "summary.md").is_file())
            self.assertEqual(document_calls, embedder.document_calls, "benchmark must reuse stored document embeddings")
            self.assertEqual(1, embedder.query_calls, "all routes must reuse one query embedding batch")
            summary = (output / "summary.md").read_text()
            for phase in ("document_embedding_seconds", "query_embedding_seconds", "float32_index_build_seconds", "4bit_index_load_seconds", "end_to_end_seconds"):
                self.assertIn(phase, summary)
            again = run_cli(["benchmark", "--approved-input", str(approval), "--fixture", str(fixture), "--index", str(index), "--output", str(output)], dependencies=deps)
            self.assertEqual(22, again.exit_code)

    def test_measured_index_timings_flow_from_metadata_into_benchmark_evidence(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); approval = approved(root); embedder = FakeEmbedder(); deps = dependencies(root, embedder)
            tick = {"value": 0}
            def clock():
                tick["value"] += 1
                return float(tick["value"] ** 2)
            deps.perf_counter = clock
            fixture, index = self._build_benchmark_index(root, approval, deps)
            metadata = json.loads((index / "baseline-results.json").read_text())
            measured = metadata["build_timings_seconds"]
            required = {"document_embedding_seconds", "float32_index_build_seconds", "float32_index_save_seconds", "2bit_index_build_seconds", "4bit_index_build_seconds", "2bit_index_save_seconds", "4bit_index_save_seconds", "index_end_to_end_seconds"}
            self.assertEqual(required, set(measured))
            self.assertTrue(all(value > 0 for value in measured.values()))
            self.assertGreater(len(set(measured.values())), 3)
            output = root / "timing-evidence"
            result = run_cli(["benchmark", "--approved-input", str(approval), "--fixture", str(fixture), "--index", str(index), "--output", str(output)], dependencies=deps)
            self.assertIn(result.exit_code, (0, 40))
            phases = json.loads((output / "results.json").read_text())["phase_timings"]
            for name, value in measured.items():
                self.assertEqual(value, phases[name])
            self.assertEqual(0, metadata["vector_count"] - len(json.loads((index / "manifest.json").read_text())["chunks"]))
            self.assertEqual(384, metadata["dimension"])
            self.assertEqual(["CPUExecutionProvider"], metadata["actual_providers"])
            storage = json.loads((output / "results.json").read_text())["storage_files"]
            self.assertEqual(metadata["vector_count"] * metadata["dimension"] * 4, storage["float32_raw_vector_bytes"])
            self.assertGreater(storage["float32_persisted_npy_bytes"], storage["float32_raw_vector_bytes"])
            routed_storage = json.loads((output / "results.json").read_text())["benchmark"]["storage"]
            self.assertTrue(all(item["float32_vector_bytes"] == storage["float32_raw_vector_bytes"] for item in routed_storage))

    def test_storage_gate_denominator_excludes_npy_header_bytes(self):
        with tempfile.TemporaryDirectory() as temporary:
            vectors = np.zeros((1, 25), dtype=np.float32)
            path = Path(temporary) / "vectors.npy"
            with path.open("xb") as stream:
                np.save(stream, vectors, allow_pickle=False)
            raw_bytes = _raw_float32_bytes(vectors)
            persisted_bytes = path.stat().st_size
        candidate_bytes = 40
        self.assertEqual(100, raw_bytes)
        self.assertGreater(persisted_bytes, raw_bytes)
        self.assertGreater(candidate_bytes / raw_bytes, 0.25, "raw vectors must fail the <=25% gate")
        self.assertLessEqual(candidate_bytes / persisted_bytes, 0.25, "NPY header would incorrectly pass the gate")

    def test_benchmark_pass_returns_safe_success_after_persisting_evidence(self):
        class TinyTurbo(FakeTurbo):
            stored = {}
            def write(self, path):
                Path(path).write_bytes(b"T" + bytes([self.bits]))
                self.stored[self.bits] = self
            @classmethod
            def load(cls, path):
                bits = Path(path).read_bytes()[1]
                return cls.stored[bits]

        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); approval = approved(root); deps = dependencies(root)
            deps.make_turbovec = lambda dimension, bits: TinyTurbo(dimension, bits=bits)
            deps.load_turbovec = TinyTurbo.load
            ticks = iter(range(10_000))
            deps.perf_counter = lambda: next(ticks)
            fixture, index = self._build_benchmark_index(root, approval, deps)
            output = root / "pass-evidence"
            result = run_cli(["benchmark", "--approved-input", str(approval), "--fixture", str(fixture), "--index", str(index), "--output", str(output)], dependencies=deps)
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
