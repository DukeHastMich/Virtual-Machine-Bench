# Shared Wavetable Banks

This root-level directory is the host SoundFont repository shared by every
wavetable-capable sound card.

Brick 5 automatic selection policy:

1. Use the configured filename when it exists.
2. The factory/automatic request is `SB16.sf2`.
3. If `SB16.sf2` is absent, choose the smallest `.sf2` in this directory.
4. Equal-size fallbacks are ordered by filename for deterministic selection.
5. If the user explicitly selects some other bank and that file later disappears,
   do not silently substitute another bank.
6. If there are no `.sf2` files, the daughterboard remains installed but the host
   renderer stays silent and reports the condition in diagnostics.

Large/third-party `.sf2` files belong here locally and should remain ignored by git.
Brick 5 adds a MeltySynth host renderer solely for loading and MIDI state; PCM output
remains intentionally disconnected until Brick 6.
