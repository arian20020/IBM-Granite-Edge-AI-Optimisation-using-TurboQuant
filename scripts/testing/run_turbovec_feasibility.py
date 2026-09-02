"""CLI entry point for the controlled TurboVec feasibility campaign."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from scripts.testing.turbovec.embedding import DeterministicEmbeddingProvider, lock_model_assets
from scripts.testing.turbovec.runner import run_fixture_campaign


def parser() -> argparse.ArgumentParser:
    result=argparse.ArgumentParser(); commands=result.add_subparsers(dest="command",required=True)
    fixture=commands.add_parser("fixture"); fixture.add_argument("--output-root",type=Path,required=True); fixture.add_argument("--run-id",required=True)
    preflight=commands.add_parser("preflight"); preflight.add_argument("--model-root",type=Path,required=True)
    commands.add_parser("validate").add_argument("--run-directory",type=Path,required=True)
    commands.add_parser("evaluate").add_argument("--run-directory",type=Path,required=True)
    commands.add_parser("embedding-smoke").add_argument("--model-root",type=Path,required=True)
    return result


def main(argv=None) -> int:
    args=parser().parse_args(argv)
    if args.command=="fixture": run_fixture_campaign(DeterministicEmbeddingProvider(),args.output_root,args.run_id); return 0
    if args.command=="preflight": print(json.dumps(lock_model_assets(args.model_root),sort_keys=True)); return 0
    raise SystemExit(f"{args.command} requires locked live assets and is not a fixture command")


if __name__=="__main__": raise SystemExit(main())
