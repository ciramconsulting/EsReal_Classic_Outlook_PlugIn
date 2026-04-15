using System;
using System.Collections.Generic;

namespace EsRealOutlookAddin
{
    /// <summary>
    /// Detecteert gratis/publieke mailproviders waarbij phishing risico bestaat.
    /// De ingebouwde lijst kan NIET uitgeschakeld worden via config.
    /// Bedrijven kunnen extra domeinen toevoegen via EsRealConfig.ExtraFreeMailDomains.
    /// </summary>
    internal static class FreeMailDetector
    {
        // Ingebouwde lijst - niet aanpasbaar via config
        private static readonly HashSet<string> BuiltIn =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Google
            "gmail.com", "googlemail.com",
            // Microsoft
            "hotmail.com", "hotmail.be", "hotmail.fr", "hotmail.nl",
            "outlook.com", "outlook.be", "outlook.fr", "outlook.nl",
            "live.com", "live.be", "live.fr", "live.nl",
            "msn.com",
            // Yahoo
            "yahoo.com", "yahoo.co.uk", "yahoo.fr", "yahoo.de",
            "ymail.com",
            // Apple
            "icloud.com", "me.com", "mac.com",
            // Privacy/encrypted
            "protonmail.com", "proton.me", "tutanota.com", "tutamail.com",
            "mailfence.com", "disroot.org",
            // Overig gratis
            "gmx.com", "gmx.net", "gmx.de",
            "mail.com", "email.com",
            "aol.com",
            "zoho.com",
            "yandex.com", "yandex.ru",
            "qq.com", "163.com", "126.com",
            "telenet.be",
            "telenetmail.be",
            "proximus.be",
            "skynet.be",
            "belgacom.net",
            "voo.be",
            "orange.be",
            "scarlet.be",
            "pandora.be",
            "edpnet.be",
            "brutele.be",
            "belcenter.be",
            "dommel.be",
            "tiscali.be",
            "adsl.tiscali.be",
            "chello.be"
        };





        /// <summary>
        /// Controleer of een domein een gratis mailprovider is.
        /// Combineert de ingebouwde lijst met eventuele extra config-domeinen.
        /// </summary>
        public static bool IsFreeMailDomain(string domain, EsRealConfig cfg)
        {
            if (string.IsNullOrEmpty(domain)) return false;
            domain = domain.Trim().ToLowerInvariant();

            if (BuiltIn.Contains(domain)) return true;

            // Extra domeinen uit config
            if (cfg != null && !string.IsNullOrEmpty(cfg.ExtraFreeMailDomains))
            {
                foreach (var extra in cfg.ExtraFreeMailDomains.Split(','))
                {
                    var d = extra.Trim().ToLowerInvariant();
                    if (!string.IsNullOrEmpty(d) && d == domain) return true;
                }
            }

            return false;
        }
    }
}
