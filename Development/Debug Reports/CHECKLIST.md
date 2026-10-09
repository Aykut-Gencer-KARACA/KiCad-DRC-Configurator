# Debug checklist – where else can bugs hide?

Every finding of a debug round becomes a question here. Before a calculator (or any feature) is called finished, walk
through the whole list. Numbers in brackets point to the report where the pattern was found (01 = bandwidth).

## Inputs
- [ ] Every number box: empty, "abc", 0, negative, "1,5" (comma), 1e-20, 1e20 → red box and "—" results, never an exception. (01)
- [ ] Every unit selector: change the unit with a value typed in; results must follow.
- [ ] A mode switch that changes the meaning of a field also changes its **label** (e.g. rise time ↔ frequency). (01 #1)
- [ ] Parsing and formatting always with `InvariantCulture` (Turkish Windows writes 0,5 and changes i/I).

## Results and formatting
- [ ] Formatters with tiny, huge, zero, negative and NaN values: no "0" for 1e-15, no 16-digit numbers, no "NaN". (01 #4, #5)
- [ ] Units: metric and imperial show the same quantity; the secondary unit in parentheses is correct.

## State
- [ ] Switching calculators, tabs and languages keeps what the user typed (or restores it deliberately). (01 #2)
- [ ] Values taken from the rest of the app (stackup, copper…) follow changes made on other tabs. (01 #3)
- [ ] A list that is refilled keeps the user's choice when it still exists. (01 #3)

## Drawing
- [ ] No `new Font` / `new Pen` / `new Brush` in a paint routine without `using` (GDI leak). (01 #6)
- [ ] Figures drawn with NaN, 0 and extreme values do not throw (unit test calls the painters directly). (01 #5)
- [ ] Labels do not overlap at the smallest window size (900 × 640); drop a label rather than overlap it. (01 #8, #12)
- [ ] Labels keep clear of every marker line and chip in the drawing, for all input values (short, at the limit, long);
      place them with `Fig.FreeSpot`. A label stays in the zone whose meaning/colour it carries. (01 #18–#20)
- [ ] The drawing still makes physical sense (e.g. what is above/below a plane). (01 #13)
- [ ] Nothing is clipped at the figure edges. (01 #14)

## Layout
- [ ] Fixed pixel sizes go through `CalcUi.Px()`; check a screenshot with `KICAD_DRC_TEST_DPI=1.5`. (01 #7)
- [ ] Auto-sized docked panels do not shrink by themselves: measure text instead. (01 #11)
- [ ] Texts containing "&" in labels/lists: `UseMnemonic = false` / `NoPrefix`. (01 #10)

## Explanation pages
- [ ] Every text block wraps at the smallest window width, including text inside boxes and tables. (01 #15)
- [ ] Figures/demos in the guide use a model that is checked against physics (final value, limits, trend). (01)
- [ ] The page answers: what is it, what does the problem look like, how is it calculated, example, what do I do, FAQ, terms.

## Resources
- [ ] Controls kept in a cache but not on screen are disposed when the window closes. (01 #9)

## Reference comparison (Saturn)
- [ ] Compare every output, in both input modes, both unit systems, all option positions.
- [ ] A mismatch: check constants and their rounding (1/7 → 0.142857), coefficients (0.457 vs 0.475), unit conversions.
- [ ] Deliberate deviations: keep a "compat" column that reproduces Saturn exactly and document why ours differs.

## Production outputs (see production_outputs.md)
- [ ] Compare the kicad-cli options with the manufacturer's CURRENT guide (it changes: JLC turned X2 back on for KiCad 9).
- [ ] Anything plotted from a saved board: zone fills refilled (`--check-zones`), otherwise outdated copper is sent.
- [ ] CPL = exactly the placed BOM parts (no empty or unannotated designators, nothing missing); every row inside the outline.
- [ ] Bottom side: rotation convention of the manufacturer (JLC: 180 − angle), X not negated; check with a 0°/180° part.
- [ ] Build the package from a real project copy and read every file, not only the app's own output list.
- [ ] Rotation of polarised parts (electrolytic/tantalum capacitors, diodes, LEDs) depends on the LCSC PART, not on the
      package name (EasyEDA "-FD"/"-RD"; diode pin 1 = K or A). Never apply a correction by name to them: check the part's
      JLC footprint (EasyEDA API: pad 1 position) or report them for the preview. (production_outputs.md, 2026-10-08)
- [ ] Codes written into a schematic: verify by letting KiCad read them back (kicad-cli BOM) and compare netlists/ERC
      before and after; back up first and only write while KiCad is closed.

## Test scripts (PowerShell pitfalls met so far)
- [ ] Function names must not collide with aliases (`h` = Get-History, `compare` = Compare-Object).
- [ ] Values passed to .NET reflection: create them with `[Type]::new()` or unwrap `.PSObject.BaseObject`.
- [ ] In array literals the comma binds tighter than `+`: wrap concatenations in parentheses.
- [ ] A GUI exe only waits in PowerShell when its output is piped (`| Out-String`).
- [ ] Target process bitness matters for cross-process structures (Saturn is 32-bit).
