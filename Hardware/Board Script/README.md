# VMB Board Script (`.bs`) prototype

Board Script is a declarative motherboard composition language.  It does **not** contain silicon emulation.  A `.bs` file chooses documented parts, places them, applies board-level straps, and joins sourced pins into named nets.  Behavioral workers remain reusable code modules.

The prototype deliberately separates four truths:

1. `PARTS-METADATA.json` — manufacturer, part number, package, role, provenance.
2. `WORKER-METADATA.json` — part-to-worker binding plus sourced logical/physical pin knowledge.
3. `.bs` — board population, placement, straps, and nets.
4. worker code — actual silicon behavior.

The compiler refuses unknown part IDs, missing workers, unknown pins, duplicate refdes/nets, impossible board coordinates, unsupported rotations, and invalid strap values.  Missing fabrication evidence does **not** become guessed geometry: a board may be logic-valid while `ManufacturingReady=False`.

## Prototype grammar

```text
board <board-id> {
    title "human name";
    units mm;
    outline <width> <height>;

    component <refdes> : <part-id> {
        place <x> <y> rot <0|90|180|270> side <top|bottom>;
        strap <name> = <string|number|true|false>;
    }

    net <name> {
        endpoint <refdes>.<pin>;
        endpoint <refdes>.<pin>;
        ...
    }
}
```

`#` and `//` introduce line comments.

## First worker bindings

- `headland-ht12-a` -> `headland.ht12-a` -> real existing `HeadlandHt12` worker.
- `ct-82c211` -> `ct.neat.82c211` -> split 82C211 prototype worker.
- `ct-82c212` -> `ct.neat.82c212` -> split 82C212 prototype worker.
- `ct-82c215` -> `ct.neat.82c215` -> split 82C215 prototype worker.
- `ct-82c206` -> `ct.neat.82c206` -> split 82C206 prototype worker.

The split NEAT workers are intentionally **parallel prototypes**, not replacements for the already-green live `NeatCs8221Chipset`.  Migration only happens after behavioral equivalence testing.

## Command-line prototype

After a build on a machine with the .NET toolchain:

```text
VirtualComputer.Modern.exe --board-script-check "Board Scripts\Samples\NEAT-CS8221-Reference.bs" --source-root "C:\path\to\VMB"
```

The command parses the script, recursively loads part/worker metadata from the supplied source root, constructs registered workers, validates nets/placement, and reports whether the board has enough evidence to be considered manufacturing-ready. It also writes `<script>.check.txt` unless `--report <path>` is supplied.

The included `INVALID-Invented-Part-Must-Fail.bs` is a required negative test.  If it ever compiles successfully, the Board Script compiler has violated VMB's no-fantasy-hardware rule.

## Deliberately not in prototype 0

No runtime motherboard hot-swap, automatic ISA fabric binding, DRAM topology compiler, full package pin maps, footprints, placement collision geometry, power-domain/ERC, trace routing, or manufacturing export is claimed yet.  Those belong after this grammar/worker boundary proves stable.
