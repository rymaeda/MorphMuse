using CamBam.CAD;
using CamBam.Geom;
using CamBam.UI;
using MorphMuse.Services;
using System.Collections.Generic;
using System.Drawing;

public class MorphMuseController
{
    private readonly CamBamUI _ui;

    public MorphMuseController(CamBamUI ui)
    {
        _ui = ui;
    }

    public void Execute()
    {
        // Attempt to retrieve selected polylines from the current CAD view
        if (!PolylineManager.TryCreateFromSelection(out PolylineManager manager))
            return;

        // Process the open polyline (generatrix) with smoothing parameters
        var processor = new OpenPolylineProcessor(manager.OpenPoly, 0.01, 0.01);

        // Order closed polylines (contours) based on their relation to the generatrix
        var orderedContours = LayerGenerator.GenerateContoursByGeratrizOrder(
            manager.ClosedPoly,
            processor.SimplifiedPoints
        );

        // Sample points along the contours with controlled density
        var sampledCurves = CurveSampler.GenerateSampledPointsFromContours(
            orderedContours,
            processor.SimplifiedPoints,
            0.05,
            0.05
        );

        // Simplify each sampled curve to reduce geometric complexity
        var simplifiedCurves = SimplifyAll(sampledCurves);

        // Abort if there are not enough curves to build a surface
        if (simplifiedCurves.Count < 2)
            return;

        // Save the currently active layer to restore it later
        string originalLayerName = _ui.ActiveView.CADFile.ActiveLayerName;

        // Create a new layer to store the final mesh
        string layerName = CreateUniqueLayer("Mesh");
        Layer layer = _ui.ActiveView.CADFile.Layers[layerName];
        layer.Color = Color.DeepSkyBlue;

        // Structures to accumulate all vertices and faces of the final surface
        Point3FArray allPoints = new Point3FArray();
        Dictionary<Point3F, int> pointIndex = new Dictionary<Point3F, int>();
        List<TriangleFace> allFaces = new List<TriangleFace>();

        // Generate surfaces between each pair of consecutive curves
        for (int i = 0; i < simplifiedCurves.Count - 1; i++)
        {
            var lower = simplifiedCurves[i];
            var upper = simplifiedCurves[i + 1];

            Surface partialSurface = SurfaceBuilderCopilot.BuildSurfaceBetweenCurves(lower, upper);

            // Reindex the vertices of the partial surface into the final mesh
            for (int f = 0; f < partialSurface.Faces.Length; f++)
            {
                TriangleFace face = partialSurface.Faces[f];

                Point3F pa = partialSurface.Points[face.A];
                Point3F pb = partialSurface.Points[face.B];
                Point3F pc = partialSurface.Points[face.C];

                int ia = AddPoint(pa, allPoints, pointIndex);
                int ib = AddPoint(pb, allPoints, pointIndex);
                int ic = AddPoint(pc, allPoints, pointIndex);

                allFaces.Add(new TriangleFace(ia, ib, ic));
            }
        }

        // Create the final consolidated surface
        Surface finalSurface = new Surface
        {
            Points = allPoints,
            Faces = allFaces.ToArray()
        };

        // Add the surface to the newly created layer
        _ui.ActiveView.CADFile.Add(finalSurface);

        // Restore the previously active layer
        _ui.ActiveView.CADFile.SetActiveLayer(originalLayerName);

        // Adjust the view to fit the new geometry and refresh the display
        _ui.ActiveView.ZoomToFit();
        _ui.ActiveView.RefreshView();
    }

    public void GenerateOffsetSurfaces(Polyline closedBase, double offsetStep, float tolerance)
    {
        var cadFile = _ui.ActiveView.CADFile;

        // Cria layer único para a malha completa
        string layerName = CreateUniqueLayer("MalhaOffset_");
        Layer layer = cadFile.Layers[layerName];
        layer.Color = Color.DeepSkyBlue;

        // Estruturas acumuladoras
        Point3FArray allPoints = new Point3FArray();
        Dictionary<Point3F, int> pointIndex = new Dictionary<Point3F, int>();
        List<TriangleFace> allFaces = new List<TriangleFace>();

        // Inicializa com a curva base
        List<Polyline> currentLayer = new List<Polyline> { closedBase };

        while (currentLayer.Count > 0)
        {
            List<Polyline> nextLayer = new List<Polyline>();

            foreach (Polyline poly in currentLayer)
            {
                Polyline[] offsets = poly.CreateOffsetPolyline(-offsetStep, tolerance);

                foreach (Polyline offset in offsets)
                {
                    if (!offset.Closed || offset.Points.Count < 3)
                        continue;

                    // Gera superfície entre curva atual e offset
                    //Surface faixa = SurfaceBuilderCopilot.BuildSurfaceBetweenCurves(poly.Points, offset.Points);
                    var lower = Geometry3F.ToPointList(poly.Points);
                    var upper = Geometry3F.ToPointList(offset.Points);
                    Surface faixa = SurfaceBuilderCopilot.BuildSurfaceBetweenCurves(lower, upper);

                    // Reindexa pontos e faces
                    for (int f = 0; f < faixa.Faces.Length; f++)
                    {
                        TriangleFace face = faixa.Faces[f];
                        Point3F pa = faixa.Points[face.A];
                        Point3F pb = faixa.Points[face.B];
                        Point3F pc = faixa.Points[face.C];

                        int ia = AddPoint(pa, allPoints, pointIndex);
                        int ib = AddPoint(pb, allPoints, pointIndex);
                        int ic = AddPoint(pc, allPoints, pointIndex);

                        allFaces.Add(new TriangleFace(ia, ib, ic));
                    }

                    nextLayer.Add(offset);
                }
            }

            currentLayer = nextLayer;
        }

        // Cria superfície final consolidada
        Surface finalSurface = new Surface
        {
            Points = allPoints,
            Faces = allFaces.ToArray()
        };

        cadFile.Add(finalSurface);
        _ui.ActiveView.ZoomToFit();
        _ui.ActiveView.RefreshView();
    }
    private int AddPoint(Point3F p, Point3FArray points, Dictionary<Point3F, int> indexMap)
    {
        int index;
        if (!indexMap.TryGetValue(p, out index))
        {
            index = points.Count;
            points.Add(p);
            indexMap[p] = index;
        }
        return index;
    }
    private List<List<Point3F>> SimplifyAll(List<List<Point3F>> curves)
    {
        var result = new List<List<Point3F>>();
        foreach (var curve in curves)
            result.Add(PolylineSimplifier.SimplifyDouglasPeucker(curve, 0.001));
        return result;
    }

    private string CreateUniqueLayer(string baseName)
    {
        int index = 1;
        string layerName;
        var cadFile = _ui.ActiveView.CADFile;

        do
        {
            layerName = $"{baseName}{index:D4}";
            index++;
        }
        while (cadFile.HasLayer(layerName));

        cadFile.CreateLayer(layerName);
        cadFile.SetActiveLayer(layerName);
        return layerName;
    }
}