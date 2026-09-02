Virtual Computer ATAPI controller repair
========================================

Drop-in file:
  IdeController.vb

Replace the project's existing IdeController.vb with that file.

Also included:
  IDE-ATAPI-Controller.md          protocol/state-machine documentation
  IdeController.vb.pre-atapi-fix.bak  exact original controller from the uploaded zip
  IdeController.patch.diff        unified diff against the original

Primary fixes:
  - separate master/slave task-file and transfer state
  - simultaneous ATA and ATAPI reset signatures
  - correct ATAPI Interrupt Reason completion (03h for command complete)
  - bounded multi-DRQ PIO data-in phases using the host byte-count limit
  - coherent UNIT ATTENTION / REQUEST SENSE lifecycle
  - explicit rejection of unimplemented packet DMA
  - added READ TOC, MODE SENSE(6/10), READ(12), SEEK, PREVENT/ALLOW,
    and minimal READ SUB-CHANNEL support
  - extensive inline documentation

Validation note:
  This execution environment did not contain dotnet/MSBuild/vbc, so the source
  could not be compiled here. Structural source checks passed (balanced blocks,
  parentheses, and unchanged public controller interface). Build it in the
  project's normal Visual Studio/.NET environment before replacing a known-good
  executable.
