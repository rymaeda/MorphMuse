using CamBam.CAD;
using CamBam.Geom;
using System;
using System.Collections.Generic;

namespace MorphMuse.Services
{
    internal class ConvexCapBuilder
    {

        public static List<Surface> CloseNonConvexPolyline(List<Point3F> polyline, double simplificationTolerance)
        {
            var simplified = PolylineSimplifier.SimplifyDouglasPeucker(polyline, simplificationTolerance);
            var convexGroups = SegmentIntoConvexSubPolylines(simplified);
            var surfaces = new List<Surface>();

            foreach (var group in convexGroups)
            {
                Point3F center = Geometry3F.GetCentroid(group);
                var points = new Point3FArray();
                var indexMap = new Dictionary<Point3F, int>();
                var faces = new List<TriangleFace>();

                int centerIndex = Geometry3F.AddPoint(center, points, indexMap);
                for (int i = 0; i < group.Count; i++)
                {
                    int ia = Geometry3F.AddPoint(group[i], points, indexMap);
                    int ib = Geometry3F.AddPoint(group[(i + 1) % group.Count], points, indexMap);
                    faces.Add(new TriangleFace(centerIndex, ia, ib));
                }

                surfaces.Add(new Surface { Points = points, Faces = faces.ToArray() });
            }

            return surfaces;
        }

        public static List<List<Point3F>> SegmentIntoConvexSubPolylines(List<Point3F> curve)
        {
            var groups = new List<List<Point3F>>();
            var currentGroup = new List<Point3F>();

            int count = curve.Count;
            for (int i = 0; i < count; i++)
            {
                Point3F prev = curve[(i - 1 + count) % count];
                Point3F curr = curve[i];
                Point3F next = curve[(i + 1) % count];

                Vector3F v1 = Geometry3F.FromPoints(prev, curr);
                Vector3F v2 = Geometry3F.FromPoints(curr, next);
                double angle = Geometry3F.AngleBetween(v1, v2) * (180.0 / Math.PI);

                currentGroup.Add(curr);

                if (angle > 180.0)
                {
                    if (currentGroup.Count >= 3)
                        groups.Add(new List<Point3F>(currentGroup));
                    currentGroup.Clear();
                }
            }

            if (currentGroup.Count >= 3)
                groups.Add(currentGroup);

            return groups;
        }
    }
}
