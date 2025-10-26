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

            // Sincroniza a rotação da curva superior com a inferior
            int matchIndex = FindClosestIndex(lower[0], upper);
            upper = RotateCurve(upper, matchIndex);

            // Fecha as curvas se não forem fechadas
            if (Geometry3F.Distance(lower[0], lower[lower.Count - 1]) > 1e-6)
                lower.Add(lower[0]);

            if (Geometry3F.Distance(upper[0], upper[upper.Count - 1]) > 1e-6)
                upper.Add(upper[0]);

            int i = 0, j = 0;

            // Triangulação adaptativa baseada em distância
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

            // Consome os pontos restantes
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

            return new Surface
            {
                Points = points,
                Faces = faces.ToArray()
            };
        }

        public static void GenerateCapSurface(List<Point3F> topCurve, Point3F center, Point3FArray points, Dictionary<Point3F, int> indexMap, List<TriangleFace> faces)
        {
            // Add the center point to the points list and get its index.
            int centerIndex = Geometry3F.AddPoint(center, points, indexMap);

            // For each consecutive segment of the curve, create a triangle between the center and the two segment points.
            for (int i = 0; i < topCurve.Count - 1; i++)
            {
                int ia = Geometry3F.AddPoint(topCurve[i], points, indexMap);         // Index of the current point
                int ib = Geometry3F.AddPoint(topCurve[i + 1], points, indexMap);     // Index of the next point
                faces.Add(new TriangleFace(centerIndex, ia, ib));         // Triangle: center → current point → next point
            }

            // Close the cap by connecting the last point to the first, forming the final triangle.
            int iaLast = Geometry3F.AddPoint(topCurve[topCurve.Count - 1], points, indexMap); // Last point of the curve
            int ibFirst = Geometry3F.AddPoint(topCurve[0], points, indexMap);                 // First point of the curve
            faces.Add(new TriangleFace(centerIndex, iaLast, ibFirst));             // Final triangle to close the cap
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

                    int ia = Geometry3F.AddPoint(pa, points, indexMap);
                    int ib = Geometry3F.AddPoint(pb, points, indexMap);
                    int ic = Geometry3F.AddPoint(pc, points, indexMap);

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

            int ia = Geometry3F.AddPoint(a, points, pointIndex);
            int ib = Geometry3F.AddPoint(b, points, pointIndex);
            int ic = Geometry3F.AddPoint(c, points, pointIndex);

            // Orientação fixa: sempre a → c → b
            faces.Add(new TriangleFace(ia, ic, ib));
        }

        //public static int AddPoint(Point3F p, Point3FArray points, Dictionary<Point3F, int> indexMap)
        //{
        //    if (!indexMap.TryGetValue(p, out int index))
        //    {
        //        index = points.Count;
        //        points.Add(p);
        //        indexMap[p] = index;
        //    }
        //    return index;
        //}

        private static bool IsDegenerate(Point3F a, Point3F b, Point3F c)
        {
            var ab = Geometry3F.FromPoints(a, b);
            var ac = Geometry3F.FromPoints(a, c);
            var cross = Geometry3F.Cross(ab, ac);
            return Geometry3F.Length(cross) * 0.5 < 1e-6;
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

                //CamBam.ThisApplication.AddLogMessage($"[Copilot] Curva rotacionada por {bestOffset} posições.");
            }
            else
            {
                //CamBam.ThisApplication.AddLogMessage($"[Copilot] Curva já estava alinhada (offset = 0).");
            }
        }


        static int FindClosestIndex(Point3F target, List<Point3F> curve)
        {
            int bestIndex = 0;
            double bestDistance = Geometry3F.Distance(target, curve[0]);

            for (int i = 1; i < curve.Count; i++)
            {
                double d = Geometry3F.Distance(target, curve[i]);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        static List<Point3F> RotateCurve(List<Point3F> curve, int offset)
        {
            var result = new List<Point3F>();
            for (int i = 0; i < curve.Count; i++)
                result.Add(curve[(i + offset) % curve.Count]);
            return result;
        }
    }
}