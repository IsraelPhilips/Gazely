"""Score human-verified audio annotations against production replay events.

Annotations: {events:[{start_ms,end_ms,reference,kind,verified}],
negative_spans:[{start_ms,end_ms,verified}], provenance:...}.
Unverified annotations are excluded, never inferred from recognizer output.
"""
import argparse, csv, json, pathlib

def evaluate(rows, annotations, realtime):
    positives=[e for e in annotations.get('events',[]) if e.get('verified')]
    result=[]
    recognized=[r for r in rows if r['event'] in ('partial','final','refined') and r['reference']]
    for expected in positives:
        candidates=[r for r in recognized if float(r['audio_end_ms'])>=expected['start_ms']
                    and float(r['audio_start_ms'])<=expected['end_ms']
                    and float(r['audio_end_ms'])<=expected['end_ms']+16000]
        hits=[r for r in candidates if r['reference']==expected['reference']]
        stable=[r for r in hits if r['event'] in ('final','refined')]
        # Stable means committed recognizer result, not independently verified UI output.
        result.append(dict(expected=expected,hit=bool(stable),first_visible_ms=
            min((float(r['wall_ms'])-expected['end_ms'] for r in hits),default=None) if realtime else None,
            first_committed_ms=min((float(r['wall_ms'])-expected['end_ms'] for r in stable),default=None) if realtime else None))
    false_calls=[]
    for span in annotations.get('negative_spans',[]):
        if not span.get('verified'):continue
        false_calls.extend(r for r in recognized if float(r['audio_start_ms'])>=span['start_ms'] and float(r['audio_end_ms'])<=span['end_ms'])
    return dict(verified_events=len(positives),exact_reference_recall=sum(r['hit'] for r in result)/len(result) if result else None,
        correction_events=[r for r in result if r['expected'].get('kind')=='correction'],events=result,
        false_reference_events=false_calls,unverified_excluded=sum(not e.get('verified') for e in annotations.get('events',[])),
        latency_note='Recognizer callback timing; UI/adapter latency excluded. Accelerated timing is never a live latency measurement.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('run',type=pathlib.Path);p.add_argument('annotations',type=pathlib.Path);a=p.parse_args()
    with (a.run/'events.tsv').open(encoding='utf-8-sig',newline='') as f:rows=list(csv.DictReader(f,delimiter='\t'))
    manifest=json.loads((a.run/'manifest.json').read_text(encoding='utf-8-sig'))
    result=evaluate(rows,json.loads(a.annotations.read_text(encoding='utf-8-sig')),manifest['mode']=='realtime')
    (a.run/'reference-score.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps(result,indent=2))
