# Manual Evidence — CP66/CP67

## Intel 8284A
- Intel iAPX 86/88/186/188 User's Manual, order 210912-001 (Intel): documents selected frequency source /3 CLK, PCLK at half CPU clock, OSC behavior, CSYNC forcing/synchronization, and timing relationships.
  - https://www.bitsavers.org/components/intel/8086/210912-001_iAPX_86_88_186_188_Users_Manual_1985.pdf
- Intel-compatible 8284A pin/function documentation confirms RDY1/RDY2 are qualified by active-low AEN1/AEN2; ASYNC high selects one synchronization stage and low selects two; READY is synchronized to CPU clock.
- Implementation boundary: digital timing semantics only. Crystal analog startup, thresholds, propagation tolerances and metastability are not fabricated.

## Intel 8282 / 8283
- Intel MCS-51 User's Manual includes the Intel 8282/8283 device data: STB high makes the latch transparent, high-to-low latches data; OE active low enables three-state outputs; 8282 is non-inverting, 8283 inverting.
  - https://bitsavers.trailing-edge.com/components/intel/8051/MCS-51_Users_Manual_Jan81.pdf

## Intel 8286 / 8287
- Intel iAPX 286 Hardware Reference shows 8287 bus-buffer use in 80286 memory systems; contemporary compatible-device documentation identifies 8286 as eight-bit bidirectional non-inverting and 8287 as its inverting counterpart, with active-low OE and T direction control.
  - https://www.bitsavers.org/components/intel/80286/1983_iAPX_286_Hardware_Reference.pdf
- Harris 1986 Products Guide independently documents 82C86H/82C87H as bipolar-8286/8287-compatible CMOS devices and confirms the functional distinction.
  - https://www.bitsavers.org/components/harris/_dataBooks/1986_Harris_Products_Guide.pdf

## Intel 8288 — CP68 partial
- Intel iAPX 86/88/186/188 User's Manual 210912-001 documents 8288 status sampling and the S2/S1/S0 command decode. The eight status codes map to interrupt acknowledge, I/O read/write, halt, code access, memory read/write, and passive; code access and memory read both generate memory-read command behavior.
  - https://www.bitsavers.org/components/intel/8086/210912-001_iAPX_86_88_186_188_Users_Manual_1985.pdf
- Only the status decoder is implemented in CP68. Clocked command timing, advanced write commands, ALE/DEN/DT-R/MCE-PDEN and AEN/CEN/IOB behavior remain TITS UP; Code Store compile_allowed remains false.
