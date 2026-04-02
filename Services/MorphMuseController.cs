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
            return; // Mensagem de erro já exibida no ValidateSelection
        }

        // Determina o modo de operação
        if (selectionManager.CounterClosedP == 1 && selectionManager.CounterOpenP == 1)
        {
            ExecuteVolumeWithCap(selectionManager);
        }
        else if (selectionManager.CounterClosedP == 0 && selectionManager.CounterOpenP == 2)
        {
            ExecuteSweepBetweenOpenCurves(selectionManager);
        }
    }

    private void ExecuteVolumeWithCap(PolylineManager selectionManager)
    {
        var simplifiedClosedCurves = PrepareClosedCurves(selectionManager);
        if (simplifiedClosedCurves.Count < 2) return;

        string originalLayerName = _ui.ActiveView.CADFile.ActiveLayerName;
        string surfaceLayerName = CreateUniqueLayer("MorphVolume");
        
        Point3FArray finalSurfacePoints = new Point3FArray();
        Dictionary<Point3F, int> finalPointIndex = new Dictionary<Point3F, int>();
        List<TriangleFace> finalSurfaceFaces = new List<TriangleFace>();

        // Gera a superfície lateral (curvas fechadas)
        SurfaceBuilderCopilot.GenerateLateralSurface(simplifiedClosedCurves, finalSurfacePoints, finalPointIndex, finalSurfaceFaces, isClosed: true);

        // Gera a tampa (cap)
        List<Point3F> topmostSimplifiedCurve = simplifiedClosedCurves[simplifiedClosedCurves.Count - 1];
        SurfaceBuilderCopilot.GenerateCapSurface(topmostSimplifiedCurve, finalSurfacePoints, finalPointIndex, finalSurfaceFaces);

        AddSurfaceToCAD(finalSurfacePoints, finalSurfaceFaces, surfaceLayerName, originalLayerName);
    }

    private void ExecuteSweepBetweenOpenCurves(PolylineManager selectionManager)
    {
        var openPolys = selectionManager.SelectedOpenPolys;
        if (openPolys.Count < 2) return;

        // Assume que a primeira curva selecionada é o TRILHO (Rail) e a segunda é a FORMA (Profile)
        // O usuário pode inverter a seleção se desejar o contrário.
        Polyline railPoly = openPolys[0];
        Polyline profilePoly = openPolys[1];

        string originalLayerName = _ui.ActiveView.CADFile.ActiveLayerName;
        string surfaceLayerName = CreateUniqueLayer("MorphSweep");

        Point3FArray finalSurfacePoints = new Point3FArray();
        Dictionary<Point3F, int> finalPointIndex = new Dictionary<Point3F, int>();
        List<TriangleFace> finalSurfaceFaces = new List<TriangleFace>();

        // Prepara as curvas (amostragem e simplificação)
        var units = SettingsManager.GetUnits();
        var adaptiveParams = _settingsManager.GetDefaultAdaptiveParameters();
        double dpTolerance = SettingsManager.ConvertFromMillimeters(adaptiveParams.DouglasPeuckerTolerance, units);
        
        // Converter Rail para List<Point3F>
        var railPoints = new List<Point3F>();
        for (int i = 0; i < railPoly.Points.Count; i++)
        {
            var p = railPoly.Points[i];
            railPoints.Add(new Point3F((float)p.Point.X, (float)p.Point.Y, (float)p.Point.Z));
        }
        var simplifiedRail = PolylineSimplifier.SimplifyDouglasPeucker(railPoints, dpTolerance);

        // Converter Profile para List<Point3F>
        var profilePoints = new List<Point3F>();
        for (int i = 0; i < profilePoly.Points.Count; i++)
        {
            var p = profilePoly.Points[i];
            profilePoints.Add(new Point3F((float)p.Point.X, (float)p.Point.Y, (float)p.Point.Z));
        }
        var simplifiedProfile = PolylineSimplifier.SimplifyDouglasPeucker(profilePoints, dpTolerance);

        // Gera os contornos de Sweep (posicionando e rotacionando o profile ao longo do rail)
        var sweepContours = SweepGenerator.GenerateSweepContours(simplifiedRail, simplifiedProfile);

        // Gera a superfície lateral entre os contornos gerados (isClosed: false)
        SurfaceBuilderCopilot.GenerateLateralSurface(sweepContours, finalSurfacePoints, finalPointIndex, finalSurfaceFaces, isClosed: false);

        AddSurfaceToCAD(finalSurfacePoints, finalSurfaceFaces, surfaceLayerName, originalLayerName);
    }

    private void AddSurfaceToCAD(Point3FArray points, List<TriangleFace> faces, string layerName, string originalLayerName)
    {
        Surface surfaceEntity = new Surface
        {
            Points = points,
            Faces = faces.ToArray()
        };
        
        _ui.ActiveView.CADFile.Layers[layerName].Color = Color.DeepSkyBlue;
        _ui.ActiveView.CADFile.Add(surfaceEntity);
        _ui.ActiveView.CADFile.SetActiveLayer(originalLayerName);
        _ui.ActiveView.ZoomToFit();
        _ui.ActiveView.RefreshView();
    }

    private List<List<Point3F>> PrepareClosedCurves(PolylineManager selectionManager)
    {
        var units = SettingsManager.GetUnits();
        Polyline guideCurve = selectionManager.ClosedPoly;
        var adaptiveParams = _settingsManager.GetSmartAdaptiveParameters(guideCurve);

        double dpTolerance = SettingsManager.ConvertFromMillimeters(adaptiveParams.DouglasPeuckerTolerance, units);
        double samplingStep = SettingsManager.ConvertFromMillimeters(adaptiveParams.SamplingStepClosedPoly, units) / 5;

        var openCurveProcessor = new OpenPolylineProcessor(selectionManager.OpenPoly, samplingStep, dpTolerance);
        var orderedClosedCurves = LayerGenerator.GenerateContoursByGeratrizOrder(selectionManager.ClosedPoly, openCurveProcessor.SimplifiedPoints);
        var sampledClosedCurves = CurveSampler.GenerateSampledPointsFromContours(orderedClosedCurves, openCurveProcessor.SimplifiedPoints, samplingStep, dpTolerance);

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
