#!/usr/bin/env python3
"""Print a Luban xlsx's header rows and data rows (read-only, for verification).

Usage: python Tools/ConfigBootstrap/dump_table.py Hero.xlsx HeroGrowthConfig
"""

from pathlib import Path
import posixpath
import re
import sys
import xml.etree.ElementTree as ET
import zipfile

DATAS = Path(__file__).resolve().parents[2] / "Configs/GameConfig/Datas"
NS = {"s": "http://schemas.openxmlformats.org/spreadsheetml/2006/main"}
REL = "http://schemas.openxmlformats.org/officeDocument/2006/relationships"


def column(address):
    """Convert a cell's column letters to a zero-based index."""
    result = 0
    for letter in re.match(r"[A-Z]+", address)[0]:
        result = result * 26 + ord(letter) - ord("A") + 1
    return result - 1


def value(cell, strings):
    """Read typed values and formula text without requiring an Excel package."""
    formula = cell.find("s:f", NS)
    if formula is not None:
        return "=" + (formula.text or "")
    raw = cell.find("s:v", NS)
    text = raw.text if raw is not None else None
    kind = cell.get("t")
    if kind == "s":
        return strings[int(text)]
    if kind == "inlineStr":
        return "".join(part.text or "" for part in cell.findall(".//s:t", NS))
    if kind == "b":
        return text == "1"
    if text is not None and kind not in ("str", "e"):
        number = float(text)
        return int(number) if number.is_integer() else number
    return text


def main():
    """Print the first twelve rows of the requested workbook sheet."""
    sys.stdout.reconfigure(encoding="utf-8")
    name = sys.argv[1]
    with zipfile.ZipFile(DATAS / name) as workbook:
        sheets = ET.fromstring(workbook.read("xl/workbook.xml")).findall("s:sheets/s:sheet", NS)
        selected = sys.argv[2] if len(sys.argv) > 2 else sheets[0].get("name")
        sheet = next((item for item in sheets if item.get("name") == selected), None)
        if sheet is None:
            raise SystemExit(f"Unknown sheet {selected}; choices: {[item.get('name') for item in sheets]}")
        relationships = {item.get("Id"): item.get("Target") for item in ET.fromstring(workbook.read("xl/_rels/workbook.xml.rels"))}
        target = relationships[sheet.get("{" + REL + "}id")]
        target = target.lstrip("/") if target.startswith("/") else posixpath.normpath("xl/" + target)
        strings = []
        if "xl/sharedStrings.xml" in workbook.namelist():
            strings = ["".join(part.text or "" for part in item.findall(".//s:t", NS))
                       for item in ET.fromstring(workbook.read("xl/sharedStrings.xml")).findall("s:si", NS)]
        rows = ET.fromstring(workbook.read(target)).findall("s:sheetData/s:row", NS)
        width = max((column(cell.get("r")) + 1 for row in rows for cell in row.findall("s:c", NS)), default=0)
        print(f"{name} / {selected}")
        for row in rows:
            if int(row.get("r")) > 12:
                break
            values = [None] * width
            for cell in row.findall("s:c", NS):
                values[column(cell.get("r"))] = value(cell, strings)
            if any(item is not None for item in values):
                print(f"{int(row.get('r')):3d}: {values}")


if __name__ == "__main__":
    main()
