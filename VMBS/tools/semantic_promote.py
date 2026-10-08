#!/usr/bin/env python3
"""CP06: explicit semantic promotion for every KiCad construct present in the specimen."""
from collections import Counter

ENGINEERING_ROOTS={'setup','net_class','layers','module','footprint','zone','pad','segment','via','net'}
DOCUMENT_ROOTS={'title_block','page','general'}
PLOT_ROOTS={'pcbplotparams'}
TEXT_ROOTS={'effects','font'}
MODEL_ROOTS={'model'}

def ser(ka,n):
    out={'head':ka.head(n),'values':ka.values(n),'children':[],'span':[n.start,n.end]}
    for c in ka.child_nodes(n): out['children'].append(ser(ka,c))
    return out

def _walk(ka,roots):
    st=[(r,None) for r in roots if isinstance(r,ka.Node)]
    while st:
        n,p=st.pop(); yield n,p
        for c in reversed(list(ka.child_nodes(n))): st.append((c,n))

def build(ka,roots):
    sections={k:[] for k in ('board','design_rules','net_classes','layer_table','footprint_metadata','models_3d','text_effects','zone_rules','document_metadata','plot_metadata','legacy_statistics','other')}
    accounted=[]
    for n,p in _walk(ka,roots):
        h=ka.head(n); ph=ka.head(p) if p else None
        rec={'head':h,'values':ka.values(n),'span':[n.start,n.end],'parent':ph}
        # Numeric children of (layers ...) are actual layer table rows, not unknown heads.
        if ph=='layers' and h and h.isdigit():
            rec['layer_id']=int(h); rec['fields']=ka.values(n); sections['layer_table'].append(rec); accounted.append((n.start,n.end,'layer_table_row')); continue
        if h=='kicad_pcb': sections['board'].append(rec)
        elif h=='setup': sections['design_rules'].append(ser(ka,n))
        elif h=='net_class': sections['net_classes'].append(ser(ka,n))
        elif h in ('descr','tags','tedit','path','attr','autoplace_cost180') and ph in ('module','footprint'): sections['footprint_metadata'].append(rec)
        elif h=='model': sections['models_3d'].append(ser(ka,n))
        elif h in ('font','thickness','justify') or ph in ('font','effects'): sections['text_effects'].append(rec)
        elif h in ('hatch','connect_pads','min_thickness','thermal_bridge_width','arc_segments') or (ph in ('zone','fill') and h not in ('polygon','filled_polygon')): sections['zone_rules'].append(rec)
        elif h in ('title_block','title','date','rev','company','comment','page'): sections['document_metadata'].append(rec)
        elif h=='pcbplotparams' or ph=='pcbplotparams': sections['plot_metadata'].append(rec)
        elif ph=='general' or h=='general': sections['legacy_statistics'].append(rec)
        else: sections['other'].append(rec)
        accounted.append((n.start,n.end,h))
    return {'sections':sections,'semantic_node_occurrences':len(accounted),'section_counts':{k:len(v) for k,v in sections.items()}}
