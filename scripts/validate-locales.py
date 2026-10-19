"""Validate translation catalogs using only the Python 3 standard library."""
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent


def read_catalog(path):
    root = ET.parse(path).getroot()
    if root.tag != "language" or root.get("culture") != path.stem:
        raise ValueError(f"{path.name}: culture must match filename")
    if root.get("{http://www.w3.org/XML/1998/namespace}space") != "preserve":
        raise ValueError(f"{path.name}: keep xml:space=\"preserve\"")
    entries = {}
    for node in root:
        key = node.get("key")
        if node.tag != "string" or not key or key in entries or len(node):
            raise ValueError(f"{path.name}: invalid or duplicate string key {key!r}")
        entries[key] = node.text or ""
    return entries


def main():
    english = read_catalog(ROOT / "locales/en.xml")
    if not english or any(not text for text in english.values()):
        raise ValueError("en.xml: reference translations must not be empty")
    references = set()
    for source in ROOT.glob("*.cs"):
        references.update(re.findall(r'\b(?:Ui\.)?T\("([^"]+)"\)', source.read_text(encoding="utf-8-sig")))
    if references - english.keys():
        raise ValueError(f"Missing English keys: {sorted(references - english.keys())}")
    for path in sorted((ROOT / "locales").glob("*.xml")):
        entries = read_catalog(path)
        unknown = entries.keys() - english.keys()
        if unknown:
            raise ValueError(f"{path.name}: unknown keys {sorted(unknown)}")
        translated = sum(bool(entries.get(key)) for key in english)
        print(f"{path.name}: {translated}/{len(english)} translated; "
              f"{len(english) - translated} use English fallback")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, ET.ParseError, OSError) as error:
        print(error, file=sys.stderr)
        sys.exit(1)
