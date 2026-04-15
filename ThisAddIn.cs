using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Office.Interop.Outlook;
using Microsoft.Office.Tools;
using Task = System.Threading.Tasks.Task;
using SysException = System.Exception;

namespace EsRealOutlookAddin
{
    public partial class ThisAddIn
    {
        private EsRealConfig    _cfg;
        private EsRealApiClient _api;
        private EsRealTaskPane  _explorerPaneControl;
        private CustomTaskPane  _explorerTaskPane;

        private readonly Dictionary<Inspector, CustomTaskPane> _inspectorPanes
            = new Dictionary<Inspector, CustomTaskPane>();
        private Inspectors _inspectors;

        private string _lastKey = "";

        internal static void Log(string msg)
        {
            EsRealTaskPane.Log(msg);
        }

        // ── Startup ───────────────────────────────────────────────────────

        private void ThisAddIn_Startup(object sender, EventArgs e)
        {
ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            _cfg = RegistryConfig.Load();
            _api = new EsRealApiClient(_cfg);

            // Initialiseer vertalingen - registry taal of systeem taal
            Strings.Init(string.IsNullOrEmpty(_cfg.Language) ? null : _cfg.Language);

            // Debug logging - standaard UIT, aan via registry DebugLogging=1
            EsRealTaskPane.LoggingEnabled = _cfg.DebugLogging;

            // Wis oud logbestand bij opstarten (alleen als logging aan staat)
            if (_cfg.DebugLogging)
            {
                try
                {
                    string logPath = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                        "EsReal_debug.log");
                    System.IO.File.Delete(logPath);
                }
                catch { }
            }
            Log("=== Startup ===");

            _explorerPaneControl = new EsRealTaskPane(_api, _cfg);
            _explorerPaneControl.RefreshRequested += (s, ea) => ForceRefresh();
            _explorerTaskPane = CustomTaskPanes.Add(_explorerPaneControl, "EsReal Trust");
            _explorerTaskPane.Width       = 280;
            _explorerTaskPane.DockPosition = Microsoft.Office.Core.MsoCTPDockPosition.msoCTPDockPositionRight;
            _explorerTaskPane.Visible     = true;

            // Hook alle bestaande explorers
            foreach (Explorer ex in Application.Explorers)
                HookExplorer(ex);

            Application.Explorers.NewExplorer += (Explorer ex) =>
            {
                Log("NewExplorer");
                _lastKey = "";
                HookExplorer(ex);
                OnSelectionChange();
            };

            _inspectors = Application.Inspectors;
            _inspectors.NewInspector += OnNewInspector;

            foreach (Inspector insp in Application.Inspectors)
                HookInspector(insp);

            Log("Startup complete");
        }

        // ── Explorer ──────────────────────────────────────────────────────

        private readonly List<Explorer> _hookedExplorers = new List<Explorer>();

        private void HookExplorer(Explorer explorer)
        {
            if (explorer == null) return;

            // Voorkom dubbele hooks
            foreach (var e in _hookedExplorers)
            {
                try { if (e == explorer) { Log("Already hooked"); return; } }
                catch { }
            }

            Log("HookExplorer");
            _hookedExplorers.Add(explorer);

            ((ExplorerEvents_10_Event)explorer).SelectionChange += OnSelectionChange;
            ((ExplorerEvents_10_Event)explorer).FolderSwitch    += () =>
            {
                Log("FolderSwitch");
                _lastKey = "";
                OnSelectionChange();
            };
        }

        private void OnSelectionChange()
        {
            try
            {
                var explorer = Application.ActiveExplorer();
                if (explorer == null) { Log("SC: no explorer"); return; }

                if (explorer.Selection == null || explorer.Selection.Count == 0)
                {
                    Log("SC: empty");
                    _explorerPaneControl.ShowEmpty();
                    return;
                }

                var item = explorer.Selection[1] as MailItem;
                if (item == null)
                {
                    Log("SC: not mail");
                    _explorerPaneControl.ShowEmpty();
                    return;
                }

                string key = BuildKey(item);
                Log("SC: key=" + key.Substring(0, Math.Min(20, key.Length)) + " same=" + (key == _lastKey));

                if (key == _lastKey) return;
                _lastKey = key;

                ProcessMail(item, _explorerPaneControl);
            }
            catch (SysException ex) { Log("SC error: " + ex.Message); }
        }

        private static string BuildKey(MailItem item)
        {
            try
            {
                var id = item.EntryID;
                if (!string.IsNullOrEmpty(id)) return id;
            }
            catch { }
            try
            {
                return (item.SenderEmailAddress ?? "") + "|"
                     + (item.Subject ?? "") + "|"
                     + item.ReceivedTime.Ticks;
            }
            catch { }
            return Guid.NewGuid().ToString();
        }

        // ── Inspector ─────────────────────────────────────────────────────

        private void OnNewInspector(Inspector inspector) => HookInspector(inspector);

        private void HookInspector(Inspector inspector)
        {
            if (inspector == null) return;

            // Voorkom dubbele TaskPanes bij herhaald openen van dezelfde mail
            if (_inspectorPanes.ContainsKey(inspector))
            {
                Log("HookInspector: already hooked, skip");
                return;
            }

            Log("HookInspector");

            var pane = new EsRealTaskPane(_api, _cfg);
            pane.RefreshRequested += (s, ea) => ForceRefreshInspector(inspector, pane);
            var tp = CustomTaskPanes.Add(pane, "EsReal Trust", inspector);
            tp.Width = 280;
            tp.DockPosition = Microsoft.Office.Core.MsoCTPDockPosition.msoCTPDockPositionRight;
            tp.Visible = true;
            _inspectorPanes[inspector] = tp;

            var mail = inspector.CurrentItem as MailItem;
            if (mail != null) ProcessMail(mail, pane);

            ((InspectorEvents_Event)inspector).Close += () =>
            {
                Log("InspectorClose");
                try
                {
                    CustomTaskPane closingTp;
                    if (_inspectorPanes.TryGetValue(inspector, out closingTp))
                    {
                        _inspectorPanes.Remove(inspector);
                        CustomTaskPanes.Remove(closingTp);
                    }
                }
                catch { }
            };
        }

