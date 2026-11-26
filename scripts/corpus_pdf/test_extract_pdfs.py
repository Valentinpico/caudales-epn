"""python3 -m unittest scripts/corpus_pdf/test_extract_pdfs.py"""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from extract_pdfs import drop_boilerplate, is_heading, redact, slugify, to_markdown  # noqa: E402


class ExtractPdfsTests(unittest.TestCase):
    def test_headings(self):
        self.assertTrue(is_heading("3.2. Resumen del proyecto"))
        self.assertFalse(is_heading("SEMILLA"))
        self.assertFalse(is_heading("30 cm)"))
        self.assertFalse(is_heading("27.32 g/kg"))
        self.assertFalse(is_heading("texto normal de un párrafo"))

    def test_boilerplate_is_dropped_by_repetition(self):
        words = ["uno", "dos", "tres", "cuatro"]
        pages = [f"ESCUELA POLITÉCNICA NACIONAL\ntexto propio de la página {w}\n{i}" for i, w in enumerate(words, 1)]
        cleaned = drop_boilerplate(pages)
        self.assertEqual(cleaned[0].strip(), "texto propio de la página uno")

    def test_redacts_ids_and_emails(self):
        self.assertEqual(redact("Cédula 1234567890, a@b.edu.ec"), "Cédula [cédula omitida], [correo omitido]")

    def test_markdown_has_source_title_and_sections(self):
        md = to_markdown("Postulación", ["1. Información básica\ntexto"], "/files/p.pdf")
        self.assertTrue(md.startswith("<!-- source: /files/p.pdf -->\n# Postulación\n\n## 1. Información básica\ntexto"))

    def test_slugify(self):
        self.assertEqual(slugify("Postulación del proyecto PISF-25-03"), "postulacion-del-proyecto-pisf-25-03")


if __name__ == "__main__":
    unittest.main()
