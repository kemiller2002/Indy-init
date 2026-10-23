#!/usr/bin/env python3
"""Offline negative-case tests for context and release-discovery gates."""
import hashlib, importlib.util, pathlib, tempfile, unittest
HERE=pathlib.Path(__file__).resolve().parent
def load(path):
    spec=importlib.util.spec_from_file_location(path.replace('-','_').replace('.py',''),HERE/path)
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module
ctx=load('compile-agent-context.py')
stack=load('discover-echelon-stack.py')
class ContextTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.addCleanup(self.tmp.cleanup)
        self.root=pathlib.Path(self.tmp.name)
        raw=b'# UI-001: Dashboard\nMust show data.\n# UI-002: Graph\nMust be accessible.\n'
        (self.root/'requirements.md').write_bytes(raw)
        self.manifest=dict(schema_version=1,source_sha='a'*40,interface_manifest_sha='b'*40,dependency_lock_sha='c'*40,sources=[dict(path='requirements.md',sha256=hashlib.sha256(raw).hexdigest())],agents=[dict(name='ui-agent',requirement_ids=['UI-001'],owned_paths=['src/ui'],allowed_decisions=['layout'],prohibited_decisions=['policy'],outputs=['screen'],acceptance_tests=['ui-smoke'])])
    def test_bounded_context(self):
        result=ctx.compile_packets(self.root,self.manifest)
        self.assertIn('Must show data',result[0]['requirements'][0]['text'])
        self.assertNotIn('Must be accessible',result[0]['requirements'][0]['text'])
    def test_changed_source_rejected(self):
        self.manifest['sources'][0]['sha256']='0'*64
        with self.assertRaisesRegex(ValueError,'drift'):ctx.compile_packets(self.root,self.manifest)
    def test_missing_requirement_rejected(self):
        self.manifest['agents'][0]['requirement_ids']=['UI-999']
        with self.assertRaises(ValueError):ctx.compile_packets(self.root,self.manifest)
    def test_overlap_rejected(self):
        self.manifest['agents'].append(dict(self.manifest['agents'][0],name='second-agent'))
        with self.assertRaises(ValueError):ctx.compile_packets(self.root,self.manifest)
    def test_path_escape_rejected(self):
        self.manifest['sources'][0]['path']='../outside'
        with self.assertRaises(ValueError):ctx.compile_packets(self.root,self.manifest)
class StackTests(unittest.TestCase):
    def test_npm_latest(self):
        data={'dist-tags':{'latest':'0.9.0'},'versions':{'0.9.0':{'dist':{'integrity':'sha512-abc','tarball':'https://example.test/file.tgz'}}}}
        result=stack.inventory(dict(schema_version=1,components=[dict(name='forma',kind='npm',package='forma')]),lambda _:data)
        self.assertEqual(result['components'][0]['version'],'0.9.0')
        self.assertEqual(result['overall_status'],'DISCOVERED_NOT_VERIFIED')
    def test_missing_release_blocks(self):
        result=stack.inventory(dict(schema_version=1,components=[dict(name='folio',kind='github_release',repository='owner/folio')]),lambda _:{})
        self.assertEqual(result['overall_status'],'BLOCKED')
    def test_prerelease_blocks(self):
        result=stack.inventory(dict(schema_version=1,components=[dict(name='folio',kind='github_release',repository='owner/folio')]),lambda _:{'tag_name':'v2.0.0-beta','prerelease':True})
        self.assertEqual(result['overall_status'],'BLOCKED')
    def test_missing_required_component_blocks(self):
        result=stack.inventory(dict(schema_version=1,required_names=['dokimos'],components=[dict(name='folio',kind='github_release',repository='owner/folio')]),lambda _:{'tag_name':'v1'})
        self.assertEqual(result['overall_status'],'BLOCKED')
if __name__=='__main__':unittest.main()
