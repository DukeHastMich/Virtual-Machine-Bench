"""Bounded, local ISA8 routing pipeline with independently checked KiCad output."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
from export_dsn import export
from import_session import import_session
from validate_board import validate
from kicad_check import check_design
from nudge_repair import repair, target_count


def run(source, directory, java, jar, seconds=180, schematic=None, kicad_cli=None, netlist=None, nudge=True, repair_seconds=180, edge_clearance=.5, hole_clearance=.25):
    """Strip, export, route, import and validate; fail visibly on timeout or bad copper."""
    directory=directory.resolve(); directory.mkdir(parents=True,exist_ok=True)
    if (directory/'result.ses').exists():
        raise ValueError('Choose a fresh output directory; refusing to reuse a stale session.')
    source=source.resolve()
    if source.parent==directory:
        raise ValueError('Keep the input outside the output directory.')
    export(source,directory/'input.dsn',edge_clearance=edge_clearance,hole_clearance=hole_clearance)
    routing_rules=json.loads((directory/'input.manifest.json').read_text())['required_router_settings']
    env=dict(os.environ, APPDATA=str(directory/'appdata'), LOCALAPPDATA=str(directory/'localappdata'))
    timeout=f'{seconds//3600:02}:{seconds//60%60:02}:{seconds%60:02}'
    cmd=[str(java.resolve()),'-Xmx3g','-jar',str(jar.resolve()),'-de','input.dsn','-do','result.ses',
         '--gui.enabled=false','--usage_and_diagnostic_data.disable_analytics=true',
         '--router.optimizer.enabled=false','--router.autorouter.max_passes=30',
         '--router.job_timeout='+timeout]
    cmd += [f'--router.{key}={str(value).lower() if isinstance(value,bool) else value}'
            for key,value in routing_rules.items()]
    (directory/'command.json').write_text(json.dumps(cmd,indent=2))
    print('Routing; progress log:',directory/'routing.log',flush=True)
    with (directory/'routing.log').open('w') as log:
        subprocess.run(cmd,cwd=directory,env=env,stdout=log,stderr=subprocess.STDOUT,
                       check=True,timeout=seconds+60)
    import_session(source,directory/'result.ses',directory/'routed.kicad_pcb')
    report=validate(directory/'routed.kicad_pcb',directory/'validation.json')
    if any(report[k] for k in ('missing_links','clearance_conflicts','physical_short_pairs','outside_copper')):
        raise RuntimeError('Routing output failed independent validation; see validation.json')
    electrical=check_design(directory/'routed.kicad_pcb',directory/'electrical-checks',schematic,kicad_cli,netlist=netlist)
    final_board=directory/'routed.kicad_pcb'
    if nudge and target_count(electrical['drc']):
        print('Trying bounded clearance repair...',flush=True)
        fixed=repair(final_board,directory/'nudge-repair',kicad_cli,initial=electrical['drc'],seconds=repair_seconds)
        print('Clearance repair:',fixed['status'],flush=True)
        final_board=Path(fixed['board'])
        if fixed['accepted']:
            electrical=check_design(final_board,directory/'final-electrical-checks',schematic,kicad_cli,netlist=netlist)
    (directory/'result.json').write_text(json.dumps(dict(board=str(final_board),checks=electrical['status'],
        repair_enabled=nudge),indent=2),encoding='utf-8')
    print('Copper connectivity/clearance audit passed:',directory/'routed.kicad_pcb')
    print('Final checks:',electrical['status'],'— selected board:',final_board)
    if electrical['status'] in ('failed','error'):
        raise RuntimeError('KiCad reported violations or a checker failure; output retained for review.')


if __name__=='__main__':
    root=Path(__file__).resolve().parent
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument('source',type=Path);ap.add_argument('output',type=Path)
    ap.add_argument('--java',type=Path,default=root/'tools/java/jdk-25.0.4.1+1-jre/bin/java.exe')
    ap.add_argument('--jar',type=Path,default=root/'tools/freerouting.jar')
    ap.add_argument('--seconds',type=int,default=180)
    ap.add_argument('--schematic',type=Path)
    ap.add_argument('--kicad-cli',type=Path)
    ap.add_argument('--netlist',type=Path)
    ap.add_argument('--no-nudge',action='store_true',help='Disable automatic post-route clearance repair')
    ap.add_argument('--repair-seconds',type=int,default=180)
    ap.add_argument('--edge-clearance',type=float,default=.5,help='Required copper-to-edge distance in mm')
    ap.add_argument('--hole-clearance',type=float,default=.25,help='Required copper-to-NPTH distance in mm')
    args=ap.parse_args(); run(args.source,args.output,args.java,args.jar,args.seconds,args.schematic,args.kicad_cli,args.netlist,not args.no_nudge,args.repair_seconds,args.edge_clearance,args.hole_clearance)
