using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using CamBam.CAD;
using CamBam.Geom;

namespace MorphMuse.Services
{
    public class CurveSelectionDialog : Form
    {
        private RadioButton rbCurve1AsRail;
        private RadioButton rbCurve2AsRail;
        private Panel pnlPreview1;
        private Panel pnlPreview2;
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
            this.Width = 340;
            this.Height = 310;
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
                Width = 300,
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
                Left = 20,
                Top = 75,
                Width = 200,
                Height = 30,
                Checked = true,
                AutoSize = false
            };

            // Preview for curve 1
            pnlPreview1 = new Panel()
            {
                Left = 230,
                Top = 60,
                Width = 90,
                Height = 60,
                BorderStyle = BorderStyle.FixedSingle
            };
            pnlPreview1.Paint += (s, e) => DrawCurvePreview(e.Graphics, pnlPreview1.ClientSize, _openCurves[0].Polyline, _openCurves[1].Polyline);

            // Curve 2 radio button with identification
            rbCurve2AsRail = new RadioButton()
            {
                Text = curve2Id,
                Left = 20,
                Top = 150,
                Width = 200,
                Height = 30,
                AutoSize = false
            };

            // Preview for curve 2
            pnlPreview2 = new Panel()
            {
                Left = 230,
                Top = 135,
                Width = 90,
                Height = 60,
                BorderStyle = BorderStyle.FixedSingle
            };
            pnlPreview2.Paint += (s, e) => DrawCurvePreview(e.Graphics, pnlPreview2.ClientSize, _openCurves[1].Polyline, _openCurves[0].Polyline);

            // Info text
            Label lblInfo = new Label()
            {
                Text = "The other curve will be used as Form (profile definition).",
                Left = 20,
                Top = 205,
                Width = 300,
                Height = 30,
                Font = new Font(this.Font.FontFamily, this.Font.Size, FontStyle.Italic),
                ForeColor = SystemColors.GrayText,
                AutoSize = false
            };

            // OK Button
            btnOk = new Button()
            {
                Text = "OK",
                Left = 150,
                Top = 245,
                Width = 80,
                Height = 30,
                DialogResult = DialogResult.OK
            };
            btnOk.Click += BtnOk_Click;

            // Cancel Button
            btnCancel = new Button()
            {
                Text = "Cancel",
                Left = 240,
                Top = 245,
                Width = 80,
                Height = 30,
                DialogResult = DialogResult.Cancel
            };

            // Add all controls to form
            this.Controls.Add(lblTitle);
            this.Controls.Add(rbCurve1AsRail);
            this.Controls.Add(pnlPreview1);
            this.Controls.Add(rbCurve2AsRail);
            this.Controls.Add(pnlPreview2);
            this.Controls.Add(lblInfo);
            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }

        /// <summary>
        /// Draws a normalized preview of <paramref name="polyline"/>'s XY shape, fitted
        /// (with margin) inside the target panel's client area. Arcs are approximated by
        /// their control points (start/end), which is sufficient for a small thumbnail-style
        /// preview. The curve's start point is highlighted with a small blue circle (black
        /// outline) so the user can identify the curve's direction/origin at a glance.
        ///
        /// The polyline with the LARGER bounding box always fills the available preview
        /// area (as before). The polyline with the SMALLER bounding box is drawn using the
        /// SAME scale as the larger one -- so their relative sizes are visually comparable --
