using System;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;

namespace EsRealOutlookAddin
{
    /// <summary>
    /// Details overlay - verschijnt bij klik op badge.
    /// Zelfde inhoud als de browser plugin overlay:
    /// organisatie, categorie, status, trust score, blockchain chip.
    /// Sluit bij klik buiten of op ✕.
    /// </summary>
    public class DetailOverlay : Form
    {
        private static readonly Color C_NAVY  = Color.FromArgb(5,  13, 26);
        private static readonly Color C_NAVY2 = Color.FromArgb(13, 26, 46);
        private static readonly Color C_CYAN  = Color.FromArgb(0,  180, 216);
        private static readonly Color C_WHITE = Color.FromArgb(241, 245, 249);
        private static readonly Color C_MUTED = Color.FromArgb(100, 116, 139);
        private static readonly Color C_GREEN = Color.FromArgb(74,  222, 128);
        private static readonly Color C_RED   = Color.FromArgb(248, 113, 113);
        private static readonly Color C_AMBER = Color.FromArgb(251, 191, 36);

        public DetailOverlay(VerifyResult result, string email)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar   = false;
            TopMost         = true;
            BackColor       = C_NAVY;
            Width           = 300;
            AutoSize        = true;
            AutoSizeMode    = AutoSizeMode.GrowAndShrink;
            Padding         = new Padding(0);
            StartPosition   = FormStartPosition.Manual;

            // Sluit bij klik buiten
            Deactivate += (s, e) => Close();

            BuildUI(result, email);

            // Fade in
            Opacity = 0;
            Shown  += async (s, e) =>
            {
                for (double o = 0; o <= 1; o += 0.1)
                {
                    Opacity = o;
                    await System.Threading.Tasks.Task.Delay(16);
                }
                Opacity = 1;
            };
        }

        private void BuildUI(VerifyResult result, string email)
        {
            var ok         = result.IsVerified;
            var accentColor = result.IsError ? C_AMBER : ok ? C_GREEN : C_RED;
            var icon        = result.IsError ? "⚠" : ok ? "✓" : "✗";

            var panel = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents  = false,
                AutoSize      = true,
                AutoSizeMode  = AutoSizeMode.GrowAndShrink,
                BackColor     = C_NAVY,
                Padding       = new Padding(0),
            };

            // ── Accent lijn bovenaan ──────────────────────────────────────
            panel.Controls.Add(new Panel
            {
                Width     = 300, Height = 3,
                BackColor = accentColor,
            });

