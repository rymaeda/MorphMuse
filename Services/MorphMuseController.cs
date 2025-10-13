using CamBam.CAD;
using CamBam.Geom;
using CamBam.UI;
using MorphMuse;
using MorphMuse.Services;
using PluginSettings;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Linq; // Add this using directive at the top with other usings

// Main controller for the MorphMuse plugin, responsible for generating morph surfaces.
public class MorphMuseController
{
    private readonly CamBamUI _ui; // Reference to the main CamBam UI.
    private Form1 _formInstance;   // Instance of the fallback form.

    // Constructor receives the CamBam UI.
    public MorphMuseController(CamBamUI ui)
    {
        _ui = ui;
    }

    // Main execution method for the plugin.
    public void Execute()
    {
        // Validate if the selection contains one open and one closed polyline.
        if (!PolylineManager.ValidateSelection(out PolylineManager selectionManager))
        {
            ShowFallbackForm(); // Show fallback form if selection is invalid.
            return;
        }

        // Prepare and simplify the selected closed curves.
        var simplifiedClosedCurves = PrepareClosedCurves(selectionManager);
        if (simplifiedClosedCurves.Count < 2)
            return; // Ensure there are at least two curves to generate the surface.

        // Save the current active layer name before creating a new layer for the surface.
        string originalLayerName = _ui.ActiveView.CADFile.ActiveLayerName;
        string surfaceLayerName = CreateUniqueLayer("MorphSurface");
        Layer layer = _ui.ActiveView.CADFile.Layers[surfaceLayerName];
        layer.Color = Color.DeepSkyBlue; // Set the color of the new layer.

        // Structures to store the final surface points and faces.
        Point3FArray finalSurfacePoints = new Point3FArray();
        Dictionary<Point3F, int> finalPointIndex = new Dictionary<Point3F, int>();
        List<TriangleFace> finalSurfaceFaces = new List<TriangleFace>();

        // Generate the lateral surface between the simplified closed curves.
        SurfaceBuilderCopilot.GenerateLateralSurface(simplifiedClosedCurves, finalSurfacePoints, finalPointIndex, finalSurfaceFaces);

        // Generate the cap surface using the topmost curve.
        List<Point3F> topmostSimplifiedCurve = simplifiedClosedCurves[simplifiedClosedCurves.Count - 1];
        Point3F topCapCenter = GetCentroid(topmostSimplifiedCurve);
        SurfaceBuilderCopilot.GenerateCapSurface(topmostSimplifiedCurve, topCapCenter, finalSurfacePoints, finalPointIndex, finalSurfaceFaces);

        // Create the surface entity and add it to the CAD file.
        Surface surfaceEntity = new Surface
        {
            Points = finalSurfacePoints,
            Faces = finalSurfaceFaces.ToArray()
        };
        CamBam.ThisApplication.AddLogMessage($"Number of unique vertices: {finalPointIndex.Count}");
        _ui.ActiveView.CADFile.Add(surfaceEntity);

        // Restore the original layer and update the view.
        _ui.ActiveView.CADFile.SetActiveLayer(originalLayerName);
        _ui.ActiveView.ZoomToFit();
        _ui.ActiveView.RefreshView();
    }

    // Prepares and simplifies the selected closed curves.
    private List<List<Point3F>> PrepareClosedCurves(PolylineManager selectionManager)
    {
        var settingsManager = new SettingsManager(); // Settings manager instance.
        var units = SettingsManager.GetUnits();       // Get current drawing units.

        // Use the closed polyline as a guide curve if available.
        Polyline guideCurve = selectionManager.ClosedPoly != null ? selectionManager.ClosedPoly : null;
        var adaptiveParams = guideCurve != null
            ? settingsManager.GetAdaptiveParametersFromGuideCurve(guideCurve)
            : settingsManager.GetDefaultAdaptiveParameters(); // Use centralized method for default values.

        // Convert tolerance and sampling step to current units.
        double dpTolerance = SettingsManager.ConvertFromMillimeters(adaptiveParams.DouglasPeuckerTolerance, units);
        double samplingStep = SettingsManager.ConvertFromMillimeters(adaptiveParams.SamplingStep, units);

        // Process the open curve for simplification.
        var openCurveProcessor = new OpenPolylineProcessor(
            selectionManager.OpenPoly,
            samplingStep,
            dpTolerance
        );

        //
        // Create set of closed curves according to the generatrix.
        var orderedClosedCurves = LayerGenerator.GenerateContoursByGeratrizOrder(
            selectionManager.ClosedPoly,
            openCurveProcessor.SimplifiedPoints
        );

        // Generates the sampled data points defined by the curves.
        var sampledClosedCurves = CurveSampler.GenerateSampledPointsFromContours(
            orderedClosedCurves,
            openCurveProcessor.SimplifiedPoints,
            samplingStep,
            dpTolerance
        );

        // Simplify all sampled curves using Douglas-Peucker algorithm.
        return SimplifyAll(sampledClosedCurves, dpTolerance);
    }

    // Simplifies all curves using the Douglas-Peucker algorithm.
    private List<List<Point3F>> SimplifyAll(List<List<Point3F>> curves, double tolerance)
    {
        var result = new List<List<Point3F>>();
        foreach (var curve in curves)
            result.Add(PolylineSimplifier.SimplifyDouglasPeucker(curve, tolerance));
        return result;
    }

    // Calculates the centroid of a polygon (area centroid) given a list of points.
    private Point3F GetCentroid(List<Point3F> points)
    {
        if (points == null || points.Count < 3)
            return new Point3F(0, 0, 0);

        // Assume all Z are equal (planar polygon)
        double z0 = points[0].Z;

        double area = 0;
        double cx = 0;
        double cy = 0;

        int count = points.Count;
        for (int i = 0; i < count; i++)
        {
            var p0 = points[i];
            var p1 = points[(i + 1) % count];

            double cross = p0.X * p1.Y - p1.X * p0.Y;
            area += cross;
            cx += (p0.X + p1.X) * cross;
            cy += (p0.Y + p1.Y) * cross;
        }

        area *= 0.5;
        if (Math.Abs(area) < 1e-8)
            return new Point3F(0, 0, 0);

        cx /= (6 * area);
        cy /= (6 * area);

        return new Point3F(cx, cy, z0);
    }

    // Creates a unique layer for the surface, avoiding duplicate names.
    private string CreateUniqueLayer(string baseName)
    {
        int index = 1;
        string layerName;
        var cadFile = _ui.ActiveView.CADFile;

        do
        {
            layerName = $"{baseName}{index:D2}";
            index++;
        }
        while (cadFile.HasLayer(layerName));

        cadFile.CreateLayer(layerName);
        cadFile.SetActiveLayer(layerName);
        return layerName;
    }

    // Shows the fallback form if the selection is invalid.
    private void ShowFallbackForm()
    {
        if (_formInstance == null || _formInstance.IsDisposed)
        {
            _formInstance = new Form1();
            _formInstance.StartPosition = FormStartPosition.CenterParent;
            _formInstance.Show(Form.ActiveForm);
        }
        else
        {
            _formInstance.BringToFront();
        }
    }
}