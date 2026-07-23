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

        var simplifiedCurves = isTwoOpen ? PrepareOpenCurves(selectionManager) : PrepareClosedCurves(selectionManager);
        if (simplifiedCurves.Count < 2)
            return;

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

        // Gera a superfície lateral
        SurfaceBuilderCopilot.GenerateLateralSurface(simplifiedCurves, finalSurfacePoints, finalPointIndex, finalSurfaceFaces, isClosed: !isTwoOpen);

        // Gera a superfície de fechamento (cap) apenas se for fechada
        if (!isTwoOpen)
        {
            List<Point3F> topmostSimplifiedCurve = simplifiedCurves[simplifiedCurves.Count - 1];
            SurfaceBuilderCopilot.GenerateCapSurface(topmostSimplifiedCurve, finalSurfacePoints, finalPointIndex, finalSurfaceFaces);
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

    private List<List<Point3F>> PrepareClosedCurves(PolylineManager selectionManager)
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

        var sampledClosedCurves = CurveSampler.GenerateSampledPointsFromContours(
            orderedClosedCurves,
            openCurveProcessor.SimplifiedPoints,
            samplingStep,
            dpTolerance
        );

        return SimplifyAll(sampledClosedCurves, dpTolerance);
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