        // ── Verwerken ─────────────────────────────────────────────────────

        private void ProcessMail(MailItem item, EsRealTaskPane pane)
        {
            string email = "";
            string domain = "";
            try
            {
                email = ResolveSmtp(item);
                if (string.IsNullOrEmpty(email) || !email.Contains("@"))
                { Log("ProcessMail: no email"); return; }
                email  = email.Trim().ToLowerInvariant();
                domain = email.Split('@')[1];
            }
            catch (SysException ex) { Log("ProcessMail resolve error: " + ex.Message); return; }

            Log("ProcessMail: " + domain);
            pane.SetLoading(email, domain);

            var capturedItem = item;
            var capturedDomain = domain;
            Task.Run(async () =>
            {
                try
                {
                    var result = await _api.VerifyAsync(capturedDomain).ConfigureAwait(false);

                    // Gratis mailprovider check - altijd, ongeacht verified status
                    result.IsFreeMailDomain = FreeMailDetector.IsFreeMailDomain(capturedDomain, _cfg);

                    Log("Verify: " + capturedDomain + " ok=" + result.IsVerified +
                        " freeMail=" + result.IsFreeMailDomain);
                    pane.SetResult(result);
                }
                catch (SysException ex) { Log("Verify error: " + ex.Message); }
            });
        }

        private static string ResolveSmtp(MailItem item)
        {
            try
            {
                var addr = item.SenderEmailAddress ?? "";
                if (addr.Contains("@")) return addr;

                var entry = item.Sender;
                if (entry == null) return addr;

                try
                {
                    var u = entry.GetExchangeUser();
                    if (u != null && !string.IsNullOrEmpty(u.PrimarySmtpAddress)) return u.PrimarySmtpAddress;
                }
                catch { }

                try
                {
                    var dl = entry.GetExchangeDistributionList();
                    if (dl != null && !string.IsNullOrEmpty(dl.PrimarySmtpAddress)) return dl.PrimarySmtpAddress;
                }
                catch { }

                try
                {
                    const string PR = "http://schemas.microsoft.com/mapi/proptag/0x39FE001E";
                    var smtp = entry.PropertyAccessor.GetProperty(PR) as string;
                    if (!string.IsNullOrEmpty(smtp) && smtp.Contains("@")) return smtp;
                }
                catch { }

                if (!string.IsNullOrEmpty(entry.Address) && entry.Address.Contains("@"))
                    return entry.Address;

                return addr;
            }
            catch { return ""; }
        }

        // ── Refresh ───────────────────────────────────────────────────────

        private void ForceRefresh()
        {
            Log("ForceRefresh");
            try
            {
                var explorer = Application.ActiveExplorer();
                if (explorer == null || explorer.Selection.Count == 0) return;
                var item = explorer.Selection[1] as MailItem;
                if (item == null) return;
                var email = ResolveSmtp(item);
                if (!string.IsNullOrEmpty(email) && email.Contains("@"))
                    _api.InvalidateDomain(email.Split('@')[1]);
                _lastKey = "";
                ProcessMail(item, _explorerPaneControl);
            }
            catch (SysException ex) { Log("ForceRefresh error: " + ex.Message); }
        }

        private void ForceRefreshInspector(Inspector inspector, EsRealTaskPane pane)
        {
            try
            {
                var mail = inspector.CurrentItem as MailItem;
                if (mail == null) return;
                var email = ResolveSmtp(mail);
                if (!string.IsNullOrEmpty(email) && email.Contains("@"))
                    _api.InvalidateDomain(email.Split('@')[1]);
                ProcessMail(mail, pane);
            }
            catch (SysException ex) { Log("ForceRefreshInspector error: " + ex.Message); }
        }

        // ── Public helpers ────────────────────────────────────────────────

        public void ToggleTaskPane()
        {
            try
            {
                var insp = Application.ActiveInspector();
                if (insp != null && _inspectorPanes.TryGetValue(insp, out var p))
                { p.Visible = !p.Visible; return; }
                if (_explorerTaskPane != null)
                    _explorerTaskPane.Visible = !_explorerTaskPane.Visible;
            }
            catch { }
        }

        public bool IsTaskPaneVisible()
        {
            try
            {
                var insp = Application.ActiveInspector();
                if (insp != null && _inspectorPanes.TryGetValue(insp, out var p)) return p.Visible;
                return _explorerTaskPane != null && _explorerTaskPane.Visible;
            }
            catch { return false; }
        }

        public EsRealApiClient GetApiClient() => _api;
        public EsRealConfig    GetConfig()    => _cfg;

        // ── Shutdown ──────────────────────────────────────────────────────

        private void ThisAddIn_Shutdown(object sender, EventArgs e)
        {
            Log("=== Shutdown ===");
            _api?.Dispose();
        }

        #region VSTO generated code
        private void InternalStartup()
        {
            Startup  += ThisAddIn_Startup;
            Shutdown += ThisAddIn_Shutdown;
        }
        #endregion
    }
}
