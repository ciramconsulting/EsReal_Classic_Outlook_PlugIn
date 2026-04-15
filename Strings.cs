using System.Collections.Generic;
using System.Globalization;

namespace EsRealOutlookAddin
{
    /// <summary>
    /// Lokalisatie voor de EsReal Outlook plugin.
    /// Talen: NL, EN, FR, DE, ES
    /// Prioriteit: Registry instelling > Systeem taal > Engels (fallback)
    /// </summary>
    internal static class Strings
    {
        private static Dictionary<string, string> _current;

        public static void Init(string languageCode = null)
        {
            // Bepaal taal: registry > systeem > fallback Engels
            string lang = languageCode;
            if (string.IsNullOrEmpty(lang))
                lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();

            switch (lang)
            {
                case "nl": _current = NL; break;
                case "fr": _current = FR; break;
                case "de": _current = DE; break;
                case "es": _current = ES; break;
                default:   _current = EN; break;
            }
        }

        public static string Get(string key)
        {
            if (_current == null) Init();
            string val;
            return _current.TryGetValue(key, out val) ? val : (EN.TryGetValue(key, out val) ? val : key);
        }

        // ── Sleutels ──────────────────────────────────────────────────────

        // Header
        public static string HeaderSubtitle       => Get("HeaderSubtitle");

        // Status
        public static string StatusWaiting        => Get("StatusWaiting");
        public static string StatusVerifying       => Get("StatusVerifying");
        public static string StatusVerified        => Get("StatusVerified");
        public static string StatusNotFound        => Get("StatusNotFound");
        public static string StatusError           => Get("StatusError");

        // Detail rijen
        public static string RowOrganisation       => Get("RowOrganisation");
        public static string RowCategory           => Get("RowCategory");
        public static string RowTrusted            => Get("RowTrusted");
        public static string RowTrustedYes         => Get("RowTrustedYes");
        public static string RowTrustedNo          => Get("RowTrustedNo");
        public static string RowDns                => Get("RowDns");
        public static string RowDnsActive          => Get("RowDnsActive");
        public static string RowBlockchain         => Get("RowBlockchain");
        public static string RowBlockchainConfirmed => Get("RowBlockchainConfirmed");
        public static string RowTrustScore         => Get("RowTrustScore");

        // vDOMAIN chips
        public static string PrivateVDomain        => Get("PrivateVDomain");
        public static string PublicVDomain         => Get("PublicVDomain");
        public static string ManagedBy             => Get("ManagedBy");
        public static string ManagedByOrg          => Get("ManagedByOrg");

        // Subdomain
        public static string ViaParentDomain       => Get("ViaParentDomain");
        public static string FallbackDisabled      => Get("FallbackDisabled");

        // Gratis mail waarschuwing
        public static string FreeMailTitle         => Get("FreeMailTitle");
        public static string FreeMailBody          => Get("FreeMailBody");

        // Empty state
        public static string EmptyHint             => Get("EmptyHint");

        // Enterprise domein-aanvraag
        public static string RequestBtn            => Get("RequestBtn");
        public static string RequestSending        => Get("RequestSending");
        public static string RequestOk             => Get("RequestOk");
        public static string RequestErr            => Get("RequestErr");

        // ── Vertalingen ───────────────────────────────────────────────────

        private static readonly Dictionary<string, string> EN = new Dictionary<string, string>
        {
            { "HeaderSubtitle",        "vDomain Trust" },
            { "StatusWaiting",         "Select an email to verify..." },
            { "StatusVerifying",       "Verifying {0}\u2026" },
            { "StatusVerified",        "\u2713 {0}  Verified" },
            { "StatusNotFound",        "\u2717 {0}  Not in registry" },
            { "StatusError",           "\u26a0 {0}" },
            { "RowOrganisation",       "Organisation" },
            { "RowCategory",           "Category" },
            { "RowTrusted",            "Trusted" },
            { "RowTrustedYes",         "Yes \u2713" },
            { "RowTrustedNo",          "No \u2717" },
            { "RowDns",                "DNS" },
            { "RowDnsActive",          "Active \u2713" },
            { "RowBlockchain",         "Blockchain" },
            { "RowBlockchainConfirmed","Confirmed \u2713 ({0})" },
            { "RowTrustScore",         "Trust score" },
            { "PrivateVDomain",        "\U0001F512 Private vDOMAIN" },
            { "PublicVDomain",         "\U0001F310 Public vDOMAIN" },
            { "ManagedBy",             "Managed by {0}" },
            { "ManagedByOrg",          "Managed by your organisation" },
            { "ViaParentDomain",       "\u2192 via {0}" },
            { "FallbackDisabled",      "Subdomain fallback disabled" },
            { "FreeMailTitle",         "Free mail provider" },
            { "FreeMailBody",          "Phishing can occur via this type of domain.\nBe careful with attachments and links." },
            { "EmptyHint",             "Click on an email to\nverify the sender." },
            { "RequestBtn",            "Request domain from IT" },
            { "RequestSending",        "Sending request\u2026" },
            { "RequestOk",             "Request sent \u2014 IT will review it." },
            { "RequestErr",            "Request failed" },
        };

