using System;
using System.Windows.Forms;

namespace GedValid
{
    public partial class Settings : Form
    {
        public DASettings SettingsValue { get; set; }

        public Settings(DASettings settings)
        {
            SettingsValue = settings;
            InitializeComponent();

            chkIgCust.Checked = SettingsValue.IgnoreCustom;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            SettingsValue.IgnoreCustom = chkIgCust.Checked;
        }
    }
}
