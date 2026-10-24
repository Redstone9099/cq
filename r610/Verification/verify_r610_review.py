"""Check declared R6.10 changes, Windows-tested inputs and preserved assets.
This check does not start Direct3D or a game server.
"""
from pathlib import Path
import hashlib,json
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256((ROOT/p).read_bytes()).hexdigest()
E=ROOT/'Evidence/R6.10'
ci=json.loads((E/'windows-ci.json').read_text())
assert ci['conclusion']=='success'
changes={x['path']:x for x in json.loads((E/'changes.json').read_text())}
base=json.loads((E/'base-r69-manifest.json').read_text())
count=0
for entry in base:
    p=entry['path'];change=changes.get(p)
    if change:assert change['before_sha256']==entry['sha256'],p
    assert sha(p)==(change['after_sha256'] if change else entry['sha256']),p
    count+=1
for p,change in changes.items():assert sha(p)==change['after_sha256'],p
for entry in ci['tested_inputs']:assert sha(entry['path'])==entry['sha256'],entry['path']
for entry in base:
    if entry['path'].startswith('client/'):
        assert sha(entry['path'])==entry['sha256'],'Existing asset changed: '+entry['path']
print(json.dumps({'baseline_files_checked':count,'declared_changes':len(changes),
    'windows_tested_inputs_checked':len(ci['tested_inputs']),'windows_ci':ci['run_url'],
    'gameplay_executed':False,'fps_measured':False},ensure_ascii=False,indent=2))
