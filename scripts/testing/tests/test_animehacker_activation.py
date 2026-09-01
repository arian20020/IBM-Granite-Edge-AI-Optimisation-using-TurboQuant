import unittest


class AnimehackerActivationTests(unittest.TestCase):
    def test_tq_requires_exact_flags_and_runtime_allocation(self):
        from scripts.testing.campaigns.animehacker.activation import classify_activation

        log = "llama_kv_cache: CPU KV buffer size = 42.00 MiB"
        command = ["llama-server.exe", "-ctk", "tq3_0", "-ctv", "tq3_0", "-ngl", "0"]
        result = classify_activation(command, log, expected_cache="tq3_0", backend="cpu")
        self.assertTrue(result["activated"])
        self.assertEqual(result["actual_device"], "CPU")
        self.assertEqual(result["proof_kind"], "runtime-plus-source-linked")

    def test_flag_only_claim_is_rejected(self):
        from scripts.testing.campaigns.animehacker.activation import classify_activation

        command = ["llama-server.exe", "-ctk", "tq3_0", "-ctv", "tq3_0"]
        result = classify_activation(command, "server listening", expected_cache="tq3_0", backend="cpu")
        self.assertFalse(result["activated"])
        self.assertIn("KV allocation", result["reason"])

    def test_sycl_requires_device_and_offload_proof(self):
        from scripts.testing.campaigns.animehacker.activation import classify_activation

        command = ["llama-server.exe", "-ctk", "f16", "-ctv", "f16", "-ngl", "1"]
        log = ("using device SYCL0 (Intel(R) UHD Graphics)\n"
               "offloaded 1/37 layers to GPU\n"
               "llama_kv_cache: SYCL0 KV buffer size = 80.00 MiB")
        result = classify_activation(command, log, expected_cache="f16", backend="sycl-partial")
        self.assertTrue(result["activated"])
        self.assertEqual(result["actual_device"], "SYCL0")


if __name__ == "__main__":
    unittest.main()
