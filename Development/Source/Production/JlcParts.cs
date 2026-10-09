using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace KiCadDrc
{
    // JLC's public part search service. Results are kept 3 days in <data folder>\part-cache.json.
    // Without internet nothing is returned; production files are generated without the information.
    static class JlcParts
    {
        const string Url = "https://jlcpcb.com/api/overseas-pcb-order/v1/shoppingCart/smtGood/selectSmtComponentList";

        public static Dictionary<string, PartInfo> Fetch(KiCadEnvironment env, IEnumerable<string> codes, out int missing)
        {
            string cachePath = Path.Combine(env.DataDir, "part-cache.json");
            var cache = new Dictionary<string, object>();
            try { if (File.Exists(cachePath)) cache = (Dictionary<string, object>)S.Json().DeserializeObject(File.ReadAllText(cachePath, Encoding.UTF8)); }
            catch (Exception) { cache = new Dictionary<string, object>(); }

            var result = new Dictionary<string, PartInfo>();
            var toQuery = new List<string>();
            var unique = codes.Distinct().ToList();
            foreach (var code in unique)
            {
                var entry = cache.ContainsKey(code) ? cache[code] as Dictionary<string, object> : null;
                DateTime t;
                // entries written before the attrition fields were stored are fetched again
                if (entry != null && entry.ContainsKey("date") && entry.ContainsKey("least") && DateTime.TryParse(entry["date"] as string, S.Inv, DateTimeStyles.None, out t) && (DateTime.Now - t).TotalDays < 3)
                    result[code] = Parse(entry);
                else toQuery.Add(code);
            }
            // KICAD_DRC_OFFLINE=1: do not go online (for tests); only the cache is used
            bool offline = Environment.GetEnvironmentVariable("KICAD_DRC_OFFLINE") == "1";
            if (toQuery.Count > 0 && !offline)
            {
                // .NET 4 defaults to TLS 1.0; JLC needs TLS 1.2 (3072 = Tls12, which has no name in .NET 4).
                // The default limit of 2 connections per host would serialise the parallel queries.
                System.Net.ServicePointManager.SecurityProtocol |= (System.Net.SecurityProtocolType)3072;
                System.Net.ServicePointManager.DefaultConnectionLimit = Math.Max(System.Net.ServicePointManager.DefaultConnectionLimit, 8);
                var sync = new object();
                System.Threading.Tasks.Parallel.ForEach(toQuery, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 6 }, code =>
                {
                    var raw = Query(code);
                    if (raw == null) return;
                    lock (sync) { cache[code] = raw; result[code] = Parse(raw); }
                });
            }
            // For codes that could not be fetched an older (expired) cache entry is used when there is one
            foreach (var code in toQuery.Where(x => !result.ContainsKey(x) && cache.ContainsKey(x)))
            {
                var entry = cache[code] as Dictionary<string, object>;
                if (entry != null) result[code] = Parse(entry);
            }
            missing = unique.Count(k => !result.ContainsKey(k));
            try { S.Write(cachePath, JsonWriter.Write(new SortedDictionary<string, object>(cache, StringComparer.Ordinal))); }
            catch (Exception) { }
            return result;
        }

        // Raw information of one code (plain dictionary written to the cache); null on failure
        static Dictionary<string, object> Query(string code)
        {
            try
            {
                var req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(Url);
                req.Method = "POST"; req.ContentType = "application/json"; req.UserAgent = "Mozilla/5.0"; req.Timeout = 20000;
                byte[] body = Encoding.UTF8.GetBytes("{\"keyword\":\"" + code + "\",\"currentPage\":1,\"pageSize\":5}");
                using (var s = req.GetRequestStream()) s.Write(body, 0, body.Length);
                string text;
                using (var resp = req.GetResponse()) using (var r = new StreamReader(resp.GetResponseStream(), Encoding.UTF8)) text = r.ReadToEnd();
                var root = (Dictionary<string, object>)S.Json().DeserializeObject(text);
                var list = ((Dictionary<string, object>)((Dictionary<string, object>)root["data"])["componentPageInfo"])["list"] as object[];
                var p = list == null ? null : list.OfType<Dictionary<string, object>>().FirstOrDefault(x => (x["componentCode"] as string) == code);
                if (p == null) return null;
                Func<string, string> get = name => p.ContainsKey(name) && p[name] != null ? Convert.ToString(p[name], S.Inv) : "";
                string type = get("componentLibraryType") == "base" ? "Basic" : (get("preferredComponentFlag") == "True" ? "Preferred" : "Extended");
                var prices = new ArrayList();
                var pl = p.ContainsKey("componentPrices") ? p["componentPrices"] as object[] : null;
                if (pl != null)
                    foreach (Dictionary<string, object> f in pl)
                        prices.Add(new ArrayList { S.D(f["startNumber"]), S.D(f["endNumber"]), S.D(f["productPrice"]) });
                return new Dictionary<string, object>
                {
                    { "date", DateTime.Now.ToString("yyyy-MM-dd HH:mm", S.Inv) }, { "manufacturer", get("componentBrandEn") }, { "mpn", get("componentModelEn") },
                    { "package", get("componentSpecificationEn") }, { "description", get("describe").Length > 0 ? get("describe") : get("componentTypeEn") },
                    { "type", type }, { "stock", get("stockCount") }, { "datasheet", get("dataManualUrl") }, { "prices", prices },
                    { "loss", get("lossNumber") }, { "least", get("leastPatchNumber") }, { "reel", get("encapsulationNumber") }
                };
            }
            catch (Exception) { return null; }
        }

        // JLC descriptions end with a "ROHS" tag and sometimes contain the Chinese word for through-hole (U+63D2 U+4EF6)
        const string ChineseThroughHole = "插件";

        static PartInfo Parse(Dictionary<string, object> h)
        {
            Func<string, string> get = name => h.ContainsKey(name) && h[name] != null ? Convert.ToString(h[name], S.Inv) : "";
            string description = Regex.Replace(get("description").Replace(ChineseThroughHole, "THT"), @"\s+ROHS\s*$", "", RegexOptions.IgnoreCase);
            var info = new PartInfo
            {
                Manufacturer = get("manufacturer"), Mpn = get("mpn"), Package = get("package").Replace(ChineseThroughHole, "THT").Replace("Plugin", "THT"),
                Description = description, Type = get("type"), Datasheet = get("datasheet")
            };
            long stock; long.TryParse(get("stock"), NumberStyles.Any, S.Inv, out stock); info.Stock = stock;
            int n;
            if (int.TryParse(get("loss"), NumberStyles.Integer, S.Inv, out n)) info.Loss = n;
            if (int.TryParse(get("least"), NumberStyles.Integer, S.Inv, out n)) info.LeastPatch = n;
            if (int.TryParse(get("reel"), NumberStyles.Integer, S.Inv, out n)) info.Reel = n;
            var pl = h.ContainsKey("prices") ? h["prices"] as IEnumerable : null;
            if (pl != null) foreach (IEnumerable f in pl) info.Prices.Add(f.Cast<object>().Select(x => S.D(x)).ToArray());
            return info;
        }
    }
}
