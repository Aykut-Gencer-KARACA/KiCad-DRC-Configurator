# Third-party notices

## Fabrication Toolkit (KiCad plugin)

`Development/Source/Production/JlcPlacement.cs` contains the rotation correction table of the Fabrication Toolkit
plugin (`transformations.csv`: footprint name patterns and the rotation JLCPCB needs), adapted: the polarised capacitor
rows are left out and EasyEDA / LCSC footprints get no correction.

- Project: https://github.com/bennymeg/Fabrication-Toolkit
- Copyright: the Fabrication Toolkit authors (bennymeg and contributors)
- License: Apache License 2.0, https://www.apache.org/licenses/LICENSE-2.0

The BOM / CPL column layout of the production files follows the same plugin, so that JLCPCB reads them the same way.

## JLCPCB data

`Development/Reference Data/stackups.json` lists the PCB stackups JLCPCB offers, read from JLCPCB's public impedance
calculator service (see `Development/Reference Data/SOURCES.txt`). The DRC limits, part prices and assembly fees used by
the app come from JLCPCB's public pages and services. JLCPCB and LCSC are trademarks of their owners; this project is
not affiliated with them.

## Saturn PCB Toolkit

The PCB calculators are written from published formulas (IPC standards, handbooks). Saturn PCB Toolkit
(https://www.saturnpcb.com) was only used as a black-box reference to compare results; none of its files are part of
this repository.