        private static readonly Dictionary<string, string> NL = new Dictionary<string, string>
        {
            { "HeaderSubtitle",        "vDomain Trust" },
            { "StatusWaiting",         "Selecteer een email om te verifi\u00ebren\u2026" },
            { "StatusVerifying",       "Verifieer {0}\u2026" },
            { "StatusVerified",        "\u2713 {0}  Geverifieerd" },
            { "StatusNotFound",        "\u2717 {0}  Niet in registry" },
            { "StatusError",           "\u26a0 {0}" },
            { "RowOrganisation",       "Organisatie" },
            { "RowCategory",           "Categorie" },
            { "RowTrusted",            "Trusted" },
            { "RowTrustedYes",         "Ja \u2713" },
            { "RowTrustedNo",          "Nee \u2717" },
            { "RowDns",                "DNS" },
            { "RowDnsActive",          "Actief \u2713" },
            { "RowBlockchain",         "Blockchain" },
            { "RowBlockchainConfirmed","Confirmed \u2713 ({0})" },
            { "RowTrustScore",         "Trust score" },
            { "PrivateVDomain",        "\U0001F512 Private vDOMAIN" },
            { "PublicVDomain",         "\U0001F310 Public vDOMAIN" },
            { "ManagedBy",             "Beheerd door {0}" },
            { "ManagedByOrg",          "Beheerd door uw organisatie" },
            { "ViaParentDomain",       "\u2192 via {0}" },
            { "FallbackDisabled",      "Subdomein fallback uitgeschakeld" },
            { "FreeMailTitle",         "Gratis mailprovider" },
            { "FreeMailBody",          "Phishing kan via dit soort domeinen.\nWees voorzichtig met bijlagen en links." },
            { "EmptyHint",             "Klik op een email om\nde afzender te verifi\u00ebren." },
            { "RequestBtn",            "Domein aanvragen bij IT" },
            { "RequestSending",        "Verzoek verzenden\u2026" },
            { "RequestOk",             "Verzoek verzonden \u2014 IT bekijkt het." },
            { "RequestErr",            "Verzoek mislukt" },
        };

        private static readonly Dictionary<string, string> FR = new Dictionary<string, string>
        {
            { "HeaderSubtitle",        "vDomain Trust" },
            { "StatusWaiting",         "S\u00e9lectionnez un email pour v\u00e9rifier\u2026" },
            { "StatusVerifying",       "V\u00e9rification de {0}\u2026" },
            { "StatusVerified",        "\u2713 {0}  V\u00e9rifi\u00e9" },
            { "StatusNotFound",        "\u2717 {0}  Non enregistr\u00e9" },
            { "StatusError",           "\u26a0 {0}" },
            { "RowOrganisation",       "Organisation" },
            { "RowCategory",           "Cat\u00e9gorie" },
            { "RowTrusted",            "Fiable" },
            { "RowTrustedYes",         "Oui \u2713" },
            { "RowTrustedNo",          "Non \u2717" },
            { "RowDns",                "DNS" },
            { "RowDnsActive",          "Actif \u2713" },
            { "RowBlockchain",         "Blockchain" },
            { "RowBlockchainConfirmed","Confirm\u00e9 \u2713 ({0})" },
            { "RowTrustScore",         "Score de confiance" },
            { "PrivateVDomain",        "\U0001F512 vDOMAIN Priv\u00e9" },
            { "PublicVDomain",         "\U0001F310 vDOMAIN Public" },
            { "ManagedBy",             "G\u00e9r\u00e9 par {0}" },
            { "ManagedByOrg",          "G\u00e9r\u00e9 par votre organisation" },
            { "ViaParentDomain",       "\u2192 via {0}" },
            { "FallbackDisabled",      "Repli sous-domaine d\u00e9sactiv\u00e9" },
            { "FreeMailTitle",         "Fournisseur de messagerie gratuit" },
            { "FreeMailBody",          "Le phishing peut survenir via ce type de domaine.\nSoyez prudent avec les pi\u00e8ces jointes et les liens." },
            { "EmptyHint",             "Cliquez sur un email pour\nv\u00e9rifier l'exp\u00e9diteur." },
            { "RequestBtn",            "Demander le domaine \u00e0 l'IT" },
            { "RequestSending",        "Envoi de la demande\u2026" },
            { "RequestOk",             "Demande envoy\u00e9e \u2014 l'IT va l'examiner." },
            { "RequestErr",            "\u00c9chec de la demande" },
        };

