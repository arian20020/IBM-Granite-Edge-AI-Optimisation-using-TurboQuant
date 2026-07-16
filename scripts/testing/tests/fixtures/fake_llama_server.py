import argparse
import json
import sys
import time
from http.server import BaseHTTPRequestHandler, HTTPServer

parser = argparse.ArgumentParser()
parser.add_argument("--port", type=int, required=True)
args = parser.parse_args()
payload = bytearray(24 * 1024 * 1024)
for offset in range(0, len(payload), 4096):
    payload[offset] = 1
print("llama_kv_cache: size = 12.00 MiB (128 cells, 4 layers, 1/1 seqs)", file=sys.stderr, flush=True)

class Handler(BaseHTTPRequestHandler):
    def log_message(self, *args):
        pass
    def do_GET(self):
        self.send_response(200); self.end_headers(); self.wfile.write(b'{"status":"ok"}')
    def do_POST(self):
        length = int(self.headers.get("content-length", "0")); self.rfile.read(length)
        self.send_response(200); self.send_header("Content-Type", "text/event-stream"); self.end_headers()
        time.sleep(0.3)
        self.wfile.write(b'data: {"content":"X","stop":false}\n\n'); self.wfile.flush()
        self.wfile.write(b'data: {"content":"","stop":true}\n\n'); self.wfile.flush()

HTTPServer(("127.0.0.1", args.port), Handler).serve_forever()
