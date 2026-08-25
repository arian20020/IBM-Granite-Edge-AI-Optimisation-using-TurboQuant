import os
import shutil
import subprocess
import tempfile
import textwrap
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
ACQUISITION_SCRIPT = (
    REPOSITORY_ROOT
    / "scripts"
    / "hardware-inspection"
    / "Acquire-HardwareInspectionLlmFitCandidate.ps1"
)


def powershell_executable():
    for name in ("powershell.exe", "powershell", "pwsh.exe", "pwsh"):
        candidate = shutil.which(name)
        if candidate:
            return candidate
    raise unittest.SkipTest("PowerShell executable is not available")


class LlmFitCandidateAcquisitionTests(unittest.TestCase):
    def run_copy_probe(self, probe):
        with tempfile.TemporaryDirectory() as directory:
            destination = Path(directory) / "payload.bin"
            environment = os.environ.copy()
            environment["GRANITE_ACQUISITION_TEST_SCRIPT"] = str(ACQUISITION_SCRIPT)
            environment["GRANITE_ACQUISITION_TEST_DESTINATION"] = str(destination)
            return subprocess.run(
                [
                    powershell_executable(),
                    "-NoProfile",
                    "-NonInteractive",
                    "-ExecutionPolicy",
                    "Bypass",
                    "-Command",
                    textwrap.dedent(probe),
                ],
                env=environment,
                text=True,
                capture_output=True,
                timeout=20,
                check=False,
            )

    def test_archive_member_copy_accepts_forward_only_zip_entry_stream(self):
        """Catch regressions that attempt to seek a ZIP member before copying it."""
        result = self.run_copy_probe(
            r"""
            $source = Get-Content -LiteralPath $env:GRANITE_ACQUISITION_TEST_SCRIPT -Raw
            $mainStart = $source.IndexOf('$downloadTemp = $null')
            if ($mainStart -lt 0) { throw 'Unable to isolate acquisition functions.' }
            Invoke-Expression $source.Substring(0, $mainStart)

            $archiveBytes = New-Object System.IO.MemoryStream
            $writer = New-Object System.IO.Compression.ZipArchive(
                $archiveBytes,
                [System.IO.Compression.ZipArchiveMode]::Create,
                $true)
            $entry = $writer.CreateEntry('payload.bin')
            $entryWriter = $entry.Open()
            try {
                $entryWriter.Write([byte[]](0x47, 0x72, 0x61, 0x6e, 0x69, 0x74, 0x65), 0, 7)
            }
            finally {
                $entryWriter.Dispose()
                $writer.Dispose()
            }

            $archiveBytes.Position = 0
            $reader = New-Object System.IO.Compression.ZipArchive(
                $archiveBytes,
                [System.IO.Compression.ZipArchiveMode]::Read,
                $false)
            $entryReader = $reader.GetEntry('payload.bin').Open()
            try {
                if ($entryReader.CanSeek) { throw 'Probe did not create a forward-only stream.' }
                Copy-StreamToNewFile `
                    -SourceStream $entryReader `
                    -DestinationPath $env:GRANITE_ACQUISITION_TEST_DESTINATION
            }
            finally {
                $entryReader.Dispose()
                $reader.Dispose()
            }

            $actual = [System.IO.File]::ReadAllBytes(
                $env:GRANITE_ACQUISITION_TEST_DESTINATION)
            if ([System.Text.Encoding]::ASCII.GetString($actual) -cne 'Granite') {
                throw 'Copied archive member bytes changed.'
            }
            """
        )

        self.assertEqual(0, result.returncode, result.stderr)

    def test_archive_copy_rewinds_seekable_stream_after_validation_reads(self):
        """Catch regressions that publish only the unread tail of the archive."""
        result = self.run_copy_probe(
            r"""
            $source = Get-Content -LiteralPath $env:GRANITE_ACQUISITION_TEST_SCRIPT -Raw
            $mainStart = $source.IndexOf('$downloadTemp = $null')
            if ($mainStart -lt 0) { throw 'Unable to isolate acquisition functions.' }
            Invoke-Expression $source.Substring(0, $mainStart)

            $archive = [System.IO.MemoryStream]::new(
                [byte[]](0x47, 0x72, 0x61, 0x6e, 0x69, 0x74, 0x65))
            $archive.Position = $archive.Length
            try {
                Copy-StreamToNewFile `
                    -SourceStream $archive `
                    -DestinationPath $env:GRANITE_ACQUISITION_TEST_DESTINATION
            }
            finally {
                $archive.Dispose()
            }

            $actual = [System.IO.File]::ReadAllBytes(
                $env:GRANITE_ACQUISITION_TEST_DESTINATION)
            if ([System.Text.Encoding]::ASCII.GetString($actual) -cne 'Granite') {
                throw 'The complete seekable stream was not copied.'
            }
            """
        )

        self.assertEqual(0, result.returncode, result.stderr)


if __name__ == "__main__":
    unittest.main()
