// Fetches the stackups from JLC's impedance API and writes "Development/Reference Data/stackups.json" (embedded in the app).
// Usage: node update-stackups.js [raw-json-folder]   (without a folder the API is queried)
const fs = require("fs"), path = require("path"), https = require("https");
const LAYER_COUNTS = [4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 30, 32];
const OUTER = [1, 2], INNER = [0.5, 1, 2];   // JLC page: multilayer outer 1/2 oz, inner 0.5/1/2 oz

async function download(L) {
  // The API returns at most 1000 records per page
  const list = [];
  for (let page = 1; ; page++) {
    const body = (await request(L, page)).body;
    list.push(...body.list);
    if (!body.list.length || list.length >= body.total) return { body: { list } };
  }
}

function request(L, page) {
  return new Promise((resolve, reject) => {
    const body = JSON.stringify({ plateLayerNumber: L, pageSize: 1000, pageNum: page });
    const req = https.request("https://jlcpcb.com/api/jlcTools/impedance/selectPageImpedanceDefaultTemplate",
      { method: "POST", headers: { "Content-Type": "application/json", "User-Agent": "Mozilla/5.0" } }, res => {
        let v = ""; res.on("data", d => v += d); res.on("end", () => resolve(JSON.parse(v)));
      });
    req.on("error", reject); req.end(body);
  });
}

function layerStack(t) {
  // Copper / dielectric sequence; consecutive dielectrics (e.g. 2116+2313) are merged into one layer
  const k = [];
  const copper = th => k.push({ type: "cu", t: th });
  const dielectric = (type, th, dk, material) => {
    const last = k[k.length - 1];
    if (last && last.type !== "cu") { last.dk = (last.dk * last.t + dk * th) / (last.t + th); last.t += th; last.material += "+" + material; if (type === "core") last.type = "core"; }
    else k.push({ type, t: th, dk, material });
  };
  for (const b of t.basicDataList) {
    if (b.materialType === 1) copper(b.topConductorThick);
    else if (b.materialType === 0) dielectric("prepreg", b.dielectricThick, b.dielectricConstant, (b.specificationsModel || "PP") + (b.materialName.match(/\*\d/) || [""])[0]);
    else if (b.materialType === 2) {
      // Both faces of a core are copper clad; the "with/without copper" flag only says whether the thickness includes
      // copper, dielectricThick is the bare dielectric thickness in both cases
      if (b.topConductorThick > 0) copper(b.topConductorThick);
      dielectric("core", b.dielectricThick, b.dielectricConstant, "FR4");
      if (b.botConductorThick > 0) copper(b.botConductorThick);
    } else return null;
  }
  return k;
}

(async () => {
  const source = process.argv[2], result = [];
  for (const L of LAYER_COUNTS) {
    const raw = source ? JSON.parse(fs.readFileSync(path.join(source, "L" + L + ".json"))) : await download(L);
    if (!source) console.log("L" + L + ": " + raw.body.list.length + " records");
    const chosen = new Map();
    for (const t of raw.body.list) {
      if (t.plateLayerNumber !== L || t.auditStatus !== 2 || t.receptionDefaultFlag !== 1) continue;
      // Only JLC's own named stackups (custom ones are marked with the Chinese word for "custom")
      if (!/^JLC\d{5}/.test(t.appointName || "") || /自定义/.test(t.appointName)) continue;
      if (!OUTER.includes(t.cuprumThickness) || !INNER.includes(t.innerCopperThickness)) continue;
      const key = [t.appointName, t.plateThickness, t.cuprumThickness, t.innerCopperThickness].join("|");
      const previous = chosen.get(key);
      if (!previous || t.deskSort < previous.deskSort || (t.deskSort === previous.deskSort && t.urgentState < previous.urgentState)) chosen.set(key, t);
    }
    for (const t of chosen.values()) {
      const k = layerStack(t);
      if (!k || k.filter(x => x.type === "cu").length !== L) { console.error("skipped:", t.appointName); continue; }
      const r = x => Math.round(x * 10000) / 10000;
      result.push({ name: t.appointName, L, thick: t.plateThickness, outer: t.cuprumThickness, inner: t.innerCopperThickness, order: t.deskSort,
        layers: k.map(x => x.type === "cu" ? [r(x.t)] : [x.type === "core" ? 1 : 0, r(x.t), Math.round(x.dk * 100) / 100, x.material]) });
    }
  }
  result.sort((a, b) => a.L - b.L || a.thick - b.thick || a.outer - b.outer || a.inner - b.inner || a.order - b.order || a.name.localeCompare(b.name));
  fs.writeFileSync(path.join(__dirname, "..", "..", "Reference Data", "stackups.json"), JSON.stringify(result));
  console.log("stackups:", result.length);
})();
