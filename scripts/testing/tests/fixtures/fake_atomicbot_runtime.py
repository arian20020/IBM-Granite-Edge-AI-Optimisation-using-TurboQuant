import argparse
import subprocess
import sys
import time


parser = argparse.ArgumentParser()
parser.add_argument("--mode", choices=("success", "timeout", "child"), default="success")
args = parser.parse_args()

if args.mode == "child":
    subprocess.Popen([sys.executable, "-c", "import time; time.sleep(60)"])
    time.sleep(60)
elif args.mode == "timeout":
    time.sleep(60)
else:
    print("fake atomicbot complete")
