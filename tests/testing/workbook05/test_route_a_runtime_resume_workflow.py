from __future__ import annotations

import re
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
WORKFLOW_PATH = (
    REPOSITORY_ROOT / ".github/workflows/workbook-05-runtime-resume.yml"
)


class RouteARuntimeResumeWorkflowTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.workflow = WORKFLOW_PATH.read_text(encoding="utf-8")

    def _job_block(self, job_id: str) -> str:
        marker = f"  {job_id}:\n"
        start = self.workflow.index(marker)
        content_start = start + len(marker)
        next_job = re.search(
            r"(?m)^  [A-Za-z0-9_-]+:\s*$",
            self.workflow[content_start:],
        )
        end = (
            len(self.workflow)
            if next_job is None
            else content_start + next_job.start()
        )
        return self.workflow[start:end]

    def test_manual_inputs_are_bound_to_the_exact_timeout_prerequisite(self) -> None:
        required = (
            "workflow_dispatch:",
            "resume_workspace:",
            r"default: 'C:\w5a\phase2-31391119557-4'",
            "resume_run_id:",
            "default: '31391119557'",
            "resume_run_attempt:",
            "default: 4",
            "resume_artifact_name:",
            "default: 'workbook-05-build-route-a-runtime-31391119557-4'",
            "resume_expected_artifact_digest:",
            (
                "default: 'sha256:"
                "b2b9f0ca8528f1d6135d03c77ac964f8799ffc8989cc1a03515a6d5c7c73c1a4'"
            ),
            "resume_actual_artifact_digest:",
            "expected_cache_sha256:",
            (
                "default: '"
                "b5c0606efa261a9525f5562eb973919d508f5242a567f82a46d0b0b9df725c77'"
            ),
            "resume_head_sha:",
            "default: 'fb5d3349aae9d7acb6bd8132cbffa2303e40cabd'",
        )
        for token in required:
            with self.subTest(token=token):
                self.assertIn(token, self.workflow)

    def test_workflow_is_read_only_pinned_and_non_cancelling(self) -> None:
        self.assertIn("contents: read", self.workflow)
        self.assertIn("actions: read", self.workflow)
        self.assertIn("cancel-in-progress: false", self.workflow)
        self.assertIn("persist-credentials: false", self.workflow)
        self.assertNotIn("contents: write", self.workflow)
        self.assertNotIn("pull-requests: write", self.workflow)

        uses_lines = [
            line.strip()
            for line in self.workflow.splitlines()
            if "uses:" in line
        ]
        self.assertGreaterEqual(len(uses_lines), 5)
        for line in uses_lines:
            with self.subTest(line=line):
                self.assertRegex(
                    line,
                    r"uses:\s+actions/[A-Za-z0-9_-]+@[0-9a-f]{40}$",
                )

    def test_self_hosted_resume_job_has_exact_guard_labels_and_timeout(self) -> None:
        block = self._job_block("collect-route-a-runtime-resume")
        self.assertIn("github.event_name == 'workflow_dispatch'", block)
        self.assertIn(
            (
                "github.repository == "
                "'arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant'"
            ),
            block,
        )
        self.assertIn(
            "runs-on: [self-hosted, Windows, X64, workbook05, intel-target]",
            block,
        )
        self.assertIn("timeout-minutes: 720", block)
        self.assertIn(
            (
                'shell: powershell -NoLogo -NoProfile -ExecutionPolicy Bypass '
                '-Command ". \'{0}\'"'
            ),
            block,
        )

    def test_prior_artifact_identity_is_checked_before_download_and_resume(self) -> None:
        block = self._job_block("collect-route-a-runtime-resume")
        identity = block.index("Assert-Workbook05ArtifactIdentity.ps1")
        download = block.index(
            "actions/download-artifact@fa0a91b85d4f404e444e00e005971372dc801d16"
        )
        resume = block.index("Invoke-Workbook05RouteARuntimeResume.ps1")
        self.assertLess(identity, download)
        self.assertLess(download, resume)
        self.assertIn("github-token: ${{ secrets.GITHUB_TOKEN }}", block)
        self.assertIn("repository: ${{ github.repository }}", block)
        self.assertIn("run-id: ${{ inputs.resume_run_id }}", block)
        self.assertIn(
            "WB05_RESUME_RUN_ATTEMPT: ${{ inputs.resume_run_attempt }}",
            block,
        )
        self.assertIn(
            "-ResumeRunAttempt $env:WB05_RESUME_RUN_ATTEMPT",
            block,
        )
        self.assertIn(
            "-ResumeWorkspaceDirectory $env:WB05_RESUME_WORKSPACE",
            block,
        )
        self.assertIn(
            "-ExpectedArtifactDigest $env:WB05_RESUME_EXPECTED_DIGEST",
            block,
        )
        self.assertIn("InternalDeadlineSeconds 39600", block)

    def test_new_evidence_is_bound_uploaded_and_independently_validated(self) -> None:
        collector = self._job_block("collect-route-a-runtime-resume")
        validator = self._job_block("validate-route-a-runtime-resume")
        self.assertIn("Write-Workbook05BuildBundleMetadata.ps1", collector)
        self.assertIn("if: always()", collector)
        self.assertIn("actions/upload-artifact@", collector)
        self.assertIn(
            (
                "workbook-05-build-route-a-runtime-resume-"
                "${{ github.run_id }}-${{ github.run_attempt }}"
            ),
            collector,
        )
        self.assertIn("needs: collect-route-a-runtime-resume", validator)
        self.assertIn("runs-on: windows-latest", validator)
        self.assertIn("actions/download-artifact@", validator)
        self.assertIn(
            "scripts.testing.workbook05.build_bundle_validation",
            validator,
        )
        self.assertIn("--component runtime", validator)
        self.assertIn("--run-id '${{ github.run_id }}'", validator)
        self.assertIn("--run-attempt '${{ github.run_attempt }}'", validator)

    def test_workflow_does_not_run_models_or_modify_repository_state(self) -> None:
        forbidden = (
            "huggingface.co",
            ".gguf",
            ".safetensors",
            "benchmark_app",
            "git push",
            "gh pr merge",
            "Invoke-Expression",
            "git reset --hard",
            "git clean",
        )
        for token in forbidden:
            with self.subTest(token=token):
                self.assertNotIn(token.lower(), self.workflow.lower())


if __name__ == "__main__":
    unittest.main()
