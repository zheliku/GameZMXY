"""Check planner-facing Luban enum values against their Chinese aliases.

Schema names, resource paths, framework group names and LegacyId provenance stay
technical. Data enum cells must use the alias declared by their own enum type;
the audit derives columns from __tables__/__beans__ instead of guessing from
matching English words. It never writes a workbook.
"""

import argparse
import json
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'LegacyMigration'))
from audit_official_level_dependencies import xlsx_rows


def cells(row):
    """Return column names without their physical row suffixes."""
    return {re.sub(r'\d+$', '', key): value for key, value in row.items()}


def enum_definitions(rows):
    """Keep aliases local to the declared enum, including repeated None names."""
    result = {}
    owner = None
    for raw in rows:
        row = cells(raw)
        owner = row.get('B') or owner
        if owner and row.get('H') and row['H'] != '*items' and not (row.get('A') or '').startswith('##'):
            result.setdefault(owner, {})[row['H']] = row.get('I')
    return result


def bean_definitions(rows):
    """Read field order and types from the same schema consumed by Luban."""
    result = {}
    owner = None
    for raw in rows:
        row = cells(raw)
        owner = row.get('B') or owner
        if owner and row.get('J') and row.get('L') and not (row.get('A') or '').startswith('##'):
            result.setdefault(owner, []).append((row['J'], row['L']))
    return result


def flattened_fields(name, beans):
    """Expand bean columns, including the first leaf of each nested list."""
    result = []
    for field, kind in beans[name]:
        element = kind.removeprefix('(list),').removeprefix('list,')
        if element in beans:
            result.extend(flattened_fields(element, beans))
        else:
            result.append((field, kind))
    return result


def inspect_sources(config_dir):
    """Return exact edits for English enum cells and all untranslated enum aliases."""
    books = {path.name: xlsx_rows(path) for path in sorted(config_dir.glob('*.xlsx'))}
    enums = enum_definitions(books['__enums__.xlsx']['Sheet1'])
    beans = bean_definitions(books['__beans__.xlsx']['Sheet1'])
    errors, edits, inspected = [], [], []
    for raw in books['__tables__.xlsx']['Sheet1']:
        table = cells(raw)
        if not table.get('E') or (table.get('A') or '').startswith('##'):
            continue
        for source in table['E'].split(','):
            if '@' in source:
                sheet, book = source.split('@')
            else:
                book = source
                sheet = next(iter(books[book]))
            rows = books[book][sheet]
            comment = next((raw for raw in rows if cells(raw).get('A') == '##'), None)
            if comment is None:
                raise ValueError(f'{book}/{sheet}: missing Chinese column descriptions')
            first_row = max(int(re.search(r'\d+$', key)[0]) for key in comment) + 1
            if table.get('D') in ('1', 'true'):
                type_row = cells(next(raw for raw in rows if cells(raw).get('A') == '##type'))
                fields = [(col, kind) for col, kind in type_row.items() if col != 'A' and kind]
            else:
                kind = table['C']
                if kind not in beans:
                    kind = table['B'].rsplit('.', 1)[0] + '.' + kind
                field_types = dict(flattened_fields(kind, beans))
                headers = {}
                for raw in rows:
                    row = cells(raw)
                    if row.get('A') == '##var':
                        headers.update({col: value.lstrip('*') for col, value in row.items() if col != 'A' and value})
                fields = [(col, field_types[field]) for col, field in headers.items()]
            comments = cells(comment)
            for col, kind in fields:
                if not re.search(r'[\u4e00-\u9fff]', comments.get(col) or ''):
                    errors.append(f'{book}/{sheet}/{col}: missing Chinese explanation')
                if kind not in enums:
                    continue
                choices = enums[kind]
                inspected.append({'book': book, 'sheet': sheet, 'column': col, 'type': kind})
                for raw in rows:
                    for address, value in raw.items():
                        if re.sub(r'\d+$', '', address) != col or int(re.search(r'\d+$', address)[0]) < first_row or value in (None, ''):
                            continue
                        if value in choices:
                            alias = choices[value]
                            if not alias or not re.search(r'[\u4e00-\u9fff]', alias):
                                errors.append(f'{book}/{sheet}/{address}: {kind}.{value} has no Chinese alias')
                            else:
                                edits.append({'book': book, 'sheet': sheet, 'cell': address, 'old': value, 'new': alias})
                        elif value not in choices.values():
                            errors.append(f'{book}/{sheet}/{address}: unknown {kind} value {value!r}')
    missing_aliases = [{'type': kind, 'name': name} for kind, values in enums.items()
                       for name, alias in values.items() if not alias or not re.search(r'[\u4e00-\u9fff]', alias)]
    return {'workbooks': len(books), 'enum_columns': inspected, 'english_enum_cells': edits,
            'missing_aliases': missing_aliases, 'errors': errors}


def main():
    """Report the full audit, optionally saving a read-only authoring plan."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--config-dir', type=Path, default=Path(__file__).resolve().parents[2] / 'Configs/GameConfig/Datas')
    parser.add_argument('--report', type=Path)
    args = parser.parse_args()
    report = inspect_sources(args.config_dir)
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(f"CONFIG READABILITY: {report['workbooks']} workbooks, {len(report['enum_columns'])} enum columns, "
          f"{len(report['english_enum_cells'])} English values, {len(report['missing_aliases'])} missing aliases, {len(report['errors'])} errors")
    for error in report['errors']:
        print(error)
    return int(bool(report['english_enum_cells'] or report['missing_aliases'] or report['errors']))


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    raise SystemExit(main())
