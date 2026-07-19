import unittest

from scripts.testing.official_openvino.acquisition import (
    audit_checkout,
    audit_environment,
    redact_environment,
)


class OfficialOpenVINOAcquisitionTests(unittest.TestCase):
    def checkout(self, **overrides):
        value = {"tag": "2026.2.1", "commit": "a" * 40,
                 "remote": "https://github.com/openvinotoolkit/openvino.git",
                 "dirty": False, "submodules": [], "tree_sha256": "b" * 64}
        value.update(overrides)
        return value

    def test_audit_rejects_wrong_tag_and_dirty_checkout(self):
        with self.assertRaisesRegex(ValueError, "expected tag 2026.2.1"):
            audit_checkout(self.checkout(tag="master"), expected_tag="2026.2.1",
                           expected_remote="https://github.com/openvinotoolkit/openvino.git")
        with self.assertRaisesRegex(ValueError, "dirty checkout"):
            audit_checkout(self.checkout(dirty=True), expected_tag="2026.2.1",
                           expected_remote="https://github.com/openvinotoolkit/openvino.git")

    def test_audit_accepts_exact_openvino_and_genai_releases(self):
        runtime = audit_checkout(self.checkout(), expected_tag="2026.2.1",
                                 expected_remote="https://github.com/openvinotoolkit/openvino.git")
        genai = self.checkout(tag="2026.2.1.0",
                              remote="https://github.com/openvinotoolkit/openvino.genai.git")
        audit_checkout(genai, expected_tag="2026.2.1.0",
                       expected_remote="https://github.com/openvinotoolkit/openvino.genai.git")
        self.assertEqual(runtime["commit"], "a" * 40)

    def test_environment_requires_exact_packages_and_cpu_gpu_inventory(self):
        evidence = {"python_version": "3.11.9",
                    "packages": {"openvino": "2026.2.1", "openvino-genai": "2026.2.1.0"},
                    "devices": ["CPU", "GPU.0"], "installed_ram_bytes": 32 * 1024**3,
                    "os": "Windows"}
        self.assertTrue(audit_environment(evidence)["accepted"])
        for mutation, message in (({"devices": ["CPU"]}, "GPU"),
                                  ({"packages": {"openvino": "2026.2.0",
                                                 "openvino-genai": "2026.2.1.0"}}, "2026.2.1")):
            broken = dict(evidence); broken.update(mutation)
            with self.assertRaisesRegex(ValueError, message):
                audit_environment(broken)

    def test_environment_redaction_excludes_secret_like_values(self):
        redacted = redact_environment({"PATH": "safe", "HF_TOKEN": "secret",
                                       "GITHUB_TOKEN": "secret", "ONEAPI_ROOT": "C:/oneapi"})
        self.assertEqual(redacted, {"PATH": "safe", "ONEAPI_ROOT": "C:/oneapi"})


if __name__ == "__main__":
    unittest.main()
