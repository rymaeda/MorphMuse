using CamBam.CAD;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace MorphMuse.Services
{
    public class CurveSelectionDialog : Form
    {
        private RadioButton rbCurve1AsRail;
        private RadioButton rbCurve2AsRail;
        private Button btnOk;
        private Button btnCancel;
        private List<CurveInfo> _openCurves;

        public CurveInfo SelectedRail { get; private set; }
        public CurveInfo SelectedForm { get; private set; }

        public CurveSelectionDialog(List<CurveInfo> openCurves)
        {
            _openCurves = openCurves;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            // Configure form properties
            this.Text = "Select Rail Curve";
            this.Width = 550;
            this.Height = 280;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.AutoScaleMode = AutoScaleMode.Font;

            // Title label
            Label lblTitle = new Label()
            {
                Text = "Which curve is the Rail (path to be offset)?",
                Left = 20,
                Top = 20,
                Width = 500,
                Height = 30,
                Font = new Font(this.Font.FontFamily, this.Font.Size + 1, FontStyle.Bold),
                AutoSize = false
            };

            // Get curve identifications
            string curve1Id = _openCurves[0].GetIdentification();
            string curve2Id = _openCurves[1].GetIdentification();

            CamBam.ThisApplication.AddLogMessage($"Dialog - Curve 1: {curve1Id}");
            CamBam.ThisApplication.AddLogMessage($"Dialog - Curve 2: {curve2Id}");

            // Curve 1 radio button with identification
            rbCurve1AsRail = new RadioButton()
            {
                Text = curve1Id,
                Left = 40,
                Top = 70,
                Width = 480,
                Height = 30,
                Checked = true,
                AutoSize = false
            };

            // Curve 2 radio button with identification
            rbCurve2AsRail = new RadioButton()
            {
                Text = curve2Id,
                Left = 40,
                Top = 110,
                Width = 480,
                Height = 30,
                AutoSize = false
            };

            // Info text
            Label lblInfo = new Label()
            {
                Text = "The other curve will be used as Form (profile definition).",
                Left = 20,
                Top = 160,
                Width = 500,
                Height = 30,
                Font = new Font(this.Font.FontFamily, this.Font.Size, FontStyle.Italic),
                ForeColor = SystemColors.GrayText,
                AutoSize = false
            };

            // OK Button
            btnOk = new Button()
            {
                Text = "OK",
                Left = 350,
                Top = 210,
                Width = 80,
                Height = 30,
                DialogResult = DialogResult.OK
            };
            btnOk.Click += BtnOk_Click;

            // Cancel Button
            btnCancel = new Button()
            {
                Text = "Cancel",
                Left = 440,
                Top = 210,
                Width = 80,
                Height = 30,
                DialogResult = DialogResult.Cancel
            };

            // Add all controls to form
            this.Controls.Add(lblTitle);
            this.Controls.Add(rbCurve1AsRail);
            this.Controls.Add(rbCurve2AsRail);
            this.Controls.Add(lblInfo);
            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            // Determine which curve is rail and which is form based on radio button selection
            if (rbCurve1AsRail.Checked)
            {
                SelectedRail = _openCurves[0];
                SelectedForm = _openCurves[1];
            }
            else
            {
                SelectedRail = _openCurves[1];
                SelectedForm = _openCurves[0];
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
