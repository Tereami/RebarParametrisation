#region License
/*Данный код опубликован под лицензией Creative Commons Attribution-ShareAlike.
Разрешено использовать, распространять, изменять и брать данный код за основу для производных в коммерческих и
некоммерческих целях, при условии указания авторства и если производные лицензируются на тех же условиях.
Код поставляется "как есть". Автор не несет ответственности за возможные последствия использования.
Зуев Александр, 2020, все права защищены.
This code is listed under the Creative Commons Attribution-ShareAlike license.
You may use, redistribute, remix, tweak, and build upon this work non-commercially and commercially,
as long as you credit the author by linking back and license your new creations under the same terms.
This code is provided 'as is'. Author disclaims any implied warranty.
Zuev Aleksandr, 2020, all rigths reserved.*/
#endregion
#region Usings
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
#endregion

namespace RebarParametrisation
{
    public partial class FormSelectParams : Form
    {
        public FormSelectParams()
        {
            InitializeComponent();
            chkboxHostid.Checked = Settings.UseHostId;
            chkboxUniqId.Checked = Settings.UseUniqueHostId;
            chkboxHostMark.Checked = Settings.UseHostMark;
            chkboxUseThickness.Checked = Settings.UseHostThickness;

            chkboxRebarWeight.Checked = Settings.UseRebarWeight;
            chkboxRebarLength.Checked = Settings.UseRebarLength;
            chkboxRebarDiameter.Checked = Settings.UseRebarDiameter;

            cmbboxConcreteClass.Text = Settings.DefaultConcreteClass.ToString("F0");

            switch (Settings.LinkFilesSetting)
            {
                case Settings.ProcessedLinkFiles.NoLinks:
                    radioNoLinks.Checked = true;
                    break;
                case Settings.ProcessedLinkFiles.OnlyLibs:
                    radioOnlyLibs.Checked = true;
                    break;
                case Settings.ProcessedLinkFiles.AllKrFiles:
                    radioAllKrLinks.Checked = true;
                    break;
            }

        }

        private void buttonOk_Click(object sender, EventArgs e)
        {
            Settings.UseHostId = chkboxHostid.Checked;
            Settings.UseUniqueHostId = chkboxUniqId.Checked;
            Settings.UseHostMark = chkboxHostMark.Checked;
            Settings.UseHostThickness = chkboxUseThickness.Checked;

            Settings.UseRebarWeight = chkboxRebarWeight.Checked;
            Settings.UseRebarLength = chkboxRebarLength.Checked;
            Settings.UseRebarDiameter = chkboxRebarDiameter.Checked;

            double c = double.Parse(cmbboxConcreteClass.Text);
            Settings.DefaultConcreteClass = c;


            if (radioNoLinks.Checked == true) Settings.LinkFilesSetting = Settings.ProcessedLinkFiles.NoLinks;
            if (radioOnlyLibs.Checked == true) Settings.LinkFilesSetting = Settings.ProcessedLinkFiles.OnlyLibs;
            if (radioAllKrLinks.Checked == true) Settings.LinkFilesSetting = Settings.ProcessedLinkFiles.AllKrFiles;

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void chkboxHostid_CheckedChanged(object sender, EventArgs e)
        {
            textBoxHostId.Enabled = chkboxHostid.Checked;
        }

        private void chkboxUniqId_CheckedChanged(object sender, EventArgs e)
        {
            textBoxHostUniqId.Enabled = chkboxUniqId.Checked;
        }

        private void chkboxRebarWeight_CheckedChanged(object sender, EventArgs e)
        {
            textBoxWeight.Enabled = chkboxRebarWeight.Checked;
        }

        private void chkboxRebarLength_CheckedChanged(object sender, EventArgs e)
        {
            textBoxLength.Enabled = chkboxRebarLength.Checked;
        }

        private void chkboxRebarDiameter_CheckedChanged(object sender, EventArgs e)
        {
            textBoxDiameter.Enabled = chkboxRebarDiameter.Checked;
        }

        private void chkboxHostMark_CheckedChanged(object sender, EventArgs e)
        {
            textBoxHostMark.Enabled = chkboxHostMark.Checked;
        }

        private void chkboxUseThickness_CheckedChanged(object sender, EventArgs e)
        {
            textBoxHostThickness.Enabled = chkboxUseThickness.Checked;
        }
    }
}
