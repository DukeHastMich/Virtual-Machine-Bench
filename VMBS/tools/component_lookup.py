import sqlite3, json, sys
from pathlib import Path
DB=Path(__file__).resolve().parents[1]/'compiler_db'/'component_store.sqlite3'

def lookup(q):
    con=sqlite3.connect(DB); con.row_factory=sqlite3.Row
    row=con.execute('''SELECT c.* FROM component c LEFT JOIN alias a ON a.part_id=c.part_id
                       WHERE c.part_id=? OR a.alias=? COLLATE NOCASE
                       ORDER BY CASE WHEN c.part_id=? THEN 0 ELSE 1 END LIMIT 1''',(q,q,q)).fetchone()
    if not row: return None
    d=dict(row)
    d['aliases']=[x[0] for x in con.execute('SELECT alias FROM alias WHERE part_id=? ORDER BY alias',(d['part_id'],))]
    d['properties']={k:json.loads(v) for k,v in con.execute('SELECT key,value_json FROM property WHERE part_id=? ORDER BY key',(d['part_id'],))}
    d['sources']=[json.loads(x[0]) for x in con.execute('SELECT source_json FROM source WHERE part_id=? ORDER BY seq',(d['part_id'],))]
    return d

if __name__=='__main__':
    if len(sys.argv)!=2: raise SystemExit('usage: component_lookup.py <PART_ID|alias>')
    r=lookup(sys.argv[1]); print(json.dumps(r,indent=2) if r else 'NOT FOUND')
