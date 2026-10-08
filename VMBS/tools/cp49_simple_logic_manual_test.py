#!/usr/bin/env python3
import json, pathlib, sys
ROOT=pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'tools'))
from ic_logic_models import comb
manual=json.loads((ROOT/'compiler_db/simple_logic_manuals_cp49.json').read_text())
store=json.loads((ROOT/'compiler_db/component_store.json').read_text())
code=json.loads((ROOT/'compiler_db/code_store.json').read_text())
families={x['vmb_binding']['module_id']:x for x in store['variant_stub_policy']['families'] if x.get('vmb_binding',{}).get('module_id')}
mods={x['module_id']:x for x in code['modules']}
expected={
 'VMB.LOGIC.74XX00':lambda a,b:1-(a&b),
 'VMB.LOGIC.74XX02':lambda a,b:1-(a|b),
 'VMB.LOGIC.74XX08':lambda a,b:a&b,
 'VMB.LOGIC.74XX32':lambda a,b:a|b,
 'VMB.LOGIC.74XX86':lambda a,b:a^b,
}
for d in manual['parts']:
 mid=d['module_id']; assert mid in families,mid; assert mid in mods,mid
 imp=mods[mid]['implementation']; assert imp['status']=='implemented' and imp['compile_allowed']
 assert (ROOT.parent/imp['source']).exists(),imp['source']
 assert d['primary_url'].startswith('https://')
 if mid=='VMB.LOGIC.74XX04':
  for a in (0,1): assert comb(mid,{'1A':a})['1Y']==1-a
 else:
  for a in (0,1):
   for b in (0,1): assert comb(mid,{'1A':a,'1B':b})['1Y']==expected[mid](a,b)
print('CP49 PASS: 6 simplest 74xx logic families have primary documentation, source-backed implementations, and exhaustive basic truth-table checks.')
