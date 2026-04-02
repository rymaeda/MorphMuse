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
                "Invalid Selection. Please select one open and one closed polyline.",
                "Invalid Selection.",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );
            return;
        }

        var simplifiedClosedCurves = PrepareClosedCurves(selectionManager);
        if (simplifiedClosedCurves.Count < 2)
            return;

        string originalLayerName = _ui.ActiveView.CADFile.ActiveLayerName;
        string surfaceLayerName = CreateUniqueLayer("MorphSurface");
        Layer layer = _ui.ActiveView.CADFile.Layers[surfaceLayerName];
        layer.Color = Color.DeepSkyBlue;

        Point3FArray finalSurfacePoints = new Point3FArray();
        Dictionary<Point3F, int> finalPointIndex = new Dictionary<Point3F, int>();
        List<TriangleFace> finalSurfaceFaces = new List<TriangleFace>();

        // Gera a superfície lateral (atualmente assume curvas fechadas conforme seleção atual)
        SurfaceBuilderCopilot.GenerateLateralSurface(simplifiedClosedCurves, finalSurfacePoints, finalPointIndex, finalSurfaceFaces, isClosed: true);

        // Gera a superfície de fechamento (cap) usando o novo método de Ear Clipping
        List<Point3F> topmostSimplifiedCurve = simplifiedClosedCurves[simplifiedClosedCurves.Count - 1];
        SurfaceBuilderCopilot.GenerateCapSurface(topmostSimplifiedCurve, finalSurfacePoints, finalPointIndex, finalSurfaceFaces);

        Surface surfaceEntity = new Surface
        {
            Points = finalSurfacePoints,
            Faces = finalSurfaceFaces.ToArray()
        };
        
        CamBam.ThisApplication.AddLogMessage($"Number of unique vertices: {finalPointIndex.Count}");
        _ui.ActiveView.CADFile.Add(surfaceEntity);

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
