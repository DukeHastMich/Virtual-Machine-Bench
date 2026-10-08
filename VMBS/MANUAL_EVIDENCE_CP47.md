# CP47 Manual evidence — RTL8019AS / 93LC46C / SST39SF040 / FPGA

Primary rule: manuals constrain semantics; topology alone does not authorize invented decode behavior.

## RTL8019AS
- Manufacturer product page: https://www.realtek.com/Product/Index?cate_id=786&id=502&menu_id=1006
- Datasheet copy inspected: https://www.tme.com/Document/f6444b58fe7d137bfcb43c6c92090b97/RTL8019AS.pdf (Realtek RTL8019AS datasheet, 2005-08-26).
- Memory interface evidence: BCSB is active-low BROM chip select; BA21..14 are BROM address; BD7..0 are BROM data; EECS is active-high 9346 select; EESK/EEDI are controller outputs and EEDO is controller input.
- BCSB decode is not arbitrary: it depends on selected BROM base matching SA19..14 and SMEMR#, or SMEMW# when flash-write is enabled.
- BA/BD pins are multiplexed with startup jumper/configuration functions and change role after reset sampling.
- Page-3 9346CR exposes EEPROM mode plus EECS/EESK/EEDI/EEDO. This evidence authorizes an IC-boundary Microwire pin seam; it does not authorize embedding the EEPROM in the controller.

## 93LC46C
- Manufacturer page: https://www.microchip.com/en-us/product/93LC46C
- Primary datasheet DS20001749L: https://ww1.microchip.com/downloads/aemDocuments/documents/MPD/ProductDocuments/DataSheets/93AA46A-B-C-93LC46A-B-C-93C46A-B-C-1-Kbit-Microwire-Compatible-Serial-EEPROM-Data-Sheet-DS20001749.pdf
- 93LC46C is 1 Kbit, ORG-selectable x8/x16, 2.5-5.5 V. CS/CLK/DI/DO are the 3-wire interface; ORG selects organization.
- In x16, READ/WRITE use six address bits and 16 data bits. EWEN/EWDS gate write/erase operations. Device powers up write-disabled.
- Inputs are sampled on rising CLK; DO is normally high-Z except reads/status. This supports the existing pin-level Microwire worker and identifies future high-Z/readiness fidelity work.

## SST39SF040
- Manufacturer page: https://www.microchip.com/en-us/product/SST39SF040
- Primary datasheet DS20001749L: https://ww1.microchip.com/downloads/aemDocuments/documents/MPD/ProductDocuments/DataSheets/SST39SF010A-SST39SF020A-SST39SF040-Data-Sheet-DS20005022.pdf
- Device is 4 Mbit x8 parallel flash, 4.5-5.5 V. Keep flash command/write semantics separate from RTL8019 BROM address-selection semantics.

## FPGA language stress test
- AMD/Xilinx Spartan-3A family datasheet DS529 and Spartan-3 generation configuration guide UG332 were identified as appropriate documented programmable-logic examples.
- Current AMD documentation also reinforces the architectural split: FPGA configuration mode determines configuration pins and I/O-bank voltage constraints; programmed personality is distinct from the physical device/package.
- Board Script therefore needs board-owned configuration topology/payload/pin assignment/bank-voltage facts while Component Store owns reusable device/package/bank/configuration constraints.

## Archive note
The web research environment exposed the manuals for inspection but the isolated build container could not resolve external hosts, so CP47 records source URLs + extracted evidence rather than pretending PDF bytes were archived locally. A later connected ingestion step can cache the exact PDFs and hashes.
