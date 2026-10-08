"""Integration failure/cancellation gates, without needing Java or KiCad."""
import json
import os
from pathlib import Path
import sys
import tempfile
import time
import unittest
from unittest.mock import patch
import experimental_routing as host

class HostTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.addCleanup(self.tmp.cleanup)
        self.root=Path(self.tmp.name);self.source=self.root/'source.kicad_pcb';self.source.write_text('unchanged input')
        self.rules=self.root/'rules.json';self.rules.write_text(json.dumps(dict(edge_clearance_mm=.5,hole_clearance_mm=.25)))
        self.config={k:sys.executable for k in ('python','java','freerouting_jar','kicad_cli')}
        self.config.update(geometry_dependencies=str(self.root),skidl_dependencies=str(self.root))
        self.cfg=self.root/'runtime.json';self.cfg.write_text(json.dumps(self.config))

    def run_host(self):
        return host.route(self.source,self.rules,config_path=self.cfg,output_root=self.root/'runs')

    def test_cancel_preserves_source_and_records_cancelled(self):
        with patch.object(host,'execute',side_effect=KeyboardInterrupt):result=self.run_host()
        self.assertEqual(result['status'],'cancelled');self.assertFalse(result['accepted'])
        self.assertEqual(self.source.read_text(),'unchanged input')

    def fake(self,semantic=True,status='failed'):
        def execute(command,directory,env,label,timeout):
            if label=='routing':
                folder=directory/'route';folder.mkdir();board=folder/'routed.kicad_pcb';board.write_text('candidate')
                (folder/'result.json').write_text(json.dumps(dict(board=str(board))))
                checks=folder/'electrical-checks';checks.mkdir()
                (checks/'latest.json').write_text(json.dumps(dict(status=status,drc=dict(errors=16,warnings=0),erc=dict(status='review',warnings=71))))
            elif label=='bs-import':(directory/'candidate.bs.json').write_text('{}')
            elif label=='bs-verify':(directory/'bs-verify.log').write_text(json.dumps(dict(semantic_equal=semantic)))
            return 0
        return execute

    def test_failed_checks_remain_unaccepted_draft(self):
        with patch.object(host,'execute',side_effect=self.fake()):result=self.run_host()
        self.assertEqual(result['status'],'failed');self.assertFalse(result['accepted'])
        self.assertTrue(Path(result['board_script']).is_file())

    def test_semantic_mismatch_never_passes(self):
        with patch.object(host,'execute',side_effect=self.fake(False,'passed')):result=self.run_host()
        self.assertEqual(result['status'],'error');self.assertFalse(result['accepted'])

    def test_missing_runtime_rejected_before_output(self):
        self.config['java']=str(self.root/'missing.exe');self.cfg.write_text(json.dumps(self.config))
        with self.assertRaises(ValueError):self.run_host()
        self.assertFalse((self.root/'runs').exists())

    def test_real_process_timeout_stops_worker(self):
        start=time.monotonic()
        with self.assertRaises(TimeoutError):
            host.execute([sys.executable,'-c','import time; time.sleep(30)'],self.root,dict(os.environ),'timeout',.2)
        self.assertLess(time.monotonic()-start,20)

class PreflightTests(unittest.TestCase):
    def test_existing_copper_requires_explicit_reroute(self):
        config=json.loads((host.BACKEND/'runtime.local.json').read_text(encoding='utf-8-sig'))
        os.environ['VMBS_ROUTER_DEPS']=config['geometry_dependencies']
        sys.path.insert(0,str(host.BACKEND))
        from preflight import inspect
        from board_geometry import load
        from sexpr import dump
        fixture=host.BACKEND/'testdata'/'legacy.kicad_pcb'
        rules=host.BACKEND/'isa8.rules.json'
        with self.assertRaisesRegex(ValueError,'Existing copper preserved'):inspect(fixture,rules)
        self.assertEqual(inspect(fixture,rules,True)['status'],'supported')
        with tempfile.TemporaryDirectory() as temp:
            stripped=Path(temp)/'empty.kicad_pcb'
            board=load(fixture)
            board=[n for n in board if not isinstance(n,list) or n[0] not in ('segment','via','zone')]
            stripped.write_text(dump(board))
            self.assertEqual(inspect(stripped,rules)['status'],'supported')

if __name__=='__main__':unittest.main()
