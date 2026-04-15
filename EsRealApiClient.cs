using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EsRealOutlookAddin
{
    public class EsRealApiClient : IDisposable
    {
        private readonly HttpClient   _http;
        private readonly EsRealConfig _cfg;

        // In-memory cache
        private readonly Dictionary<string, CacheEntry> _cache
            = new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);

        // Persistente cache op schijf
        private static readonly string CacheFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EsReal", "domain_cache.json");

        private class CacheEntry
        {
            public VerifyResult Result { get; set; }
            public DateTime     At     { get; set; }

            // TTL wordt bepaald via de config - zie EsRealConfig.EffectiveTtl*
            public EsRealConfig Cfg    { get; set; }

            public bool IsExpired()
            {
                if (Cfg == null) return true;
                var ttl = Result.IsVerified ? Cfg.EffectiveTtlVerified
                        : Result.IsError    ? Cfg.EffectiveTtlError
                        : Cfg.EffectiveTtlNotFound;
                return DateTime.UtcNow - At > ttl;
            }
        }

        public EsRealApiClient(EsRealConfig cfg)
        {
            _cfg = cfg;
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            _http.DefaultRequestHeaders.Add("x-esreal-ref", cfg.Ref);
            _http.DefaultRequestHeaders.Add("User-Agent",   "EsReal-Outlook-VSTO/1.0");
            LoadCacheFromDisk();
        }

        // ── Verify ────────────────────────────────────────────────────────

        public async Task<VerifyResult> VerifyAsync(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain))
                return VerifyResult.Error("Leeg domein");

            domain = domain.Trim().ToLowerInvariant();

            // Cache check
            CacheEntry hit;
            if (_cache.TryGetValue(domain, out hit) && !hit.IsExpired())
            {
                EsRealTaskPane.Log("Cache hit: " + domain + " (age=" +
                    (int)(DateTime.UtcNow - hit.At).TotalMinutes + "min)");
                return hit.Result;
            }

            // Doe de API call voor dit domein
            var result = await CallApiAsync(domain).ConfigureAwait(false);

            // Subdomain fallback: als niet gevonden en fallback aan
            if (!result.IsVerified && !result.IsError && _cfg.SubdomainFallback)
            {
                int parts = domain.Split('.').Length;
                int maxStrips = Math.Min(_cfg.SubdomainFallbackDepth, parts - 2);
                // Minimaal 2 delen nodig (bv. fgov.be) - strip nooit naar TLD alleen

                for (int i = 0; i < maxStrips; i++)
                {
                    int dot = domain.IndexOf('.');
                    if (dot < 0) break;
                    string parent = domain.Substring(dot + 1);

                    // Stop als het hoofddomein minder dan 2 delen heeft
                    if (parent.Split('.').Length < 2) break;

                    EsRealTaskPane.Log("Subdomain fallback: " + domain + " → " + parent);

                    // Cache check voor parent
                    CacheEntry parentHit;
                    if (_cache.TryGetValue(parent, out parentHit) && !parentHit.IsExpired())
                    {
                        EsRealTaskPane.Log("Cache hit (parent): " + parent);
                        var cached = parentHit.Result;
                        if (cached.IsVerified)
                        {
                            // Bewaar ook het originele subdomein in cache
                            var subResult = CloneWithSubdomainInfo(cached, domain, parent);
                            Store(domain, subResult);
                            return subResult;
                        }
                        domain = parent;
                        continue;
                    }

                    var parentResult = await CallApiAsync(parent).ConfigureAwait(false);
                    if (parentResult.IsVerified)
                    {
                        // Gevonden via hoofddomein - bewaar beide in cache
                        var subResult = CloneWithSubdomainInfo(parentResult, domain, parent);
                        Store(domain, subResult);   // bosa.fgov.be → result van fgov.be
                        Store(parent, parentResult); // fgov.be → eigen result
                        return subResult;
                    }

                    domain = parent; // verder omhoog
                    result = parentResult;
                }
            }

            // Subdomain fallback uitgeschakeld melding
            if (!result.IsVerified && !result.IsError && !_cfg.SubdomainFallback
                && domain.Split('.').Length > 2)
            {
                result.FallbackDisabledNote = true;
            }

            Store(domain, result);
            return result;
        }

        /// <summary>
        /// Directe API call zonder fallback logica.
        /// </summary>
        private async Task<VerifyResult> CallApiAsync(string domain)
        {
            try
            {
                var url  = _cfg.Endpoint.TrimEnd('/') + "?domain=" + Uri.EscapeDataString(domain);
                var resp = await _http.GetAsync(url).ConfigureAwait(false);

                if (!resp.IsSuccessStatusCode)
                {
                    EsRealTaskPane.Log("API non-200 for " + domain + ": " + (int)resp.StatusCode);
                    return VerifyResult.NotFound(domain);
                }

                var json   = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                EsRealTaskPane.Log("API raw for " + domain + ": " + json);
                var result = Parse(json, domain);
                EsRealTaskPane.Log("Parsed: vdomain_verified=" + result.VdomainValid +
                    " trusted=" + result.Trusted + " status=" + result.Status +
                    " trustscore=" + result.TrustScore);
                return result;
            }
            catch (TaskCanceledException)
            {
                return VerifyResult.Error("Timeout (8s)");
            }
            catch (Exception ex)
            {
                return VerifyResult.Error(ex.Message);
            }
        }

        /// <summary>
        /// Kopieer een verified result en voeg subdomain info toe.
        /// </summary>
        private static VerifyResult CloneWithSubdomainInfo(
            VerifyResult source, string originalDomain, string matchedDomain)
        {
            return new VerifyResult
            {
                Domain            = originalDomain,
                MatchedDomain     = matchedDomain,
                Trusted           = source.Trusted,
                VdomainValid      = source.VdomainValid,
                AnchorConfirmed   = source.AnchorConfirmed,
                OrgApproved       = source.OrgApproved,
                Status            = source.Status,
                OrgName           = source.OrgName,
                Category          = source.Category,
                SiteType          = source.SiteType,
                TrustScore        = source.TrustScore,
                DnsActive         = source.DnsActive,
                AnchorState       = source.AnchorState,
                AnchorChain       = source.AnchorChain,
                AnchorTxid        = source.AnchorTxid,
                AnchorConfirmedAt = source.AnchorConfirmedAt,
                AnchorExplorer    = source.AnchorExplorer,
            };
        }

        // ── Cache opslaan ─────────────────────────────────────────────────

        private void Store(string domain, VerifyResult result)
        {
            var entry = new CacheEntry { Result = result, At = DateTime.UtcNow, Cfg = _cfg };
            _cache[domain] = entry;
            SaveCacheToDisk();
        }

        public void InvalidateDomain(string domain)
        {
            if (string.IsNullOrEmpty(domain)) return;

            domain = domain.Trim().ToLowerInvariant();

            // Invalideer het domein zelf
            _cache.Remove(domain);

            // Als dit een subdomain was met een fallback match,
            // invalideer dan ook het gematchte hoofddomein
            // zodat refresh ook fgov.be opnieuw ophaalt bij bosa.fgov.be
            var toRemove = new System.Collections.Generic.List<string>();
            foreach (var kv in _cache)
            {
                if (!string.IsNullOrEmpty(kv.Value.Result.MatchedDomain) &&
                    kv.Value.Result.MatchedDomain == domain)
                {
                    toRemove.Add(kv.Key);
                }
                // Verwijder ook entries waarvan het MatchedDomain overeenkomt
                if (kv.Key == domain) toRemove.Add(kv.Key);
            }
            foreach (var key in toRemove)
                _cache.Remove(key);

            // Invalideer ook het hoofddomein bij subdomain strips
            // bv: bosa.fgov.be → verwijder ook fgov.be uit cache
            var parts = domain.Split('.');
            for (int i = 1; i < parts.Length - 1; i++)
            {
                var parent = string.Join(".", parts, i, parts.Length - i);
                _cache.Remove(parent);
            }

            SaveCacheToDisk();
        }

        public void ClearCache()
        {
            _cache.Clear();
            try { if (File.Exists(CacheFile)) File.Delete(CacheFile); } catch { }
        }

        // ── Persistente cache ─────────────────────────────────────────────

        // Fingerprint van de config - als die wijzigt wordt de cache geleegd
        private string ConfigFingerprint =>
            (_cfg.Ref ?? "") + "|" + (_cfg.Endpoint ?? "") + "|" + (_cfg.Enterprise ?? "");

        private static readonly string FingerprintFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EsReal", "config.fingerprint");

        private void LoadCacheFromDisk()
        {
            try
            {
                // Controleer of de config gewijzigd is sinds de laatste run
                // Als Ref, Endpoint of Enterprise anders is → cache wissen
                if (File.Exists(FingerprintFile))
                {
                    var savedFingerprint = File.ReadAllText(FingerprintFile).Trim();
                    if (savedFingerprint != ConfigFingerprint)
                    {
                        EsRealTaskPane.Log("Config changed - clearing cache (old: "
                            + savedFingerprint + " new: " + ConfigFingerprint + ")");
                        try { File.Delete(CacheFile); } catch { }
                        File.WriteAllText(FingerprintFile, ConfigFingerprint);
                        return;
                    }
                }
                else
                {
                    // Eerste keer - sla fingerprint op
                    Directory.CreateDirectory(Path.GetDirectoryName(FingerprintFile));
                    File.WriteAllText(FingerprintFile, ConfigFingerprint);
                }

                if (!File.Exists(CacheFile)) return;

                var json = File.ReadAllText(CacheFile);
                var arr  = JArray.Parse(json);

                int loaded = 0;
                foreach (var item in arr)
                {
                    try
                    {
                        var domain = item.Value<string>("domain") ?? "";
                        var at     = DateTime.SpecifyKind(
                        item.Value<DateTime>("at"), DateTimeKind.Utc);
                        var result = item["result"]?.ToObject<VerifyResult>();

                        if (string.IsNullOrEmpty(domain) || result == null) continue;

                        var entry = new CacheEntry { Result = result, At = at, Cfg = _cfg };
                        if (!entry.IsExpired())
                        {
                            _cache[domain] = entry;
                            loaded++;
                        }
                    }
                    catch { }
                }

                EsRealTaskPane.Log("Cache loaded from disk: " + loaded + " entries");
            }
            catch (Exception ex)
            {
                EsRealTaskPane.Log("Cache load error: " + ex.Message);
            }
        }

        private void SaveCacheToDisk()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CacheFile));

                var arr = new JArray();
                foreach (var kv in _cache)
                {
                    if (!kv.Value.IsExpired())
                    {
                        arr.Add(new JObject
                        {
                            ["domain"] = kv.Key,
                            ["at"]     = kv.Value.At.ToUniversalTime().ToString("o"),
                            ["result"] = JObject.FromObject(kv.Value.Result),
                        });
                    }
                }

                File.WriteAllText(CacheFile, arr.ToString(Formatting.None));
            }
            catch (Exception ex)
            {
                EsRealTaskPane.Log("Cache save error: " + ex.Message);
            }
        }

        // ── JSON parsing ──────────────────────────────────────────────────

        // Helpers voor null-safe JSON lezen (bv. anchor.chain kan null zijn)
        private static string SafeString(JToken token, string key)
        {
            if (token == null) return "";
            var val = token[key];
            if (val == null || val.Type == Newtonsoft.Json.Linq.JTokenType.Null) return "";
            return val.Value<string>() ?? "";
        }

        private static string SafeChildString(JToken token, string key, string childKey)
        {
            if (token == null) return "";
            var child = token[key];
            if (child == null || child.Type == Newtonsoft.Json.Linq.JTokenType.Null) return "";
            var val = child[childKey];
            if (val == null || val.Type == Newtonsoft.Json.Linq.JTokenType.Null) return "";
            return val.Value<string>() ?? "";
        }

        private static VerifyResult Parse(string json, string domain)
        {
            try
            {
                var o      = JObject.Parse(json);
                var anchor = o["anchor"];

                var validation = o["validation"];

                // Verified = validation.vdomain_verified == true
                // Niet in registry = status "unknown" of vdomain_verified false
                bool vdomainVerified = validation?.Value<bool>("vdomain_verified") ?? false;

                return new VerifyResult
                {
                    Domain          = domain,
                    Trusted         = o.Value<bool>("trusted"),
                    VdomainValid    = vdomainVerified,
                    OrgApproved     = o.Value<bool>("organization_approved"),
                    Status          = o.Value<string>("status")   ?? "",
                    OrgName         = o.Value<string>("org_name") ?? "",
                    Category        = o.Value<string>("category") ?? "",
                    SiteType        = o.Value<string>("site_type") ?? "",
                    TrustScore      = o.Value<int>("trust_score"),
                    DnsActive       = validation?.Value<bool>("dns_active") ?? false,
                    AnchorState     = anchor?.Value<string>("state")   ?? "",
                    AnchorChain     = SafeString(anchor, "chain"),
                    AnchorTxid      = SafeString(anchor, "txid"),
                    AnchorConfirmedAt = SafeChildString(anchor, "confirmed_at", "utc"),
                    AnchorExplorer  = SafeString(anchor, "explorer"),
                    AnchorConfirmed = validation?.Value<bool>("anchor_confirmed") ?? false,
                };
            }
            catch (Exception parseEx)
            {
                EsRealTaskPane.Log("Parse EXCEPTION for " + domain + ": " + parseEx.Message + " | " + parseEx.StackTrace);
                return VerifyResult.NotFound(domain);
            }
        }

        // ── Enterprise domein-aanvraag ─────────────────────────────────────

        private static readonly string RequestEndpoint =
            "{endpoint}";

        /// <summary>
        /// Stuurt een aanvraag naar IT om het domein toe te voegen aan de private vDOMAIN registry.
        /// Gooit een exception bij HTTP-fouten of netwerkproblemen.
        /// </summary>
        public async Task RequestDomainAsync(string domain)
        {
            if (string.IsNullOrWhiteSpace(_cfg.ApiKey) || string.IsNullOrWhiteSpace(_cfg.ApiKeyRequest))
                throw new InvalidOperationException("Enterprise API keys niet geconfigureerd.");

            domain = domain.Trim().ToLowerInvariant();

            var body = Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                domain    = domain,
                org_name  = domain,
                category  = "service",
                site_type = "website",
                status    = "verified"
            });

            var request = new HttpRequestMessage(HttpMethod.Post, RequestEndpoint)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            };
            request.Headers.Add("x-api-key",         _cfg.ApiKey);
            request.Headers.Add("x-api-key-request", _cfg.ApiKeyRequest);

            var resp = await _http.SendAsync(request).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException("HTTP " + (int)resp.StatusCode);

            EsRealTaskPane.Log("RequestDomain OK: " + domain);
        }

        public void Dispose()
        {
            SaveCacheToDisk();
            // Sla fingerprint op bij afsluiten
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FingerprintFile));
                File.WriteAllText(FingerprintFile, ConfigFingerprint);
            }
            catch { }
            if (_http != null) _http.Dispose();
        }
    }

    public class VerifyResult
    {
        public string Domain            { get; set; } = "";
        public bool   Trusted           { get; set; }
        public bool   VdomainValid      { get; set; }  // validation.vdomain_valid
        public bool   AnchorConfirmed   { get; set; }  // validation.anchor_confirmed
        public bool   OrgApproved       { get; set; }
        public string Status            { get; set; } = "";
        public string OrgName           { get; set; } = "";
        public string Category          { get; set; } = "";
        public string SiteType          { get; set; } = "";
        public int    TrustScore        { get; set; }
        public bool   DnsActive         { get; set; }
        public string AnchorState       { get; set; } = "";
        public string AnchorChain       { get; set; } = "";
        public string AnchorTxid        { get; set; } = "";
        public string AnchorConfirmedAt { get; set; } = "";
        public string AnchorExplorer    { get; set; } = "";
        public bool   IsError           { get; set; }
        public string ErrorMessage      { get; set; } = "";

        /// <summary>
        /// Het domein waarop de match gevonden werd.
        /// Verschilt van Domain als subdomain fallback gebruikt werd.
        /// bv: Domain="bosa.fgov.be", MatchedDomain="fgov.be"
        /// </summary>
        public string MatchedDomain      { get; set; } = "";
        public bool   IsSubdomainMatch   => !string.IsNullOrEmpty(MatchedDomain) && MatchedDomain != Domain;
        public bool   FallbackDisabledNote  { get; set; } // subdomein maar fallback uitgeschakeld
        public bool   IsFreeMailDomain      { get; set; } // gratis mailprovider waarschuwing

        // Correct: vdomain_valid moet true zijn - trusted+status alleen is niet voldoende
        // zele.be: trusted=true, status="verified" maar vdomain_valid=false → NIET verified
        public bool IsVerified   => VdomainValid && !IsError;
        public bool IsBlockchain => AnchorConfirmed && !string.IsNullOrEmpty(AnchorTxid);
        public string DisplayOrg => string.IsNullOrEmpty(OrgName) ? (IsSubdomainMatch ? MatchedDomain : Domain) : OrgName;

        public static VerifyResult NotFound(string d) =>
            new VerifyResult { Domain = d, Trusted = false, Status = "unknown" };

        public static VerifyResult Error(string msg) =>
            new VerifyResult { IsError = true, ErrorMessage = msg };
    }
}
