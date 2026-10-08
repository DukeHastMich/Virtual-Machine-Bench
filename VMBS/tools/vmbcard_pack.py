#!/usr/bin/env python3
"""Package VMB-BOARD-CODE-1 as a runtime-loadable VMB-CARD-1 artifact."""
import argparse, json, pathlib

def scalar(v):
    if isinstance(v,(str,int,float,bool)) or v is None: return v
    return v

def build(board):
    ins=board['instructions']
    modules=[x for x in ins if x.get('op')=='ALLOC_MODULE']
    platform=next(x for x in ins if x.get('op')=='ATTACH_PLATFORM')
    controls=[x for x in ins if x.get('op')=='CREATE_CONTROL']
    props=[x for x in ins if x.get('op')=='DEFINE_PROPERTY' and x.get('property') in ('IO_BASE','IRQ','BOOT_ROM')]
    primary=next((m for m in modules if m['module_id'].startswith('VMB.NET.')),modules[0])
    fin=next(x for x in ins if x.get('op')=='FINALIZE_BOARD')
    if fin.get('hardware_tits') != 0 or fin.get('default_execution_tits') != 0:
        raise SystemExit('Refusing to package a card that is incomplete for VMB default execution')
    install={
      'card_id':'SERGEY.ISA8.RTL8019',
      'display_name':'ISA 8-bit Ethernet Controller — RTL8019AS (compiled BS)',
      'category':'NETWORK','bus':'ISA8','connector_bits':8,
      'board_code_format':board['format'],'primary_module_id':primary['module_id'],
      'required_modules':[m['module_id'] for m in modules],
      'controls':[{'id':c['id'],'kind':c['kind'],'elements':c['elements'],'change_while_powered':c['change_while_powered']} for c in controls],
      'properties':[{'name':p['property'],'allowed':p['allowed_values'],'default':p.get('default_value'),'selectors':p.get('selectors',[])} for p in props],
      'persistent_devices':[x for x in ins if x.get('op')=='CREATE_DEVICE_SLOT'],
      'completion':{'hardware_tits':fin['hardware_tits'],'execution_tits':fin['execution_tits'],'default_execution_tits':fin['default_execution_tits']},
      'contraction':{'kind':'FAT_DEFAULT_EXECUTION','root':primary['id'],'note':'Current VB Ne2000Isa backend supplies default boot-ROM-disabled NIC behavior; discrete flash/EEPROM modules exist for later skinny wiring.'}
    }
    return {'format':'VMB-CARD-1','install':install,'board_code':board}

def main():
    ap=argparse.ArgumentParser(); ap.add_argument('board_code'); ap.add_argument('output'); a=ap.parse_args()
    board=json.load(open(a.board_code,encoding='utf-8'))
    if board.get('format')!='VMB-BOARD-CODE-1': raise SystemExit('Expected VMB-BOARD-CODE-1')
    out=build(board); p=pathlib.Path(a.output); p.parent.mkdir(parents=True,exist_ok=True)
    p.write_text(json.dumps(out,indent=2,sort_keys=False)+'\n',encoding='utf-8')
    print(p)
if __name__=='__main__': main()
