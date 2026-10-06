#!/usr/bin/env python3
"""Count diagnostic occurrences by stage and owning source; no dedup by warning ID."""
from pathlib import Path
import collections, json, re
root=Path(__file__).parent
results={}
for log in sorted((root/'artifacts').glob('*.publish.log')):
    counts=collections.Counter()
    for line in log.read_text().splitlines():
        match=re.search(r'(?:warning|error) (IL\d+):',line)
        if not match: continue
        stage='compiler'
        if 'Trim analysis warning' in line: stage='trim'
        if 'AOT analysis warning' in line: stage='aot'
        owner='dependency'
        origin=line.split(': ',1)[0]
        if '/investigations/NativeAot/Program.cs' in origin or '/investigations/BindingGenerators/Program.cs' in origin: owner='probe'
        elif '/ReactiveUI.Validation/' in origin or 'ReactiveUI.Validation.' in origin: owner='validation'
        counts[(stage,owner,match[1])]+=1
    results[log.name]=[{'stage':stage,'owner':owner,'code':code,'occurrences':n} for (stage,owner,code),n in sorted(counts.items())]
print(json.dumps(results,indent=2))
