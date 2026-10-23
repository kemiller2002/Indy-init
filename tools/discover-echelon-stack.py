#!/usr/bin/env python3
"""Discover latest approved-stable release candidates; discovery is NOT verification."""
import argparse, json, re, urllib.parse, urllib.request
from datetime import datetime, timezone
from pathlib import Path
def fetch(url):
    req=urllib.request.Request(url,headers={'User-Agent':'echelon-preflight/1','Accept':'application/vnd.github+json'})
    with urllib.request.urlopen(req,timeout=15) as response: return json.load(response)
def discover(component, reader=fetch):
    name=component['name']; kind=component['kind']
    if kind=='npm':
        package=component['package']
        data=reader('https://registry.npmjs.org/'+urllib.parse.quote(package,safe='@/'))
        version=data['dist-tags']['latest']
        if '-' in version: raise ValueError('latest dist-tag is prerelease')
        dist=data['versions'][version]['dist']
        if not dist.get('integrity') or not dist.get('tarball'): raise ValueError('artifact integrity/url unavailable')
        return dict(name=name,kind=kind,version=version,integrity=dist['integrity'],artifact_url=dist['tarball'],status='DISCOVERED_NOT_VERIFIED')
    if kind=='github_release':
        repo=component['repository']
        if not re.fullmatch(r'[\w.-]+/[\w.-]+',repo): raise ValueError('invalid repo')
        data=reader('https://api.github.com/repos/'+repo+'/releases/latest')
        if data.get('draft') or data.get('prerelease'): raise ValueError('not a stable release')
        return dict(name=name,kind=kind,version=data['tag_name'],release_url=data.get('html_url'),status='DISCOVERED_NOT_VERIFIED')
    raise ValueError('unsupported source: '+kind)
def inventory(config, reader=fetch):
    if config.get('schema_version')!=1: raise ValueError('unsupported schema')
    components=config.get('components',[])
    if not components: raise ValueError('no components')
    names=set(); results=[]
    for component in components:
        name=component['name']
        if name in names: raise ValueError('duplicate component: '+name)
        names.add(name)
        try: results.append(discover(component,reader))
        except (KeyError,ValueError,OSError,TimeoutError) as ex:
            results.append(dict(name=name,status='BLOCKED',reason=str(ex)))
    for required in config.get('required_names',[]):
        if required not in names: results.append(dict(name=required,status='BLOCKED',reason='missing inventory source'))
    return dict(schema_version=1,discovered_at_utc=datetime.now(timezone.utc).isoformat(),overall_status='BLOCKED' if any(x['status']=='BLOCKED' for x in results) else 'DISCOVERED_NOT_VERIFIED',components=results)
def main():
    p=argparse.ArgumentParser();p.add_argument('config',type=Path);p.add_argument('--out',type=Path,required=True);a=p.parse_args()
    result=inventory(json.loads(a.config.read_text(encoding='utf-8')))
    a.out.parent.mkdir(parents=True,exist_ok=True)
    a.out.write_text(json.dumps(result,indent=2,sort_keys=True)+'\n',encoding='utf-8')
    print(result['overall_status'])
    return 1 if result['overall_status']=='BLOCKED' else 0
if __name__=='__main__': raise SystemExit(main())
