import json, sqlite3, hashlib, re, sys
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
MAN=ROOT/'analysis_cp06'/'ISA8_Ethernet.kicad_pcb.full.manifest.json'
OUT=ROOT/'compiler_db'
OUT.mkdir(exist_ok=True)

def norm(s):
    return re.sub(r'\s+',' ',str(s or '').strip()).upper()

def parse_value(value, lib):
    v=norm(value); l=norm(lib)
    rec={}
    if 'CAPACITOR' in l or l.startswith('CAPACITOR_'):
        rec['class']='capacitor'
        rec['capacitance']=value
    elif 'RESISTOR' in l:
        rec['class']='resistor_array' if 'ARRAY' in l else 'resistor'
        rec['resistance']=value
    elif 'CRYSTAL' in l:
        rec['class']='crystal'; rec['frequency']=value
    elif 'SWITCH' in l:
        rec['class']='switch'
    elif 'CONNECTOR_RJ' in l:
        rec['class']='connector_magnetic_rj45'
    elif 'CONN_EDGE' in l:
        rec['class']='edge_connector'
    elif 'CONN_PIN_HEADER' in l:
        rec['class']='pin_header'
    elif 'MOUNTINGHOLE' in l:
        rec['class']='mounting_hole'
    elif 'RTL8019AS' in v:
        rec['class']='ethernet_controller'
    elif 'SST39SF040' in v:
        rec['class']='parallel_flash'
    elif '93LC46C' in v:
        rec['class']='serial_eeprom'
    elif 'LOGO' in l or 'LOGO' in v:
        rec['class']='board_graphic'
    else: rec['class']='component'
    return rec

def package_from_lib(lib):
    x=lib.split(':',1)[-1]
    return x

def canonical_key(spec):
    # Only source-given intrinsic fields. No reference/position/UUID/net data.
    return json.dumps({
        'library': spec.get('library',''),
        'value': spec.get('value',''),
        'properties': spec.get('properties',{})
    }, sort_keys=True, separators=(',',':'))

def part_id(key):
    # 128-bit stable global identifier; board-local tokens remain compact and independent.
    return hashlib.sha256(key.encode()).hexdigest()[:32].upper()

manifest=json.load(open(MAN,encoding='utf-8'))
dic=manifest['components']['dictionary']
records=[]
local_map=[]
for tok,item in sorted(dic.items(), key=lambda kv:int(kv[0],16)):
    spec=item['spec']; key=canonical_key(spec); pid=part_id(key)
    detail=parse_value(spec['value'],spec['library'])
    rec={
      'part_id':pid,
      'canonical_key':key,
      'display_name':spec['value'],
      'class':detail.pop('class'),
      'source_kind':'cad-derived',
      'record_status':'partial',
      'manufacturer':None,
      'manufacturer_part_number':None,
      'package':package_from_lib(spec['library']),
      'cad_library_id':spec['library'],
      'properties':{**detail, **spec.get('properties',{})},
      'aliases':[spec['value']],
      'sources':[{'kind':'pcb_cad','name':'ISA8_Ethernet.kicad_pcb','field':'value/library'}],
      'behavior_model':None,
      'electrical_model':None,
      'validation_rules':[],
      'synthesis_rules':[]
    }
    # Carefully researched exact records. Keep unknowns unknown.
    if norm(spec['value'])=='RTL8019AS':
        rec.update(manufacturer='Realtek Semiconductor',manufacturer_part_number='RTL8019AS',record_status='researched')
        rec['properties'].update({'package_family':'100-pin PQFP','integrated_sram_bytes':16384,'software_compatibility':['NE2000','RTL8019'],'ethernet':['10Base5','10Base2','10BaseT'],'isa_widths':[8,16],'pnp':True,'full_duplex':True})
        rec['sources'].append({'kind':'manufacturer_web','url':'https://www.realtek.com/Product/Index?cate_id=786&id=502&menu_id=1006'})
    elif norm(spec['value'])=='SST39SF040':
        rec.update(manufacturer='Microchip Technology / SST',manufacturer_part_number='SST39SF040',record_status='researched')
        rec['properties'].update({'density_bits':4194304,'organization':'512K x8','supply_v':[4.5,5.5],'sector_bytes':4096,'memory_type':'parallel_flash','read_access_ns_options':[55,70]})
        rec['sources'].append({'kind':'manufacturer_web','url':'https://www.microchip.com/en-us/product/SST39SF040','datasheet':'DS20005022'})
    elif norm(spec['value'])=='93LC46C':
        rec.update(manufacturer='Microchip Technology',manufacturer_part_number='93LC46C',record_status='researched')
        rec['properties'].update({'density_bits':1024,'interface':'Microwire 3-wire','organization':'hardware selectable x8/x16','supply_v':[2.5,5.5],'max_clock_hz':3000000,'memory_type':'serial_eeprom'})
        rec['sources'].append({'kind':'manufacturer_web','url':'https://www.microchip.com/en-us/product/93LC46C'})
    elif norm(spec['value'])=='RJMG1BD3B8K1ANR':
        rec.update(manufacturer='Amphenol Communications Solutions',manufacturer_part_number='RJMG1BD3B8K1ANR',record_status='researched')
        rec['properties'].update({'ports':'1x1','contacts_per_port':8,'orientation':'right angle','termination':'through hole','integrated_magnetics':True,'performance_category':'100BASE-TX','contact_plating_gold_um':0.76})
        rec['sources'].append({'kind':'manufacturer_web','url':'https://www.amphenol-cs.com/product/rjmg1bd3b8k1anr.html'})
    records.append(rec)
    local_map.append({'token':tok,'part_id':pid,'instances':item['instances']})

