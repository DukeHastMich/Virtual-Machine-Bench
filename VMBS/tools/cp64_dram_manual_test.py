#!/usr/bin/env python3
from dram_x1_models import MCM6665A,TMS4256
for cls,cells,refresh_rows,window in ((MCM6665A,65536,128,2_000_000),(TMS4256,262144,256,4_000_000)):
 m=cls();assert m.cells==cells and len(m.last_refresh)==refresh_rows
 rb=m.row_bits;cb=m.col_bits
 for row,col in ((0,0),(1,2),((1<<rb)-1,(1<<cb)-1)):
  m.write(row,col,1);assert m.read(row,col)==1;m.write(row,col,0);assert m.read(row,col)==0
 assert len(m.overdue_refresh_rows(window+1))==refresh_rows
 for r in range(refresh_rows):m.refresh(r,window+1)
 assert m.overdue_refresh_rows(window+1)==[]
print('CP64 x1 DRAM manual-backed tests: PASS')
