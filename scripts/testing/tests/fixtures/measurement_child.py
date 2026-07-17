import sys
import time


payload = bytearray(32 * 1024 * 1024)
for offset in range(0, len(payload), 4096):
    payload[offset] = 1
print("llama_kv_cache: layer 0: dev = CPU, size = 12.50 MiB", file=sys.stderr, flush=True)
time.sleep(0.35)
sys.stdout.write("X")
sys.stdout.flush()
time.sleep(0.15)
sys.stdout.write("done\n")
sys.stdout.flush()
raise SystemExit(0 if payload else 1)