        private static readonly Dictionary<string, string> DE = new Dictionary<string, string>
        {
            { "HeaderSubtitle",        "vDomain Trust" },
            { "StatusWaiting",         "E-Mail ausw\u00e4hlen zum Pr\u00fcfen\u2026" },
            { "StatusVerifying",       "{0} wird gepr\u00fcft\u2026" },
            { "StatusVerified",        "\u2713 {0}  Verifiziert" },
            { "StatusNotFound",        "\u2717 {0}  Nicht registriert" },
            { "StatusError",           "\u26a0 {0}" },
            { "RowOrganisation",       "Organisation" },
            { "RowCategory",           "Kategorie" },
            { "RowTrusted",            "Vertrauensw\u00fcrdig" },
            { "RowTrustedYes",         "Ja \u2713" },
            { "RowTrustedNo",          "Nein \u2717" },
            { "RowDns",                "DNS" },
            { "RowDnsActive",          "Aktiv \u2713" },
            { "RowBlockchain",         "Blockchain" },
            { "RowBlockchainConfirmed","Best\u00e4tigt \u2713 ({0})" },
            { "RowTrustScore",         "Vertrauenswert" },
            { "PrivateVDomain",        "\U0001F512 Privates vDOMAIN" },
            { "PublicVDomain",         "\U0001F310 \u00d6ffentliches vDOMAIN" },
            { "ManagedBy",             "Verwaltet von {0}" },
            { "ManagedByOrg",          "Verwaltet von Ihrer Organisation" },
            { "ViaParentDomain",       "\u2192 \u00fcber {0}" },
            { "FallbackDisabled",      "Subdomain-Fallback deaktiviert" },
            { "FreeMailTitle",         "Kostenloser E-Mail-Anbieter" },
            { "FreeMailBody",          "Phishing kann \u00fcber diese Art von Domain auftreten.\nSeien Sie vorsichtig mit Anh\u00e4ngen und Links." },
            { "EmptyHint",             "Klicken Sie auf eine E-Mail, um\nden Absender zu \u00fcberpr\u00fcfen." },
            { "RequestBtn",            "Dom\u00e4ne bei IT anfragen" },
            { "RequestSending",        "Anfrage wird gesendet\u2026" },
            { "RequestOk",             "Anfrage gesendet \u2014 IT pr\u00fcft sie." },
            { "RequestErr",            "Anfrage fehlgeschlagen" },
        };

        private static readonly Dictionary<string, string> ES = new Dictionary<string, string>
        {
            { "HeaderSubtitle",        "vDomain Trust" },
            { "StatusWaiting",         "Seleccione un correo para verificar\u2026" },
            { "StatusVerifying",       "Verificando {0}\u2026" },
            { "StatusVerified",        "\u2713 {0}  Verificado" },
            { "StatusNotFound",        "\u2717 {0}  No registrado" },
            { "StatusError",           "\u26a0 {0}" },
            { "RowOrganisation",       "Organizaci\u00f3n" },
            { "RowCategory",           "Categor\u00eda" },
            { "RowTrusted",            "De confianza" },
            { "RowTrustedYes",         "S\u00ed \u2713" },
            { "RowTrustedNo",          "No \u2717" },
            { "RowDns",                "DNS" },
            { "RowDnsActive",          "Activo \u2713" },
            { "RowBlockchain",         "Blockchain" },
            { "RowBlockchainConfirmed","Confirmado \u2713 ({0})" },
            { "RowTrustScore",         "Puntuaci\u00f3n de confianza" },
            { "PrivateVDomain",        "\U0001F512 vDOMAIN Privado" },
            { "PublicVDomain",         "\U0001F310 vDOMAIN P\u00fablico" },
            { "ManagedBy",             "Gestionado por {0}" },
            { "ManagedByOrg",          "Gestionado por su organizaci\u00f3n" },
            { "ViaParentDomain",       "\u2192 v\u00eda {0}" },
            { "FallbackDisabled",      "Subdomain fallback desactivado" },
            { "FreeMailTitle",         "Proveedor de correo gratuito" },
            { "FreeMailBody",          "El phishing puede ocurrir a trav\u00e9s de este tipo de dominio.\nTenga cuidado con los archivos adjuntos y los enlaces." },
            { "EmptyHint",             "Haga clic en un correo para\nverificar el remitente." },
            { "RequestBtn",            "Solicitar dominio a IT" },
            { "RequestSending",        "Enviando solicitud\u2026" },
            { "RequestOk",             "Solicitud enviada \u2014 IT la revisar\u00e1." },
            { "RequestErr",            "Error al enviar la solicitud" },
        };
    }
}