/// unless its bounding box would then be smaller than 20% of the larger curve's
/// bounding box, in which case an extra "boost" factor is applied (to that smaller
/// curve only) so it never shrinks below that 20% minimum relative size.
/// </summary>
private void DrawCurvePreview(Graphics g, Size clientSize, Polyline polyline, Polyline otherPolyline)
{
    g.Clear(Color.White);

    if (polyline == null || polyline.Points.Count < 2)
        return;

    var pts = polyline.Points.ToArray().Select(p => p.Point).ToList();

    double minX = pts.Min(p => p.X);
    double maxX = pts.Max(p => p.X);
    double minY = pts.Min(p => p.Y);
    double maxY = pts.Max(p => p.Y);

    double width = maxX - minX;
    double height = maxY - minY;

    if (width <= 0 && height <= 0)
        return;

    const int margin = 4;
    double availableWidth = clientSize.Width - 2 * margin;
    double availableHeight = clientSize.Height - 2 * margin;

    double ownScale = ComputeFitScale(width, height, availableWidth, availableHeight);
    if (ownScale <= 0 || double.IsInfinity(ownScale) || double.IsNaN(ownScale))
        return;

    double scale = ownScale;

    double thisSize = BoundingBoxSize(width, height);
    if (TryGetBounds(otherPolyline, out double otherWidth, out double otherHeight))
    {
        double otherScale = ComputeFitScale(otherWidth, otherHeight, availableWidth, availableHeight);
        double otherSize = BoundingBoxSize(otherWidth, otherHeight);

        // The curve with the larger bounding box keeps its own fit-to-panel scale
        // (i.e. it needs a smaller scale factor to fit). The other one (smaller
        // bounding box) adopts that same scale so relative sizes are comparable.
        bool thisIsSmaller = ownScale >= otherScale;
        if (thisIsSmaller && otherSize > 0)
        {
            double sharedScale = otherScale;

            // Enforce a minimum relative size of 40% for the smaller curve
            // compared to the larger one, boosting only this curve's scale if needed.
            double ratio = (thisSize * sharedScale) / (otherSize * otherScale);
            const double minRatio = 0.4;

            scale = ratio < minRatio
                ? sharedScale * (minRatio / ratio)
                : sharedScale;
        }
    }

    // Center the shape within the panel.
    double offsetX = margin + (availableWidth - width * scale) / 2.0;
    double offsetY = margin + (availableHeight - height * scale) / 2.0;

    PointF[] screenPoints = pts.Select(p =>
    {
        float sx = (float)(offsetX + (p.X - minX) * scale);
        // Invert Y so the preview matches CAD orientation (Y up).
        float sy = (float)(clientSize.Height - offsetY - (p.Y - minY) * scale);
        return new PointF(sx, sy);
    }).ToArray();

    using (var pen = new Pen(Color.Black, 1f))
    {
        if (screenPoints.Length >= 2)
        {
            g.DrawLines(pen, screenPoints);
        }
    }

    // Highlight the start point with a small blue circle (black outline).
    const float startPointRadius = 3f;
    PointF startPoint = screenPoints[0];
    RectangleF startPointRect = new RectangleF(
        startPoint.X - startPointRadius,
        startPoint.Y - startPointRadius,
        startPointRadius * 2f,
        startPointRadius * 2f);

    using (var startPointBrush = new SolidBrush(Color.Blue))
    using (var startPointPen = new Pen(Color.Black, 1f))
    {
        g.FillEllipse(startPointBrush, startPointRect);
        g.DrawEllipse(startPointPen, startPointRect);
    }
}

private static double ComputeFitScale(double width, double height, double availableWidth, double availableHeight)
{
    double scaleX = width > 0 ? availableWidth / width : availableHeight;
    double scaleY = height > 0 ? availableHeight / height : availableWidth;
    return Math.Min(scaleX, scaleY);
}

/// <summary>
/// A single scalar "size" measure for a bounding box (its diagonal), used to
/// compare the relative sizes of two curves' bounding boxes.
/// </summary>
private static double BoundingBoxSize(double width, double height)
{
    return Math.Sqrt(width * width + height * height);
}

private static bool TryGetBounds(Polyline polyline, out double width, out double height)
{
    width = 0;
    height = 0;

    if (polyline == null || polyline.Points.Count < 2)
        return false;

    var pts = polyline.Points.ToArray().Select(p => p.Point).ToList();
    double minX = pts.Min(p => p.X);
    double maxX = pts.Max(p => p.X);
    double minY = pts.Min(p => p.Y);
    double maxY = pts.Max(p => p.Y);

    width = maxX - minX;
    height = maxY - minY;
    return width > 0 || height > 0;
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
