"""Decode offline-search IDs and report replay timings, not UI accuracy scores."""
import argparse, csv, json, math, pathlib

def percentile(values, fraction):
    values=sorted(values)
    return values[max(0,math.ceil(len(values)*fraction)-1)] if values else None

def summarize(run, corpus):
    summary=json.loads((run/'summary.json').read_text(encoding='utf-8'))
    queries=json.loads((run/'retrieval.json').read_text(encoding='utf-8'))
    verses=[line.split('\t',3) for line in corpus.read_text(encoding='utf-8-sig').splitlines()]
    rows=[]
    for query in queries:
        references=[]
        for record in filter(None,query['response'].split(';')):
            index,cosine,reranker=record.split(',')
            book,chapter,verse,_=verses[int(index)]
            references.append(f'{book} {chapter}:{verse}')
        rows.append(dict(text=query['text'],references='; '.join(references),
                         search_ms=query['search_ms'],queue_ms=query['queue_ms']))
    output=run/'retrieval-references.csv'
    with output.open('w',encoding='utf-8',newline='') as stream:
        writer=csv.DictWriter(stream,fieldnames=['text','references','search_ms','queue_ms'])
        writer.writeheader();writer.writerows(rows)
    summary['search_p95_ms']=percentile([r['search_ms'] for r in rows],.95)
    summary['search_queue_p95_ms']=percentile([r['queue_ms'] for r in rows],.95)
    summary['search_queries_with_results']=sum(bool(r['references']) for r in rows)
    if (run/'feed.csv').exists():
        with (run/'feed.csv').open(encoding='utf-8',newline='') as stream:
            feeds=list(csv.DictReader(stream))
        delays=[float(r['lag_seconds']) for r in feeds if r['lag_seconds']]
        summary['feed_lag_p95_seconds']=percentile(delays,.95)
        summary['feed_frames_over_two_seconds']=sum(v>2 for v in delays)
        summary['maximum_add_audio_call_seconds']=max((float(r['call_seconds']) for r in feeds),default=0)
    summary['interpretation']='Raw semantic candidates; excludes Gazely lexical/context searches, candidate gates, SP and presentation.'
    (run/'combined-summary.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')
    print(json.dumps(summary,indent=2))

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('run',type=pathlib.Path)
    parser.add_argument('corpus',type=pathlib.Path);args=parser.parse_args()
    summarize(args.run,args.corpus)
