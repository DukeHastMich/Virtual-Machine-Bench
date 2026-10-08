#!/usr/bin/env python3
"""VMBS CP05 semantic coverage auditor.
Enumerates every KiCad S-expression node head and classifies each occurrence as
SEMANTIC, STRUCTURAL, or PRESERVED_ONLY. Preservation is independently guaranteed
by the byte-exact lexical token stream; this audit measures understanding.
"""
from collections import Counter, defaultdict

# Nodes directly represented by CP03/CP04 semantic dictionaries/inventories.
DIRECT = {
 'footprint':'component/footprint instance+type','module':'legacy footprint instance+type',
 'pad':'pad type+occurrence','segment':'trace segment type+occurrence','via':'via/drill type+occurrence',
 'zone':'zone type+occurrence','net':'net table/connectivity','layer':'layer metadata',
 'gr_line':'drawing','gr_arc':'drawing','gr_circle':'drawing','gr_rect':'drawing','gr_poly':'drawing',
 'gr_curve':'drawing','gr_text':'drawing','dimension':'drawing',
 'fp_line':'footprint drawing','fp_arc':'footprint drawing','fp_circle':'footprint drawing',
 'fp_rect':'footprint drawing','fp_poly':'footprint drawing','fp_text':'component metadata/drawing',
 'property':'component metadata'
}
# Child records whose meaning is already consumed as fields of a direct semantic atom.
STRUCTURAL = {
 'at','start','end','mid','center','pts','xy','size','drill','layers','width','stroke','fill','effects',
 'tstamp','uuid','locked','roundrect_rratio','chamfer_ratio','chamfer','solder_mask_margin',
 'solder_paste_margin','solder_paste_margin_ratio','clearance','zone_connect','thermal_width','thermal_gap',
 'net_name','polygon','filled_polygon','filled_areas_thickness'
}

def audit(ka, roots):
    counts=Counter(); examples=defaultdict(list); parent_counts=defaultdict(Counter)
    stack=[]
    for r in roots:
        if isinstance(r,ka.Node): stack.append((r,None))
    while stack:
        n,parent=stack.pop(); h=ka.head(n) or '<empty>'; counts[h]+=1
        if len(examples[h])<3: examples[h].append({'parent':parent,'start':n.start,'end':n.end})
        parent_counts[h][parent or '<root>']+=1
        for c in ka.child_nodes(n): stack.append((c,h))
    records=[]; semantic=structural=preserved=0
    for h,n in sorted(counts.items(), key=lambda kv:(-kv[1],kv[0])):
        if h in DIRECT:
            status='SEMANTIC'; note=DIRECT[h]; semantic+=n
        elif h in STRUCTURAL:
            status='STRUCTURAL'; note='field/geometry child consumed by semantic parent where supported'; structural+=n
        else:
            status='PRESERVED_ONLY'; note='byte-exactly preserved; no dedicated semantic atom yet'; preserved+=n
        records.append({'head':h,'count':n,'status':status,'note':note,
                        'parents':dict(parent_counts[h].most_common(8)),'examples':examples[h]})
    total=sum(counts.values())
    return {'node_occurrences':total,'semantic_occurrences':semantic,'structural_occurrences':structural,
            'preserved_only_occurrences':preserved,'unique_heads':len(counts),
            'semantic_heads':sum(1 for r in records if r['status']=='SEMANTIC'),
            'structural_heads':sum(1 for r in records if r['status']=='STRUCTURAL'),
            'preserved_only_heads':sum(1 for r in records if r['status']=='PRESERVED_ONLY'),
            'records':records}
