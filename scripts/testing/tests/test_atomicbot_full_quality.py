import time

import pytest

from scripts.testing.tools.run_atomicbot_full_quality import (
    acquire_runner_lock,
    call_with_deadline,
    completion_is_terminal,
)


def test_call_with_deadline_returns_completed_value():
    assert call_with_deadline(lambda: "done", 1) == "done"


def test_call_with_deadline_enforces_total_wall_clock_limit():
    started = time.monotonic()
    with pytest.raises(TimeoutError, match="wall-clock deadline"):
        call_with_deadline(lambda: time.sleep(1), 0.05)
    assert time.monotonic() - started < 0.5


def test_runner_lock_rejects_a_second_controller(tmp_path):
    first, release = acquire_runner_lock(tmp_path)
    try:
        with pytest.raises(RuntimeError, match="another quality runner"):
            acquire_runner_lock(tmp_path)
    finally:
        release()
    assert first.closed


def test_completion_requires_six_terminal_prompt_measurements():
    assert completion_is_terminal({"statuses": {f"P{i}": "complete" for i in range(1, 7)}})
    assert completion_is_terminal({"statuses": {
        **{f"P{i}": "complete" for i in range(1, 6)}, "P6": "timeout"
    }})
    assert completion_is_terminal({"statuses": {
        **{f"P{i}": "complete" for i in range(1, 6)}, "P6": "safety-blocked"
    }})
    assert not completion_is_terminal({"statuses": {"P1": "complete"}})
    assert not completion_is_terminal({"statuses": {
        **{f"P{i}": "complete" for i in range(1, 6)}, "P6": "failed"
    }})
