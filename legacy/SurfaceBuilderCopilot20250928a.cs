using CamBam.CAD;
using CamBam.Geom;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MorphMuse.Services
{
    public static class SurfaceBuilderCopilot
    {
        public static Surface BuildSurfaceBetweenCurves(List<Point3F> lower, List<Point3F> upper)
        {
            var points = new Point3FArray();
            var pointIndex = new Dictionary<Point3F, int>();
            var faces = new List<TriangleFace>();
            //CamBam.ThisApplication.AddLogMessage($"lower.Count= {lower.Count:F4}");
            CamBam.ThisApplication.AddLogMessage($"[Copilot] --- Início da triangulação ---");
            CamBam.ThisApplication.AddLogMessage($"[Copilot] lower.Count = {lower.Count}");
            CamBam.ThisApplication.AddLogMessage($"[Copilot] upper.Count = {upper.Count}");

            int maxLog = Math.Min(5, Math.Min(lower.Count, upper.Count));
            for (int k = 0; k < maxLog; k++)
            {
                var pl = lower[k];
                var pu = upper[k];
                CamBam.ThisApplication.AddLogMessage(
                    $"[Copilot] Ponto {k}: lower=({pl.X:F3}, {pl.Y:F3}, {pl.Z:F3}) | upper=({pu.X:F3}, {pu.Y:F3}, {pu.Z:F3})");
            }
            if (Geometry3F.Distance(lower[0], lower[lower.Count - 1]) > 1e-6)
                lower.Add(lower[0]);

            if (Geometry3F.Distance(upper[0], upper[upper.Count - 1]) > 1e-6)
                upper.Add(upper[0]);

            int i = 0, j = 0;

            while (i < lower.Count - 1 && j < upper.Count - 1)
            {
                //Point3F a = lower[i];
                //Point3F b = upper[j];
                //Point3F aNext = lower[i + 1];
                //Point3F bNext = upper[j + 1];
                //double dANextToB = Geometry3F.Distance(aNext, b);
                //double dAtoBNext = Geometry3F.Distance(a, bNext);

                //if (dANextToB < dAtoBNext)
                //{
                //    AddTriangle(a, b, aNext, points, pointIndex, faces);
                //    i++;
                //}
                //else
                //{
                //    AddTriangle(a, b, bNext, points, pointIndex, faces);
                //    j++;
                //}
                double fracLower = i / (double)(lower.Count - 1);
                double fracUpper = j / (double)(upper.Count - 1);
                //CamBam.ThisApplication.AddLogMessage($"lower.Count= {lower.Count:F4}");

                if (fracLower <= fracUpper && i < lower.Count - 1)
                {
                    AddTriangle(lower[i], upper[j], lower[i + 1], points, pointIndex, faces);
                    i++;
                }
                else if (j < upper.Count - 1)
                {
                    AddTriangle(lower[i], upper[j], upper[j + 1], points, pointIndex, faces);
                    j++;
                }
            }

            while (i < lower.Count - 1 || j < upper.Count - 1)
            {
                if (i < lower.Count - 1 && (j == upper.Count - 1 || i / (double)(lower.Count - 1) <= j / (double)(upper.Count - 1)))
                {
                    AddTriangle(lower[i], upper[j], lower[i + 1], points, pointIndex, faces);
                    i++;
                }
                else if (j < upper.Count - 1)
                {
                    AddTriangle(lower[i], upper[j], upper[j + 1], points, pointIndex, faces);
                    j++;
                }
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

                AlignCurveToPrevious(lower, upper); // Align upper and lower points

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

            // Orientação fixa: sempre a → c → b
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
        private static bool ChooseAdvanceByArea(Point3F a, Point3F b, Point3F aNext, Point3F bNext)
        {
            double areaA = TriangleArea(a, b, aNext);
            double areaB = TriangleArea(a, b, bNext);
            return areaA >= areaB; // true → avança i (lower), false → avança j (upper)
        }

        private static double TriangleArea(Point3F a, Point3F b, Point3F c)
        {
            var ab = Geometry3F.FromPoints(a, b);
            var ac = Geometry3F.FromPoints(a, c);
            var cross = Geometry3F.Cross(ab, ac);
            return Geometry3F.Length(cross) * 0.5;
        }
        private static bool ChooseAdvanceByAngle(Point3F a, Point3F b, Point3F aNext, Point3F bNext)
        {
            var ab = Geometry3F.FromPoints(a, b);
            var va = Geometry3F.FromPoints(a, aNext);
            var vb = Geometry3F.FromPoints(b, bNext);

            double angleA = AngleBetween(ab, va);
            double angleB = AngleBetween(ab, vb);

            return angleA <= angleB; // menor ângulo → avança i (lower)
        }

        private static double AngleBetween(Vector3F u, Vector3F v)
        {
            double dot = u.X * v.X + u.Y * v.Y + u.Z * v.Z;
            double lu = Geometry3F.Length(u);
            double lv = Geometry3F.Length(v);
            if (lu < 1e-8 || lv < 1e-8) return Math.PI;
            double cos = Math.Max(-1.0, Math.Min(1.0, dot / (lu * lv)));
            return Math.Acos(cos);
        }
       public static void AlignCurveToPrevious(List<Point3F> previous, List<Point3F> current)
        {
            int n = current.Count;
            int m = previous.Count;
            if (n < 3 || m < 3) return;

            int minCount = Math.Min(n, m);
            double bestScore = double.MaxValue;
            int bestOffset = 0;

            for (int offset = 0; offset < n; offset++)
            {
                double score = 0;
                for (int i = 0; i < minCount; i++)
                {
                    int j = (i + offset) % n;
                    score += Geometry3F.Distance(previous[i], current[j]);
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    bestOffset = offset;
                }
            }

            if (bestOffset > 0)
            {
                var rotated = new List<Point3F>();
                rotated.AddRange(current.Skip(bestOffset));
                rotated.AddRange(current.Take(bestOffset));
                current.Clear();
                current.AddRange(rotated);

                CamBam.ThisApplication.AddLogMessage($"[Copilot] Curva rotacionada por {bestOffset} posições.");
            }
            else
            {
                CamBam.ThisApplication.AddLogMessage($"[Copilot] Curva já estava alinhada (offset = 0).");
            }
        }
    }
}