import json, pathlib
r=pathlib.Path(__file__).resolve().parents[1]
cs=json.load(open(r/'compiler_db/component_store.json')); code=json.load(open(r/'compiler_db/code_store.json'))
mods={m['module_id']:m for m in code['modules']}
for x in cs['records']:
 if x['display_name'].startswith('AT27') and x.get('programmed_state'):
  mid=x['vmb_binding']['module_id']; assert mid in mods
  pc=mods[mid]['implementation']['payload_contract']['capacity_bytes']; assert x['programmed_state']['capacity_bytes']==pc,(x['display_name'],pc,x['programmed_state']['capacity_bytes'])
  assert x['programmed_state']['mutable_in_system'] is False
print('CP129 PASS')
