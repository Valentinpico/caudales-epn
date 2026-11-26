"""Extrae el texto de PDF del proyecto a Markdown para el corpus del asistente documental.

Se ejecuta fuera de línea y el Markdown generado se versiona en docs/corpus/: el backend solo
indexa Markdown, así no necesita una biblioteca de PDF para el corpus permanente.

Uso:
    python3 scripts/corpus_pdf/extract_pdfs.py                       # la postulación del proyecto
    python3 scripts/corpus_pdf/extract_pdfs.py ruta.pdf --source /files/documentos/x.pdf

Backend de texto: pypdf si está instalado; si no, la herramienta `pdftotext` (poppler).
Los números de cédula y los correos electrónicos se reemplazan: el asistente es público.
"""

from __future__ import annotations

import argparse
import re
import shutil
import subprocess
import unicodedata
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_PDF = "info/Postulación Proyecto Caudales.pdf"
DEFAULT_SOURCE = "/files/documentos/propuesta/postulacion-proyecto-caudales.pdf"
DEFAULT_TITLE = "Postulación del proyecto de investigación PISF-25-03"
OUT_DIR = REPO_ROOT / "docs" / "corpus"

_WS = re.compile(r"[ \t]+")
_NUMBERED = re.compile(r"^\d+(\.\d+)*\.?\s+(?P<rest>\S.*)$")
_CEDULA = re.compile(r"\b\d{10}\b")
_EMAIL = re.compile(r"[\w.+-]+@[\w-]+(\.[\w-]+)+")
_DIGITS = re.compile(r"\d+")


def slugify(text: str) -> str:
    ascii_only = unicodedata.normalize("NFKD", text).encode("ascii", "ignore").decode("ascii")
    return re.sub(r"[^a-z0-9]+", "-", ascii_only.lower()).strip("-")


def redact(text: str) -> str:
    return _EMAIL.sub("[correo omitido]", _CEDULA.sub("[cédula omitida]", text))


def is_heading(line: str) -> bool:
    """Sección numerada que empieza en mayúscula: "3.2. Resumen del proyecto".

    Las líneas en mayúsculas no se promueven: en el formulario de postulación son valores
    ("SEMILLA", "DEPARTAMENTO DE MATEMATICA") y convertirlas en encabezado los sacaría del texto.
    """
    s = line.strip()
    if not s or len(s) > 90 or (s.endswith((")", "]")) and "(" not in s and "[" not in s):
        return False
    m = _NUMBERED.match(s)
    return bool(m) and sum(c.isalpha() for c in m.group("rest")) >= 3 and m.group("rest")[0].isupper()


def _key(line: str) -> str:
    normalized = _WS.sub(" ", line).strip().lower()
    # Page markers ("7", "Página 3 de 12") compare equal whatever the number.
    return _DIGITS.sub("#", normalized) if len(_DIGITS.sub("", normalized).strip()) <= 30 else normalized


def drop_boilerplate(pages: list[str]) -> list[str]:
    """Quita membretes y números de página: líneas cortas repetidas en al menos la mitad de las páginas."""
    if len(pages) < 3:
        return pages
    counts: dict[str, int] = {}
    for page in pages:
        for key in {_key(l) for l in page.split("\n") if l.strip() and len(l.strip()) <= 90}:
            counts[key] = counts.get(key, 0) + 1
    repeated = {k for k, n in counts.items() if k and n >= len(pages) / 2}
    return ["\n".join(l for l in p.split("\n") if not l.strip() or _key(l) not in repeated) for p in pages]


def to_markdown(title: str, pages: list[str], source: str | None) -> str:
    body = []
    for page in drop_boilerplate(pages):
        lines = [l.rstrip() for l in page.replace("\r\n", "\n").split("\n")]
        body.append("\n".join(f"## {l.strip()}" if is_heading(l) else l for l in lines).strip())
    text = re.sub(r"\n{3,}", "\n\n", "\n\n".join(b for b in body if b))
    header = f"<!-- source: {source} -->\n" if source else ""
    return f"{header}# {title}\n\n{redact(text)}\n"


def extract_pages(pdf: Path) -> list[str]:
    try:
        from pypdf import PdfReader
        return [p.extract_text() or "" for p in PdfReader(str(pdf)).pages]
    except ImportError:
        pass
    if not shutil.which("pdftotext"):
        raise SystemExit("Instala pypdf (pip install pypdf) o poppler (pdftotext).")
    out = subprocess.run(["pdftotext", "-enc", "UTF-8", str(pdf), "-"], capture_output=True, text=True, check=True).stdout
    pages = out.split("\f")
    return pages[:-1] if pages and not pages[-1].strip() else pages


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("pdf", nargs="?", default=DEFAULT_PDF)
    parser.add_argument("--title", default=None)
    parser.add_argument("--source", default=None, help="Enlace que se mostrará en las citas.")
    args = parser.parse_args()

    pdf = Path(args.pdf) if Path(args.pdf).is_absolute() else REPO_ROOT / args.pdf
    is_default = args.pdf == DEFAULT_PDF
    title = args.title or (DEFAULT_TITLE if is_default else pdf.stem.replace("_", " "))
    source = args.source or (DEFAULT_SOURCE if is_default else None)

    pages = extract_pages(pdf)
    markdown = to_markdown(title, pages, source)
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    out = OUT_DIR / f"{slugify(title)}.md"
    out.write_text(markdown, encoding="utf-8")
    print(f"{pdf.name} -> {out.relative_to(REPO_ROOT)} ({len(pages)} páginas, {len(markdown)} caracteres)")


if __name__ == "__main__":
    main()
