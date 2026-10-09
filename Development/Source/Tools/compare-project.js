// For the project update test: shows the .kicad_pro differences and whether .kicad_pcb is unchanged outside general/layers/setup.
// Usage: node compare-project.js before.kicad_pro after.kicad_pro before.kicad_pcb after.kicad_pcb
const fs = require("fs");
const [proA, proB, pcbA, pcbB] = process.argv.slice(2);

const a = JSON.parse(fs.readFileSync(proA, "utf8")), b = JSON.parse(fs.readFileSync(proB, "utf8"));
const diffs = [];
(function walk(x, y, path) {
  if (typeof x !== "object" || x === null || typeof y !== "object" || y === null) {
    if (JSON.stringify(x) !== JSON.stringify(y)) diffs.push(path + ": " + JSON.stringify(x) + " -> " + JSON.stringify(y));
    return;
  }
  for (const k of new Set([...Object.keys(x), ...Object.keys(y)])) walk(x[k], y[k], path + "." + k);
})(a, b, "");
const grouped = {};
for (const d of diffs) { const k = d.split(":")[0].split(".").slice(0, 4).join("."); grouped[k] = (grouped[k] || 0) + 1; }
console.log("PRO: " + diffs.length + " differences");
for (const [k, n] of Object.entries(grouped)) console.log("   " + k + "  (" + n + ")");

function stripBlocks(t) {
  const ranges = [];
  for (const name of ["\t(general", "\t(layers", "\t(setup"]) {
    const i = t.indexOf(name);
    if (i < 0) continue;
    let depth = 0, j = i + 1;
    for (; j < t.length; j++) {
      const c = t[j];
      if (c === '"') { j++; while (t[j] !== '"') { if (t[j] === "\\") j++; j++; } continue; }
      if (c === "(") depth++;
      else if (c === ")") { if (--depth === 0) break; }
    }
    ranges.push([i, j]);
  }
  ranges.sort((x, y) => y[0] - x[0]);
  for (const [i, j] of ranges) t = t.slice(0, i) + "<BLOCK>" + t.slice(j + 1);
  return t;
}
const p1 = stripBlocks(fs.readFileSync(pcbA, "utf8")), p2 = stripBlocks(fs.readFileSync(pcbB, "utf8"));
console.log("PCB unchanged outside general/layers/setup: " + (p1 === p2) + "  (" + p1.length + " / " + p2.length + " characters)");
if (p1 !== p2) { let i = 0; while (p1[i] === p2[i]) i++; console.log("   first difference: ..." + JSON.stringify(p1.slice(i - 40, i + 60)) + "\n   replaced by:      ..." + JSON.stringify(p2.slice(i - 40, i + 60))); }
