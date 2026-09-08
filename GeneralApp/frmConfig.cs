using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BAFL_Campaign_App
{
    public partial class frmConfig : Form
    {

        public frmConfig()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            DialogResult a = MessageBox.Show("Are you sure you wish to exit?", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (a == DialogResult.Yes)
                this.Close();
            else return;
        }

        private void frmConfig_Load(object sender, EventArgs e)
        {
            try
            {
                string File = Application.StartupPath + "\\" + "Config.xml";
                if (System.IO.File.Exists(File))
                {
                    StreamReader objReader = new StreamReader(File);
                    txtConfig.Text = objReader.ReadToEnd();
                    objReader.Close();
                }
                else
                {
                    MessageBox.Show("No Configuration file found...", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                FileStream objFS = new FileStream(Application.StartupPath + "\\" + "Config.xml", FileMode.Create, FileAccess.Write);
                StreamWriter objSR = new StreamWriter(objFS);
                objSR.WriteLine(txtConfig.Text);
                objSR.Close();
                MessageBox.Show("Configuration saved successfully!!!", "File Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                this.Close();
            }

        }
    }

}
