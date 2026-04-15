using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using QRCoder;
using System.Drawing.Text;
using System.Windows.Forms;
using SysAction = System.Action;

namespace EsRealOutlookAddin
{
    public class EsRealTaskPane : UserControl
    {
        // Gedeelde logger - ook gebruikt door EsRealApiClient
        internal static bool LoggingEnabled = false;

        internal static void Log(string msg)
        {
            if (!LoggingEnabled) return;
            try
            {
                string path = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "EsReal_debug.log");
                System.IO.File.AppendAllText(path,
                    DateTime.Now.ToString("HH:mm:ss.fff") + "  " + msg + "");
            }
            catch { };
        }
          

        // ── Palet ─────────────────────────────────────────────────────────
        static readonly Color BG = Color.FromArgb(12, 16, 28);
        static readonly Color CARD = Color.FromArgb(20, 27, 46);
        static readonly Color CARD2 = Color.FromArgb(26, 34, 58);
        static readonly Color BORDER = Color.FromArgb(38, 50, 80);
        static readonly Color CYAN = Color.FromArgb(0, 186, 219);
        static readonly Color WHITE = Color.FromArgb(235, 242, 255);
        static readonly Color MUTED = Color.FromArgb(100, 118, 158);
        static readonly Color GREEN = Color.FromArgb(34, 211, 102);
        static readonly Color RED = Color.FromArgb(248, 72, 72);
        static readonly Color AMBER = Color.FromArgb(251, 176, 24);
        static readonly Color GREEN_DIM = Color.FromArgb(16, 42, 24);
        static readonly Color RED_DIM = Color.FromArgb(42, 14, 14);
        static readonly Color AMBER_DIM = Color.FromArgb(40, 30, 8);

        readonly EsRealApiClient _api;
        readonly EsRealConfig _cfg;
        public event EventHandler RefreshRequested;

        // State
        VerifyResult _result;
        string _email = "";
        string _domain = "";
        bool _loading;
        Bitmap _qrBitmap = null;

        public EsRealTaskPane(EsRealApiClient api, EsRealConfig cfg)
        {
            _api = api; _cfg = cfg;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            BackColor = BG;
            ForeColor = WHITE;
            MinimumSize = new Size(260, 400);
            Cursor = Cursors.Default;
            Click += OnPanelClick;
            MouseMove += OnPanelMouseMove;
        }

        // ── Public API ────────────────────────────────────────────────────

        public void SetLoading(string email, string domain)
        {
            if (InvokeRequired) { Invoke(new SysAction(() => SetLoading(email, domain))); return; }
            _email = email; _domain = domain; _loading = true; _result = null;
            Invalidate();
        }

        private void GenerateQr(string domain)
        {
            try
            {
                if (_qrBitmap != null) { _qrBitmap.Dispose(); _qrBitmap = null; }
                var url = "https://" + domain;
                using (var gen = new QRCodeGenerator())
                using (var data = gen.CreateQrCode(url, QRCodeGenerator.ECCLevel.M))
                using (var qr = new QRCode(data))
                {
                    // Render as Bitmap: pixels per module, dark=cyan, light=card color
                    _qrBitmap = qr.GetGraphic(
                        4,
                        System.Drawing.Color.FromArgb(0, 186, 227),   // cyan
                        System.Drawing.Color.FromArgb(17, 21, 40),    // card bg
                        false);
                }
            }
            catch { _qrBitmap = null; }
        }

        public void SetResult(VerifyResult result)
        {
            if (InvokeRequired) { Invoke(new SysAction(() => SetResult(result))); return; }
            _result = result; _loading = false;
            _requestState = RequestState.Hidden;
            GenerateQr(result.MatchedDomain.Length > 0 ? result.MatchedDomain : result.Domain);
            Invalidate();
        }

        public void ShowEmpty()
        {
            if (InvokeRequired) { Invoke(new SysAction(ShowEmpty)); return; }
            _result = null; _loading = false; _email = ""; _domain = "";
            _requestState = RequestState.Hidden;
            Invalidate();
        }

        // ── Paint ─────────────────────────────────────────────────────────

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int W = ClientSize.Width;
            int y = 0;

            // Header
            y = DrawHeader(g, W, y);

