using Microsoft.Win32;

namespace EsRealOutlookAddin
{
    /// <summary>
    /// Laadt EsReal configuratie uit Windows Registry.
    ///
    /// Prioriteit (hoog naar laag):
    ///   1. GPO  - HKLM\SOFTWARE\Policies\EsReal\Outlook  (IT beheer, niet overschrijfbaar)
    ///   2. User - HKCU\SOFTWARE\EsReal\Outlook            (gebruiker voorkeur)
    ///   3. Default - ingebouwde standaardwaarden
    ///
    /// Uitrollen via GPO/SCCM/Intune:
    ///   - Schrijf naar HKLM\SOFTWARE\Policies\EsReal\Outlook
    ///   - Gebruikers kunnen deze waarden NIET overschrijven
    ///   - Zie EsReal_Outlook_Policy.reg voor een voorbeeldbestand
    /// </summary>
    public static class RegistryConfig
    {
        private const string GPO_KEY  = @"SOFTWARE\Policies\EsReal\Outlook";
        private const string USER_KEY = @"SOFTWARE\EsReal\Outlook";

        public static EsRealConfig Load()
        {
            var gpo  = ReadKey(Registry.LocalMachine, GPO_KEY);
            var user = ReadKey(Registry.CurrentUser,  USER_KEY);

            return new EsRealConfig
            {
                // API instellingen - GPO heeft prioriteit
                Endpoint   = GetString(gpo, user, "Endpoint",   ""),
                Ref        = GetString(gpo, user, "Ref",        ""),
                AutoVerify = GetBool  (gpo, user, "AutoVerify", true),
                Enterprise = GetString(gpo, user, "Enterprise", ""),

                // Subdomain fallback
                SubdomainFallback      = GetBool(gpo, user, "SubdomainFallback",      true),
                SubdomainFallbackDepth = GetInt (gpo, user, "SubdomainFallbackDepth", 2),

                // Cache TTL (minuten) - minimum wordt nog steeds afgedwongen in EsRealConfig
                CacheTtlVerifiedMinutes  = GetInt(gpo, user, "CacheTtlVerifiedMinutes",  360),
                CacheTtlNotFoundMinutes  = GetInt(gpo, user, "CacheTtlNotFoundMinutes",  15),
                CacheTtlErrorMinutes     = GetInt(gpo, user, "CacheTtlErrorMinutes",     5),

                // Gratis mailproviders - extra domeinen bovenop de ingebouwde lijst
                ExtraFreeMailDomains = GetString(gpo, user, "ExtraFreeMailDomains", ""),

                // Taal: "nl","en","fr","de","es" - leeg = systeem taal
                Language = GetString(gpo, user, "Language", ""),

                // Debug logging naar bureaublad (0=uit, 1=aan) - default UIT
                DebugLogging = GetBool(gpo, user, "DebugLogging", false),

                // Enterprise request API keys - beide nodig voor enterprise modus
                ApiKey        = GetString(gpo, user, "ApiKey",        ""),
                ApiKeyRequest = GetString(gpo, user, "ApiKeyRequest", ""),
            };
        }

        public static void SaveUserSetting(string key, object value)
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(USER_KEY, true))
                    k?.SetValue(key, value);
            }
            catch { }
        }

        // ── Registry helpers ──────────────────────────────────────────────

        private static System.Collections.Generic.Dictionary<string, object>
            ReadKey(RegistryKey hive, string path)
        {
            var d = new System.Collections.Generic.Dictionary<string, object>(
                System.StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var k = hive.OpenSubKey(path, false))
                {
                    if (k == null) return d;
                    foreach (var name in k.GetValueNames())
                    {
                        var v = k.GetValue(name);
                        if (v != null) d[name] = v;
                    }
                }
            }
            catch { }
            return d;
        }

        private static string GetString(
            System.Collections.Generic.Dictionary<string, object> gpo,
            System.Collections.Generic.Dictionary<string, object> user,
            string name, string defaultVal)
        {
            object v;
            if (gpo.TryGetValue(name, out v)  && v is string gs && gs.Length > 0) return gs;
            if (user.TryGetValue(name, out v) && v is string us && us.Length > 0) return us;
            return defaultVal;
        }

        private static bool GetBool(
            System.Collections.Generic.Dictionary<string, object> gpo,
            System.Collections.Generic.Dictionary<string, object> user,
            string name, bool defaultVal)
        {
            object v;
            if (gpo.TryGetValue(name, out v)  && v is int gi) return gi != 0;
            if (user.TryGetValue(name, out v) && v is int ui) return ui != 0;
            return defaultVal;
        }

        private static int GetInt(
            System.Collections.Generic.Dictionary<string, object> gpo,
            System.Collections.Generic.Dictionary<string, object> user,
            string name, int defaultVal)
        {
            object v;
            if (gpo.TryGetValue(name, out v)  && v is int gi) return gi;
            if (user.TryGetValue(name, out v) && v is int ui) return ui;
            return defaultVal;
        }
    }

    public class EsRealConfig
    {
        public string Endpoint               { get; set; } = "";
        public string Ref                    { get; set; } = "";
        public bool   AutoVerify             { get; set; } = true;
        public string Enterprise             { get; set; } = "";
        public bool   SubdomainFallback      { get; set; } = true;
        public int    SubdomainFallbackDepth { get; set; } = 2;
        public int    CacheTtlVerifiedMinutes  { get; set; } = 360;
        public int    CacheTtlNotFoundMinutes  { get; set; } = 15;
        public int    CacheTtlErrorMinutes     { get; set; } = 5;
        public string ExtraFreeMailDomains   { get; set; } = "";

        /// <summary>
        /// Taal van de plugin: nl, en, fr, de, es
        /// Leeg = systeem taal gebruiken
        /// </summary>
        public string Language               { get; set; } = "";

        /// <summary>Debug logging naar EsReal_debug.log op bureaublad (default: uit)</summary>
        public bool DebugLogging             { get; set; } = false;

        /// <summary>Enterprise API key voor domein-aanvragen (x-api-key header)</summary>
        public string ApiKey                 { get; set; } = "";
        /// <summary>Enterprise request API key (x-api-key-request header)</summary>
        public string ApiKeyRequest          { get; set; } = "";

        /// <summary>Enterprise modus actief als beide API keys ingevuld zijn</summary>
        public bool IsEnterpriseMode         => !string.IsNullOrEmpty(ApiKey) && !string.IsNullOrEmpty(ApiKeyRequest);

        // Minimum TTL - niet overschrijfbaar via registry
        internal const int MIN_TTL_VERIFIED_MINUTES = 60;
        internal const int MIN_TTL_NOTFOUND_MINUTES = 5;
        internal const int MIN_TTL_ERROR_MINUTES    = 2;

        public System.TimeSpan EffectiveTtlVerified =>
            System.TimeSpan.FromMinutes(System.Math.Max(CacheTtlVerifiedMinutes, MIN_TTL_VERIFIED_MINUTES));
        public System.TimeSpan EffectiveTtlNotFound =>
            System.TimeSpan.FromMinutes(System.Math.Max(CacheTtlNotFoundMinutes, MIN_TTL_NOTFOUND_MINUTES));
        public System.TimeSpan EffectiveTtlError =>
            System.TimeSpan.FromMinutes(System.Math.Max(CacheTtlErrorMinutes, MIN_TTL_ERROR_MINUTES));
    }
}
