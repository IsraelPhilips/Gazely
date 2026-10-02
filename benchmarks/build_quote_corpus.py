"""Build the local retrieval corpus from eBible's downloaded VPL archive."""
import hashlib,json,pathlib,re,zipfile
root=pathlib.Path(__file__).resolve().parent.parent
archive=root/'benchmarks/tools/eng-kjv_vpl.zip'
app=root/'EasyWorshipController_MVP_v6_2_CODEX_HANDOFF'
source=(app/'EasyWorshipController.cs').read_text(encoding='utf-8-sig')
books=re.findall(r'AddBook\("([^"]+)"',source)
codes='GEN EXO LEV NUM DEU JOS JDG RUT 1SA 2SA 1KI 2KI 1CH 2CH EZR NEH EST JOB PSA PRO ECC SOL ISA JER LAM EZE DAN HOS JOE AMO OBA JON MIC NAH HAB ZEP HAG ZEC MAL MAT MAR LUK JOH ACT ROM 1CO 2CO GAL EPH PHI COL 1TH 2TH 1TI 2TI TIT PHM HEB JAM 1PE 2PE 1JO 2JO 3JO JUD REV'.split()
assert len(books)==len(codes)==66
names=dict(zip(codes,books))
rows=[];seen=set()
with zipfile.ZipFile(archive) as z:
    for line in z.read('eng-kjv_vpl.txt').decode('utf-8-sig').splitlines():
        m=re.match(r'^(\S+) (\d+):(\d+) (.*)$',line)
        if not m or m[1] not in names:continue
        key=(names[m[1]],m[2],m[3])
        if key in seen:raise ValueError('Duplicate reference '+str(key))
        seen.add(key)
        text=m[4].replace('\t',' ')
        rows.append('\t'.join((*key,text)))
assert len(rows)==31102,len(rows)
destination=app/'corpus/kjv.tsv'
destination.parent.mkdir(exist_ok=True)
destination.write_text('\n'.join(rows)+'\n',encoding='utf-8')
metadata=dict(source_url='https://ebible.org/Scriptures/eng-kjv_vpl.zip',
    rights_url='https://ebible.org/eng-kjv/copyright.htm',
    license='Public domain outside the United Kingdom; retain publisher notice concerning UK printing rights before worldwide release.',
    archive_sha256=hashlib.sha256(archive.read_bytes()).hexdigest(),
    corpus_sha256=hashlib.sha256(destination.read_bytes()).hexdigest(),
    verses=len(rows),books=66,edition='KJV standardized 1769 text from CrossWire/eBible',
    processing='Retained 66-book canon, converted reference keys to canonical app names and tabs; verse wording and supplied bracketed words preserved. Other books in source omitted.')
(destination.parent/'provenance.json').write_text(json.dumps(metadata,indent=2)+'\n',encoding='utf-8')
print(json.dumps(metadata,indent=2))
