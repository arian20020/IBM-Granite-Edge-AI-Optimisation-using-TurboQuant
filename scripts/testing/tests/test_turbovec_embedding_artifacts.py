import json
from pathlib import Path
import tempfile
import unittest

import numpy as np

from scripts.testing.turbovec.artifacts import (
    generate_embedding_artifact,
    load_embedding_artifact,
)
from scripts.testing.turbovec.dataset import build_frozen_dataset
from scripts.testing.turbovec.embedding import DeterministicEmbeddingProvider
from scripts.testing.run_turbovec_feasibility import parser


IDENTITY = {
    "repository": "ibm-granite/granite-embedding-small-english-r2",
    "revision": "2ab6fa8ea2d674564defd37171ae19079b864b33",
    "dimension": 384,
    "dtype": "float32",
    "normalization": "l2",
    "device": "CPU",
    "tokenizer_sha256": "1" * 64,
}


class EmbeddingArtifactTests(unittest.TestCase):
    def test_cli_exposes_separate_generate_and_verify_phases(self):
        generated = parser().parse_args([
            "generate-embeddings", "--model-root", "model", "--scale", "1000",
            "--artifact-root", "artifacts", "--batch-size", "16",
        ])
        verified = parser().parse_args([
            "verify-embeddings", "--scale", "1000", "--artifact-root", "artifacts",
        ])
        self.assertEqual("generate-embeddings", generated.command)
        self.assertEqual(1_000, generated.scale)
        self.assertEqual("verify-embeddings", verified.command)

    def test_generation_publishes_one_verified_reusable_artifact(self):
        dataset = build_frozen_dataset(30)
        with tempfile.TemporaryDirectory() as directory:
            target = Path(directory) / "scale-30"
            generate_embedding_artifact(
                DeterministicEmbeddingProvider(), dataset, target, IDENTITY, batch_size=16
            )
            documents, queries, manifest = load_embedding_artifact(target, dataset)

            self.assertEqual((30, 384), documents.shape)
            self.assertEqual((128, 384), queries.shape)
            self.assertEqual(np.dtype("<f4"), documents.dtype)
            self.assertTrue(np.allclose(np.linalg.norm(documents, axis=1), 1.0, atol=1e-5))
            self.assertEqual(dataset.corpus_sha256, manifest["inputs"]["corpus_sha256"])
            self.assertEqual(dataset.query_sha256, manifest["inputs"]["query_sha256"])
            self.assertEqual(IDENTITY, manifest["model"])
            self.assertEqual(2, len(manifest["files"]))
            self.assertFalse((Path(directory) / ".staging-scale-30").exists())

            with self.assertRaises(FileExistsError):
                generate_embedding_artifact(
                    DeterministicEmbeddingProvider(), dataset, target, IDENTITY, batch_size=16
                )

    def test_loader_rejects_corruption_wrong_dataset_and_unnormalized_values(self):
        dataset = build_frozen_dataset(30)
        with tempfile.TemporaryDirectory() as directory:
            target = Path(directory) / "scale-30"
            generate_embedding_artifact(
                DeterministicEmbeddingProvider(), dataset, target, IDENTITY, batch_size=16
            )
            document_path = target / "documents.f32"
            document_path.write_bytes(document_path.read_bytes() + b"x")
            with self.assertRaisesRegex(ValueError, "artifact file integrity"):
                load_embedding_artifact(target, dataset)

    def test_generation_failure_never_publishes_target(self):
        class FailingProvider:
            def embed_documents(self, texts):
                raise RuntimeError("controlled provider failure")

            def embed_queries(self, texts):
                raise AssertionError("query embedding must not start")

        dataset = build_frozen_dataset(30)
        with tempfile.TemporaryDirectory() as directory:
            target = Path(directory) / "scale-30"
            with self.assertRaisesRegex(RuntimeError, "controlled provider failure"):
                generate_embedding_artifact(FailingProvider(), dataset, target, IDENTITY, batch_size=16)
            self.assertFalse(target.exists())
            failures = list(Path(directory).glob(".failed-scale-30-*"))
            self.assertEqual(1, len(failures))
            failure = json.loads((failures[0] / "failure.json").read_text(encoding="utf-8"))
            self.assertEqual("RuntimeError", failure["error_type"])

    def test_batch_size_is_bounded(self):
        dataset = build_frozen_dataset(30)
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaisesRegex(ValueError, "batch size"):
                generate_embedding_artifact(
                    DeterministicEmbeddingProvider(), dataset, Path(directory) / "bad", IDENTITY, batch_size=17
                )


if __name__ == "__main__":
    unittest.main()