            if (!_loading && _result == null && string.IsNullOrEmpty(_domain))
            {
                DrawEmpty(g, W, y);
                return;
            }

            // Gratis mailprovider waarschuwing banner
            if (!_loading && _result != null && _result.IsFreeMailDomain)
                y = DrawFreeMailBanner(g, W, y);

            // Status card
            y = DrawStatusCard(g, W, y);

            if (_loading)
            {
                DrawLoadingCard(g, W, y);
                return;
            }

            if (_result != null)
                DrawResultCard(g, W, y);
        }

        // ── Header ────────────────────────────────────────────────────────

        // Versienummer uit de assembly — eenmalig gelezen
        static readonly string PluginVersion = "v" +
            System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3);

        int DrawHeader(Graphics g, int W, int y)
        {
            int H = 68;
            // Achtergrond
            g.FillRectangle(new SolidBrush(CARD), 0, y, W, H);

            // "Es" wit + "Real" cyaan + "®" muted
            float x = 14;
            using (var fBold = new Font("Segoe UI", 13f, FontStyle.Bold))
            {
                g.DrawString("Es", fBold, new SolidBrush(WHITE), x, y + 8);
                x += g.MeasureString("Es", fBold).Width - 5;
                g.DrawString("Real", fBold, new SolidBrush(CYAN), x, y + 8);
                x += g.MeasureString("Real", fBold).Width - 5;
            }
            using (var fReg = new Font("Segoe UI", 8f))
                g.DrawString("\u00ae", fReg, new SolidBrush(MUTED), x, y + 10);

            // Versienummer — rechts naast de refresh knop
            using (var fVer = new Font("Consolas", 7f))
            {
                var sz = g.MeasureString(PluginVersion, fVer);
                g.DrawString(PluginVersion, fVer, new SolidBrush(MUTED),
                    W - 38 - sz.Width, y + 14);
            }

            // Refresh knop rechtsboven
            _refreshRect = new Rectangle(W - 34, y + 8, 24, 24);
            DrawRoundRect(g, _refreshRect, 6, CARD2, BORDER);
            using (var fIcon = new Font("Segoe UI", 10f))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("\u21bb", fIcon, new SolidBrush(MUTED), _refreshRect, sf);
            }

            // Subtitel
            using (var fSub = new Font("Segoe UI", 7.5f))
                g.DrawString(Strings.HeaderSubtitle, fSub, new SolidBrush(MUTED), 14, y + 30);

            // Modus badge — Enterprise (cyaan) of Public (muted)
            bool isEnterprise = _cfg != null && _cfg.IsEnterpriseMode;
            string modeLabel  = isEnterprise ? "ENTERPRISE" : "PUBLIC";
            Color modeBg      = isEnterprise ? Color.FromArgb(20, 0, 186, 219)  : Color.FromArgb(15, 100, 118, 158);
            Color modeBd      = isEnterprise ? Color.FromArgb(90, 0, 186, 219)  : Color.FromArgb(70, 100, 118, 158);
            Color modeTx      = isEnterprise ? CYAN                              : MUTED;

            using (var fMode = new Font("Segoe UI", 6.5f, FontStyle.Bold))
            {
                var sz       = g.MeasureString(modeLabel, fMode);
                int badgeW   = (int)sz.Width + 10;
                int badgeH   = 14;
                var badgeRect = new Rectangle(14, y + 47, badgeW, badgeH);
                DrawRoundRect(g, badgeRect, 3, modeBg, modeBd);
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(modeLabel, fMode, new SolidBrush(modeTx), badgeRect, sf);
            }

            // Cyaan lijn onderaan
            using (var br = new LinearGradientBrush(
                new Point(0, y + H - 2), new Point(W, y + H - 2),
                CYAN, Color.FromArgb(0, CYAN)))
                g.FillRectangle(br, 0, y + H - 2, W, 2);

            return y + H;
        }

        // ── Status card ───────────────────────────────────────────────────

        int DrawStatusCard(Graphics g, int W, int y)
        {
            y += 10;
            int H = 48;
            var rect = new Rectangle(10, y, W - 20, H);

            Color accent = _loading ? AMBER
                         : (_result == null ? MUTED
                         : _result.IsVerified ? GREEN
                         : _result.IsError ? AMBER : RED);

            Color cardBg = _loading ? AMBER_DIM
                         : (_result == null ? CARD
                         : _result.IsVerified ? GREEN_DIM
                         : _result.IsError ? AMBER_DIM : RED_DIM);

            DrawRoundRect(g, rect, 10, cardBg, accent);

            // Dot
            g.FillEllipse(new SolidBrush(accent), rect.X + 12, rect.Y + 16, 10, 10);

            // Tekst
            string statusTxt = _loading
                ? string.Format(Strings.StatusVerifying, _domain)
                : _result == null ? Strings.StatusWaiting
                : _result.IsVerified ? "\u2713 " + _result.DisplayOrg + " \u2014 Geverifieerd"
                : _result.IsError ? "\u26a0 " + _result.ErrorMessage
                : "\u2717 " + _result.Domain + " \u2014 Niet in registry";

            using (var f = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            {
                var sf = new StringFormat
                {
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                };
                g.DrawString(statusTxt, f, new SolidBrush(accent),
                    new RectangleF(rect.X + 28, rect.Y, rect.Width - 36, rect.Height), sf);
            }

            return y + H;
        }

        // ── Loading ───────────────────────────────────────────────────────

        void DrawLoadingCard(Graphics g, int W, int y)
        {
            y += 10;
            var rect = new Rectangle(10, y, W - 20, 44);
            DrawRoundRect(g, rect, 10, CARD, BORDER);
            using (var f = new Font("Segoe UI", 8.5f))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("Bezig met verifi\u00ebren\u2026", f, new SolidBrush(MUTED), rect, sf);
            }
        }

        // ── Result card ───────────────────────────────────────────────────

        void DrawResultCard(Graphics g, int W, int y)
        {
            if (_result == null) return;
            bool ok = _result.IsVerified;

            y += 10;
            int pad = 14;
            int lx = 10 + pad;       // label x
            int vx = lx + 96;        // value x
            int vw = W - 20 - pad - 96 - pad; // value width
            int rowH = 26;

            // Badge pill
            DrawBadgePill(g, new Rectangle(lx, y, 80, 22), ok);
            using (var f = new Font("Segoe UI", 8f))
                g.DrawString(_domain, f, new SolidBrush(MUTED), vx, y + 4);
            y += 26;

            // Subdomain fallback melding
            if (_result != null && _result.IsSubdomainMatch)
            {
                using (var f = new Font("Segoe UI", 7f))
                    g.DrawString(string.Format(Strings.ViaParentDomain, _result.MatchedDomain),
                        f, new SolidBrush(CYAN), vx, y + 2);
                y += 18;
            }
            else if (_result != null && _result.FallbackDisabledNote)
            {
                using (var f = new Font("Segoe UI", 7f))
                    g.DrawString(Strings.FallbackDisabled,
                        f, new SolidBrush(AMBER), vx, y + 2);
                y += 18;
            }
            else
            {
                y += 6;
            }

            // Private / Public vDOMAIN chip — alleen tonen als verified
            if (ok)
            {
                bool isPrivate = _result.OrgApproved;
                Color chipBg = isPrivate ? Color.FromArgb(10, 0, 180, 120) : Color.FromArgb(10, 60, 80, 140);
                Color chipBd = isPrivate ? Color.FromArgb(0, 160, 100) : Color.FromArgb(80, 110, 180);
                Color chipTx = isPrivate ? Color.FromArgb(0, 200, 130) : Color.FromArgb(120, 150, 220);
                string chipLabel = isPrivate ? Strings.PrivateVDomain : Strings.PublicVDomain;
                var chipRect = new Rectangle(lx, y, W - 20 - pad * 2, 22);
                DrawRoundRect(g, chipRect, 6, chipBg, chipBd);
                using (var f = new Font("Segoe UI", 7.5f, FontStyle.Bold))
                {
                    var sf = new StringFormat { LineAlignment = StringAlignment.Center };
                    g.DrawString(chipLabel, f, new SolidBrush(chipTx),
                        new RectangleF(chipRect.X + 8, chipRect.Y, chipRect.Width - 8, chipRect.Height), sf);
                }
                if (isPrivate)
                {
                    string org = (_cfg != null && !string.IsNullOrEmpty(_cfg.Enterprise))
                        ? string.Format(Strings.ManagedBy, _cfg.Enterprise)
                        : Strings.ManagedByOrg;
                    using (var f = new Font("Segoe UI", 7f))
                        g.DrawString(org, f, new SolidBrush(chipTx),
                            new RectangleF(chipRect.X + 8, chipRect.Y + 24, chipRect.Width, 16));
                    y += chipRect.Height + 22;
                }
                else
                {
                    y += chipRect.Height + 6;
                }
            }

            // Divider
            g.DrawLine(new Pen(BORDER, 1), lx, y, W - 10 - pad, y);
            y += 10;

            // Rijen
            (string k, string v, Color c)[] rows =
            {
                (Strings.RowOrganisation,  _result.OrgName.Length   > 0 ? _result.OrgName   : "\u2014", WHITE),
                (Strings.RowCategory,    _result.Category.Length  > 0 ? _result.Category  : "\u2014", WHITE),
                (Strings.RowTrusted,      _result.Trusted ? "Ja \u2713" : "Nee \u2717",
                                 _result.Trusted ? GREEN : RED),
                (Strings.RowDns, _result.DnsActive ? "Actief \u2713" : "\u2014",
                                 _result.DnsActive ? GREEN : MUTED),
                (Strings.RowBlockchain,   _result.AnchorState == "confirmed"
                                     ? "Confirmed \u2713 (" + _result.AnchorChain + ")"
                                     : _result.AnchorState.Length > 0 ? _result.AnchorState : "\u2014",
                                 _result.AnchorState == "confirmed" ? GREEN : MUTED),
            };

            using (var fKey = new Font("Segoe UI", 7.5f))
            using (var fVal = new Font("Segoe UI", 8f, FontStyle.Bold))
            {
                foreach (var row in rows)
                {
                    g.DrawString(row.k, fKey, new SolidBrush(MUTED), lx, y + 4);
                    g.DrawString(row.v, fVal, new SolidBrush(row.c), vx, y + 4);
                    y += rowH;
                }
            }

            y += 4;
            g.DrawLine(new Pen(BORDER, 1), lx, y, W - 10 - pad, y);
            y += 10;

            // Trust score
            using (var fKey = new Font("Segoe UI", 7.5f))
            using (var fVal = new Font("Segoe UI", 8f, FontStyle.Bold))
            {
                g.DrawString(Strings.RowTrustScore, fKey, new SolidBrush(MUTED), lx, y + 4);

                if (_result.TrustScore > 0)
                {
                    Color sc = _result.TrustScore >= 80 ? GREEN
                             : _result.TrustScore >= 50 ? AMBER : RED;

                    bool nearPerfect  = _result.TrustScore >= 95;
                    string scoreTxt   = nearPerfect ? "\u007e 100" : _result.TrustScore + " / 100";
                    int    displayFill = nearPerfect ? 100 : _result.TrustScore;

                    g.DrawString(scoreTxt, fVal, new SolidBrush(sc), vx, y + 4);
                    y += rowH;

                    // Score balk
                    int bw = W - 20 - pad * 2;
                    var trackRect = new Rectangle(lx, y, bw, 5);
                    DrawRoundRect(g, trackRect, 3, BORDER, Color.Transparent);
                    int fillW = (int)(bw * displayFill / 100.0);
                    if (fillW > 0)
                    {
                        using (var br = new LinearGradientBrush(
                            new Point(lx, y), new Point(lx + fillW, y), CYAN, sc))
                            g.FillRectangle(br, new Rectangle(lx, y, fillW, 5));
                    }
                    y += 14;
                }
                else
                {
                    g.DrawString("\u2014", fVal, new SolidBrush(MUTED), vx, y + 4);
                    y += rowH;
                }
            }

            // QR Code sectie
            if (_qrBitmap != null)
            {
                y += 8;
                int qrSize = System.Math.Min(_qrBitmap.Width, W - 40);
                int qrX = (W - qrSize) / 2;

                // Kader
                var qrRect = new Rectangle(lx, y, W - 20 - pad * 2, qrSize + 24);
                DrawRoundRect(g, qrRect, 8, Color.FromArgb(17, 21, 40), BORDER);

                // Titel
                using (var f = new Font("Segoe UI", 7.5f))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center };
                    g.DrawString("QR CODE", f, new SolidBrush(MUTED),
                        new RectangleF(qrRect.X, qrRect.Y + 5, qrRect.Width, 14), sf);
                }

                // QR bitmap gecentreerd
                g.DrawImage(_qrBitmap, new Rectangle(qrX, y + 18, qrSize, qrSize));
                y += qrSize + 24 + 6;
            }

            // Blockchain link
            if (_result.IsBlockchain && _result.AnchorExplorer.Length > 0)
            {
                y += 6;
                int txLen = Math.Min(20, _result.AnchorTxid.Length);
                string txText = "\u26d3  " + _result.AnchorChain + "  \u00b7  "
                              + _result.AnchorTxid.Substring(0, txLen) + "\u2026";
                var chainRect = new Rectangle(lx, y, W - 20 - pad * 2, 24);
                DrawRoundRect(g, chainRect, 6, Color.FromArgb(10, 0, 186, 219),
                    Color.FromArgb(60, 0, 186, 219));
                _chainRect = chainRect;
                _chainUrl = _result.AnchorExplorer;
                using (var f = new Font("Consolas", 7.5f))
                {
                    var sf = new StringFormat
                    {
                        LineAlignment = StringAlignment.Center,
                        Trimming = StringTrimming.EllipsisCharacter,
                        FormatFlags = StringFormatFlags.NoWrap
                    };
                    g.DrawString(txText, f, new SolidBrush(CYAN), chainRect, sf);
                }
                y += 30;
            }
            else
            {
                _chainRect = Rectangle.Empty;
                _chainUrl = "";
            }

            // vDOMAIN Notary link - alleen bij organization_approved = false
            if (_result != null && !_result.OrgApproved && !_result.IsError && _result.Trusted)
            {
                y += 6;
                string notaryDomain = _result.MatchedDomain.Length > 0
                    ? _result.MatchedDomain : _result.Domain;
                _notaryUrl = "https://esreal.org/vdomain?domain=" + notaryDomain;
                _notaryRect = new Rectangle(lx, y, W - 20 - pad * 2, 24);
                DrawRoundRect(g, _notaryRect, 6,
                    Color.FromArgb(10, 80, 110, 180),
                    Color.FromArgb(60, 80, 130, 220));
                using (var f = new Font("Segoe UI", 7.5f))
                {
                    var sf = new StringFormat
                    {
                        LineAlignment = StringAlignment.Center,
                        Trimming = StringTrimming.EllipsisCharacter,
                        FormatFlags = StringFormatFlags.NoWrap
                    };
                    g.DrawString("Open EsReal Public Notary",
                        f, new SolidBrush(Color.FromArgb(120, 160, 240)), _notaryRect, sf);
                }
                y += 30;
            }
            else
            {
                _notaryRect = Rectangle.Empty;
                _notaryUrl = "";
            }

            // Enterprise request box: alleen in enterprise modus, domein niet verified, geen fout
            if (_cfg != null && _cfg.IsEnterpriseMode
                && !_result.IsVerified && !_result.IsError)
            {
                y += 8;
                DrawRequestBox(g, W, y, pad, lx);
            }
            else
            {
                _requestRect = Rectangle.Empty;
                _requestState = _requestState == RequestState.Hidden
                    ? RequestState.Hidden : _requestState;
            }
        }

        // ── Request box ───────────────────────────────────────────────────

        void DrawRequestBox(Graphics g, int W, int y, int pad, int lx)
        {
            int bw = W - 20 - pad * 2;
            var boxRect = new Rectangle(lx, y, bw, 54);

            // Achtergrond van de box
            DrawRoundRect(g, boxRect, 8,
                Color.FromArgb(10, 0, 186, 219),
                Color.FromArgb(50, 0, 186, 219));

            // Knop (idle / sending / ok / error)
            int btnH = 26;
            var btnRect = new Rectangle(lx + 6, y + 6, bw - 12, btnH);

            switch (_requestState)
            {
                case RequestState.Hidden:
                    _requestState = RequestState.Idle;
                    DrawRoundRect(g, btnRect, 6,
                        Color.FromArgb(40, 0, 186, 219),
                        Color.FromArgb(100, 0, 186, 219));
                    _requestRect = btnRect;
                    using (var f = new Font("Segoe UI", 8f, FontStyle.Bold))
                    {
                        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString(Strings.RequestBtn, f, new SolidBrush(CYAN), btnRect, sf);
                    }
                    break;

                case RequestState.Idle:
                    DrawRoundRect(g, btnRect, 6,
                        Color.FromArgb(40, 0, 186, 219),
                        Color.FromArgb(100, 0, 186, 219));
                    _requestRect = btnRect;
                    using (var f = new Font("Segoe UI", 8f, FontStyle.Bold))
                    {
                        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString(Strings.RequestBtn, f, new SolidBrush(CYAN), btnRect, sf);
                    }
                    break;

                case RequestState.Sending:
                    DrawRoundRect(g, btnRect, 6,
                        Color.FromArgb(20, 0, 186, 219),
                        Color.FromArgb(50, 0, 186, 219));
                    _requestRect = Rectangle.Empty; // niet klikbaar tijdens verzenden
                    using (var f = new Font("Segoe UI", 8f))
                    {
                        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString(Strings.RequestSending, f, new SolidBrush(MUTED), btnRect, sf);
                    }
                    break;

                case RequestState.Ok:
                    _requestRect = Rectangle.Empty;
                    using (var f = new Font("Segoe UI", 8f, FontStyle.Bold))
                    {
                        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString("\u2713  " + Strings.RequestOk, f, new SolidBrush(GREEN),
                            new RectangleF(lx + 6, y + 6, bw - 12, boxRect.Height - 12), sf);
                    }
                    break;

                case RequestState.Error:
                    _requestRect = btnRect; // opnieuw proberen is toegelaten
                    DrawRoundRect(g, btnRect, 6,
                        Color.FromArgb(30, 248, 72, 72),
                        Color.FromArgb(80, 248, 72, 72));
                    using (var f = new Font("Segoe UI", 7.5f))
                    {
                        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center,
                            Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                        string errTxt = "\u26a0  " + Strings.RequestErr
                            + (string.IsNullOrEmpty(_requestErrorMsg) ? "" : ": " + _requestErrorMsg);
                        g.DrawString(errTxt, f, new SolidBrush(RED), btnRect, sf);
                    }
                    break;
            }
        }

        // ── Badge pill ────────────────────────────────────────────────────

        void DrawBadgePill(Graphics g, Rectangle r, bool verified)
        {
            Color bg = verified ? Color.FromArgb(20, 50, 28) : Color.FromArgb(50, 16, 16);
            Color bd = verified ? GREEN : RED;
            Color tx = verified ? GREEN : RED;
            string lbl = verified ? "\u2713  EsReal" : "\u2717  ?";
            DrawRoundRect(g, r, r.Height / 2, bg, bd);
            using (var f = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(lbl, f, new SolidBrush(tx), r, sf);
            }
        }

        // ── Empty ─────────────────────────────────────────────────────────

        int DrawFreeMailBanner(Graphics g, int W, int y)
        {
            y += 6;
            int H = 52;
            var rect = new Rectangle(10, y, W - 20, H);
            DrawRoundRect(g, rect, 8, Color.FromArgb(45, 30, 8), Color.FromArgb(220, 120, 20));
            using (var fIcon = new Font("Segoe UI", 12f))
            {
                var sf = new StringFormat { LineAlignment = StringAlignment.Center };
                g.DrawString("⚠", fIcon, new SolidBrush(AMBER),
                    new RectangleF(rect.X + 10, rect.Y + 4, 24, H - 8), sf);
            }
            using (var fBold = new Font("Segoe UI", 8f, FontStyle.Bold))
                g.DrawString(Strings.FreeMailTitle, fBold, new SolidBrush(AMBER),
                    rect.X + 36, rect.Y + 8);
            using (var fNorm = new Font("Segoe UI", 7.5f))
                g.DrawString(Strings.FreeMailBody,
                    fNorm, new SolidBrush(Color.FromArgb(210, 160, 80)),
                    new RectangleF(rect.X + 36, rect.Y + 24, rect.Width - 46, H - 24));
            return y + H + 4;
        }

        void DrawEmpty(Graphics g, int W, int y)
        {
            y += 40;
            using (var f = new Font("Segoe UI", 9f))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                var r = new RectangleF(20, y, W - 40, 60);
                g.DrawString(Strings.EmptyHint,
                    f, new SolidBrush(MUTED), r, sf);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────

        static void DrawRoundRect(Graphics g, Rectangle r, int radius, Color fill, Color border)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            var path = RoundedPath(r, radius);
            if (fill != Color.Transparent)
                g.FillPath(new SolidBrush(fill), path);
            if (border != Color.Transparent)
                g.DrawPath(new Pen(border, 1f), path);
        }

        static GraphicsPath RoundedPath(Rectangle r, int radius)
        {
            int d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // ── Klik afhandeling ──────────────────────────────────────────────

        Rectangle _refreshRect = Rectangle.Empty;
        Rectangle _chainRect = Rectangle.Empty;
        string _chainUrl = "";
        Rectangle _notaryRect = Rectangle.Empty;
        string _notaryUrl = "";
        Rectangle _requestRect = Rectangle.Empty;

        // States voor de request knop
        enum RequestState { Hidden, Idle, Sending, Ok, Error }
        RequestState _requestState = RequestState.Hidden;
        string _requestErrorMsg = "";

        void OnPanelClick(object sender, EventArgs e)
        {
            var pt = PointToClient(Cursor.Position);
            if (_refreshRect != Rectangle.Empty && _refreshRect.Contains(pt))
            {
                RefreshRequested?.Invoke(this, EventArgs.Empty);
                return;
            }
            if (_chainRect != Rectangle.Empty && _chainRect.Contains(pt)
                && !string.IsNullOrEmpty(_chainUrl))
            {
                try
                {
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(_chainUrl)
                        { UseShellExecute = true });
                }
                catch { }
                return;
            }
            if (_notaryRect != Rectangle.Empty && _notaryRect.Contains(pt)
                && !string.IsNullOrEmpty(_notaryUrl))
            {
                try
                {
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(_notaryUrl)
                        { UseShellExecute = true });
                }
                catch { }
                return;
            }
            if (_requestRect != Rectangle.Empty && _requestRect.Contains(pt)
                && (_requestState == RequestState.Idle || _requestState == RequestState.Error))
            {
                SendDomainRequest();
            }
        }

        void SendDomainRequest()
        {
            if (_api == null || _result == null) return;

            string domain = !string.IsNullOrEmpty(_result.MatchedDomain)
                ? _result.MatchedDomain
                : _result.Domain;

            if (string.IsNullOrEmpty(domain)) return;

            _requestState = RequestState.Sending;
            _requestErrorMsg = "";
            Invalidate();

            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    await _api.RequestDomainAsync(domain).ConfigureAwait(false);
                    if (InvokeRequired)
                        Invoke(new SysAction(() => { _requestState = RequestState.Ok; Invalidate(); }));
                    else { _requestState = RequestState.Ok; Invalidate(); }
                }
                catch (System.Exception ex)
                {
                    string msg = ex.Message;
                    if (InvokeRequired)
                        Invoke(new SysAction(() => { _requestState = RequestState.Error; _requestErrorMsg = msg; Invalidate(); }));
                    else { _requestState = RequestState.Error; _requestErrorMsg = msg; Invalidate(); }
                }
            });
        }

        void OnPanelMouseMove(object sender, MouseEventArgs e)
        {
            bool onRefresh  = _refreshRect  != Rectangle.Empty && _refreshRect.Contains(e.Location);
            bool onChain    = _chainRect    != Rectangle.Empty && _chainRect.Contains(e.Location);
            bool onNotary   = _notaryRect   != Rectangle.Empty && _notaryRect.Contains(e.Location);
            bool onRequest  = _requestRect  != Rectangle.Empty && _requestRect.Contains(e.Location);
            Cursor = (onRefresh || onChain || onNotary || onRequest) ? Cursors.Hand : Cursors.Default;
        }
    }
}
