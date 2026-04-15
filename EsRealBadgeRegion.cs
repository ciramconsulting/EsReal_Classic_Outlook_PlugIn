using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Office.Interop.Outlook;
using Microsoft.Office.Tools.Outlook;
using SysAction = System.Action;

namespace EsRealOutlookAddin
{
    // =========================================================================
    // EsRealBadgeRegion - inline badge-strip bovenaan elke gelezen MailItem
    //
    // FormRegionControl is abstract en heeft nauwelijks leden - niet bruikbaar
    // voor directe UI. Oplossing: FormRegionBase erven, maar alle UI in een
    // eigen UserControl (_strip) stoppen die we in this.OutlookFormRegion.Form
    // hosten zodra de FormRegion toont.
    // =========================================================================

    public partial class EsRealBadgeRegion : FormRegionBase
    {
        private BadgeStrip _strip;

        internal EsRealBadgeRegion(Microsoft.Office.Interop.Outlook.FormRegion formRegion)
            : base(Globals.Factory, formRegion)
        {
        }

        private void EsRealBadgeRegion_FormRegionShowing(object sender, EventArgs e)
        {
            try
            {
                _strip = new BadgeStrip();

                // Outlook geeft ons een Form via de FormRegion - daar hosten we de strip
                var form = this.OutlookFormRegion?.Form;
                if (form != null)
                {
                    _strip.Dock = DockStyle.Top;
                    form.Controls.Add(_strip);
                    form.Controls.SetChildIndex(_strip, 0);
                }

                var mail = this.OutlookItem as MailItem;
                if (mail != null)
                    _strip.LoadMail(mail, Globals.ThisAddIn.GetApiClient());
            }
            catch { }
        }

        private void EsRealBadgeRegion_FormRegionClosed(object sender, EventArgs e)
        {
            try { _strip?.Dispose(); } catch { }
        }

        // =========================================================================
        // Factory
        // =========================================================================

        [FormRegionMessageClass(FormRegionMessageClassAttribute.Note)]
        [FormRegionName("EsRealOutlookAddin.EsRealBadgeRegion")]
        public partial class Factory : IFormRegionFactory
        {
            public FormRegionManifest Manifest { get; private set; }

            [System.Diagnostics.DebuggerNonUserCode]
            public Factory()
            {
                this.Manifest = Globals.Factory.CreateFormRegionManifest();
                this.Manifest.FormRegionName = "EsRealOutlookAddin.EsRealBadgeRegion";
            }

            [System.Diagnostics.DebuggerNonUserCode]
            public IFormRegion CreateFormRegion(
                Microsoft.Office.Interop.Outlook.FormRegion formRegion)
            {
                var region = new EsRealBadgeRegion(formRegion);
                region.FormRegionShowing += region.EsRealBadgeRegion_FormRegionShowing;
                region.FormRegionClosed  += region.EsRealBadgeRegion_FormRegionClosed;
                return region;
            }

            [System.Diagnostics.DebuggerNonUserCode]
            public byte[] GetFormRegionStorage(
                object outlookItem,
                OlFormRegionMode formRegionMode,
                OlFormRegionSize formRegionSize)
            {
                return null;
            }

            [System.Diagnostics.DebuggerNonUserCode]
            public bool IsDisplayedForItem(
                object outlookItem,
                OlFormRegionMode formRegionMode,
                OlFormRegionSize formRegionSize)
            {
                return formRegionMode == OlFormRegionMode.olFormRegionRead;
            }

            [System.Diagnostics.DebuggerNonUserCode]
            public FormRegionKindConstants Kind
            {
                get { return (FormRegionKindConstants)1; } // 1 = Adjoining
            }
        }
    }

    // =========================================================================
    // BadgeStrip - de zichtbare UI, gewone UserControl
    // =========================================================================

    internal class BadgeStrip : UserControl
    {
        private static readonly Color C_NAVY2 = Color.FromArgb(13,  26,  46);
        private static readonly Color C_CYAN  = Color.FromArgb(0,   180, 216);
        private static readonly Color C_MUTED = Color.FromArgb(100, 116, 139);
        private static readonly Color C_GREEN = Color.FromArgb(74,  222, 128);
        private static readonly Color C_RED   = Color.FromArgb(248, 113, 113);
        private static readonly Color C_AMBER = Color.FromArgb(251, 191, 36);

        private Label        _lblLogo;
        private BadgeControl _badge;
        private Label        _lblInfo;
        private string       _currentEmail = "";

        public BadgeStrip()
        {
            Height         = 28;
            BackColor      = C_NAVY2;
            DoubleBuffered = true;

            _lblLogo = new Label
            {
                Text      = "EsReal\u00ae",
                Font      = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = C_CYAN,
                AutoSize  = true,
                Location  = new Point(8, 6),
            };

            _badge = new BadgeControl
            {
                Location = new Point(70, 4),
                Visible  = false,
            };

            _lblInfo = new Label
            {
                Text      = "Verificatie bezig\u2026",
                Font      = new Font("Segoe UI", 7.5f),
                ForeColor = C_MUTED,
                AutoSize  = true,
                Location  = new Point(155, 6),
            };

            Controls.Add(_lblLogo);
            Controls.Add(_badge);
            Controls.Add(_lblInfo);

            Paint += (s, e) =>
            {
                using (var pen = new Pen(C_CYAN, 1f))
                    e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
            };
        }

        public void LoadMail(MailItem item, EsRealApiClient api)
        {
            if (item == null || api == null) return;

            string email  = "";
            string domain = "";

            try
            {
                email = item.SenderEmailAddress ?? "";
                if (!email.Contains("@"))
                {
                    var sender = item.Sender;
                    if (sender != null)
                        email = sender.GetExchangeUser()?.PrimarySmtpAddress ?? email;
                }
                if (!email.Contains("@")) return;

                email  = email.Trim().ToLowerInvariant();
                domain = email.Split('@')[1];
            }
            catch { return; }

            _currentEmail = email;
            SetLoading(domain);

            Task.Run(async () =>
            {
                var result = await api.VerifyAsync(domain).ConfigureAwait(false);
                SetResult(result);
            });
        }

        private void SetLoading(string domain)
        {
            if (InvokeRequired) { Invoke(new SysAction(() => SetLoading(domain))); return; }
            _badge.SetLoading(_currentEmail);
            _badge.Visible     = true;
            _lblInfo.Text      = "Verifieer " + domain + "\u2026";
            _lblInfo.ForeColor = C_MUTED;
        }

        private void SetResult(VerifyResult result)
        {
            if (InvokeRequired) { Invoke(new SysAction(() => SetResult(result))); return; }

            _badge.SetResult(result, _currentEmail);
            _badge.Visible = true;

            if (result.IsError)
            {
                _lblInfo.Text      = "\u26a0 Fout: " + result.ErrorMessage;
                _lblInfo.ForeColor = C_AMBER;
            }
            else if (result.IsVerified)
            {
                string score = result.TrustScore > 0
                    ? "  \u2014  Trust score: " + result.TrustScore + "/100"
                    : "";
                _lblInfo.Text      = _currentEmail + "  \u2014  " + result.DisplayOrg + score;
                _lblInfo.ForeColor = C_GREEN;
            }
            else
            {
                _lblInfo.Text      = _currentEmail + "  \u2014  Niet in EsReal registry";
                _lblInfo.ForeColor = C_RED;
            }
        }
    }
}