            // ── Header ────────────────────────────────────────────────────
            var header = new TableLayoutPanel
            {
                Width         = 300, Height = 52,
                BackColor     = C_NAVY2,
                Padding       = new Padding(12, 8, 8, 8),
                ColumnCount   = 3,
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 24));

            // Icoon cirkel
            var iconLbl = new Label
            {
                Text      = icon,
                Width     = 28, Height = 28,
                TextAlign = ContentAlignment.MiddleCenter,
                Font      = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = accentColor,
                BackColor = Color.FromArgb(30, accentColor.R, accentColor.G, accentColor.B),
            };
            iconLbl.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.FillEllipse(new SolidBrush(Color.FromArgb(40, accentColor.R, accentColor.G, accentColor.B)),
                    0, 0, 27, 27);
            };

            // Titel + sub
            var texts = new Panel { Width = 210, Height = 36, BackColor = C_NAVY2 };
            var titleLbl = new Label
            {
                Text      = ok  ? $"{result.DisplayOrg} - Geverifieerd"
                           : result.IsError ? "Verificatie fout"
                           : $"{result.Domain} - Niet in registry",
                Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = accentColor,
                AutoSize  = true,
                Location  = new Point(0, 0),
                MaximumSize = new Size(210, 0),
            };
            var subLbl = new Label
            {
                Text      = email,
                Font      = new Font("Segoe UI", 7.5f),
                ForeColor = C_MUTED,
                AutoSize  = true,
                Location  = new Point(0, 18),
                MaximumSize = new Size(210, 0),
            };
            texts.Controls.Add(titleLbl);
            texts.Controls.Add(subLbl);

            // Sluit knop
            var closeBtn = new Button
            {
                Text      = "✕",
                Width     = 20, Height = 20,
                FlatStyle = FlatStyle.Flat,
                ForeColor = C_MUTED,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", 8),
                Cursor    = Cursors.Hand,
            };
            closeBtn.FlatAppearance.BorderSize = 0;
            closeBtn.Click += (s, e) => Close();

            header.Controls.Add(iconLbl);
            header.Controls.Add(texts);
            header.Controls.Add(closeBtn);
            panel.Controls.Add(header);

            if (!result.IsError)
            {
                // ── Detail rijen ──────────────────────────────────────────
                var body = new TableLayoutPanel
                {
                    Width       = 300,
                    AutoSize    = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    BackColor   = C_NAVY,
                    Padding     = new Padding(12, 6, 12, 6),
                    ColumnCount = 2,
                };
                body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
                body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));

                void AddRow(string key, string val, Color valColor)
                {
                    body.Controls.Add(new Label { Text = key, Font = new Font("Segoe UI", 7.5f), ForeColor = C_MUTED, AutoSize = true, Padding = new Padding(0,3,0,3) });
                    body.Controls.Add(new Label { Text = val, Font = new Font("Segoe UI", 7.5f, FontStyle.Bold), ForeColor = valColor, AutoSize = true, Padding = new Padding(0,3,0,3) });
                }



                if (!string.IsNullOrEmpty(result.OrgName))   AddRow("Organisatie", result.OrgName,   C_WHITE);
                if (!string.IsNullOrEmpty(result.Category))  AddRow("Categorie",   result.Category,  C_WHITE);
                if (!string.IsNullOrEmpty(result.SiteType))  AddRow("Type",        result.SiteType,  C_WHITE);
                AddRow("Status",     result.Status,             ok ? C_GREEN : C_RED);
                AddRow("Trusted",    result.Trusted ? "Ja ✓" : "Nee ✗", result.Trusted ? C_GREEN : C_RED);
                AddRow("DNS",        result.DnsActive ? "Actief ✓" : "-", result.DnsActive ? C_GREEN : C_MUTED);
                AddRow("Blockchain", result.AnchorState == "confirmed" ? $"Confirmed ✓ ({result.AnchorChain})" : (result.AnchorState.Length > 0 ? result.AnchorState : "-"),
                    result.AnchorState == "confirmed" ? C_GREEN : C_MUTED);
                if (result.TrustScore > 0)
                    AddRow("Trust score", $"~ {result.TrustScore} %",
                        result.TrustScore >= 80 ? C_GREEN : result.TrustScore >= 50 ? C_AMBER : C_RED);

                panel.Controls.Add(body);

                // ── Trust score balk ──────────────────────────────────────
                if (result.TrustScore > 0)
                {
                    var barWrap = new Panel { Width = 300, Height = 10, BackColor = C_NAVY, Padding = new Padding(12, 0, 12, 0) };
                    var barBg   = new Panel { Left = 12, Top = 3, Width = 276, Height = 5, BackColor = Color.FromArgb(30, 255, 255, 255) };
                    var scoreColor = result.TrustScore >= 80 ? C_GREEN : result.TrustScore >= 50 ? C_AMBER : C_RED;
                    var barFg   = new Panel { Left = 0, Top = 0, Width = (int)(276 * result.TrustScore / 100.0), Height = 5, BackColor = scoreColor };
                    barBg.Controls.Add(barFg);
                    barWrap.Controls.Add(barBg);
                    panel.Controls.Add(barWrap);
                }

                // ── Blockchain link ───────────────────────────────────────
                if (result.IsBlockchain && !string.IsNullOrEmpty(result.AnchorExplorer))
                {
                    var chainBtn = new LinkLabel
                    {
                        Text       = $"⛓ {result.AnchorChain} · {result.AnchorTxid.Substring(0, Math.Min(16, result.AnchorTxid.Length))}… · {result.AnchorConfirmedAt.Substring(0, Math.Min(10, result.AnchorConfirmedAt.Length))}",
                        Width      = 300,
                        Padding    = new Padding(12, 4, 12, 4),
                        Font       = new Font("Consolas", 7.5f),
                        BackColor  = Color.FromArgb(15, 0, 180, 216),
                        LinkColor  = C_CYAN,
                        AutoSize   = false, Height = 24,
                        TextAlign  = ContentAlignment.MiddleLeft,
                    };
                    chainBtn.LinkClicked += (s, e) =>
                    {
                        try { Process.Start(new ProcessStartInfo(result.AnchorExplorer) { UseShellExecute = true }); }
                        catch { }
                    };
                    panel.Controls.Add(chainBtn);
                }
            }
            else
            {
                panel.Controls.Add(new Label
                {
                    Text      = result.ErrorMessage,
                    Font      = new Font("Segoe UI", 8),
                    ForeColor = C_AMBER,
                    Padding   = new Padding(12, 8, 12, 8),
                    Width     = 300, AutoSize = false, Height = 32,
                });
            }

            // ── Footer ────────────────────────────────────────────────────
            var footer = new Panel { Width = 300, Height = 22, BackColor = Color.FromArgb(8, 18, 36) };
            footer.Controls.Add(new Label
            {
                Text      = "EsReal® vDomain Trust Verification · esreal.org",
                Font      = new Font("Segoe UI", 6.5f),
                ForeColor = C_MUTED,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
            });
            panel.Controls.Add(footer);

            // Border
            Paint += (s, e) =>
            {
                ControlPaint.DrawBorder(e.Graphics, ClientRectangle,
                    Color.FromArgb(80, 0, 180, 216), 1, ButtonBorderStyle.Solid,
                    Color.FromArgb(80, 0, 180, 216), 1, ButtonBorderStyle.Solid,
                    Color.FromArgb(80, 0, 180, 216), 1, ButtonBorderStyle.Solid,
                    Color.FromArgb(80, 0, 180, 216), 1, ButtonBorderStyle.Solid);
            };

            Controls.Add(panel);
        }

        // Voorkomt focus steal van Outlook
        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                return cp;
            }
        }
    }
}
