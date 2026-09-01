"""Run the frozen GTQ-PROMPTS-v1 quality screen against blind cache labels."""

from __future__ import annotations

import argparse
import hashlib
import json
import subprocess
import time
import urllib.request
from pathlib import Path


def request(port: int, messages: list[dict[str, str]], max_tokens: int = 256) -> str:
    body = json.dumps({"messages": messages, "temperature": 0, "top_p": 1,
                       "seed": 42, "max_tokens": max_tokens}).encode()
    req = urllib.request.Request(f"http://127.0.0.1:{port}/v1/chat/completions",
                                 data=body, headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=2400) as response:
        return json.loads(response.read())["choices"][0]["message"]["content"]


def wait_ready(port: int, proc: subprocess.Popen, timeout: int = 180) -> None:
    deadline = time.time() + timeout
    while time.time() < deadline:
        if proc.poll() is not None:
            raise RuntimeError(f"server exited {proc.returncode}")
        try:
            with urllib.request.urlopen(f"http://127.0.0.1:{port}/health", timeout=2):
                return
        except Exception:
            time.sleep(1)
    raise TimeoutError("server did not become ready")


def render_prompt(prompt: dict, prompt_root: Path) -> str:
    text = prompt["turns"][0]["content"]
    if prompt.get("context_facts"):
        text = "SUPPLIED TEST FACTS:\n" + "\n".join(f"- {x}" for x in prompt["context_facts"]) + "\n\n" + text
    if prompt.get("fixture_path"):
        fixture = (prompt_root / prompt["fixture_path"]).read_text(encoding="utf-8")
        text = "SUPPLIED LONG CONTEXT:\n" + fixture + "\n\n" + text
    return text


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--server", type=Path, required=True)
    parser.add_argument("--model", type=Path, required=True)
    parser.add_argument("--prompt-set", type=Path, required=True)
    parser.add_argument("--output-root", type=Path, required=True)
    parser.add_argument("--port", type=int, default=19080)
    args = parser.parse_args()
    prompt_set = json.loads(args.prompt_set.read_text(encoding="utf-8"))
    args.output_root.mkdir(parents=True, exist_ok=True)
    # Labels deliberately conceal the precision ordering during later adjudication.
    configurations = (("configuration-A", "q8_0"), ("configuration-B", "turbo3"))
    manifest = {"prompt_set": prompt_set["prompt_set_id"], "prompt_set_sha256":
                hashlib.sha256(args.prompt_set.read_bytes()).hexdigest(), "runs": []}
    for index, (blind_label, cache) in enumerate(configurations):
        root = args.output_root / blind_label
        root.mkdir(exist_ok=True)
        stdout = (root / "server-stdout.log").open("wb")
        stderr = (root / "server-stderr.log").open("wb")
        port = args.port + index
        # P5 is 8,706 tokens after the model's chat template; 16K is the smallest
        # standard context tier that can execute the frozen fixture without truncation.
        command = [str(args.server), "-m", str(args.model), "-c", "16384", "-t", "8", "-tb", "8",
                   "-ctk", cache, "-ctv", cache, "-ngl", "0", "--host", "127.0.0.1",
                   "--port", str(port), "-np", "1", "--cache-ram", "0", "--fit", "off",
                   "-fa", "on"]
        proc = subprocess.Popen(command, stdout=stdout, stderr=stderr)
        try:
            wait_ready(port, proc)
            for prompt in prompt_set["prompts"]:
                pid = prompt["prompt_id"]
                record_path = root / f"{pid}.json"
                if record_path.is_file():
                    record = json.loads(record_path.read_text(encoding="utf-8"))
                    manifest["runs"].append({"blind_label": blind_label, **record})
                    print(f"{blind_label} {pid} resumed", flush=True)
                    continue
                try:
                    if pid == "P6":
                        first = request(port, [{"role": "user", "content": prompt["turns"][0]["content"]}], 32)
                        messages = [{"role": "user", "content": prompt["turns"][0]["content"]},
                                    {"role": "assistant", "content": first},
                                    {"role": "user", "content": prompt["turns"][2]["content"]}]
                        output = request(port, messages, 32)
                        record = {"prompt_id": pid, "turn_1": first, "output": output, "status": "complete"}
                    else:
                        output = request(port, [{"role": "user", "content": render_prompt(prompt, args.prompt_set.parent)}])
                        record = {"prompt_id": pid, "output": output, "status": "complete"}
                except TimeoutError as exc:
                    output = ""
                    record = {"prompt_id": pid, "output": "", "status": "timeout",
                              "error": f"{type(exc).__name__}: {exc}", "timeout_seconds": 2400}
                record["output_sha256"] = hashlib.sha256(output.encode()).hexdigest()
                record_path.write_text(json.dumps(record, indent=2), encoding="utf-8")
                (root / f"{pid}-response.txt").write_text(output, encoding="utf-8")
                manifest["runs"].append({"blind_label": blind_label, **record})
                print(f"{blind_label} {pid} complete", flush=True)
        finally:
            proc.terminate()
            try:
                proc.wait(20)
            except subprocess.TimeoutExpired:
                subprocess.run(["taskkill", "/PID", str(proc.pid), "/T", "/F"], capture_output=True)
            stdout.close(); stderr.close()
    (args.output_root / "quality-run-manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
