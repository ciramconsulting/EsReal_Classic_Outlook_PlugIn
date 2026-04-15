# EsReal VSTO Classic Outlook Plugin

Developed by [Ciram Consulting BV](https://ciram-consulting.com) - published as Open Source under the EsReal ORG initiative.
Used in products for EsReal BV, created and maintained by Ciram Consulting BV.

---

## About

The EsReal VSTO Classic Outlook Plugin is a Microsoft Outlook add-in that automatically verifies the vDOMAIN of every sender's email address.

When you open or select an email, the plugin shows a trust panel on the right side of Outlook with real-time verification results from the EsReal vDOMAIN registry.

A vDOMAIN (Verified Domain) is a domain that has been cryptographically registered and anchored in the EsReal registry - optionally confirmed on a public blockchain. This allows organisations and individuals to prove that their email domain is legitimate, verified, and tamper-evident.

---

## What it does

- Automatically detects the sender domain when you select an email in Outlook
- Queries the EsReal vDOMAIN API in real time
- Shows a trust panel with verification results directly in Outlook
- Supports subdomain fallback - e.g. `bosa.fgov.be` is verified via `fgov.be`
- Detects free mail providers (Gmail, Outlook.com, etc.) and shows a warning
- Shows blockchain anchor details when the domain is confirmed on-chain
- Supports Enterprise mode with private vDOMAIN registries and domain request workflows
- Configurable via Windows Registry - user settings or GPO for IT deployment
- Available in NL, EN, FR, DE and ES

---

## Developed by

| Field | Info |
|---|---|
| Developer | [Ciram Consulting BV](https://ciram-consulting.com) |
| Protocol owner | EsReal BV |
| Open Source initiative | EsReal ORG |
| License | Open Source - see [LICENSE](LICENSE) |

This open source repository is maintained by Ciram Consulting BV as part of the EsReal ecosystem.
Commercial products built on this protocol are developed by Ciram Consulting BV for EsReal BV.

---

## Requirements

- Windows 10 or Windows 11
- Microsoft Outlook 2016, 2019, 2021 or Microsoft 365 (classic desktop app)
- .NET Framework 4.7.2 or higher
- [Microsoft Visual Studio Tools for Office Runtime (VSTO)](https://learn.microsoft.com/en-us/visualstudio/vsto/visual-studio-tools-for-office-runtime-installation-scenarios)

---

## Installation

### Option 1 - ClickOnce installer (recommended)

1. Right-click `trust_vsto.reg` and run as administrator *(one-time setup)*
2. Double-click `EsRealOutlookAddin.vsto` to install
3. Restart Outlook

### Option 2 - Build from source

1. Clone this repository
2. Open `EsRealOutlookAddin.sln` in Visual Studio 2022
3. Restore NuGet packages
4. Build and publish via `Publish` in Visual Studio

---

## Configuration

The plugin reads its settings from the Windows Registry. All settings are optional - the plugin works with sensible defaults.

### Registry paths

| Path | Description |
|---|---|
| `HKCU\SOFTWARE\EsReal\Outlook` | User settings |
| `HKLM\SOFTWARE\Policies\EsReal\Outlook` | GPO / IT policy - overrides user settings |

### Available settings

| Key | Type | Default | Description |
|---|---|---|---|
| `Endpoint` | String | *(required)* | EsReal vDOMAIN API endpoint URL |
| `Ref` | String | *(empty)* | Reference identifier sent with each API request |
| `AutoVerify` | DWORD | `1` | Automatically verify on email selection |
| `Enterprise` | String | *(empty)* | Enterprise name shown in the panel |
| `SubdomainFallback` | DWORD | `1` | Try parent domain if subdomain is not found |
| `SubdomainFallbackDepth` | DWORD | `2` | How many subdomain levels to strip |
| `CacheTtlVerifiedMinutes` | DWORD | `360` | Cache duration for verified domains (min. 60) |
| `CacheTtlNotFoundMinutes` | DWORD | `15` | Cache duration for unknown domains (min. 5) |
| `CacheTtlErrorMinutes` | DWORD | `5` | Cache duration for errors (min. 2) |
| `ExtraFreeMailDomains` | String | *(empty)* | Comma-separated extra free mail domains to flag |
| `Language` | String | *(system)* | UI language: `nl`, `en`, `fr`, `de` or `es` |
| `DebugLogging` | DWORD | `0` | Write debug log to desktop |
| `ApiKey` | String | *(empty)* | Enterprise API key |
| `ApiKeyRequest` | String | *(empty)* | Enterprise request API key |

### Enterprise mode

Enterprise mode activates automatically when both `ApiKey` and `ApiKeyRequest` are configured.

In enterprise mode:

- The plugin queries a private vDOMAIN registry instead of the public one
- Users can request domain verification directly from within Outlook
- The panel shows an ENTERPRISE badge instead of PUBLIC

---

## Trust panel

The plugin adds a 280px panel on the right side of Outlook.

| Element | Description |
|---|---|
| ✓ EsReal badge | Domain is verified in the vDOMAIN registry |
| ✗ badge | Domain is not found in the registry |
| Trust score bar | Score from 0 to 100 |
| Organisation name | Registered organisation for the domain |
| Category | Domain category (e.g. government, finance) |
| DNS status | Whether the domain has active DNS |
| Blockchain anchor | On-chain confirmation status and transaction link |
| QR code | Quick link to the verified domain website |
| Free mail warning | Warning when sender uses Gmail, Outlook.com, etc. |

---

## API response format

The plugin calls the configured `Endpoint` with:

```
GET {Endpoint}?domain={domain}
Headers:
  x-esreal-ref: {Ref}
  User-Agent: EsReal-Outlook-VSTO/1.0
```

Expected JSON response:

```json
{
  "trusted": true,
  "organization_approved": true,
  "status": "verified",
  "org_name": "Ciram Consulting BV",
  "category": "ict",
  "site_type": "website",
  "trust_score": 98,
  "validation": {
    "vdomain_verified": true,
    "dns_active": true,
    "anchor_confirmed": true
  },
  "anchor": {
    "state": "confirmed",
    "chain": "DGB",
    "txid": "abc123...",
    "confirmed_at": { "utc": "2024-01-01T00:00:00Z" },
    "explorer": "https://digiexplorer.info/tx/abc123..."
  }
}
```

A domain is considered verified only when `validation.vdomain_verified` is `true`.

---

## Privacy & compliance

- The plugin only sends the sender domain to the API - never the full email address or message content
- Domain lookup results are cached locally in `%LOCALAPPDATA%\EsReal\domain_cache.json`
- Cache is automatically invalidated when the configuration changes
- Follow GDPR and applicable local privacy laws when deploying this plugin

---

## Debug logging

Enable debug logging via registry:

```reg
[HKEY_CURRENT_USER\SOFTWARE\EsReal\Outlook]
"DebugLogging"=dword:00000001
```

The log file is written to `EsReal_debug.log` on the user's desktop and is cleared on every Outlook startup.

---

## Contact & support

Ciram Consulting BV - Developer of the EsReal Protocol and Outlook Plugin

🌐 [ciram-consulting.com](https://ciram-consulting.com)

For EsReal vDOMAIN registry and protocol information:

🌐 [esreal.org](https://esreal.org) - [esreal.be](https://esreal.be)
