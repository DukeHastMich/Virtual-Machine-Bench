#!/usr/bin/env python3
"""CP06 coverage: every parsed node must have an explicit schema/accounting class."""
from collections import Counter,defaultdict
# Existing semantic atoms / their structural fields.
DIRECT={'footprint','module','pad','segment','via','zone','net','layer','gr_line','gr_arc','gr_circle','gr_rect','gr_poly','gr_curve','gr_text','dimension','fp_line','fp_arc','fp_circle','fp_rect','fp_poly','fp_text','property'}
STRUCTURAL={'at','start','end','mid','center','pts','xy','size','drill','layers','width','stroke','fill','effects','tstamp','uuid','locked','roundrect_rratio','chamfer_ratio','chamfer','solder_mask_margin','solder_paste_margin','solder_paste_margin_ratio','clearance','zone_connect','thermal_width','thermal_gap','net_name','polygon','filled_polygon','filled_areas_thickness'}
# Explicitly promoted CP06 schema fields/families.
PROMOTED={
 'kicad_pcb','version','host','general','thickness','drawings','tracks','zones','modules','nets','page','title_block','title','date','rev','company','comment',
 'setup','last_trace_width','user_trace_width','trace_clearance','zone_clearance','zone_45_only','trace_min','segment_width','edge_width','via_size','via_drill','via_min_size','via_min_drill','uvia_size','uvia_drill','uvia_min_size','uvia_min_drill','uvias_allowed','pcb_text_width','pcb_text_size','mod_edge_width','mod_text_size','mod_text_width','pad_size','pad_drill','pad_to_mask_clearance','aux_axis_origin','visible_elements','pcbplotparams',
 'net_class','trace_width','via_dia','uvia_dia','diff_pair_width','diff_pair_gap','add_net',
 'descr','tags','tedit','path','attr','autoplace_cost180','model','xyz','rotate','scale',
 'font','justify','angle','hatch','connect_pads','min_thickness','thermal_bridge_width','arc_segments',
 'layerselection','usegerberextensions','usegerberattributes','usegerberadvancedattributes','creategerberjobfile','excludeedgelayer','linewidth','plotframeref','viasonmask','mode','useauxorigin','hpglpennumber','hpglpenspeed','hpglpendiameter','psnegative','psa4output','plotreference','plotvalue','plotinvisibletext','padsonsilk','subtractmaskfromsilk','outputformat','mirror','drillshape','scaleselection','outputdirectory'
}

def audit(ka,roots):
 c=Counter(); parents=defaultdict(Counter); records=[]; st=[(r,None) for r in roots if isinstance(r,ka.Node)]
 while st:
  n,p=st.pop(); h=ka.head(n) or '<empty>'; ph=ka.head(p) if p else '<root>'; c[h]+=1; parents[h][ph]+=1
  for x in ka.child_nodes(n): st.append((x,n))
 sem=struc=pres=0
 for h,n in sorted(c.items(),key=lambda x:(-x[1],x[0])):
  # numeric node heads under layers are an explicit KiCad layer-row grammar.
  numeric_layer= h.isdigit() and set(parents[h]).issubset({'layers'})
  if h in DIRECT or h in PROMOTED or numeric_layer: status='SEMANTIC'; sem+=n
  elif h in STRUCTURAL: status='STRUCTURAL'; struc+=n
  else: status='PRESERVED_ONLY'; pres+=n
  records.append({'head':h,'count':n,'status':status,'parents':dict(parents[h])})
 return {'node_occurrences':sum(c.values()),'unique_heads':len(c),'semantic_occurrences':sem,'structural_occurrences':struc,'preserved_only_occurrences':pres,'preserved_only_heads':sum(r['status']=='PRESERVED_ONLY' for r in records),'records':records}
