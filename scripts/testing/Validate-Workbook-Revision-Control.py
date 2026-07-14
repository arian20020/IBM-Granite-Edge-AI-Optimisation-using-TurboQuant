"""Validate the central workbook revision register and generated Word histories."""
from __future__ import annotations
import csv, hashlib, re, zipfile
from collections import defaultdict
from pathlib import Path
from docx import Document

EXPECTED_VERSIONS={"WB-01":"1.1","WB-02":"1.1","WB-03":"1.1","WB-04":"1.2","WB-05":"1.2","WB-06":"1.2"}
EXPECTED_COUNTS={"WB-01":2,"WB-02":2,"WB-03":2,"WB-04":3,"WB-05":3,"WB-06":3}

def sha256(path: Path)->str:
    h=hashlib.sha256()
    with path.open('rb') as handle:
        for chunk in iter(lambda: handle.read(1024*1024), b''):
            h.update(chunk)
    return h.hexdigest()

def main()->int:
    root=Path(__file__).resolve().parents[2]
    errors=[]
    register=root/'docs/testing/Workbook-Revision-Register.csv'
    manifest=root/'docs/testing/workbooks/Controlled-Workbook-Manifest.csv'
    with register.open(newline='',encoding='utf-8-sig') as handle:
        rows=list(csv.DictReader(handle))
    with manifest.open(newline='',encoding='utf-8-sig') as handle:
        manifest_rows=list(csv.DictReader(handle))
    if len(rows)!=15:
        errors.append(f'Expected 15 revision rows; found {len(rows)}')
    groups=defaultdict(list)
    for row in rows:
        groups[row['Workbook_ID']].append(row)
    if set(groups)!=set(EXPECTED_VERSIONS):
        errors.append('Workbook IDs do not match WB-01 to WB-06')
    manifest_by={row['Workbook_ID']:row for row in manifest_rows}
    if len(manifest_by)!=6:
        errors.append('Workbook manifest must contain six rows')
    generated=root/'docs/testing/workbooks/generated'
    for workbook_id, expected_version in EXPECTED_VERSIONS.items():
        history=groups[workbook_id]
        if len(history)!=EXPECTED_COUNTS[workbook_id]:
            errors.append(f'{workbook_id}: expected {EXPECTED_COUNTS[workbook_id]} rows; found {len(history)}')
        current=[row for row in history if row['Status'].startswith('Current')]
        if len(current)!=1:
            errors.append(f'{workbook_id}: expected exactly one current row')
            continue
        current_row=current[0]
        if current_row['Version']!=expected_version:
            errors.append(f'{workbook_id}: current version is {current_row["Version"]}, expected {expected_version}')
        if '#8' not in current_row['Change_Reference'] or 'Pending merge' not in current_row['Change_Reference']:
            errors.append(f'{workbook_id}: current PR/merge gate is incorrect')
        manifest_row=manifest_by.get(workbook_id)
        if not manifest_row:
            continue
        for field in ('Canonical_Template_SHA256','Last_Validated_DOCX_SHA256'):
            if not re.fullmatch(r'[0-9a-f]{64}',manifest_row.get(field,'')):
                errors.append(f'{workbook_id}: invalid manifest {field}')
        template=root/manifest_row['Canonical_Text_Template']
        docx=generated/manifest_row['Controlled_File']
        if not template.is_file():
            errors.append(f'{workbook_id}: canonical template missing')
        elif sha256(template)!=manifest_row['Canonical_Template_SHA256']:
            errors.append(f'{workbook_id}: template hash mismatch')
        if not docx.is_file():
            errors.append(f'{workbook_id}: generated DOCX missing')
            continue
        if sha256(docx)!=manifest_row['Last_Validated_DOCX_SHA256']:
            errors.append(f'{workbook_id}: generated DOCX hash mismatch')
        if manifest_row['Revision']!=current_row['Version']:
            errors.append(f'{workbook_id}: manifest version mismatch')
        try:
            with zipfile.ZipFile(docx) as archive:
                bad_member=archive.testzip()
            if bad_member:
                errors.append(f'{workbook_id}: corrupt DOCX member {bad_member}')
            document=Document(docx)
            if not any(paragraph.text=='Document revision history' for paragraph in document.paragraphs):
                errors.append(f'{workbook_id}: revision heading missing')
            history_tables=[table for table in document.tables if table.rows and table.cell(0,0).text=='Version']
            if not history_tables:
                errors.append(f'{workbook_id}: revision table missing')
            elif len(history_tables[0].rows)-1!=EXPECTED_COUNTS[workbook_id]:
                errors.append(f'{workbook_id}: embedded history row count mismatch')
            if not document.paragraphs[0].text.endswith('v'+current_row['Version']):
                errors.append(f'{workbook_id}: visible title version mismatch')
        except Exception as exc:
            errors.append(f'{workbook_id}: DOCX validation failed: {exc}')
    if errors:
        print('WORKBOOK REVISION CONTROL: FAIL')
        for error in errors:
            print(' -',error)
        return 1
    print('WORKBOOK REVISION CONTROL: PASS')
    print('15 append-only revision records, six current revisions and synchronized embedded histories validated.')
    print('Boundary: this validates document change control, not hardware or model test success.')
    return 0

if __name__=='__main__':
    raise SystemExit(main())
