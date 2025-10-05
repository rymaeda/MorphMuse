using CamBam.Geom;
using System.Collections.Generic;
using MorphMuse.Services;

internal static class PolylineSimplifier
{
    public static List<Point3F> SimplifyDouglasPeucker(List<Point3F> points, double tolerance, bool closed = false)
    {
        if (points == null || points.Count < 3)
            return new List<Point3F>(points);

        var keep = new bool[points.Count];
        keep[0] = true;
        keep[points.Count - 1] = true;

        Simplify(points, 0, points.Count - 1, tolerance, keep);

        if (closed)
        {
            keep[0] = true;
            keep[points.Count - 1] = false; // Remove duplicate for closed polyline
        }

        var result = new List<Point3F>();
        for (int i = 0; i < points.Count; i++)
        {
            if (keep[i])
                result.Add(points[i]);
        }

        return result;
    }

    private static void Simplify(List<Point3F> pts, int start, int end, double tol, bool[] keep)
    {
        if (end <= start + 1)
            return;

        var a = pts[start];
        var b = pts[end];

        double maxDist = 0;
        int index = -1;

        for (int i = start + 1; i < end; i++)
        {
            double dist = PerpendicularDistance(pts[i], a, b);
            if (dist > tol && dist > maxDist)
            {
                maxDist = dist;
                index = i;
            }
        }

        if (index != -1)
        {
            keep[index] = true;
            Simplify(pts, start, index, tol, keep);
            Simplify(pts, index, end, tol, keep);
        }
    }

    private static double PerpendicularDistance(Point3F p, Point3F a, Point3F b)
    {
        var ab = Geometry3F.Subtract(b, a);
        var ap = Geometry3F.Subtract(p, a);
        var cross = Geometry3F.Cross(ab, ap);
        double area = Geometry3F.Length(cross);
        double baseLength = Geometry3F.Length(ab);
        return baseLength == 0 ? 0 : area / baseLength;
    }
}