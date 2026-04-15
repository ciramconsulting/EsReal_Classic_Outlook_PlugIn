using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EsRealOutlookAddin
{
    /// <summary>
    /// Kleine badge zoals in de browser plugin.
    /// Toont: checkmark EsReal (groen) / X ? (rood) / spinner (loading) / waarschuwing (error)
    /// Klik → DetailOverlay verschijnt.
    ///
    /// C# 7.x compatible - geen tuple-deconstruct in switch, geen 'using var'.
    /// </summary>
    public class BadgeControl : UserControl
    {
        // ── Design tokens ─────────────────────────────────────────────────
        private static readonly Color C_GREEN_BG = Color.FromArgb(220, 244, 228);
        private static readonly Color C_GREEN_BD = Color.FromArgb(134, 199, 150);
        private static readonly Color C_GREEN_TX = Color.FromArgb(21,  128, 61);
        private static readonly Color C_RED_BG   = Color.FromArgb(254, 226, 226);
        private static readonly Color C_RED_BD   = Color.FromArgb(252, 165, 165);
        private static readonly Color C_RED_TX   = Color.FromArgb(185, 28,  28);
        private static readonly Color C_GRAY_BD  = Color.FromArgb(203, 213, 225);
        private static readonly Color C_GRAY_TX  = Color.FromArgb(100, 116, 139);
        private static readonly Color C_AMB_BG   = Color.FromArgb(255, 243, 199);
        private static readonly Color C_AMB_BD   = Color.FromArgb(252, 196, 25);
        private static readonly Color C_AMB_TX   = Color.FromArgb(180, 83,   9);

        public enum BadgeState { Loading, Verified, NotFound, Error }

        private BadgeState   _state  = BadgeState.Loading;
        private VerifyResult _result = null;
        private Timer        _spinTimer;
        private int          _spinAngle;
        private string       _email  = "";

        public BadgeControl()
        {
            Width          = 76;
            Height         = 20;
            Cursor         = Cursors.Hand;
            DoubleBuffered = true;
            BackColor      = Color.Transparent;

            _spinTimer          = new Timer();
            _spinTimer.Interval = 40;
            _spinTimer.Tick    += (s, e) => { _spinAngle = (_spinAngle + 12) % 360; Invalidate(); };
            _spinTimer.Start();

            Click += OnBadgeClick;
        }

        // ── Public API ────────────────────────────────────────────────────

        public void SetLoading(string email)
        {
            _email  = email;
            _state  = BadgeState.Loading;
            _result = null;
            _spinTimer.Start();
            Invalidate();
        }

        public void SetResult(VerifyResult result, string email)
        {
            _email  = email;
            _result = result;
            _spinTimer.Stop();

            if (result.IsError)
                _state = BadgeState.Error;
            else if (result.IsVerified)
                _state = BadgeState.Verified;
            else
                _state = BadgeState.NotFound;

            Width = (_state == BadgeState.Verified) ? 76 : 40;
            Invalidate();
        }

        // ── Tekenen ───────────────────────────────────────────────────────

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            if (_state == BadgeState.Loading)
            {
                DrawSpinner(g);
                return;
            }

            Color bg, bd, tx;
            string icon, label;

            switch (_state)
            {
                case BadgeState.Verified:
                    bg = C_GREEN_BG; bd = C_GREEN_BD; tx = C_GREEN_TX;
                    icon = "\u2713"; label = "EsReal";
                    break;
                case BadgeState.NotFound:
                    bg = C_RED_BG; bd = C_RED_BD; tx = C_RED_TX;
                    icon = "\u2717"; label = "?";
                    break;
                case BadgeState.Error:
                    bg = C_AMB_BG; bd = C_AMB_BD; tx = C_AMB_TX;
                    icon = "\u26a0"; label = "";
                    break;
                default:
                    bg = C_GRAY_BD; bd = C_GRAY_BD; tx = C_GRAY_TX;
                    icon = ""; label = "";
                    break;
            }

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            var path = RoundRect(rect, 9);

            using (var bgBrush = new SolidBrush(bg))
                g.FillPath(bgBrush, path);

            using (var bdPen = new Pen(bd, 1f))
                g.DrawPath(bdPen, path);

            var font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            var text = (icon + " " + label).Trim();
            var sf   = new StringFormat
            {
                Alignment     = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            using (var txBrush = new SolidBrush(tx))
                g.DrawString(text, font, txBrush, new RectangleF(0, 0, Width, Height), sf);

            font.Dispose();
        }

        private void DrawSpinner(Graphics g)
        {
            float cx = Width  / 2f;
            float cy = Height / 2f;
            float r  = Math.Min(cx, cy) - 2f;

            using (var bgPen = new Pen(C_GRAY_BD, 2f))
            using (var fgPen = new Pen(C_GRAY_TX, 2f))
            {
                bgPen.StartCap = LineCap.Round;
                bgPen.EndCap   = LineCap.Round;
                fgPen.StartCap = LineCap.Round;
                fgPen.EndCap   = LineCap.Round;

                g.DrawEllipse(bgPen, cx - r, cy - r, r * 2, r * 2);
                g.DrawArc(fgPen, cx - r, cy - r, r * 2, r * 2, _spinAngle, 90);
            }
        }

        private static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            path.AddArc(r.X,                  r.Y,                   radius * 2, radius * 2, 180, 90);
            path.AddArc(r.Right - radius * 2, r.Y,                   radius * 2, radius * 2, 270, 90);
            path.AddArc(r.Right - radius * 2, r.Bottom - radius * 2, radius * 2, radius * 2,   0, 90);
            path.AddArc(r.X,                  r.Bottom - radius * 2, radius * 2, radius * 2,  90, 90);
            path.CloseFigure();
            return path;
        }

        // ── Klik → details overlay ────────────────────────────────────────

        private void OnBadgeClick(object sender, EventArgs e)
        {
            if (_result == null) return;
            var overlay = new DetailOverlay(_result, _email);
            var pt      = PointToScreen(new Point(0, Height + 2));
            overlay.StartPosition = FormStartPosition.Manual;
            overlay.Location      = pt;
            overlay.Show();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_spinTimer != null)
                {
                    _spinTimer.Dispose();
                    _spinTimer = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}
