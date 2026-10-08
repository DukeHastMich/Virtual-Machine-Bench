"""Run upstream KiCad DRC/ERC without mistaking missing checks for a pass."""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import uuid
from datetime import datetime, timezone


def find_cli(explicit=None):
    """Locate the configured, local portable or installed upstream checker."""
    if explicit:
        return str(Path(explicit).resolve()) if Path(explicit).is_file() else None
    candidates=[os.environ.get('KICAD_CLI'), shutil.which('kicad-cli'),
                str(Path(__file__).parent/'tools/kicad/bin/kicad-cli.exe')]
    return next((p for p in candidates if p and Path(p).is_file()),None)


def findings(node):
    """Collect KiCad issue entries across board and schematic report layouts."""
    result=[]
    if isinstance(node,dict):
        if 'severity' in node and 'description' in node:
            result.append(node)
        else:
            for value in node.values():result.extend(findings(value))
    elif isinstance(node,list):
        for value in node:result.extend(findings(value))
    return result


def run_check(cli, kind, source, output, timeout=120):
    """Run one check in a fresh report directory; preserve raw upstream evidence."""
    if source is None:
        return dict(status='not_run',reason='Schematic not supplied; PCB files lack pin electrical types.')
    source=Path(source).resolve()
    if not source.is_file():return dict(status='error',reason=f'Input not found: {source}')
    if kind=='erc' and source.suffix.lower()!='.kicad_sch':
        return dict(status='not_run',reason='Legacy schematic import is unreliable; supply --netlist or a converted .kicad_sch.')
    if not cli:return dict(status='not_run',reason='KiCad CLI not installed or configured.')
    report=output/f'{kind}.json'
    cmd=[cli,'pcb' if kind=='drc' else 'sch',kind,'--format','json',
         '--severity-all','--exit-code-violations','--output',str(report),str(source)]
    try:
        env=dict(os.environ)
        env.setdefault('KICAD_CONFIG_HOME',str(Path(__file__).parent/'tools/kicad/config'))
        proc=subprocess.run(cmd,capture_output=True,text=True,encoding='utf-8',errors='replace',timeout=timeout,env=env)
    except (OSError,subprocess.TimeoutExpired) as exc:
        return dict(status='error',reason=str(exc),command=cmd)
    (output/f'{kind}.log').write_text(proc.stdout+'\n'+proc.stderr,encoding='utf-8')
    if proc.returncode not in (0,5) or not report.exists():
        return dict(status='error',exit_code=proc.returncode,command=cmd,log=str(output/f'{kind}.log'))
    try:
        data=json.loads(report.read_text(encoding='utf-8-sig'))
        if not isinstance(data,dict):raise ValueError('Expected KiCad JSON object')
        expected=('violations','unconnected_items') if kind=='drc' else ('sheets','violations')
        if not any(key in data for key in expected):
            raise ValueError('Unrecognized KiCad report schema')
        issues=findings(data)
    except (ValueError,OSError) as exc:
        return dict(status='error',reason=f'Invalid checker report: {exc}',command=cmd)
    active=[i for i in issues if not i.get('excluded',False)]
    errors=sum(i.get('severity')=='error' for i in active)
    warnings=sum(i.get('severity')=='warning' for i in active)
    status='failed' if errors else ('review' if active or proc.returncode==5 else 'passed')
    return dict(status=status,errors=errors,warnings=warnings,excluded=len(issues)-len(active),
                exit_code=proc.returncode,report=str(report),command=cmd,findings=active,
                ignored_checks=data.get('ignored_checks',[]))


def run_netlist(netlist,board,output,timeout):
    report=output/'skidl-erc.json'
    cmd=[sys.executable,str(Path(__file__).parent/'skidl_check.py'),str(Path(netlist).resolve()),
         '--board',str(Path(board).resolve()),'--output',str(report)]
    try:
        p=subprocess.run(cmd,cwd=output,capture_output=True,text=True,encoding='utf-8',errors='replace',timeout=timeout)
        (output/'skidl.log').write_text(p.stdout+'\n'+p.stderr,encoding='utf-8')
        if p.returncode!=0: return dict(status='error',reason='SKiDL worker failed',log=str(output/'skidl.log'))
        result=json.loads(report.read_text(encoding='utf-8'))
        if result.get('status') not in ('passed','review','failed','error'):raise ValueError('Invalid ERC status')
        result['report']=str(report)
        return result
    except (OSError,ValueError,subprocess.TimeoutExpired) as exc:
        return dict(status='error',reason=str(exc))


def check_design(board, output, schematic=None, cli=None, timeout=120, netlist=None):
    """Run DRC and optional ERC, exposing incomplete coverage explicitly."""
    output=Path(output).resolve();output.mkdir(parents=True,exist_ok=True)
    # Unique run folders prevent an old successful report from masking a failed run.
    run=output/('kicad-'+uuid.uuid4().hex[:12]);run.mkdir()
    executable=find_cli(cli)
    version=None
    if executable:
        try:
            p=subprocess.run([executable,'--version'],capture_output=True,text=True,timeout=15)
            version=p.stdout.strip() if p.returncode==0 else None
        except (OSError,subprocess.TimeoutExpired):pass
    drc=run_check(executable,'drc',board,run,timeout)
    erc=run_netlist(netlist,board,run,timeout) if netlist else run_check(executable,'erc',schematic,run,timeout)
    states={drc['status'],erc['status']}
    status=next((s for s in ('error','failed','not_run','review') if s in states),'passed')
    if status=='not_run':status='incomplete'
    result=dict(status=status,engine='KiCad + SKiDL' if netlist else 'KiCad',version=version,
                source='https://github.com/KiCad/kicad-source-mirror',
                checked_at=datetime.now(timezone.utc).isoformat(),drc=drc,erc=erc,
                not_checked=['current capacity','impedance','signal integrity','timing',
                             'decoupling effectiveness']+([] if netlist else ['schematic-to-PCB parity']))
    (run/'summary.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    (output/'latest.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    return result


if __name__=='__main__':
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument('board');ap.add_argument('--schematic');ap.add_argument('--output',default='electrical-checks')
    ap.add_argument('--kicad-cli');ap.add_argument('--timeout',type=int,default=120)
    ap.add_argument('--netlist',help='KiCad exported .net with libparts pin electrical types; uses SKiDL ERC')
    a=ap.parse_args();r=check_design(a.board,a.output,a.schematic,a.kicad_cli,a.timeout,a.netlist)
    print(json.dumps({k:v for k,v in r.items() if k not in ('drc','erc')},indent=2))
    for kind in ('drc','erc'):
        print(kind.upper(),r[kind]['status'],'errors:',r[kind].get('errors'),'warnings:',r[kind].get('warnings'))
    raise SystemExit({'passed':0,'review':1,'incomplete':2,'failed':5,'error':3}[r['status']])
