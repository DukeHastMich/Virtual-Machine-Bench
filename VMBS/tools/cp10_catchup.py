#!/usr/bin/env python3
import json, sqlite3, hashlib, re
from pathlib import Path
from kicad_atomizer import parse_sexpr, head, child_nodes, values, scalar_text, Node
ROOT=Path(__file__).resolve().parents[1]
DB=ROOT/'compiler_db'; SRC=ROOT/'upstream/isa8_eth/KiCad/ISA8_Ethernet.kicad_pcb'

def scalar(v):
    if isinstance(v,tuple): return scalar_text(v)
    return None

def node_obj(n):
    # used only inside named native BS families, never as exporter access to source
    out={'kind':head(n),'values':values(n),'fields':[]}
    for c in child_nodes(n): out['fields'].append(node_obj(c))
    return out

def first(root,h): return next(child_nodes(root,h),None)

def bs_core():
    root=parse_sexpr(SRC.read_bytes())[0]
    hdr={}
    for h in ('version','host','page'):
        n=first(root,h); hdr[h]=values(n) if n else None
    tb=first(root,'title_block')
    layers=first(root,'layers'); setup=first(root,'setup'); general=first(root,'general')
    nets=[]; net_classes=[]
    for n in child_nodes(root,'net'):
        v=values(n); nets.append({'id':int(v[0]),'name':v[1]})
    for n in child_nodes(root,'net_class'): net_classes.append(node_obj(n))
    graphics=[]
    for typ in ('gr_line','gr_arc','gr_text'):
        for n in child_nodes(root,typ): graphics.append(node_obj(n))
    out={'format':'VMBS-BOARD-CORE-1','source_provenance':{'name':SRC.name,'sha256':hashlib.sha256(SRC.read_bytes()).hexdigest()},
         'header':hdr,'general':node_obj(general) if general else None,'title_block':node_obj(tb) if tb else None,
         'layers':node_obj(layers) if layers else None,'setup':node_obj(setup) if setup else None,
         'nets':nets,'net_classes':net_classes,'board_graphics':graphics,
         'counts':{'nets':len(nets),'net_classes':len(net_classes),'board_graphics':len(graphics)}}
    (ROOT/'bs_native').mkdir(exist_ok=True)
    (ROOT/'bs_native/ISA8_Ethernet.core.bs.json').write_text(json.dumps(out,indent=2))
    return out

