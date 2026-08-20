import tempfile
import unittest
from pathlib import Path

from granite_turbovec.contracts import ResearchError
from granite_turbovec.tvim_container import extract_validated_payload, write_container


class TvimContainerTests(unittest.TestCase):
    def test_round_trip_binds_payload_and_route_metadata(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); raw = root / "raw"; wrapped = root / "index-4bit.tvim"
            raw.write_bytes(b"upstream-turbovec")
            write_container(raw, wrapped, bits=4, dimension=384, count=7)
            with extract_validated_payload(wrapped, bits=4, dimension=384, count=7) as extracted:
                self.assertEqual(b"upstream-turbovec", extracted.read_bytes())
                self.assertNotEqual(wrapped, extracted)
            self.assertFalse(extracted.exists())

    def test_bad_header_fields_and_payload_hash_fail_before_consumer(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); raw = root / "raw"; wrapped = root / "index.tvim"
            raw.write_bytes(b"payload")
            write_container(raw, wrapped, bits=2, dimension=384, count=3)
            original = wrapped.read_bytes()
            cases = {
                "magic": b"BAD!" + original[4:],
                "version": original[:4] + bytes([9]) + original[5:],
                "length": original[:20] + (999).to_bytes(8, "big") + original[28:],
                "payload-hash": original[:-1] + bytes([original[-1] ^ 1]),
            }
            for name, value in cases.items():
                with self.subTest(name=name):
                    wrapped.write_bytes(value)
                    with self.assertRaises(ResearchError) as context:
                        with extract_validated_payload(wrapped, bits=2, dimension=384, count=3):
                            self.fail("consumer reached")
                    self.assertEqual("index-artifact-invalid", context.exception.code)
                    wrapped.write_bytes(original)

    def test_expected_bits_dimension_count_rejected_before_extraction(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); raw = root / "raw"; wrapped = root / "index.tvim"
            raw.write_bytes(b"payload"); write_container(raw, wrapped, bits=4, dimension=384, count=3)
            for kwargs in ({"bits": 2, "dimension": 384, "count": 3}, {"bits": 4, "dimension": 3, "count": 3}, {"bits": 4, "dimension": 384, "count": 4}):
                with self.subTest(kwargs=kwargs), self.assertRaises(ResearchError) as context:
                    with extract_validated_payload(wrapped, **kwargs):
                        self.fail("consumer reached")
                self.assertEqual("index-artifact-invalid", context.exception.code)


if __name__ == "__main__":
    unittest.main()
