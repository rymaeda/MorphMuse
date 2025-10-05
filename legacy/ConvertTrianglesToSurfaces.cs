using CamBam.Geom;
using CamBam.CAD;
using System.Collections.Generic;

public static class SurfaceUtils
{
    public static Surface ConvertTrianglesToSurface(List<Triangle> triangles)
    {
        Point3FArray points = new Point3FArray();
        Dictionary<Point3F, int> pointIndex = new Dictionary<Point3F, int>();
        List<TriangleFace> faces = new List<TriangleFace>();

        for (int i = 0; i < triangles.Count; i++)
        {
            Triangle t = triangles[i];

            int ia = AddPoint(t.A, points, pointIndex);
            int ib = AddPoint(t.B, points, pointIndex);
            int ic = AddPoint(t.C, points, pointIndex);

            faces.Add(new TriangleFace(ia, ib, ic));
        }

        Surface surface = new Surface();
        surface.Points = points;
        surface.Faces = faces.ToArray();
        return surface;
    }

    private static int AddPoint(Point3F p, Point3FArray points, Dictionary<Point3F, int> indexMap)
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
}