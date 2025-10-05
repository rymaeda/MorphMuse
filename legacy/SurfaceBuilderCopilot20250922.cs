using CamBam.Geom;
using CamBam.CAD;
using System.Collections.Generic;

namespace MorphMuse.Services
{
    public static class SurfaceBuilderCopilot
    {
        public static Surface BuildSurfaceBetweenCurves(List<Point3F> lower, List<Point3F> upper)
        {
            var points = new Point3FArray();
            var pointIndex = new Dictionary<Point3F, int>();
            var faces = new List<TriangleFace>();
            
            if (Geometry3F.Distance(lower[0], lower[lower.Count - 1]) > 1e-6)
                lower.Add(lower[0]);

            if (Geometry3F.Distance(upper[0], upper[upper.Count - 1]) > 1e-6)
                upper.Add(upper[0]);

            int i = 0, j = 0;

            while (i < lower.Count - 1 && j < upper.Count - 1)
            {
                Point3F a = lower[i];
                Point3F b = upper[j];
                Point3F aNext = lower[i + 1];
                Point3F bNext = upper[j + 1];

                double dANextToB = Geometry3F.Distance(aNext, b);
                double dAtoBNext = Geometry3F.Distance(a, bNext);

                if (dANextToB < dAtoBNext)
                {
                    AddTriangle(a, b, aNext, points, pointIndex, faces);
                    i++;
                }
                else
                {
                    AddTriangle(a, b, bNext, points, pointIndex, faces);
                    j++;
                }
            }

            while (i < lower.Count - 1)
            {
                AddTriangle(lower[i], upper[j], lower[i + 1], points, pointIndex, faces);
                i++;
            }

            while (j < upper.Count - 1)
            {
                AddTriangle(lower[i], upper[j], upper[j + 1], points, pointIndex, faces);
                j++;
            }

            var surface = new Surface
            {
                Points = points,
                Faces = faces.ToArray()
            };

            return surface;
        }

        public static void GenerateCapSurface(List<Point3F> topCurve, Point3F center, Point3FArray points, Dictionary<Point3F, int> indexMap, List<TriangleFace> faces)
        {
            int centerIndex = AddPoint(center, points, indexMap);

            for (int i = 0; i < topCurve.Count - 1; i++)
            {
                int ia = AddPoint(topCurve[i], points, indexMap);
                int ib = AddPoint(topCurve[i + 1], points, indexMap);
                faces.Add(new TriangleFace(centerIndex, ia, ib));
            }

            int iaLast = AddPoint(topCurve[topCurve.Count - 1], points, indexMap);
            int ibFirst = AddPoint(topCurve[0], points, indexMap);
            faces.Add(new TriangleFace(centerIndex, iaLast, ibFirst));
        }
        public static void GenerateLateralSurface(List<List<Point3F>> simplifiedCurves, Point3FArray points, Dictionary<Point3F, int> indexMap, List<TriangleFace> faces)
        {
            for (int i = 0; i < simplifiedCurves.Count - 1; i++)
            {
                var lower = simplifiedCurves[i];
                var upper = simplifiedCurves[i + 1];

                Surface partialSurface = SurfaceBuilderCopilot.BuildSurfaceBetweenCurves(lower, upper);

                foreach (var face in partialSurface.Faces)
                {
                    Point3F pa = partialSurface.Points[face.A];
                    Point3F pb = partialSurface.Points[face.B];
                    Point3F pc = partialSurface.Points[face.C];

                    int ia = AddPoint(pa, points, indexMap);
                    int ib = AddPoint(pb, points, indexMap);
                    int ic = AddPoint(pc, points, indexMap);

                    faces.Add(new TriangleFace(ia, ib, ic));
                }
            }
        }


        private static void AddTriangle(Point3F a, Point3F b, Point3F c,
                                Point3FArray points,
                                Dictionary<Point3F, int> pointIndex,
                                List<TriangleFace> faces)
        {
            if (IsDegenerate(a, b, c)) return;

            int ia = AddPoint(a, points, pointIndex);
            int ib = AddPoint(b, points, pointIndex);
            int ic = AddPoint(c, points, pointIndex);

            // Orientação fixa: sempre a → b → c
            faces.Add(new TriangleFace(ia, ic, ib));
        }

        private static int AddPoint(Point3F p, Point3FArray points, Dictionary<Point3F, int> indexMap)
        {
            if (!indexMap.TryGetValue(p, out int index))
            {
                index = points.Count;
                points.Add(p);
                indexMap[p] = index;
            }
            return index;
        }

        private static bool IsDegenerate(Point3F a, Point3F b, Point3F c)
        {
            var ab = Geometry3F.FromPoints(a, b);
            var ac = Geometry3F.FromPoints(a, c);
            var cross = Geometry3F.Cross(ab, ac);
            return Geometry3F.Length(cross) * 0.5 < 1e-6;
        }
    }
}