def component_v2():
    p=DB/'component_store.json'; d=json.loads(p.read_text())
    d['format']='VMBS-COMPONENT-STORE-2'; d['schema_version']=2
    d['id_policy']={'current_bits':128,'status':'provisional','note':'PART_ID width/namespace not frozen; BS local tokens isolate files from this decision.'}
    research={
      'RTL8019AS': {'manufacturer':'Realtek','mpn':'RTL8019AS','class':'ethernet_controller','package_family':'PQFP-100','interfaces':['ISA8','ISA16','NE2000','10BASE-T','AUI','BNC'],'electrical':{},'memory':{'internal_sram_bytes':16384},'configuration':['jumper','jumperless','PnP'],'vmb_module_id':'VMB.NET.RTL8019AS','support_requirements':['POWER_DECOUPLING','CLOCK_SOURCE','EEPROM_9346_COMPATIBLE','ETHERNET_MAGNETICS_IF_10BASET','ISA_CONFIGURATION_REALIZATION'], 'sources':[{'kind':'manufacturer_product_page','url':'https://www.realtek.com/Product/Index?cate_id=786&id=502&menu_id=1006','authority':'Realtek'}]},
      'SST39SF040': {'manufacturer':'Microchip/SST','mpn':'SST39SF040','class':'parallel_flash','interfaces':['JEDEC_X8_PARALLEL_MEMORY'],'electrical':{'vcc_min_v':4.5,'vcc_max_v':5.5},'memory':{'density_bits':4194304,'organization':'512Kx8','sector_bytes':4096,'endurance_cycles_typ':100000,'data_retention_years_min':100},'timing':{'read_access_ns_options':[55,70]},'vmb_module_id':'VMB.MEM.SST39SF040','support_requirements':['POWER_DECOUPLING'], 'sources':[{'kind':'datasheet','document':'DS20005022F','url':'https://ww1.microchip.com/downloads/aemDocuments/documents/MPD/ProductDocuments/DataSheets/SST39SF010A-SST39SF020A-SST39SF040-Data-Sheet-DS20005022.pdf','authority':'Microchip'}]},
      '93LC46C': {'manufacturer':'Microchip','mpn':'93LC46C','class':'serial_eeprom','interfaces':['MICROWIRE_3WIRE'],'electrical':{'vcc_min_v':2.5,'vcc_max_v':5.5},'memory':{'density_bits':1024,'organizations':['128x8','64x16'],'organization_select_pin':'ORG','endurance_cycles_min':1000000,'data_retention_years_min':200},'timing':{'clock_max_hz':3000000,'erase_write_ms_max':6},'vmb_module_id':'VMB.MEM.MICROWIRE_93LC46C','support_requirements':['POWER_DECOUPLING','ORG_STRAP_OR_CONTROL'], 'sources':[{'kind':'datasheet','document':'DS21749H','url':'https://ww1.microchip.com/downloads/en/DeviceDoc/21749H.pdf','authority':'Microchip'}]}
    }
    for r in d['records']:
        r.setdefault('aliases',[]); r.setdefault('pins',[]); r.setdefault('interfaces',[]); r.setdefault('electrical',{}); r.setdefault('timing',{}); r.setdefault('thermal',{}); r.setdefault('support_requirements',[]); r.setdefault('vmb_binding',{'module_id':None,'resolution':'none'})
        if r['display_name'] in research:
            q=research[r['display_name']]; r['manufacturer']=q['manufacturer']; r['manufacturer_part_number']=q['mpn']; r['class']=q['class']; r['record_status']='researched_partial'
            for k in ('interfaces','electrical','timing','support_requirements'): r[k]=q.get(k,r.get(k))
            r['memory']=q.get('memory',{}); r['configuration_modes']=q.get('configuration',[]); r['package_family']=q.get('package_family')
            r['vmb_binding']={'module_id':q['vmb_module_id'],'resolution':'device','status':'binding_contract_created'}
            # preserve CAD provenance, append authoritative sources without duplicates
            r['sources']=[s for s in r.get('sources',[]) if s.get('kind')=='pcb_cad']+q['sources']
    p.write_text(json.dumps(d,indent=2))
    (DB/'component_store_v2.schema.json').write_text(json.dumps({'format':'VMBS-COMPONENT-STORE-SCHEMA-2','required_record_fields':['part_id','display_name','class','record_status','aliases','pins','interfaces','electrical','timing','thermal','support_requirements','vmb_binding','sources'],'vmb_binding':{'module_id':'string|null','resolution':'none|primitive|device','status':'string'},'fact_rule':'Unknown values remain absent/null; never infer unspecified manufacturer/tolerance/rating.'},indent=2))
    # rebuild sqlite as indexed deterministic lookup
    sp=DB/'component_store.sqlite3'; sp.unlink(missing_ok=True); con=sqlite3.connect(sp); c=con.cursor()
    c.executescript('''CREATE TABLE parts(part_id TEXT PRIMARY KEY,display_name TEXT,class TEXT,manufacturer TEXT,mpn TEXT,record_status TEXT,module_id TEXT,record_json TEXT NOT NULL); CREATE TABLE aliases(alias TEXT COLLATE NOCASE,part_id TEXT,PRIMARY KEY(alias,part_id)); CREATE INDEX idx_parts_mpn ON parts(mpn); CREATE INDEX idx_parts_module ON parts(module_id);''')
    for r in d['records']:
        mid=r.get('vmb_binding',{}).get('module_id'); c.execute('INSERT INTO parts VALUES(?,?,?,?,?,?,?,?)',(r['part_id'],r['display_name'],r.get('class'),r.get('manufacturer'),r.get('manufacturer_part_number'),r.get('record_status'),mid,json.dumps(r,separators=(',',':'))))
        for a in set(r.get('aliases',[])+([r.get('manufacturer_part_number')] if r.get('manufacturer_part_number') else [])): c.execute('INSERT OR IGNORE INTO aliases VALUES(?,?)',(a,r['part_id']))
    con.commit(); con.close(); return d

