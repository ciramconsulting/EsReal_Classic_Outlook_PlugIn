using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows;
using Microsoft.Office.Tools.Ribbon;

namespace EsRealOutlookAddin
{
    [System.ComponentModel.ToolboxItem(false)]
    public partial class EsRealRibbon : RibbonBase
    {
        public EsRealRibbon()
            : base(Globals.Factory.GetRibbonFactory())
        {
            this.InitializeComponent();
        }

        private void EsRealRibbon_Load(object sender, RibbonUIEventArgs e)
        {
        }

        private void btnVerify_Click(object sender, RibbonControlEventArgs e)
        {
            Globals.ThisAddIn.ToggleTaskPane();
        }

        // ── Laad embedded icon ────────────────────────────────────────────

        private static Bitmap LoadIcon(string resourceName)
        {
            try
            {
                var asm    = Assembly.GetExecutingAssembly();
                var stream = asm.GetManifestResourceStream(
                    "EsRealOutlookAddin." + resourceName);
                if (stream != null)
                    return new Bitmap(stream);
            }
            catch { }
            return null;
        }

        // ── Designer code ─────────────────────────────────────────────────

        private RibbonTab    tabEsReal;
        private RibbonGroup  grpEsReal;
        private RibbonButton btnVerify;

        private void InitializeComponent()
        {
            this.tabEsReal = this.Factory.CreateRibbonTab();
            this.grpEsReal = this.Factory.CreateRibbonGroup();
            this.btnVerify = this.Factory.CreateRibbonButton();

            this.tabEsReal.SuspendLayout();
            this.grpEsReal.SuspendLayout();

            // tabEsReal
            this.tabEsReal.ControlId.ControlIdType = RibbonControlIdType.Office;
            this.tabEsReal.Groups.Add(this.grpEsReal);
            this.tabEsReal.Label = "EsReal";

            // grpEsReal
            this.grpEsReal.Items.Add(this.btnVerify);
            this.grpEsReal.Label = "EsReal";

            
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            string versionString = version.ToString();

            // btnVerify - eigen EsReal icoon, fallback op SecuritySettings
            this.btnVerify.Label         = "EsReal Plug-In";
            this.btnVerify.ScreenTip     = "EsReal " + versionString;
            this.btnVerify.SuperTip      = "EsReal - Trust becomes verifiable";
            this.btnVerify.ControlSize   = Microsoft.Office.Core.RibbonControlSize.RibbonControlSizeLarge;
            this.btnVerify.ShowImage     = true;
            this.btnVerify.Click        += this.btnVerify_Click;

            // Laad eigen icoon
            var icon = LoadIcon("EsRealIcon32.png");
            if (icon != null)
            {
                this.btnVerify.Image     = icon;
                this.btnVerify.ShowImage = true;
            }
            else
            {
                // Fallback: standaard Office icoon
                this.btnVerify.OfficeImageId = "SecuritySettings";
            }

            this.grpEsReal.ResumeLayout(false);
            this.grpEsReal.PerformLayout();
            this.tabEsReal.ResumeLayout(false);
            this.tabEsReal.PerformLayout();

            this.Name       = "EsRealRibbon";
            this.RibbonType = "Microsoft.Outlook.Explorer";
            this.Tabs.Add(this.tabEsReal);
            this.Load      += this.EsRealRibbon_Load;
            this.ResumeLayout(false);
        }
    }
}
