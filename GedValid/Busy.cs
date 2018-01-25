using System.Windows.Forms;

namespace GedValid
{
    public partial class Busy : Form
    {
        public Busy()
        {
            InitializeComponent();

            pictureBox1.Image = Properties.Resources.helix;
        }

        public string Msg
        {
            set { label1.Text = value; }
        }
    }
}
