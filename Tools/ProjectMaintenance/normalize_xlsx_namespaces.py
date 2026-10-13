"""Make SpreadsheetML tags readable by XLSX Diff without changing workbook content."""

import argparse
from pathlib import Path
import re
import tempfile
import xml.etree.ElementTree as ET
import zipfile


SPREADSHEET_NS = "http://schemas.openxmlformats.org/spreadsheetml/2006/main"


def xml_content(element):
    """Compare expanded XML names, attributes, text and child order, ignoring aliases."""
    return (element.tag, sorted(element.attrib.items()), element.text, element.tail,
            tuple(xml_content(child) for child in element))


def normalize_xml(content):
    """Use the default SpreadsheetML namespace, preserving any prefixed attributes."""
    root = ET.fromstring(content)
    if not root.tag.startswith("{" + SPREADSHEET_NS + "}"):
        return content
    text = content.decode("utf-8-sig")
    declaration = re.search(r'xmlns:([\w]+)=([\'"])' + re.escape(SPREADSHEET_NS) + r'\2', text)
    if declaration is None:
        return content
    prefix = declaration.group(1)
    if not re.search(r'<'+re.escape(prefix)+r':', text):
        return content
    default = re.search(r'\sxmlns=([\'"])(.*?)\1', text)
    if default is not None and default.group(2) != SPREADSHEET_NS:
        raise ValueError("Conflicting default namespace; refusing to rewrite this XML part")
    if default is None:
        text = text[:declaration.start()] + f'xmlns="{SPREADSHEET_NS}" ' + text[declaration.start():]
    # Keep xmlns:prefix so attributes and QName-valued metadata retain their bindings.
    text = re.sub(r'(<\/?)[\s]*' + re.escape(prefix) + ':', r'\1', text)
    repaired = text.encode("utf-8")
    if xml_content(root) != xml_content(ET.fromstring(repaired)):
        raise ValueError("XML content changed; refusing to replace workbook")
    return repaired


def normalize_workbook(path, write=False):
    """Repair only affected XML parts, atomically preserving all other ZIP entries."""
    with zipfile.ZipFile(path) as source:
        entries = [(info, source.read(info.filename)) for info in source.infolist()]
        comment = source.comment
    replacements = {}
    for info, content in entries:
        if not info.filename.endswith(".xml"):
            continue
        repaired = normalize_xml(content)
        if repaired != content:
            replacements[info.filename] = repaired
    if write and replacements:
        with tempfile.NamedTemporaryFile(dir=path.parent, suffix=".xlsx.tmp", delete=False) as temporary:
            temporary_path = Path(temporary.name)
        try:
            with zipfile.ZipFile(temporary_path, "w") as target:
                target.comment = comment
                for info, content in entries:
                    target.writestr(info, replacements.get(info.filename, content))
            with zipfile.ZipFile(temporary_path) as target:
                if target.testzip() is not None:
                    raise ValueError("Repaired archive failed CRC verification")
            temporary_path.replace(path)
        finally:
            temporary_path.unlink(missing_ok=True)
    return sorted(replacements)


def main():
    """Check supplied workbooks or repair them when explicitly requested."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("paths", nargs="+", type=Path)
    parser.add_argument("--write", action="store_true", help="Apply verified namespace-only repairs")
    args = parser.parse_args()
    changed = 0
    for path in args.paths:
        parts = normalize_workbook(path, args.write)
        if parts:
            changed += 1
            print(f'{"REPAIRED" if args.write else "NEEDS REPAIR"}: {path.name} ({len(parts)} XML parts)')
    print(f'{changed} workbook(s) {"repaired" if args.write else "need repair"}')


if __name__ == "__main__":
    main()
