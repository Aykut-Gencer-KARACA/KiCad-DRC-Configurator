# Debug report – Production outputs (Gerber, drill, CPL, BOM)

Date: 2026-09-27 · Version 2.2 (development) · Test board: copy of BLDC Driver RevC (6 layers, 112 footprints, 34 on the bottom)

## Method

1. **Reference:** JLC's current guides, read on 2026-09-27:
   - "How to Generate Gerber and Drill Files From KiCad 9";
   - "How To Export BOM and CPL Files From KiCad 10";
   - "Pick & Place File for PCB Assembly".
   For the bottom-side rotation, JLC's pages say nothing, so the convention of the JLC tools was compared:
   - Fabrication Toolkit (recommended by JLC's KiCad 10 guide);
   - KiBot's JLCPCB preset ("mirror_bottom: 180 - rot … used by JLCPCB");
   - Bouni's kicad-jlcpcb-tools.
2. **kicad-cli 10.0.6 options** of `pcb export gerbers / drill / pos` compared with what the app passes.
3. **Package built from the RevC copy** and every file inspected:
   - zip contents;
   - Gerber and Excellon headers;
   - every CPL row against the footprint data in the .kicad_pcb (position, side, rotation, inside the board outline);
   - BOM designators against CPL designators.
4. **Zone fills:** copper Gerbers plotted with and without `--check-zones` and compared.
5. **Footprint origin vs pad centre** of every placed part (JLC wants the part centre; KiCad writes the footprint origin).

## Findings and fixes

| # | File | Problem | Fix | Covered by |
|---|------|---------|-----|------------|
| 1 | Gerber | Zone fills were not checked before plotting (kicad-cli plots the fills saved in the file). On the RevC copy the saved F.Cu fill was outdated: 293 Gerber lines differed from a refill. The package would have contained **old copper pours**. | `--check-zones` (JLC guide: "Check zone fills before plotting"). The board file is not changed (hash checked). | F.Cu of the package identical to the refilled reference |
| 2 | Drill | PTH and NPTH holes were in one file (`MixedPlating`). JLC wants them separate. | `--excellon-separate-th` → `-PTH.drl`, `-NPTH.drl` (+ one map each); origin, zeros and oval mode written explicitly. | zip contents, headers |
| 3 | Gerber | X2 and netlist attributes were switched off (an older JLC recommendation); the KiCad 9 guide asks for both on. Silkscreen was not clipped at mask openings. | X2 + netlist on (kicad-cli default), `--subtract-soldermask`. | headers |
| 4 | CPL | Bottom-side rotation was KiCad's own angle. JLC tools use 180 − angle. Parts at ±90° are the same either way; parts at 0°/180° were 180° off, so a polarised part would have been placed reversed. On RevC the 11 affected parts are all non-polarised. | `Production.JlcRotation`: bottom 180 − angle; all angles 0 ≤ r < 360. | unit: 11 cases; all 104 rows checked against the PCB |
| 5 | CPL | Footprints not in the placed BOM went into the CPL: two footprints with an **empty designator** and an unannotated fiducial `REF**`. | `Production.CplForBom`: the CPL holds exactly the placed BOM parts. Left-out footprints are reported as a build warning. | unit; RevC: 104 CPL rows = 104 BOM designators |
| 6 | CPL | A placed BOM part without a footprint on the board was not detected. JLC would silently not place it. | Build stops before writing anything and names the parts ("update PCB from schematic or untick Place"). | unit |
| 7 | xlsx | KiCad's empty value "~" was shown in the CPL sheet. | Shown empty. | xlsx checked |
| 8 | Text | The "files ready with warnings" banner said "(stock / part information)"; warnings can now also come from the CPL. | Generic text. | gui |

## Checked and correct (no change)

- **Coordinates:**
  - Gerber, drill and CPL all use KiCad's absolute origin, so they are consistent;
  - Y is up (KiCad negates it);
  - bottom X is not negated, as the JLC tools do;
  - all 104 positions are inside the board outline.
- **Formats:**
  - Gerber RS-274X, 4.6 mm, absolute;
  - Protel extensions;
  - all copper, mask, silk and paste layers plus Edge.Cuts;
  - Excellon metric, decimal, absolute, alternate oval mode.
- **BOM:**
  - JLC columns Comment/Designator/Footprint/LCSC Part #;
  - DNP and "Place" unticked are excluded;
  - an empty value is replaced by the LCSC code.
- **Footprint origin vs pad centre:** only U7 (TO-252) differs, by 1.0 mm. That is the asymmetric tab pad; the EasyEDA footprint is body-centred.

## Not done (ideas, not decided)

- **Rotation / position correction per footprint** like the JLC tools' databases. JLC's library orientation differs from KiCad's for some packages, e.g. some SOT-23, diodes, electrolytic capacitors. Today the JLC preview must be checked; the xlsx note says so, bottom side in particular.
- **DRC before building.** JLC's guide asks for 0 errors / 0 warnings. The build could report the DRC counts (with zones refilled).
- JLC has changed its bottom-side convention in the past (KiBot notes): if the preview shows bottom parts turned by 180°, `JlcRotation` is the only place to change.

## Comparison with the Fabrication Toolkit plugin (same evening, RevC copy)

The user ran the plugin (bennymeg, v. installed 2026-09-27; options: AUTO FILL on, AUTO TRANSLATE on, EXCLUDE DNP off).
Its output (RevC\production) was compared with ours, built from the same saved board.

| File | Result |
|------|--------|
| Gerber (13 layers), PTH/NPTH drill, both drill maps | **Geometry identical**, line by line (apertures + draw commands). Only the attribute style differs: X2 `%TF` in ours, `G04` comments in the plugin's X1. Ours adds the .gbrjob. |
| CPL vs positions.csv | Positions identical for all 104 common parts. Rotation identical for 88, including every bottom part (confirms 180 − angle). |
| | **16 rotations differ**: exactly the parts matched by the plugin's correction table. |
| | 13 are EasyEDA footprints (U1 QFN +90, U2–U5 SOT-23 +180, U6 LQFP +270, Q1–Q7 DFN +270). EasyEDA footprints are drawn in JLC's orientation (pin-1 suffix -BL/-BR), so the plugin's corrections for KiCad-library footprints over-rotate them: **ours right**. |
| | 3 are KiCad `Capacitor_SMD:CP_Elec_6.3x7.7` (C32, C41, C46). Both JLC tools' tables give +180 for this KiCad footprint: **ours probably places these electrolytic capacitors reversed**; must be checked in the JLC preview. |
| | The plugin also writes 7 rows JLC cannot place: J1–J5 (solder pads, no LCSC code), and the two unnamed footprints as "_2" and "". |
| BOM | The plugin BOM has **no LCSC code in any of its 39 rows**: it reads codes from symbol fields, ours are in `<project>.lcsc.json`. It also lists J1–J5 and the two unnamed footprints; U6's value is "~". |
| netlist.ipc | IPC-D-356 netlist; ours carries the netlist as X2 attributes in the Gerbers. kicad-cli can export IPC-D-356 too (idea). |

Side observation: the board's mask bridge setting `(solder_mask_min_width 0.1)` disappeared from RevC at the 22:37 autosave
(KiCad local history). The plugin sets the minimum mask width to 0 on the open board and does not restore it; whether this
was the plugin or a manual change is not known. The mask Gerbers of both outputs are identical, so it does not change the
comparison.

## Aligned with the plugin (user: "make it the same as the plugin, but correct")

`Production\JlcPlacement.cs`:
- **Rotation corrections:** the plugin's correction table (same rows, order and matching rules; Apache-2.0, source named in the
  code). It is applied to KiCad library footprints only; EasyEDA / LCSC footprints get none. They are recognised by their
  library name (easyeda/lcsc/jlc) or EasyEDA's "_L…-W…" size notation.
  Evidence that they must not be corrected: Fabrication-Toolkit issue #135 (auto translation rotated correct EasyEDA
  chips; the author acknowledged it), kicad-jlcpcb-tools #752 (EasyEDA names encode JLC's pin-1 orientation).
