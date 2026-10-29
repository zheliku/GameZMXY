"""Audit every Luban source sheet, including nested bean tables.

The audit distinguishes tables whose types are declared by a ``##type`` row
from ``read_schema_from_file=false`` tables whose types come from __beans__.xlsx.
It never edits a workbook.
"""
from pathlib import Path
from zipfile import ZipFile
import argparse, json, re, sys, math
import xml.etree.ElementTree as E
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'LegacyMigration'))
from audit_official_level_dependencies import xlsx_rows

ROOT = Path(__file__).resolve().parents[2]

def cell_rows(rows):
    return [{re.sub(r'\d+$', '', key): value for key, value in row.items()} for row in rows]

def schema(rows):
    return [row for row in cell_rows(rows) if not str(row.get('A') or '').startswith('##')]

def beans(rows):
    result, current = {}, None
    for row in cell_rows(rows):
        if row.get('B'):
            current = row['B']
        if current and row.get('J') and row.get('L') and not str(row.get('A') or '').startswith('##'):
            result.setdefault(current, []).append((row['J'], row['L']))
    return result

def leaves(name, definitions):
    values = []
    for field, kind in definitions.get(name, []):
        element = str(kind).removeprefix('(list),').removeprefix('list,')
        if element in definitions:
            values.extend(leaves(element, definitions))
        else:
            values.append((field, kind))
    return values

def rows_by_number(rows):
    result = {}
    for raw in rows:
        if not raw:
            continue
        number = int(re.search(r'\d+$', next(iter(raw)))[0])
        result[number] = {re.sub(r'\d+$', '', key): value for key, value in raw.items()}
    return result

def is_blank(value):
    return value in (None, '')

def validate_value(value, kind):
    if is_blank(value):
        return None
    kind = str(kind)
    if kind == 'bool':
        # SpreadsheetML stores native booleans as t="b" with 0/1 payloads.
        # The lightweight reader exposes that payload as text, so accept only
        # those two native values in addition to Artifact's Python booleans.
        return None if isinstance(value, bool) or str(value) in ('0', '1', 'true', 'false') else f'boolean value is {value!r}'
    if kind == 'int':
        try:
            return None if int(str(value)) == float(str(value)) else f'int value is {value!r}'
        except (ValueError, TypeError):
            return f'int value is {value!r}'
    if kind == 'float':
        try:
            return None if math.isfinite(float(str(value))) else f'nonfinite float {value!r}'
        except (ValueError, TypeError):
            return f'float value is {value!r}'
    if kind.startswith('vector2') or kind.startswith('(array'):
        try:
            numbers = [float(item.strip()) for item in str(value).split(',')]
            if not all(math.isfinite(item) for item in numbers):
                return f'nonfinite vector/array {value!r}'
            if kind.startswith('vector2') and len(numbers) != 2:
                return f'vector2 needs two values: {value!r}'
            if kind == 'vector2i' and any(not item.is_integer() for item in numbers):
                return f'vector2i needs integer values: {value!r}'
            return None
        except ValueError:
            return f'invalid vector/array {value!r}'
    return None

