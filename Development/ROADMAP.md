# Roadmap

## Now: PCB calculators (clone of Saturn PCB Toolkit's 19 tabs, improved)
One tab per step, in Saturn's order. Each step: formulas from published sources, live figures, "How it works" page (TR/EN),
parallel check against Saturn (report in `Test Results\saturn-comparison_NN_<name>.txt`), debug round
(report in `Debug Reports\NN_<name>.md`, walk through `Debug Reports\CHECKLIST.md`).

- [x] 1 Bandwidth & max conductor length
- [ ] 2 Conductor impedance · 3 Conductor properties · 4 Unit conversion · 5 Differential pairs / crosstalk
- [ ] 6 Embedded resistors · 7 Er effective · 8 Fusing current · 9 Mechanical information · 10 Minimum conductor spacing
- [ ] 11 Ohm's law · 12 Padstack · 13 PDN · 14 Planar inductors · 15 PPM / crystal · 16 Thermal management
- [ ] 17 Via properties · 18 Wavelength · 19 XL / XC reactance

## Next: project-aware calculators and PCB analysis (idea of 2026-09-27)
The app can already read a KiCad project (stackup, layers, tracks, vias, zones, net classes). Use it:

1. **Calculators filled from the project** – stackup, copper weights, board thickness, track widths and net classes of the
   selected project instead of manual inputs; results update when the project changes.
2. **Per-net checks** – for each net: longest route vs the critical length of calculator 1 (with an assumed or entered rise
   time per net class), current capacity of the narrowest segment (IPC-2152) vs an entered current, impedance of the traces
   on each layer vs a target.
3. **Crosstalk scan** – find parallel segments of different nets on the same or adjacent layers: coupled length, spacing s,
   height above the reference plane h; estimate the coupling (e.g. near-end crosstalk ≈ K / (1 + (s/h)²), saturated after the
   critical length) and list the pairs above a threshold with a percentage, even when the DRC clearance is met.
4. **Return path problems** – signal traces crossing a gap or split in the reference plane below them.
5. Results as a report (xlsx / html) and as markers in the project.

## Later
- Basic-part alternatives for Extended JLC parts (lower assembly fees).
- Other manufacturers; Altium support.
