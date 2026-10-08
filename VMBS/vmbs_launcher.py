#!/usr/bin/env python3
"""Interactive Windows-friendly launcher for the VMBS development toolchain."""
from __future__ import annotations
import json, os, shutil, subprocess, sys
from pathlib import Path

ROOT=Path(__file__).resolve().parent
TOOLS=ROOT/'tools'; DB=ROOT/'compiler_db'
DEFAULTS={
 'component_store':DB/'component_store.json',
 'component_map':DB/'ISA8_Ethernet.component_map.json',
 'code_store':DB/'code_store.json',
 'platform_store':DB/'platform_store.json',
}

def ask(label, default=None):
    tail=f' [{default}]' if default else ''
    v=input(f'{label}{tail}: ').strip().strip('"')
    return Path(v or default) if (v or default) else None

def run(script,*args):
    cmd=[sys.executable,str(TOOLS/script),*map(str,args)]
    print('\n>',subprocess.list2cmdline(cmd))
    subprocess.check_call(cmd,cwd=ROOT)

def compile_bs(bs,out):
    out.mkdir(parents=True,exist_ok=True)
    ir=out/'board.ir.json'; plan=out/'construction.ir.json'; code=out/'board.code.json'
    board_name=bs.stem.replace('.bs','')
    vcb_dir=card_store()/board_name
    vcb_dir.mkdir(parents=True,exist_ok=True)
    vb=vcb_dir/(board_name+'.vcb.vb'); dll=vcb_dir/(board_name+'.vcb.dll')
    run('board_analyzer.py',bs,DEFAULTS['component_store'],DEFAULTS['component_map'],DEFAULTS['code_store'],DEFAULTS['platform_store'],ir)
    run('board_constructor.py',ir,DEFAULTS['code_store'],plan)
    run('construction_lower.py',plan,code)
    run('board_code_validate.py',code)
    run('vb_board_emit.py',code,vb,'--class-name',board_name+'_VCB','--board-id',board_name.upper(),'--display-name',board_name.replace('_',' '))
    print(f'\nGenerated VCB source: {vb}')
    run('vcb_compile.py',vb,dll,'--vmb-root',ROOT.parent)
    count=publish_board_count(card_store())
    print(f'Compiled VCB DLL: {dll}')
    print(f'Published board count: {count}')
    return dll

def card_store():
    p=(ROOT/'VCB Store').resolve()
    p.mkdir(parents=True,exist_ok=True)
    return p

def publish_board_count(store):
    """Producer-owned catalog generation signal: one integer, nothing clever."""
    count=sum(1 for p in store.rglob('*.vcb.dll') if p.is_file())
    tmp=store/'PCB.meta.tmp'
    tmp.write_text(str(count)+'\n',encoding='ascii')
    os.replace(tmp,store/'PCB.meta')
    return count

def menu():
    print('='*64); print(' VMBS - Board Script Toolchain'); print('='*64)
    print(' 1. Import KiCad board -> BS')
    print(' 2. Analyze BS')
    print(' 3. Compile KiCad board -> VCB')
    print(' 4. Compile existing BS -> VCB')
    print(' 5. Show manufactured VCBs')
    print(' 6. Re-publish VCB board count')
    print(' 7. Show Component Store summary')
    print(' 8. Run core regression / mutation tests')
    print(' 9. Diagnostics')
    print('10. Exit')
    print('11. Route PCB - experimental FreeRouting + ERC/DRC')

def validate_card(p):
    d=json.loads(p.read_text(encoding='utf-8'))
    if d.get('format')!='VMB-CARD-1': raise ValueError('not VMB-CARD-1')
    i=d.get('install',{}); b=d.get('board_code',{})
    if b.get('format')!='VMB-BOARD-CODE-1': raise ValueError('missing VMB-BOARD-CODE-1 payload')
    required=['card_id','display_name','category','bus','primary_module_id','required_modules','completion']
    miss=[x for x in required if x not in i]
    if miss: raise ValueError('install header missing: '+', '.join(miss))
    print(json.dumps({'valid':True,'card_id':i['card_id'],'name':i['display_name'],'bus':i['bus'],'modules':i['required_modules'],'completion':i['completion']},indent=2))

def main():
    while True:
        menu()
        c=input('\nSelect: ').strip()
        try:
            if c=='1':
                src=ask('KiCad .kicad_pcb'); out=ask('Output .bs.json',str(ROOT/'work'/f'{src.stem}.bs.json')); out.parent.mkdir(parents=True,exist_ok=True); run('bs_complete.py','import',src,out); print(out)
            elif c=='2':
                bs=ask('Board Script .bs.json'); out=ask('Output Board IR',str(ROOT/'work'/'board.ir.json')); out.parent.mkdir(parents=True,exist_ok=True); run('board_analyzer.py',bs,*DEFAULTS.values(),out)
            elif c=='3':
                src=ask('KiCad .kicad_pcb',str(ROOT/'upstream'/'isa8_eth'/'KiCad'/'ISA8_Ethernet.kicad_pcb')); out=ask('Build directory',str(ROOT/'work'/src.stem)); out.mkdir(parents=True,exist_ok=True); bs=out/(src.stem+'.bs.json'); run('bs_complete.py','import',src,bs); compile_bs(bs,out)
            elif c=='4':
                bs=ask('Board Script .bs.json'); out=ask('Build directory',str(ROOT/'work'/bs.stem)); compile_bs(bs,out)
            elif c=='5':
                boards=sorted(card_store().rglob('*.vcb.dll'))
                print(f'{len(boards)} manufactured VCB(s):')
                for b in boards: print(' ',b.relative_to(card_store()))
            elif c=='6':
                count=publish_board_count(card_store()); print(f'Published board count: {count}')
            elif c=='7':
                d=json.loads(DEFAULTS['component_store'].read_text(encoding='utf-8')); rec=d.get('records',[]); print(f"Component Store: {len(rec)} records");
                for r in rec[:30]: print(' ',r.get('part_id'),r.get('identity',{}).get('value') or r.get('canonical_key','')[:80])
            elif c=='8':
                tests=['cp14_mutation_test.py','cp15_mutation_test.py','cp16_mutation_test.py','cp18_irq_mutation_test.py','cp19_order_mutation_test.py','cp20_vb_mutation_test.py','cp22_cad_to_vb_mutation_test.py']
                for t in tests:
                    p=TOOLS/t
                    if p.exists(): print(f'\n--- {t} ---'); subprocess.check_call([sys.executable,str(p)],cwd=ROOT)
                print('\nRegression batch completed.')
            elif c=='9':
                print('Python:',sys.version.replace('\n',' ')); print('Executable:',sys.executable); print('VMBS root:',ROOT); print('VCB Store:',card_store())
                for k,p in DEFAULTS.items(): print(f'{k}:', 'OK' if p.exists() else 'MISSING', p)
                print('KiCad CLI:',shutil.which('kicad-cli') or 'not on PATH (not required for compiler core)')
            elif c=='11':
                run('experimental_routing.py')
            elif c=='10': return 0
            else: print('Unknown selection.')
        except (Exception, subprocess.CalledProcessError) as e:
            print(f'\nERROR: {e}')
        input('\nPress Enter to continue...')

if __name__=='__main__': raise SystemExit(main())
