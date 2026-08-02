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
using System.Linq;

public class MorphMuseController
{
    private readonly CamBamUI _ui;
    private readonly SettingsManager _settingsManager;

    public MorphMuseController(CamBamUI ui)
    {
        _ui = ui;
        _settingsManager = new SettingsManager();
    }

    public void Execute()
    {
        if (!PolylineManager.ValidateSelection(out PolylineManager selectionManager))
        {
            MessageBox.Show(
                "Invalid Selection. Please select one open and one closed polyline, or two open polylines.",
                "Invalid Selection.",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
            return;
        }

        bool isTwoOpen = selectionManager.CounterClosedP == 0 && selectionManager.CounterOpenP == 2;

        string originalLayerName = _ui.ActiveView.CADFile.ActiveLayerName;

        // Create an undo point BEFORE any layer/entity modification
        CamBamUI.MainUI.UndoBuffer.AddUndoPoint("MorphMuse Surface Generation");

        // Register the Layers collection itself so the undo system
        // can also remove the newly created layer on Undo.
        CamBamUI.MainUI.UndoBuffer.Add(_ui.ActiveView.CADFile.Layers);

        string surfaceLayerName = CreateUniqueLayer("MorphSurface");
        Layer layer = _ui.ActiveView.CADFile.Layers[surfaceLayerName];
        layer.Color = Color.DeepSkyBlue;

        // Register the layer's entities collection so added surfaces are also undoable
        CamBamUI.MainUI.UndoBuffer.Add(layer.Entities);

        Point3FArray finalSurfacePoints = new Point3FArray();
        Dictionary<Point3F, int> finalPointIndex = new Dictionary<Point3F, int>();
        List<TriangleFace> finalSurfaceFaces = new List<TriangleFace>();

        if (isTwoOpen)
        {
            // Two open curves flow: no risk of an offset splitting into multiple
            // disjoint contours per level, so the flattened (legacy) path is safe.
            var simplifiedCurves = PrepareOpenCurves(selectionManager);
            if (simplifiedCurves.Count < 2)
                return;

            SurfaceBuilderCopilot.GenerateLateralSurface(simplifiedCurves, finalSurfacePoints, finalPointIndex, finalSurfaceFaces, isClosed: false);
        }
        else
        {
            // Closed rail flow: the offset of a closed base polyline can, in rare
            // cases, still split into multiple contours near narrow/concave sections.
            // Spurious near-zero-area artifacts are already filtered out in
            // LayerGenerator, so here we only need to pick the single dominant
            // (largest perimeter) contour per level to keep the surface connected
            // and avoid generating disconnected/floating mesh fragments.
            var groupedLevels = PrepareClosedCurvesGrouped(selectionManager);
            if (groupedLevels.Count < 2)
                return;

            var dominantCurves = SelectDominantContourPerLevel(groupedLevels);
            if (dominantCurves.Count < 2)
                return;

            SurfaceBuilderCopilot.GenerateLateralSurface(dominantCurves, finalSurfacePoints, finalPointIndex, finalSurfaceFaces, isClosed: true);

            // Cap only the dominant contour of the topmost level.
            List<Point3F> topmostCurve = dominantCurves[dominantCurves.Count - 1];
            SurfaceBuilderCopilot.GenerateCapSurface(topmostCurve, finalSurfacePoints, finalPointIndex, finalSurfaceFaces);
        }

        Surface surfaceEntity = new Surface
        {
            Points = finalSurfacePoints,
            Faces = finalSurfaceFaces.ToArray()
        };

        CamBam.ThisApplication.AddLogMessage($"Number of unique vertices: {finalPointIndex.Count}");
        _ui.ActiveView.CADFile.Add(surfaceEntity);

        // Mark document as modified to ensure undo system registers the change
        _ui.ActiveView.CADFile.OnModified();

        _ui.ActiveView.CADFile.SetActiveLayer(originalLayerName);
        _ui.ActiveView.ZoomToFit();
        _ui.ActiveView.RefreshView();
    }

    /// <summary>
    /// Picks the dominant (largest perimeter) contour for each level, discarding
    /// any remaining secondary contours. This guarantees a single continuous
    /// lateral surface with no disconnected mesh fragments.
    /// </summary>
    private List<List<Point3F>> SelectDominantContourPerLevel(List<List<List<Point3F>>> groupedLevels)
    {
        var result = new List<List<Point3F>>();

        foreach (var level in groupedLevels)
        {
            if (level.Count == 0) continue;

            List<Point3F> dominant = level[0];
            double dominantPerimeter = CalculatePerimeter(dominant);

            for (int k = 1; k < level.Count; k++)
            {
                double perimeter = CalculatePerimeter(level[k]);
                if (perimeter > dominantPerimeter)
                {
                    dominant = level[k];
                    dominantPerimeter = perimeter;
                }
            }

            result.Add(dominant);
        }

        return result;
    }

    private static double CalculatePerimeter(List<Point3F> contour)
    {
        double perimeter = 0;
        for (int i = 0; i < contour.Count - 1; i++)
        {
            perimeter += Geometry3F.Distance(contour[i], contour[i + 1]);
        }
        if (contour.Count > 2)
        {
            perimeter += Geometry3F.Distance(contour[contour.Count - 1], contour[0]);
        }
        return perimeter;
    }

    /// <summary>
    /// Prepares closed-rail cross sections while preserving the grouping by generatrix
    /// level, so levels with multiple disjoint contours (offset splits) are not
    /// incorrectly flattened into the lateral surface sequence.
    /// </summary>
    private List<List<List<Point3F>>> PrepareClosedCurvesGrouped(PolylineManager selectionManager)
    {
        var units = SettingsManager.GetUnits();
        Polyline guideCurve = selectionManager.ClosedPoly != null ? selectionManager.ClosedPoly : null;
        var adaptiveParams = guideCurve != null
            ? _settingsManager.GetSmartAdaptiveParameters(guideCurve)
            : _settingsManager.GetDefaultAdaptiveParameters();

        double dpTolerance = SettingsManager.ConvertFromMillimeters(adaptiveParams.DouglasPeuckerTolerance, units);
        double samplingStep = SettingsManager.ConvertFromMillimeters(adaptiveParams.SamplingStepClosedPoly, units) / 5;

        var openCurveProcessor = new OpenPolylineProcessor(
            selectionManager.OpenPoly,
            samplingStep,
            dpTolerance
        );

        var orderedClosedCurves = LayerGenerator.GenerateContoursByGeratrizOrder(
            selectionManager.ClosedPoly,
            openCurveProcessor.SimplifiedPoints
        );

        var sampledGroupedCurves = CurveSampler.GenerateGroupedSampledPointsFromContours(
            orderedClosedCurves.Cast<List<CamBam.CAD.Polyline>>().ToList(),
            openCurveProcessor.SimplifiedPoints,
            samplingStep,
            dpTolerance
        );

        return SimplifyAllGrouped(sampledGroupedCurves, dpTolerance);
    }

    private List<List<Point3F>> PrepareOpenCurves(PolylineManager selectionManager)
    {
        var units = SettingsManager.GetUnits();
        Polyline guideCurve = selectionManager.OpenRailPoly;
        var adaptiveParams = guideCurve != null
            ? _settingsManager.GetSmartAdaptiveParameters(guideCurve)
            : _settingsManager.GetDefaultAdaptiveParameters();

        double dpTolerance = SettingsManager.ConvertFromMillimeters(adaptiveParams.DouglasPeuckerTolerance, units);
        double samplingStep = SettingsManager.ConvertFromMillimeters(adaptiveParams.SamplingStepClosedPoly, units) / 5;

        var openCurveProcessor = new OpenPolylineProcessor(
            selectionManager.OpenFormPoly,
            samplingStep,
            dpTolerance
        );

        var orderedOpenCurves = LayerGenerator.GenerateParallelOpenPolylinesByGeratrizOrder(
            selectionManager.OpenRailPoly,
            openCurveProcessor.SimplifiedPoints
        );

        var sampledOpenCurves = CurveSampler.GenerateSampledPointsFromContours(
            orderedOpenCurves,
            openCurveProcessor.SimplifiedPoints,
            samplingStep,
            dpTolerance
        );

        return SimplifyAll(sampledOpenCurves, dpTolerance);
    }

    private List<List<Point3F>> SimplifyAll(List<List<Point3F>> curves, double tolerance)
    {
        var result = new List<List<Point3F>>();
        foreach (var curve in curves)
            result.Add(PolylineSimplifier.SimplifyDouglasPeucker(curve, tolerance));
        return result;
    }

    /// <summary>
    /// Applies Douglas-Peucker simplification to every contour of every level,
    /// preserving the level grouping.
    /// </summary>
    private List<List<List<Point3F>>> SimplifyAllGrouped(List<List<List<Point3F>>> groupedCurves, double tolerance)
    {
        var result = new List<List<List<Point3F>>>();
        foreach (var level in groupedCurves)
        {
            var simplifiedLevel = new List<List<Point3F>>();
            foreach (var contour in level)
            {
                simplifiedLevel.Add(PolylineSimplifier.SimplifyDouglasPeucker(contour, tolerance));
            }
            result.Add(simplifiedLevel);
        }
        return result;
    }

    private string CreateUniqueLayer(string baseName)
    {
        int index = 1;
        string layerName;
        var cadFile = _ui.ActiveView.CADFile;

        do
        {
            layerName = $"{baseName}{index:D3}";
            index++;
        }
        while (cadFile.HasLayer(layerName));

        cadFile.CreateLayer(layerName);
        cadFile.SetActiveLayer(layerName);
        return layerName;
    }
}
