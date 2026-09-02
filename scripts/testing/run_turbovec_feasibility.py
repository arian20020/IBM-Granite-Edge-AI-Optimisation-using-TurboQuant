"""CLI entry point for the controlled TurboVec feasibility campaign."""

from __future__ import annotations

import argparse
import importlib.metadata
import json
from pathlib import Path
import subprocess
import sys

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
SCRIPT_DIRECTORY = Path(__file__).resolve().parent
sys.path[:] = [entry for entry in sys.path if Path(entry or ".").resolve() != SCRIPT_DIRECTORY]
sys.path.insert(0, str(REPOSITORY_ROOT))

from scripts.testing.turbovec.embedding import DeterministicEmbeddingProvider, OpenVinoGraniteEmbeddingProvider, lock_model_assets
from scripts.testing.turbovec.runner import run_fixture_campaign, run_live_campaign, write_processed_results


def parser() -> argparse.ArgumentParser:
    result=argparse.ArgumentParser(); commands=result.add_subparsers(dest="command",required=True)
    fixture=commands.add_parser("fixture"); fixture.add_argument("--output-root",type=Path,required=True); fixture.add_argument("--run-id",required=True)
    preflight=commands.add_parser("preflight"); preflight.add_argument("--model-root",type=Path,required=True)
    measured=commands.add_parser("measured"); measured.add_argument("--model-root",type=Path,required=True); measured.add_argument("--output-root",type=Path,required=True); measured.add_argument("--run-id",required=True)
    commands.add_parser("validate").add_argument("--run-directory",type=Path,required=True)
    evaluate=commands.add_parser("evaluate"); evaluate.add_argument("--run-directory",type=Path,required=True); evaluate.add_argument("--processed-root",type=Path,required=True)
    commands.add_parser("embedding-smoke").add_argument("--model-root",type=Path,required=True)
    return result


def main(argv=None) -> int:
    args=parser().parse_args(argv)
    if args.command=="fixture": run_fixture_campaign(DeterministicEmbeddingProvider(),args.output_root,args.run_id); return 0
    if args.command=="preflight": print(json.dumps(lock_model_assets(args.model_root),sort_keys=True)); return 0
    if args.command=="embedding-smoke":
        provider=OpenVinoGraniteEmbeddingProvider(args.model_root); values=provider.embed_documents(["Controlled Granite embedding smoke test."])
        print(json.dumps({"shape":list(values.shape),"finite":True,"norm":float((values[0]@values[0])**0.5),"device":provider.actual_device},sort_keys=True)); return 0
    if args.command=="measured":
        provider=OpenVinoGraniteEmbeddingProvider(args.model_root)
        manifest=json.loads((REPOSITORY_ROOT/"experiments/manifests/turbovec/feasibility-v1.json").read_text(encoding="utf-8"))
        identity={"model":lock_model_assets(args.model_root),"dependencies":manifest["dependencies"],"runtime_versions":{"openvino_genai":importlib.metadata.version("openvino-genai"),"openvino":importlib.metadata.version("openvino"),"turbovec":importlib.metadata.version("turbovec"),"numpy":importlib.metadata.version("numpy"),"psutil":importlib.metadata.version("psutil")},"requested_device":"CPU","actual_device":provider.actual_device,"source_commit":subprocess.check_output(["git","rev-parse","HEAD"],cwd=REPOSITORY_ROOT,text=True).strip(),"source_tree":subprocess.check_output(["git","rev-parse","HEAD^{tree}"],cwd=REPOSITORY_ROOT,text=True).strip()}
        run_live_campaign(provider,REPOSITORY_ROOT,args.output_root,args.run_id,identity); return 0
    if args.command=="evaluate": write_processed_results(args.run_directory,args.processed_root); return 0
    raise SystemExit(f"{args.command} requires locked live assets and is not a fixture command")


if __name__=="__main__": raise SystemExit(main())
