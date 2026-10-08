"""VMBS experimental routing entry point. Stdlib host; isolated configured worker."""
from __future__ import annotations
import argparse
from datetime import datetime,timezone
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time
import uuid

TOOLS=Path(__file__).resolve().parent
VMBS=TOOLS.parent
BACKEND=TOOLS/'experimental_router'
if hasattr(sys.stdout,'reconfigure'):sys.stdout.reconfigure(errors='replace')

def digest(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def settings(path):
    config=json.loads(Path(path).read_text(encoding='utf-8-sig'))
    for key in ('python','java','freerouting_jar','kicad_cli'):
        if not Path(config.get(key,'')).is_file():raise ValueError(f'Missing runtime: {key}; edit {path}')
    for key in ('geometry_dependencies','skidl_dependencies'):
        if not Path(config.get(key,'')).is_dir():raise ValueError(f'Missing dependency directory: {key}')
    return config

def stop_tree(process):
    if process.poll() is not None:return
    if os.name=='nt':
        subprocess.run(['taskkill','/PID',str(process.pid),'/T','/F'],capture_output=True)
    else:process.terminate()
    try:process.wait(timeout=10)
    except subprocess.TimeoutExpired:process.kill();process.wait()

def execute(command,directory,env,label,timeout=600):
    log=directory/(label+'.log');offsets={};start=time.monotonic()
    print(f'[{label}] started; Ctrl+C cancels the process tree.',flush=True)
    with log.open('w',encoding='utf-8') as stream:
        process=subprocess.Popen(command,cwd=directory,env=env,stdout=stream,stderr=subprocess.STDOUT,
            creationflags=subprocess.CREATE_NEW_PROCESS_GROUP if os.name=='nt' else 0)
        try:
            while process.poll() is None:
                for p in (log,directory/'route'/'routing.log'):
                    if not p.exists():continue
                    with p.open(encoding='utf-8',errors='replace') as reader:
                        reader.seek(offsets.get(str(p),0));text=reader.read();offsets[str(p)]=reader.tell()
                    if text:print(text.rstrip(),flush=True)
                if time.monotonic()-start>timeout:raise TimeoutError(f'{label} exceeded {timeout}s')
                time.sleep(.5)
        except BaseException:
            stop_tree(process);raise
    with log.open(encoding='utf-8',errors='replace') as reader:
        reader.seek(offsets.get(str(log),0));text=reader.read()
        if text:print(text.rstrip(),flush=True)
    return process.returncode

def route(source,rules,netlist=None,config_path=None,seconds=420,repair_seconds=120,output_root=None,reroute_existing=False):
    source=Path(source).resolve();rules=Path(rules).resolve()
    if not source.is_file() or not rules.is_file():raise ValueError('Select existing board/Board Script and project rules files')
    if source.suffix not in ('.kicad_pcb','.json'):raise ValueError('Select .kicad_pcb or .bs.json')
    if source.with_suffix('.kicad_dru').exists():raise ValueError('Custom KiCad rules are not supported by this experimental adapter')
    if seconds<1 or seconds>3600 or repair_seconds<1 or repair_seconds>600:raise ValueError('Invalid time budget')
    config=settings(config_path or BACKEND/'runtime.local.json')
    if netlist and not Path(netlist).is_file():raise ValueError('Netlist not found')
    run=Path(output_root or VMBS/'work'/'experimental-routing').resolve()/(datetime.now().strftime('%Y%m%d-%H%M%S')+'-'+uuid.uuid4().hex[:8])
    run.mkdir(parents=True)
    state=dict(status='running',experimental=True,reroute_existing=reroute_existing,source=str(source),source_sha256=digest(source),
               started_at=datetime.now(timezone.utc).isoformat(),accepted=False,run_directory=str(run.resolve()))
    state_path=run/'integration-result.json'
    def save():state_path.write_text(json.dumps(state,indent=2),encoding='utf-8')
    save()
    env=dict(os.environ,PYTHONUNBUFFERED='1',PYTHONIOENCODING='utf-8',VMBS_ROUTER_DEPS=config['geometry_dependencies'],
             VMBS_SKIDL_DEPS=config['skidl_dependencies'],KICAD_CONFIG_HOME=str(run/'kicad-config'))
    python=config['python']
    def step(name,script,*args,timeout=600):
        code=execute([python,str(script),*map(str,args)],run,env,name,timeout)
        if code:raise RuntimeError(f'{name} failed (exit {code}); see {run/(name+".log")}')
    try:
        copied=run/('source'+source.suffix);shutil.copy2(source,copied)
        local_rules=run/'project.rules.json';shutil.copy2(rules,local_rules)
        local_net=None
        if netlist:local_net=run/'schematic.net';shutil.copy2(netlist,local_net)
        board=run/'input.kicad_pcb'
        if source.suffix=='.json':step('bs-export',TOOLS/'bs_export.py',copied,board)
        else:shutil.copy2(copied,board)
        step('preflight',BACKEND/'preflight.py',board,local_rules,*(['--reroute-existing'] if reroute_existing else []))
        project=json.loads(local_rules.read_text(encoding='utf-8-sig'))
        command=[python,str(BACKEND/'run_router.py'),str(board),str(run/'route'),
                 '--java',config['java'],'--jar',config['freerouting_jar'],'--kicad-cli',config['kicad_cli'],
                 '--seconds',str(seconds),'--repair-seconds',str(repair_seconds),
                 '--edge-clearance',str(project['edge_clearance_mm']),'--hole-clearance',str(project['hole_clearance_mm'])]
        if local_net:command += ['--netlist',str(local_net)]
        (run/'command.json').write_text(json.dumps(command,indent=2))
        code=execute(command,run,env,'routing',seconds+repair_seconds+240)
        result_path=run/'route'/'result.json'
        if not result_path.exists():raise RuntimeError(f'Routing did not produce a validated result (exit {code}); inspect routing.log')
        result=json.loads(result_path.read_text());candidate=Path(result['board']).resolve()
        if not candidate.is_relative_to(run.resolve()):raise ValueError('Unexpected candidate location')
        report_dir=run/'route'/('final-electrical-checks' if (run/'route'/'final-electrical-checks'/'latest.json').exists() else 'electrical-checks')
        checks=json.loads((report_dir/'latest.json').read_text())
        state.update(status=checks['status'],board=str(candidate),checks=str(report_dir/'latest.json'),
                     drc_errors=checks['drc'].get('errors'),drc_warnings=checks['drc'].get('warnings'),
                     erc_status=checks['erc']['status'],erc_warnings=checks['erc'].get('warnings'))
        draft=run/'candidate.bs.json';step('bs-import',TOOLS/'bs_complete.py','import',candidate,draft)
        roundtrip=run/'roundtrip.kicad_pcb';step('bs-roundtrip',TOOLS/'bs_export.py',draft,roundtrip)
        step('bs-verify',TOOLS/'bs_complete.py','compare',candidate,roundtrip)
        if json.loads((run/'bs-verify.log').read_text(encoding='utf-8')) .get('semantic_equal') is not True:
            raise RuntimeError('Board Script round-trip changed CAD semantics; draft cannot be accepted')
        state['board_script']=str(draft)
        if digest(source)!=state['source_sha256']:raise RuntimeError('Source changed during routing; result requires review')
    except KeyboardInterrupt:
        state.update(status='cancelled',reason='Cancelled by user; partial files retained')
    except Exception as exc:
        state.update(status='error',reason=str(exc))
    finally:
        state['finished_at']=datetime.now(timezone.utc).isoformat();save()
    print('\nExperimental routing result:',state['status'].upper())
    print('Input preserved. No result has been accepted or published.')
    print('Report:',state_path)
    if state.get('checks'):print('Electrical / manufacturing checks:',state['checks'])
    if state.get('reason'):print(state['reason'])
    return state

def interactive():
    print('\nExperimental: two-layer legacy KiCad / VMBS-BOARD-1; Ctrl+C cancels.')
    source=input('Input .kicad_pcb or .bs.json: ').strip().strip('"')
    if not source:return
    rules=input('Project rules JSON (required; example tools/experimental_router/isa8.rules.json): ').strip().strip('"')
    netlist=input('KiCad exported .net for ERC (blank = incomplete electrical check): ').strip().strip('"')
    reroute=input('Replace existing copper in a NEW draft? Type REROUTE; Enter routes only untraced boards: ').strip()=='REROUTE'
    state=route(source,rules,netlist or None,reroute_existing=reroute)
    if state['status']=='passed':
        destination=input('All configured checks passed. Save a reviewed Board Script copy to (blank keeps draft): ').strip().strip('"')
        if destination:
            # Exclusive creation prevents replacing a source/design accidentally.
            with Path(destination).open('xb') as stream:stream.write(Path(state['board_script']).read_bytes())
            state.update(accepted=True,accepted_copy=str(Path(destination).resolve()))
            (Path(state['run_directory'])/'integration-result.json').write_text(json.dumps(state,indent=2),encoding='utf-8')
            print('Saved reviewed copy:',destination)
    else:print('Draft only: resolve failed, missing or warning checks before acceptance.')

if __name__=='__main__':
    ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('source',nargs='?');ap.add_argument('--rules')
    ap.add_argument('--netlist');ap.add_argument('--config');ap.add_argument('--seconds',type=int,default=420)
    ap.add_argument('--output-root');ap.add_argument('--repair-seconds',type=int,default=120)
    ap.add_argument('--reroute-existing',action='store_true',help='Explicitly replace existing copper in the new draft; original stays untouched')
    a=ap.parse_args()
    if not a.source:interactive()
    else:
        if not a.rules:ap.error('--rules is required')
        result=route(a.source,a.rules,a.netlist,a.config,a.seconds,a.repair_seconds,a.output_root,a.reroute_existing)
        raise SystemExit(0 if result['status']=='passed' else 2)