# SQLite deterministic record store
sqlp=OUT/'component_store.sqlite3'
if sqlp.exists(): sqlp.unlink()
con=sqlite3.connect(sqlp)
con.executescript('''
PRAGMA foreign_keys=ON;
CREATE TABLE component(
 part_id TEXT PRIMARY KEY,
 canonical_key TEXT NOT NULL UNIQUE,
 display_name TEXT NOT NULL,
 class TEXT NOT NULL,
 source_kind TEXT NOT NULL,
 record_status TEXT NOT NULL,
 manufacturer TEXT,
 manufacturer_part_number TEXT,
 package TEXT,
 cad_library_id TEXT,
 behavior_model TEXT,
 electrical_model TEXT
);
CREATE TABLE alias(part_id TEXT NOT NULL, alias TEXT NOT NULL COLLATE NOCASE, alias_kind TEXT NOT NULL DEFAULT 'source', PRIMARY KEY(part_id,alias), FOREIGN KEY(part_id) REFERENCES component(part_id));
CREATE INDEX alias_lookup ON alias(alias COLLATE NOCASE);
CREATE TABLE property(part_id TEXT NOT NULL, key TEXT NOT NULL, value_json TEXT NOT NULL, PRIMARY KEY(part_id,key), FOREIGN KEY(part_id) REFERENCES component(part_id));
CREATE TABLE source(part_id TEXT NOT NULL, seq INTEGER NOT NULL, source_json TEXT NOT NULL, PRIMARY KEY(part_id,seq), FOREIGN KEY(part_id) REFERENCES component(part_id));
CREATE TABLE validation_rule(part_id TEXT NOT NULL, seq INTEGER NOT NULL, rule_json TEXT NOT NULL, PRIMARY KEY(part_id,seq), FOREIGN KEY(part_id) REFERENCES component(part_id));
CREATE TABLE synthesis_rule(part_id TEXT NOT NULL, seq INTEGER NOT NULL, rule_json TEXT NOT NULL, PRIMARY KEY(part_id,seq), FOREIGN KEY(part_id) REFERENCES component(part_id));
''')
for r in records:
    con.execute('INSERT INTO component VALUES (?,?,?,?,?,?,?,?,?,?,?,?)',(r['part_id'],r['canonical_key'],r['display_name'],r['class'],r['source_kind'],r['record_status'],r['manufacturer'],r['manufacturer_part_number'],r['package'],r['cad_library_id'],r['behavior_model'],r['electrical_model']))
    aliases=set(r['aliases'])
    if r['manufacturer_part_number']: aliases.add(r['manufacturer_part_number'])
    for a in aliases: con.execute('INSERT OR IGNORE INTO alias(part_id,alias) VALUES (?,?)',(r['part_id'],a))
    for k,v in r['properties'].items(): con.execute('INSERT INTO property VALUES (?,?,?)',(r['part_id'],k,json.dumps(v,sort_keys=True)))
    for i,s in enumerate(r['sources']): con.execute('INSERT INTO source VALUES (?,?,?)',(r['part_id'],i,json.dumps(s,sort_keys=True)))
    for i,x in enumerate(r['validation_rules']): con.execute('INSERT INTO validation_rule VALUES (?,?,?)',(r['part_id'],i,json.dumps(x,sort_keys=True)))
    for i,x in enumerate(r['synthesis_rules']): con.execute('INSERT INTO synthesis_rule VALUES (?,?,?)',(r['part_id'],i,json.dumps(x,sort_keys=True)))
con.commit(); con.close()

json.dump({'format':'VMBS-COMPONENT-STORE-1','id_bits':128,'records':records},open(OUT/'component_store.json','w'),indent=2)
json.dump({'format':'VMBS-BOARD-COMPONENT-MAP-1','source':'ISA8_Ethernet.kicad_pcb','local_token_width_bits':8,'dictionary':local_map},open(OUT/'ISA8_Ethernet.component_map.json','w'),indent=2)
print(f'wrote {len(records)} global records and {len(local_map)} local mappings')
