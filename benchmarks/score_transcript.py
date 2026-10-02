"""Whole-run WER, using a manually corrected reference and replay transcript.csv.
No fuzzy biblical replacements. Punctuation/case are ignored; numerals are retained.
An empty reference is rejected. Insertions include overlapping committed repeats.
"""
import argparse, csv, hashlib, json, pathlib, re

def words(text):
    return re.findall(r"[^\W_]+(?:['’][^\W_]+)*", text.casefold(), re.UNICODE)

def score(reference, hypothesis):
    # Tuple: edits, substitutions, deletions, insertions; deterministic ties.
    previous = [(j, 0, 0, j) for j in range(len(hypothesis)+1)]
    for i, expected in enumerate(reference, 1):
        current = [(i, 0, i, 0)]
        for j, actual in enumerate(hypothesis, 1):
            if expected == actual:
                current.append(previous[j-1])
            else:
                e,s,d,n = previous[j-1]; substitution=(e+1,s+1,d,n)
                e,s,d,n = previous[j]; deletion=(e+1,s,d+1,n)
                e,s,d,n = current[j-1]; insertion=(e+1,s,d,n+1)
                current.append(min((substitution,deletion,insertion),key=lambda x:x[0]))
        previous=current
    edits,s,d,i=previous[-1]
    if not reference: raise ValueError('Corrected reference must not be empty')
    return dict(reference_words=len(reference),hypothesis_words=len(hypothesis),substitutions=s,deletions=d,insertions=i,wer=edits/len(reference))

if __name__ == '__main__':
    p=argparse.ArgumentParser()
    p.add_argument('corrected_reference',type=pathlib.Path)
    p.add_argument('transcript_csv',type=pathlib.Path)
    p.add_argument('--text',action='store_true',help='Hypothesis is a plain transcript rather than replay CSV')
    p.add_argument('--output',type=pathlib.Path)
    a=p.parse_args()
    if a.text:
        hypothesis=a.transcript_csv.read_text(encoding='utf-8-sig')
    else:
        with a.transcript_csv.open(encoding='utf-8-sig',newline='') as f:
            hypothesis=' '.join(row['text'] for row in csv.DictReader(f))
    result=score(words(a.corrected_reference.read_text(encoding='utf-8-sig')),words(hypothesis))
    result['normalization']='Unicode words, casefold, punctuation removed; no number/alias replacement'
    result['reference_sha256']=hashlib.sha256(a.corrected_reference.read_bytes()).hexdigest()
    result['hypothesis_sha256']=hashlib.sha256(a.transcript_csv.read_bytes()).hexdigest()
    result['reference']=str(a.corrected_reference)
    result['hypothesis']=str(a.transcript_csv)
    payload=json.dumps(result,indent=2)
    if a.output:a.output.write_text(payload+'\n',encoding='utf-8')
    print(payload)
