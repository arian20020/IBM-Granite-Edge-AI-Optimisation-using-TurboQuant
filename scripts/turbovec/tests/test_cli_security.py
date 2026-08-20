import base64
import csv
import hashlib
import io
import json
import tempfile
import unittest
import zipfile
from pathlib import Path
from types import SimpleNamespace
from unittest import mock

import numpy as np

from granite_turbovec.contracts import ResearchError


def _record_hash(data: bytes) -> str:
    return "sha256=" + base64.urlsafe_b64encode(hashlib.sha256(data).digest()).rstrip(b"=").decode("ascii")


class CliSecurityTests(unittest.TestCase):
    def _wheel_fixture(self, root: Path):
        installed = root / "site"; package = installed / "turbovec"; package.mkdir(parents=True)
        package_bytes = b"VERSION = 'approved'\n"; extension_bytes = b"native"
        (package / "__init__.py").write_bytes(package_bytes)
        (package / "core.pyd").write_bytes(extension_bytes)
        dist_info = installed / "turbovec-1.0.0.dist-info"; dist_info.mkdir()
        metadata = b"Metadata-Version: 2.1\nName: turbovec\nVersion: 1.0.0\n"
        wheel = root / "turbovec-1.0.0-py3-none-any.whl"
        rows = [
            ("turbovec/__init__.py", _record_hash(package_bytes), str(len(package_bytes))),
            ("turbovec/core.pyd", _record_hash(extension_bytes), str(len(extension_bytes))),
            ("turbovec-1.0.0.dist-info/METADATA", _record_hash(metadata), str(len(metadata))),
            ("turbovec-1.0.0.dist-info/RECORD", "", ""),
        ]
        buffer = io.StringIO(); csv.writer(buffer, lineterminator="\n").writerows(rows)
        with zipfile.ZipFile(wheel, "w", compression=zipfile.ZIP_DEFLATED) as archive:
            archive.writestr("turbovec/__init__.py", package_bytes)
            archive.writestr("turbovec/core.pyd", extension_bytes)
            archive.writestr("turbovec-1.0.0.dist-info/METADATA", metadata)
            archive.writestr("turbovec-1.0.0.dist-info/RECORD", buffer.getvalue().encode())
        distribution = SimpleNamespace(
            metadata={"Name": "turbovec", "Version": "1.0.0"},
            version="1.0.0",
            files=[Path("turbovec/__init__.py"), Path("turbovec/core.pyd"), Path("turbovec-1.0.0.dist-info/METADATA"), Path("turbovec-1.0.0.dist-info/RECORD")],
            locate_file=lambda relative: installed / relative,
        )
        spec = SimpleNamespace(origin=str(package / "__init__.py"), submodule_search_locations=[str(package)])
        return wheel, distribution, spec

    def test_approved_wheel_binds_installed_files_and_rejects_shadow_origin(self):
        from granite_turbovec.cli import _verify_wheel_distribution
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); wheel, distribution, spec = self._wheel_fixture(root)
            expected_digest = hashlib.sha256(wheel.read_bytes()).hexdigest()
            imported = SimpleNamespace(__file__=spec.origin, __path__=spec.submodule_search_locations)
            digest = _verify_wheel_distribution(wheel, distribution=distribution, module_spec=spec, imported_module=imported)
            shadow = root / "shadow" / "turbovec"; shadow.mkdir(parents=True)
            hostile = SimpleNamespace(origin=str(shadow / "__init__.py"), submodule_search_locations=[str(shadow)])
            with self.assertRaisesRegex(ResearchError, "environment-mismatch"):
                _verify_wheel_distribution(wheel, distribution=distribution, module_spec=hostile)
            with self.assertRaisesRegex(ResearchError, "environment-mismatch"):
                _verify_wheel_distribution(wheel, distribution=distribution, module_spec=spec, imported_module=SimpleNamespace(__file__=hostile.origin, __path__=hostile.submodule_search_locations))
            extra = root / "site" / "turbovec" / "shadow.py"; extra.write_text("hostile = True")
            distribution.files.append(Path("turbovec/shadow.py"))
            with self.assertRaisesRegex(ResearchError, "environment-mismatch"):
                _verify_wheel_distribution(wheel, distribution=distribution, module_spec=spec)
        self.assertEqual(expected_digest, digest)

    def test_npy_preflight_rejects_untrusted_headers_before_numpy_loader(self):
        from granite_turbovec.cli import _load_validated_npy
        cases = [
            {"descr": "<f4", "fortran_order": False, "shape": (2**40, 384)},
            {"descr": "<f8", "fortran_order": False, "shape": (1, 384)},
            {"descr": "<f4", "fortran_order": True, "shape": (1, 384)},
        ]
        for header in cases:
            with self.subTest(header=header), tempfile.TemporaryDirectory() as temporary:
                path = Path(temporary) / "bad.npy"
                text = repr(header).encode("latin1") + b"\n"
                padding = (-((10 + len(text)) % 16)) % 16
                text = text[:-1] + b" " * padding + b"\n"
                path.write_bytes(b"\x93NUMPY\x01\x00" + len(text).to_bytes(2, "little") + text)
                loader = mock.Mock(side_effect=AssertionError("np.load reached"))
                with self.assertRaises(ResearchError):
                    _load_validated_npy(path, dtype=np.dtype("<f4"), shape=(1, 384), loader=loader)
                loader.assert_not_called()

    def test_npy_preflight_rejects_bad_version_truncated_and_extra_bodies_before_loader(self):
        from granite_turbovec.cli import _load_validated_npy
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); valid = root / "valid.npy"
            with valid.open("xb") as stream:
                np.save(stream, np.zeros((1, 384), dtype=np.float32), allow_pickle=False)
            payload = valid.read_bytes()
            cases = {
                "version": payload[:6] + b"\x04\x00" + payload[8:],
                "truncated": payload[:-1],
                "extra": payload + b"x",
            }
            for name, data in cases.items():
                with self.subTest(name=name):
                    path = root / f"{name}.npy"; path.write_bytes(data)
                    loader = mock.Mock(side_effect=AssertionError("np.load reached"))
                    with self.assertRaises(ResearchError):
                        _load_validated_npy(path, dtype=np.dtype("<f4"), shape=(1, 384), loader=loader)
                    loader.assert_not_called()
