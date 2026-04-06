using System.Drawing;
using System.Windows.Forms;
using OpenNetMeter.Properties;

namespace OpenNetMeter.Views
{
    public class MyColorTable : ProfessionalColorTable
    {
        public override Color MenuItemBorder
        {
            get
            {
                if (SettingsManager.Current.DarkMode)
                    return Color.FromArgb(32, 32, 32);
                else
                    return Color.FromArgb(240, 240, 240);
            }
        }
        public override Color ButtonSelectedHighlight
        {
            get
            {
                if (SettingsManager.Current.DarkMode)
                    return Color.FromArgb(64, 64, 64);
                else
                    return Color.FromArgb(220, 220, 220);
            }
        }

        public override Color ToolStripDropDownBackground
        {
            get
            {
                if (SettingsManager.Current.DarkMode)
                    return Color.FromArgb(32, 32, 32);
                else
                    return Color.FromArgb(240, 240, 240);
            }
        }
        public override Color ImageMarginGradientBegin
        {
            get
            {
                if (SettingsManager.Current.DarkMode)
                    return Color.FromArgb(32, 32, 32);
                else
                    return Color.FromArgb(240, 240, 240);
            }
        }
        public override Color ImageMarginGradientMiddle
        {
            get
            {
                if (SettingsManager.Current.DarkMode)
                    return Color.FromArgb(32, 32, 32);
                else
                    return Color.FromArgb(240, 240, 240);
            }
        }
        public override Color ImageMarginGradientEnd
        {
            get
            {
                if (SettingsManager.Current.DarkMode)
                    return Color.FromArgb(32, 32, 32);
                else
                    return Color.FromArgb(240, 240, 240);
            }
        }

    }
    public class CustomSystemTray : ToolStripProfessionalRenderer
    {
        public CustomSystemTray() : base(new MyColorTable()) { }

    }
}
