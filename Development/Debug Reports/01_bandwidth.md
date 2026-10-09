# Debug report – Calculator 1: Bandwidth & max conductor length

Date: 2026-09-27 · Version 2.2 (development)

## Method

1. **Code review** of every file of the calculator (math, view, figures, texts) and of the calculator tab in the window.
2. **Unit tests** (`Source\Tests\unit-tests.ps1`): formulas against Saturn reference values, number formatting at
   extreme magnitudes, all four figures drawn with extreme / invalid values (must never throw).
3. **Window tests** (`Source\Tests\gui-tests.ps1`): the real window driven off screen: live update, invalid input,
   long-trace warning, units, stackup change on the left while the calculator keeps its inputs, "How it works" page,
   smallest window size, language switch with the calculator tab open.
4. **Parallel check against Saturn PCB Toolkit 8.47** (`Source\Tests\saturn-compare-bandwidth.ps1`): 265 values.
5. **Visual checks** of screenshots: normal size, smallest window (900 × 640), simulated 150 % screen scaling.

## Findings and fixes

| # | Area | Problem | Found by | Fix | Covered by |
|---|------|---------|----------|-----|------------|
| 1 | View | In frequency mode the first result showed the equivalent rise time under the label "Bandwidth". | review | The label switches to "Equivalent rise time (0.35 / f)". | review |
| 2 | Tab | Switching to another tab and back rebuilt the calculator: every input was lost. | review | Views are built once and kept; the selected stackup is passed in with a `RulesChanged` event. | gui: "same view kept, input kept" |
| 3 | Tab | The view was built before the stackup existed at start-up, so the "from stackup" materials were missing; later stackup changes were not seen. | screenshot | Views are built when the tab is shown; the material list is refreshed on stackup change and keeps the chosen entry (outer / inner). | gui: "material list follows the new stackup" |
| 4 | Formatting | Tiny values were shown as "0", huge ones as 16-digit numbers; 1000 s as "1e+09 µs". | review | Scientific notation below 1e-9 / above 1e15; time also in s and ms. | unit: Sig / Time tests |
| 5 | Figure | An invalid Er was drawn as "Er = NaN". | review | Shows "—". | unit: figures with NaN |
| 6 | Figure | Fonts were created in every paint (GDI handle leak on each redraw). | review | Shared fonts created once. | review |
| 7 | Layout | At 125/150 % screen scaling the fixed pixel sizes of the calculator parts did not grow (text clipped). | review | `CalcUi.Px()` for every fixed size; figures draw in 96-dpi units with a scale transform and pixel fonts. | screenshot at simulated 150 % (`KICAD_DRC_TEST_DPI`) |
| 8 | Layout | In a small window the list stayed 330 px wide; the zone labels of the trace picture overlapped. | screenshot | List width follows the window; labels are drawn only where they fit. | gui: "list narrower", screenshot 900 × 640 |
| 9 | Memory | Kept views that are not on screen were not disposed when the window closed. | review | Disposed on `FormClosed`. | review |
| 10 | Text | "&" in "Bandwidth & max…" disappeared (Windows treats it as a shortcut marker). | screenshot | `UseMnemonic = false` / `TextFormatFlags.NoPrefix`. | screenshot |
| 11 | Layout | The description box kept empty space after resizing (auto-size of a docked panel does not shrink). | screenshot | Replaced by the "How it works" page (its boxes measure their text). | screenshot |
| 12 | Figure | The IPC limit label was hidden under the signal-edge label. | screenshot | Picture laid out in rows (edge, IPC label, trace band, λ label). | screenshot |
| 13 | Figure | Stripline cross-section showed "air" above the top plane (the board continues there). | screenshot | Drawn as dielectric. | screenshot |
| 14 | Figure | The "voltage" axis label of the edge figure was clipped at the top. | screenshot | Larger top margin. | screenshot |

## Second round (explanation page rewrite, same day)

| # | Area | Problem | Found by | Fix | Covered by |
|---|------|---------|----------|-----|------------|
| 15 | Guide | Text inside boxes (example, notes) did not wrap to the page width: the wrap routine only looked one level deep. | review while adding new blocks | Wrapping is recursive (boxes, Q&A, glossary). | screenshots |
| 16 | Window | Apply / Restore defaults / Close were shown on the production and calculator tabs, where they do nothing useful. | user | Bottom bar only on the rules and stackup tabs. | gui: "Bottom bar …" |
| 17 | Figure | "target 3.3 V" and "input limit 3.6 V" labels overlapped. | screenshot | One above, one below its line. | screenshot |

## Third round (2026-09-27, new conversation)

| # | Area | Problem | Found by | Fix | Covered by |
|---|------|---------|----------|-----|------------|
| 18 | Figure | On "Picture" the dashed marker of the longer limit (usually λ/n) crossed the zone label "transmission line: control the impedance". | screenshot | See #19 (the first fix only handled this marker). | screenshots |
| 19 | Figure | With an entered trace the RX chip covered the zone labels (e.g. "…the impedance" at 300 mm, "plain connection is fine" at 20 mm). | screenshot | Labels inside the band keep clear of both markers and of the RX chip with its pins (`Fig.FreeSpot`); a label that fits nowhere is dropped. | unit: FreeSpot; screenshots |
| 20 | Figure | The IPC marker crossed the "your trace" tag (e.g. 60 mm), and for a short trace the tag could land far away. | screenshot | The tag stays in the zone of its verdict (✓ green, ⚠ yellow), as close to the left of the RX chip as it fits, never across a marker or the chip; otherwise dropped (the verdict text below the picture still says it). | screenshots: no trace, 20 / 44 / 60 / 120 / 300 mm, 900 × 640, 150 %, stripline |

Accepted: when the entered trace ends right at a limit (e.g. 44 or 120 mm), the RX chip sits on that limit's marker; that is what the
picture should show. In small windows and at 150 % the labels inside the band are usually dropped for lack of room.

The new demo model (ReflectionModel: lossless line, 15 Ω driver, 50 Ω trace, open receiver) is checked against physics:
final level 3.3 V, no voltage before the first arrival, +54 % overshoot limit for long traces, overshoot growing with length.

## Not bugs of ours – Saturn deviations (documented)

* Microstrip Er_eff: Saturn uses `0.457·Er + 0.67`, the published formula is `0.475·Er + 0.67` (digits swapped).
  Our microstrip length is 0.8–1.7 % shorter = on the safe side.
* Saturn multiplies by 1/n rounded to six digits (1/7 = 0.142857); we divide exactly (difference < 0.0002 %).
* Saturn's "Propagation speed" is always `c/√Er`; ours uses Er_eff of the chosen trace type (the speed the signal really has).

## Test status after the fixes

* Unit tests: 66 / 66 · Window tests: 47 / 47 · KiCad verification: 26 / 26 · Saturn comparison: 265 / 265
