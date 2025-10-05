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

// Main controller for the MorphMuse plugin, responsible for generating morph surfaces.

namespace MorphMuse.Services
{
    public enum SurfaceMode
    {
        GeneratrizClosed,
        GeneratrizOpen
    }
    public class MorphMuseController
    {
        private readonly CamBamUI _ui; // Reference to the main CamBam UI.
        private readonly SurfaceMode _mode;
        private Form1 _formInstance;   // Instance of the fallback form.
        // Constructor receives the CamBam UI.
        public MorphMuseController(CamBamUI ui)
        {
            _ui = ui;
            //bool result = PolylineManager.TryCreateFromSelection(out PolylineManager selectionManager, out var detectedMode);
            //if (!result)
            //{
            //    // handle error
            //    return;
            //}
            //_mode = (SurfaceMode)detectedMode; // Ensure mode matches the selection
        }

        // Main execution method for the plugin.
        public void Execute()
        {

            switch (_mode)
            {
                case SurfaceMode.GeneratrizClosed:
                    ExecuteWithOpenAndClosedPolylines();
                    break;
                case SurfaceMode.GeneratrizOpen:
                    ExecuteWithTwoOpenPolylines();
                    break;
            }
        }
        public void ExecuteWithOpenAndClosedPolylines()
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
            Dictionary<Point3F, int> finalPointIndex = new Dictionary<Point3F,
            int>();
            List<TriangleFace> finalSurfaceFaces = new List<TriangleFace>();
            // Generate the lateral surface between the simplified closed curves.
            SurfaceBuilderCopilot.GenerateLateralSurface(simplifiedClosedCurves,
            finalSurfacePoints, finalPointIndex, finalSurfaceFaces);
            // Generate the cap surface using the topmost curve.
            List<Point3F> topmostSimplifiedCurve =
            simplifiedClosedCurves[simplifiedClosedCurves.Count - 1];
            Point3F topCapCenter = GetCentroid(topmostSimplifiedCurve);
            SurfaceBuilderCopilot.GenerateCapSurface(topmostSimplifiedCurve,
            topCapCenter, finalSurfacePoints, finalPointIndex, finalSurfaceFaces);
            // Create the surface entity and add it to the CAD file.
            Surface surfaceEntity = new Surface
            {
                Points = finalSurfacePoints,
                Faces = finalSurfaceFaces.ToArray()
            };

            _ui.ActiveView.CADFile.Add(surfaceEntity);
            // Restore the original layer and update the view.
            _ui.ActiveView.CADFile.SetActiveLayer(originalLayerName);
            _ui.ActiveView.ZoomToFit();
            _ui.ActiveView.RefreshView();
        }

        private void ExecuteWithTwoOpenPolylines()
        {
            // Validate if the selection contains at least two open polylines.
            if (!PolylineManager.TryCreateFromSelection(out PolylineManager selectionManager, out Enum mode))
            {
                ShowFallbackForm(); // Show fallback form if selection is invalid.
                return;
            }

            // FIX: Check the count of polylines in the list, not a Points property.
            if (selectionManager.OpenPoly == null || selectionManager.OpenPoly.Count < 2)
            {
                CamBam.ThisApplication.AddLogMessage("[Copilot] Please select two open polylines.");
                return;
            }

            // Use the first as generatrix and the second as guide.
            Polyline generatrix = PolylineManager.OpenPoly;
            Polyline guide = selectionManager.OpenPoly[1];

            // Process and simplify the generatrix.
            var processor = new OpenPolylineProcessor(generatrix, 0.01, 0.01);
            var simplifiedGeneratrix = processor.SimplifiedPoints;

            // Sample the guide curve using the generatrix as reference.
            var orderedContours = new List<List<Polyline>> { new List<Polyline> { guide } };
            var sampledCurves = CurveSampler.GenerateSampledPointsFromContours(
                orderedContours,
                simplifiedGeneratrix,
                0.05,
                0.05
            );

            // Insert the generatrix as the first curve.
            sampledCurves.Insert(0, simplifiedGeneratrix);

            // Simplify all sampled curves.
            var simplifiedCurves = SimplifyAll(sampledCurves, 0.02);
            if (simplifiedCurves.Count < 2)
                return;

            // Save the current active layer name before creating a new layer for the surface.
            string originalLayerName = _ui.ActiveView.CADFile.ActiveLayerName;
            string surfaceLayerName = CreateUniqueLayer("OpenSurface");
            Layer layer = _ui.ActiveView.CADFile.Layers[surfaceLayerName];
            layer.Color = Color.DeepSkyBlue;

            // Structures to store the final surface points and faces.
            Point3FArray finalSurfacePoints = new Point3FArray();
            Dictionary<Point3F, int> finalPointIndex = new Dictionary<Point3F, int>();
            List<TriangleFace> finalSurfaceFaces = new List<TriangleFace>();

            // Generate the lateral surface between the two curves.
            SurfaceBuilderCopilot.GenerateLateralSurface(simplifiedCurves, finalSurfacePoints, finalPointIndex, finalSurfaceFaces);

            // Create the surface entity and add it to the CAD file.
            Surface surfaceEntity = new Surface
            {
                Points = finalSurfacePoints,
                Faces = finalSurfaceFaces.ToArray()
            };

            CamBam.ThisApplication.AddLogMessage($"[Copilot] Total unique vertices: {finalPointIndex.Count}");
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
            // FIX: Use the first polyline from the list, not the list itself.
            var openCurveProcessor = new OpenPolylineProcessor(
                selectionManager.OpenPoly != null && selectionManager.OpenPoly.Count > 0 ? selectionManager.OpenPoly[0] : null,
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

        // Calculates the centroid of a list of points.
        private Point3F GetCentroid(List<Point3F> points)
        {
            if (points == null || points.Count == 0)
                return new Point3F(0, 0, 0);

            float sumX = 0, sumY = 0, sumZ = 0;
            foreach (Point3F pt in points)
            {
                sumX += (float)pt.X;
                sumY += (float)pt.Y;
                sumZ += (float)pt.Z;
            }


            int count = points.Count;
            return new Point3F(sumX / count, sumY / count, sumZ / count);
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
}