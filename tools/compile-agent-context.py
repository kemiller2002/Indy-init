#!/usr/bin/env python3
"""Compile fail-closed, source-pinned, bounded agent assignments (stdlib only)."""
import argparse, hashlib, json, re
from pathlib import Path
ID = re.compile(r'(?:UI|WASM-LOAD|DCC|ADRP|DEP-CURRENT|STORAGE)-\d{3}')
def compile_packets(root, spec):
    if spec.get('schema_version') != 1: raise ValueError('unsupported schema')
    if not re.fullmatch(r'[0-9a-f]{40}', spec.get('source_sha','')): raise ValueError('source SHA required')
    if not spec.get('interface_manifest_sha') or not spec.get('dependency_lock_sha'): raise ValueError('missing frozen contract identities')
    sources = {}
    for s in spec.get('sources', []):
        p = s['path']; file = (root / p).resolve()
        if not file.is_relative_to(root.resolve()) or not file.is_file(): raise ValueError('missing or unsafe source: '+p)
        data = file.read_bytes(); sha = hashlib.sha256(data).hexdigest()
        if sha != s['sha256']: raise ValueError('source drift: '+p)
        if p in sources: raise ValueError('duplicate source: '+p)
        sources[p] = (data.decode('utf-8').splitlines(), sha)
    if not sources: raise ValueError('no pinned sources')
    seen, owned, packets = set(), {}, []
    for a in spec.get('agents', []):
        name = a.get('name','')
        if not re.fullmatch(r'[a-z][a-z0-9-]{1,48}', name) or name in seen: raise ValueError('invalid agent name')
        seen.add(name)
        ids = a.get('requirement_ids',[])
        if not ids or len(ids) != len(set(ids)): raise ValueError('missing or duplicate requirements')
        for key in ('allowed_decisions','prohibited_decisions','outputs','acceptance_tests','owned_paths'):
            if not a.get(key): raise ValueError(name+': missing '+key)
        for p in a['owned_paths']:
            if p.startswith('/') or '..' in Path(p).parts or p in owned: raise ValueError('unsafe/overlapping ownership: '+p)
            owned[p] = name
        extracts = []
        for rid in ids:
            if not ID.fullmatch(rid): raise ValueError('unsupported requirement: '+rid)
            matches = []
            for path, (lines, sha) in sources.items():
                for i, line in enumerate(lines):
                    if line.startswith('#') and re.search(r'(?<![\w-])'+re.escape(rid)+r'(?![\w-])',line):
                        level = len(line)-len(line.lstrip('#')); end = i+1
                        while end < len(lines):
                            nextline = lines[end]
                            if nextline.startswith('#') and len(nextline)-len(nextline.lstrip('#')) <= level: break
                            end += 1
                        matches.append(dict(id=rid, path=path, line=i+1, source_sha256=sha, text='\n'.join(lines[i:end]).strip()))
            if len(matches)!=1: raise ValueError(rid+': expected exactly one canonical heading; found '+str(len(matches)))
            extracts += matches
        packets.append(dict(name=name, source_sha=spec['source_sha'], interface_manifest_sha=spec['interface_manifest_sha'], dependency_lock_sha=spec['dependency_lock_sha'], owned_paths=a['owned_paths'], required_inputs=a.get('required_inputs',[]), allowed_decisions=a['allowed_decisions'], prohibited_decisions=a['prohibited_decisions'], outputs=a['outputs'], acceptance_tests=a['acceptance_tests'], requirements=extracts))
    if not packets: raise ValueError('no agents')
    return packets
def main():
    p=argparse.ArgumentParser(); p.add_argument('manifest',type=Path); p.add_argument('--root',type=Path,default=Path('.')); p.add_argument('--out',type=Path,required=True); a=p.parse_args()
    packets=compile_packets(a.root,json.loads(a.manifest.read_text(encoding='utf-8')))
    a.out.mkdir(parents=True,exist_ok=True)
    for packet in packets:
        dest=a.out/(packet['name']+'.json')
        dest.write_text(json.dumps(packet,indent=2,sort_keys=True)+'\n',encoding='utf-8')
        print(dest)
if __name__=='__main__': main()
