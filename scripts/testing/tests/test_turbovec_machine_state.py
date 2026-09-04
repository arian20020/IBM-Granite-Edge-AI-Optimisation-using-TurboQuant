from datetime import datetime, timedelta, timezone
import unittest

from scripts.testing.turbovec.machine_state import MachineSample, _committed_memory_bytes, capture_window, evaluate_readiness


GIB = 1024 ** 3
CONDITIONS = {
    "battery_saver_disabled": True,
    "no_windows_update_install": True,
    "no_active_downloads": True,
    "no_cloud_sync_activity": True,
    "no_other_compilation": True,
    "no_other_benchmark": True,
    "no_browser_or_media_workload": True,
    "previous_candidate_processes_terminated": True,
    "previous_index_unloaded": True,
    "temporary_resources_cleaned": True,
    "security_protections_enabled": True,
}


def sample(second, *, cpu=3.0, available=8 * GIB, on_ac=True, power="balanced"):
    return MachineSample(
        timestamp_utc=(datetime(2026, 9, 4, tzinfo=timezone.utc) + timedelta(seconds=second)).isoformat(),
        total_physical_ram_bytes=16 * GIB,
        available_physical_ram_bytes=available,
        committed_memory_bytes=9 * GIB,
        pagefile_used_bytes=1 * GIB,
        experiment_process_working_set_bytes=100_000_000,
        system_cpu_percent=cpu,
        experiment_process_cpu_percent=0.5,
        gpu_utilization_percent=None,
        shared_gpu_memory_bytes=None,
        power_mode=power,
        on_ac_power=on_ac,
        uptime_seconds=10_000 + second,
        top_memory_processes=(),
        top_cpu_processes=(),
        thermal_celsius=None,
    )


class MachineStateTests(unittest.TestCase):
    def test_committed_memory_uses_a_positive_host_measurement(self):
        self.assertGreater(_committed_memory_bytes(), 0)

    def test_live_capture_covers_the_requested_wall_clock_window(self):
        samples = capture_window(1, 0.2)
        start = datetime.fromisoformat(samples[0].timestamp_utc)
        end = datetime.fromisoformat(samples[-1].timestamp_utc)
        self.assertGreaterEqual((end - start).total_seconds(), 1.0)

    def test_accepts_full_stable_sixty_second_window(self):
        samples = [sample(second) for second in range(61)]
        result = evaluate_readiness(samples, CONDITIONS)
        self.assertTrue(result["ready"])
        self.assertEqual([], result["reasons"])
        self.assertLess(result["average_system_cpu_percent"], 10.0)
        self.assertLessEqual(result["available_ram_variation_fraction"], 0.05)

    def test_rejects_short_busy_unstable_low_ram_or_uncontrolled_conditions(self):
        cases = [
            ([sample(second) for second in range(30)], CONDITIONS, "60 seconds"),
            ([sample(second, cpu=12.0) for second in range(61)], CONDITIONS, "CPU"),
            ([sample(second, available=(8 if second % 2 else 6) * GIB) for second in range(61)], CONDITIONS, "RAM stability"),
            ([sample(second, available=3 * GIB) for second in range(61)], CONDITIONS, "4 GiB"),
            ([sample(second, on_ac=False) for second in range(61)], CONDITIONS, "AC power"),
            ([sample(second) for second in range(61)], {**CONDITIONS, "no_active_downloads": False}, "no_active_downloads"),
        ]
        for samples, conditions, reason in cases:
            with self.subTest(reason=reason):
                result = evaluate_readiness(samples, conditions)
                self.assertFalse(result["ready"])
                self.assertTrue(any(reason in item for item in result["reasons"]))

    def test_hard_floor_is_safety_abort_not_candidate_failure(self):
        samples = [sample(second, available=1 * GIB) for second in range(61)]
        result = evaluate_readiness(samples, CONDITIONS)
        self.assertFalse(result["ready"])
        self.assertTrue(result["safety_abort"])
        self.assertEqual("machine_safety", result["classification"])


if __name__ == "__main__":
    unittest.main()
