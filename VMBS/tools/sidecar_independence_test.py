#!/usr/bin/env python3
import json, sys
from pathlib import Path

# This is deliberately a HARD readiness gate.  The generator side is allowed to
# see only compact semantic dictionaries/occurrences.  Raw lexical tokens,
# reconstructed source, semantic source trees/spans, and original CAD are forbidden.

def main(p):
    d=json.load(open(p,encoding='utf-8'))
    allowed={'components':d.get('components',{}),'physical':d.get('physical',{})}
    required_sections=['components','footprints','pads','drills','segments','vias','zones','drawings']
    represented={
      'components': allowed['components'].get('component_instance_count',0),
      **{k:allowed['physical'].get(k,{}).get('occurrence_count',0) for k in required_sections[1:]}
    }
    # A true semantic-only KiCad exporter must additionally possess these board-level
    # structures in compact authoritative form. CP06 does not yet expose them outside
    # semantic_promoted/raw parse trees, so generation MUST fail rather than cheat.
    missing=[]
    for name in ['board_header/version/generator','general','layers','setup/design_rules',
                 'net_table','board_outline_and_nonprimitive_graphics','text_payloads_and_properties',
                 'footprint_metadata/properties','zone_full_semantics','plot/document_metadata']:
        missing.append(name)
    result={
      'test':'VMBS semantic-only reconstruction readiness',
      'raw_sidecar_allowed':False,
      'semantic_parse_tree_allowed':False,
      'compact_sections_visible':required_sections,
      'represented_occurrences':represented,
      'result':'FAIL_EXPECTED',
      'reason':'CP06 compact dictionaries/occurrence directories are not yet sufficient to emit a complete KiCad board without consulting preserved parse/source data.',
      'missing_authoritative_compact_sections':missing,
      'acceptance_rule':'PASS only when a fresh .kicad_pcb is emitted using compact VMBS dictionaries/occurrences alone and independently parsed comparison finds zero physical/electrical/property differences.'
    }
    print(json.dumps(result,indent=2))
    return 1
if __name__=='__main__':
    raise SystemExit(main(sys.argv[1]))