- **Position:** non-SMD footprints are placed at the centre of their pads' bounding box, like the plugin.
- **File formats:** CPL and BOM columns identical to the plugin. CPL: Designator,Mid X,Mid Y,Rotation,Layer; plain mm; top/bottom.
  BOM: Designator,Footprint,Quantity,Value,LCSC Part #; "C1, C2"; 0603 etc.
- **Netlist:** an IPC-D-356 netlist is added (`<name>_Netlist.ipc`, like the plugin's netlist.ipc).

Verified on the RevC copy (same saved board as the plugin run):

| Output | Result |
|--------|--------|
| Pad transform | 362/362 SMD pads equal the Gerber flashes (0 µm) and 7/7 THT pads equal the drill hits; both sides, 0/90/180/−90°. |
| Gerber + drill | 17/17 files geometry identical to the plugin (ours adds the job file; X2 vs X1 attribute style only). |
| CPL | Same header. 91 rows identical to the plugin, now including C32/C41/C46 (+180°). |
| | 13 differ on purpose: the EasyEDA parts U1–U6 and Q1–Q7. |
| | The plugin's 7 unplaceable rows (J1–J5, "", "_2") are not written. |
| BOM | Same header. All 37 groups have the same designators, footprint, quantity and value. |
| | LCSC codes: ours 37/37, plugin 0/37. The plugin's two junk groups (J1–J5, unnamed footprints) are not written. |
| IPC-D-356 | Identical to the plugin's netlist.ipc (623 data lines). |

The build result lists the corrected parts ("C32 +180°, …") so exactly those can be checked in JLC's preview.

## Correction 2026-10-08: polarised capacitors must NOT get the plugin's +180°

The plugin's table turns every KiCad `CP_Elec_*` / `C_Elec_*` / `CP_EIA-*` footprint by 180°. Checked against JLC's own data:
- C46550415 (the electrolytic used until then): its EasyEDA footprint is "CAP-SMD_BD6.3-L6.6-W6.6-LS7.3-FD". The EasyEDA API
  gives pad 1 at x 3905.988 and pad 2 at 3927.012 (origin 3916.5), so pad 1 is left; the "+" mark and the chamfered body
  corner are also on the left. KiCad's CP_Elec_6.3x7.7 has pad 1 (+) on the left too, so no turn is needed. The 2026-09-27
  CPL would have placed C32/C41/C46 **reversed**.
- "-FD" (forward) vs "-RD" (reversed) is a property of the LCSC part, not of the package (kicad-jlcpcb-tools #752), so a
  table keyed by footprint name cannot know it.
- C46528102 (KNSCHA 118EC450, the new part) has no EasyEDA device at all: JLC places it by the board's + mark.

Fix: polarised capacitors with KiCad footprints are not turned; the build lists them ("check the + in JLC's preview, choose
Confirm Parts Placement"). All other table rows (QFP, SOT-23, …) are unchanged.

## LCSC codes from the schematic (2026-10-08)

- RevC schematic: the verified code (parts list RevH, every code checked in JLC's database: MPN, maker, package, value) was
  written into the "LCSC Part" field of all 111 placed symbols (110 parts; U6 has two units).
  - 85 fields were added to KiCad library symbols; 26 empty EasyEDA fields were filled.
  - KiCad re-read the file: 110/110 codes correct.
  - Netlist nets and all other component data are identical to the backup; ERC is unchanged.
  - The project got a field name template "LCSC Part", so new symbols show the field in their E dialog.
  - J1–J5 (solder wire pads) were marked "Exclude from bill of materials": selecting the project is now fully automatic.
- App: `Production.Bom` reads "LCSC Part" (then "LCSC") first. Rows are grouped by value + footprint + code. A
  schematic code is shown blue and cannot be edited in the grid. `.lcsc.json` keeps only codes entered for parts without
  a schematic code, plus the "Place" marks.

## 2026-10-08: price and cost check (`JlcCost.cs`)

- Prices: the 40 JLC part pages of RevC show the same price ladder and attrition fields as the app's service (40/40).
- The old cost multiplied the tier price of the plain need. JLC charges more: it adds attrition and has a minimum
  quantity, and the tier is chosen by that charged quantity. Rule, checked against JLC's live PCBA calculators
  (800/800 rows up to 100 boards, 280/280 large rows above the reel size):
  - attrition = k × (lossNumber + floor(0.002 × max(0, need − encapsulationNumber))), k = 2 when both sides are assembled;
  - charged = max(need + attrition, leastPatchNumber).
- RevC parts: 5 boards $72.86 → $80.82, 10 boards $132.38 → $139.64. The xlsx equals the live calculator line by line.
- Assembly fees from jlcpcb.com/help/article/pcb-assembly-price (2026-09-09): setup, stencil, feeder loading, SMT and
  manual joints, hand-soldering labour, X-ray of leadless parts (QFN/DFN/BGA), Standard packing. Fees the page gives no
  rule for (fixtures, pre-reflow soldering, handling fee, Confirm Parts Placement) are listed in the xlsx note, not added.
- The xlsx BOM sheet has a "Charged by JLC" column; the stock warning uses the charged quantity.

## Test status after the fixes

Unit 140/140 · Window 50/50 (five production files; schematic codes) · KiCad verification 26/26 (2026-10-08).
Saturn not re-run (no calculator change).