def stores():
    code={'format':'VMBS-CODE-STORE-2','default_binding':'NONE','principle':'MODULE_ID is stable; implementation locator is replaceable. Configuration points are declarative compiler/synthesis inputs.','modules':[
      {'module_id':'VMB.NET.RTL8019AS','kind':'device','implementation':{'language':'VB.NET','status':'lookup_pending_existing_VMB_code'},'interfaces':['ISA_BUS','IRQ','BOOT_ROM','EEPROM','ETHERNET'],'configuration_points':[{'id':'IO_BASE','type':'enum','machine_effect':'ISA I/O decode base','physical_realization':'jumper|jumperless|PnP'},{'id':'IRQ','type':'enum','machine_effect':'ISA interrupt selection','physical_realization':'jumper|jumperless|PnP'},{'id':'BOOT_ROM_ENABLE','type':'boolean','machine_effect':'boot ROM mapping enabled','physical_realization':'jumper|configuration'}]},
      {'module_id':'VMB.MEM.SST39SF040','kind':'programmed_memory','implementation':{'language':'VB.NET','status':'module_or_generic_flash_binding_pending'},'configuration_points':[{'id':'CONTENTS','type':'image','machine_effect':'programmed flash bytes','physical_realization':'programmed_device_contents'}]},
      {'module_id':'VMB.MEM.MICROWIRE_93LC46C','kind':'programmed_memory','implementation':{'language':'VB.NET','status':'module_or_generic_eeprom_binding_pending'},'configuration_points':[{'id':'CONTENTS','type':'image','machine_effect':'EEPROM bytes/words','physical_realization':'programmed_device_contents'},{'id':'ORG','type':'enum','allowed':['x8','x16'],'machine_effect':'memory organization','physical_realization':'strap|controller_pin'}]}
    ]}
    (DB/'code_store.json').write_text(json.dumps(code,indent=2))
    circuits={'format':'VMBS-CIRCUIT-STORE-2','recipes':[
      {'circuit_id':'POWER_DECOUPLING','purpose':'local supply decoupling','inputs':['POWER_PIN_GROUP','GROUND'],'outputs':['DECOUPLED_POWER_GROUP'],'constraints':['component record/rule supplies capacitance and placement limits when known'],'required_parts':['CAPACITOR'],'generated_requirements':['PLACEMENT_NEAR_POWER_GROUP']},
      {'circuit_id':'ORG_STRAP_OR_CONTROL','purpose':'realize selectable logic strap','inputs':['CONFIG_PIN','LEGAL_LOGIC_LEVELS'],'outputs':['PHYSICAL_CONFIGURATION_POINT'],'required_parts':['DIRECT_STRAP_OR_JUMPER_OR_SWITCH'],'generated_requirements':[]},
      {'circuit_id':'ISA_CONFIGURATION_REALIZATION','purpose':'realize VMB-visible ISA configuration points','inputs':['MODULE_CONFIGURATION_POINTS'],'outputs':['JUMPER_DIP_STRAP_OR_PNP_CONFIGURATION'],'constraints':['preserve legal values declared by MODULE_ID'], 'required_parts':[],'generated_requirements':['SILKSCREEN_LABELS_WHEN_PHYSICAL']},
      {'circuit_id':'CLOCK_SOURCE','purpose':'satisfy IC clock endpoint','inputs':['CLOCK_REQUIREMENT'],'outputs':['CLOCK_SIGNAL'],'required_parts':['CRYSTAL_OR_OSCILLATOR_AND_SUPPORT_AS_REQUIRED'],'generated_requirements':['PLACEMENT','ROUTING_CLOCK_RULES']}
    ]}
    (DB/'circuit_store.json').write_text(json.dumps(circuits,indent=2))
    diag={'format':'VMBS-DIAGNOSTICS-1','tits':{'name':'Truncated Information To Substrate','type':'bit','DOWN':0,'UP':1,'rule':'UP only when unresolved information is required for the requested target; details are separate diagnostics.'},'resolution_states':['known_relevant','known_irrelevant','unknown_nonblocking','unresolved_required']}
    (DB/'diagnostics.schema.json').write_text(json.dumps(diag,indent=2))
    content={'format':'VMBS-PROGRAMMED-CONTENTS-1','principle':'Device identity is PART_ID; programmed bytes/equations belong to occurrence/build data.','binding_schema':{'occurrence_id':'board instance','part_id':'physical device identity','content_kind':'rom|eprom|eeprom|flash|pal_gal|firmware','blob_ref':'content-addressed or project path','sha256':'required when contents present'}}
    (DB/'programmed_contents.schema.json').write_text(json.dumps(content,indent=2))

def main():
    b=bs_core(); d=component_v2(); stores()
    status=f'''# CP10 STATUS\n\nCatch-up implementation checkpoint.\n\n## Implemented\n- Component Store promoted to schema v2; all {len(d['records'])} seed records carried forward.\n- RTL8019AS, SST39SF040 and 93LC46C records enriched from authoritative manufacturer material, without inventing unspecified facts.\n- Deterministic SQLite indexes rebuilt for PART_ID, aliases/MPN and MODULE_ID.\n- Code Store v2 seeded with three VMB-resolution MODULE_ID contracts and declarative configuration points.\n- Programmed-device content schema separates physical part identity from ROM/EEPROM/Flash contents.\n- Circuit Store v2 seeded with reusable deterministic support/configuration recipes.\n- TITS diagnostic contract frozen as a one-bit gate with separate detailed resolution states.\n- Native BS core snapshot now captures header/general/title/layers/setup/net table/net classes and {b['counts']['board_graphics']} board graphics outside the old CP07 compact physical dictionaries.\n\n## Still blocking\nCP07 sidecar-independent export is NOT yet a pass. Footprint metadata, full zones, text/graphics normalization and exporter/semantic comparator still need closure before claiming a complete `.bs` format.\n'''
    (ROOT/'CP10_STATUS.md').write_text(status)
    with (ROOT/'FORENSIC_SESSION.md').open('a') as f: f.write('\n\n## CP10 catch-up\nImplemented Component Store v2, Code Store v2, programmed contents, circuit recipes, TITS diagnostics, and first native BS core families. CP07 remains deliberately failing until sidecar-blind export + independent semantic comparison pass.\n')
main()