def cell_kinds(path):
    """Read real Excel cell storage, distinguishing booleans from numeric 0/1."""
    ns = {'s': 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
    relation = '{http://schemas.openxmlformats.org/officeDocument/2006/relationships}id'
    with ZipFile(path) as archive:
        rels = {item.get('Id'): item.get('Target') for item in E.fromstring(archive.read('xl/_rels/workbook.xml.rels'))}
        result = {}
        for sheet in E.fromstring(archive.read('xl/workbook.xml')).find('s:sheets', ns):
            part = rels[sheet.get(relation)]
            part = part.lstrip('/') if part.startswith('/') else 'xl/' + part
            result[sheet.get('name')] = {cell.get('r'): cell.get('t', 'n')
                for cell in E.fromstring(archive.read(part)).findall('.//s:sheetData/s:row/s:c', ns)}
        return result

def audit(config_dir):
    books = {p.name: xlsx_rows(p) for p in sorted(config_dir.glob('*.xlsx'))}
    storage = {name: cell_kinds(config_dir/name) for name in books}
    table_rows = books['__tables__.xlsx']['Sheet1']
    table_defs = {}
    for row in cell_rows(table_rows):
        if row.get('B') and row.get('E') and not str(row.get('A') or '').startswith('##'):
            for source in row['E'].split(','):
                table_defs[source] = row
    bean_defs = beans(books['__beans__.xlsx']['Sheet1'])
    errors, report = [], []
    for source, table in table_defs.items():
        schema_name = source.split('@')[-1]
        sheet_name = source.split('@')[0] if '@' in source else next(iter(books[schema_name]))
        if schema_name not in books or sheet_name not in books[schema_name]:
            errors.append(f'{schema_name}/{sheet_name}: source missing')
            continue
        rows = books[schema_name][sheet_name]
        numbered = rows_by_number(rows)
        var_rows = [row for row in numbered.values() if row.get('A') == '##var']
        type_rows = [row for row in numbered.values() if row.get('A') == '##type']
        comment_rows = [row for row in numbered.values() if row.get('A') == '##']
        if not var_rows or not comment_rows:
            errors.append(f'{schema_name}/{sheet_name}: missing ##var or ## comment row')
            continue
        from_file = table.get('D') == '1' or str(table.get('D')).lower() == 'true'
        if from_file and not type_rows:
            errors.append(f'{schema_name}/{sheet_name}: read_schema_from_file=true but missing ##type')
            continue
        headers = {}
        for row in var_rows:
            headers.update({col: value.lstrip('*') for col, value in row.items() if col != 'A' and value})
        if from_file:
            types = {col: value for col, value in type_rows[0].items() if col != 'A' and value}
        else:
            bean_name = table.get('C')
            if bean_name not in bean_defs:
                bean_name = table.get('B', '').rsplit('.', 1)[0] + '.' + bean_name
            field_types = dict(leaves(bean_name, bean_defs))
            types = {col: field_types.get(field) for col, field in headers.items()}
            declared = {col: value for col, value in type_rows[0].items() if col != 'A' and value} if type_rows else {}
            if declared != types:
                errors.append(f'{schema_name}/{sheet_name}: visible ##type does not match __beans__')
        missing_types = [col for col in headers if not types.get(col)]
        if missing_types:
            errors.append(f'{schema_name}/{sheet_name}: missing type for {missing_types}')
        comment = comment_rows[0]
        missing_comments = [col for col in headers if not re.search(r'[\u4e00-\u9fff]', str(comment.get(col) or ''))]
        if missing_comments:
            errors.append(f'{schema_name}/{sheet_name}: missing Chinese explanation for {missing_comments}')
        first_data = max(number for number, row in numbered.items() if row.get('A') in ('##', '##type')) + 1
        value_errors = []
        for number, row in numbered.items():
            if number < first_data or str(row.get('A') or '').startswith('##'):
                continue
            for col, value in row.items():
                if not is_blank(value) and col not in types:
                    value_errors.append(f'{col}{number}: value outside declared columns: {value!r}')
                if col not in types:
                    continue
                issue = validate_value(value, types[col])
                if issue:
                    value_errors.append(f'{col}{number}: {issue} ({types[col]})')
                if types[col] == 'bool' and not is_blank(value) and storage[schema_name][sheet_name].get(f'{col}{number}') != 'b':
                    value_errors.append(f'{col}{number}: bool must be stored as a native Excel boolean')
        errors.extend(f'{schema_name}/{sheet_name}: {item}' for item in value_errors)
        report.append({'workbook': schema_name, 'sheet': sheet_name, 'schema_from_sheet': from_file,
                       'columns': len(headers), 'type_rows': len(type_rows), 'value_errors': len(value_errors)})
    return {'sheets': report, 'errors': errors}

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--config-dir', type=Path, default=ROOT/'Configs/GameConfig/Datas')
    parser.add_argument('--report', type=Path)
    args = parser.parse_args()
    result = audit(args.config_dir)
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print(f"CONFIG SCHEMA: {len(result['sheets'])} mapped sheets, {len(result['errors'])} errors")
    for error in result['errors']:
        print('SCHEMA FAIL: ' + error)
    return int(bool(result['errors']))

if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    raise SystemExit(main())
