# CP63–CP64 manual evidence

## MC1488 / MC1489
Primary surviving manufacturer datasheets are the Motorola/onsemi MC1488 and MC1489 documents.
- MC1488 is a quad DTL/TTL-to-EIA-232 driver and performs one inversion; EIA-232 driver voltage/slew/load behavior remains electrical-domain evidence rather than Boolean behavior.
- MC1489 is the companion quad EIA-232-to-TTL receiver. Exact analog input thresholds/hysteresis remain electrical-domain evidence.

## MCM6665A
Motorola 1984 Memory Data Book identifies MCM6665A as 65,536 x 1 dynamic RAM, +5 V, multiplexed A0–A7, separate D/Q, RAS/CAS/W, three-state output, page mode, and 128-cycle / 2 ms refresh.

## TMS4256
Texas Instruments MOS Memory documentation identifies TMS4256 as 262,144 x 1 page-mode DRAM. TI's TMS4256/TMS4257 application note describes 9 row + 9 column address bits and 256 refresh cycles in 4 ms.

## Authenticity boundary
CP64 tracks refresh obligations but does not invent deterministic bit corruption after refresh expiry. Real DRAM charge loss is analog/process/temperature dependent. A later electrical/fault model may turn overdue retention into unknown/fault behavior when supported by an explicit simulation policy.

## CP65 Intel 8284A (partial by design)
Intel documentation identifies the 8284A as the iAPX 86/88 clock generator/driver with crystal or TTL source, divide-by-three CPU clock generation, PCLK at half CLK, READY synchronization, and Schmitt-input reset synchronized to CLK. CP65 implements only the clock/reset subset that was directly resolved. READY synchronization and detailed source-selection/CSYNC behavior remain TITS UP, so the Code Store entry is explicitly `partial` and `compile_allowed=false`.
