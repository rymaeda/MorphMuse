using CamBam.CAD;
using CamBam.Geom;
using System.Collections.Generic;

namespace MorphMuse.Services
{
    public class FanCapGenerator
    {
        static public void FindConvergenceCentersAndCurves(
            Polyline closedBase,
            double minOffset,
            double maxOffset,
            float tolerance,
            out List<Point3F> centers,
            out List<Polyline> finalCurves)
        {
            centers = new List<Point3F>();
            finalCurves = new List<Polyline>();

            double low = minOffset;
            double high = maxOffset;

            while ((high - low) > 0.01)
            {
                double mid = (low + high) / 2.0;
                Polyline[] offsets = closedBase.CreateOffsetPolyline(-mid, tolerance);

                if (offsets == null || offsets.Length == 0)
                {
                    high = mid;
                }
                else
                {
                    low = mid;
                    centers.Clear();
                    finalCurves.Clear();

                    for (int i = 0; i < offsets.Length; i++)
                    {
                        Polyline poly = offsets[i];
                        if (poly.Closed && poly.Points.Count >= 3)
                        {
                            finalCurves.Add(poly);
                            centers.Add(poly.GetCentroid()); // Usa API do CamBam
                        }
                    }
                }
            }
        }
        static public List<TriangleFace> GenerateFanCapForMultiple(
            List<Polyline> curves,
            List<Point3F> centers,
            Point3FArray points,
            Dictionary<Point3F, int> pointIndex)
        {  
            List<TriangleFace> faces = new List<TriangleFace>();
            int count = System.Math.Min(curves.Count, centers.Count);

            for (int i = 0; i < count; i++)
            {
                Polyline curve = curves[i];
                Point3F center = centers[i];

                if (!curve.Closed || curve.Points.Count < 3)
                    continue;

                int ic = Geometry3F.AddPoint(center, points, pointIndex);

                for (int j = 0; j < curve.Points.Count - 1; j++)
                {
                    int ia = Geometry3F.AddPoint(curve.Points[j].Point, points, pointIndex);
                    int ib = Geometry3F.AddPoint(curve.Points[j + 1].Point, points, pointIndex);
                    faces.Add(new TriangleFace(ic, ia, ib));
                }

                int iaLast = Geometry3F.AddPoint(curve.Points[curve.Points.Count - 1].Point, points, pointIndex);
                int ibFirst = Geometry3F.AddPoint(curve.Points[0].Point, points, pointIndex);
                faces.Add(new TriangleFace(ic, iaLast, ibFirst));
            }

            return faces;
        }

        public static Point3F CalculateCentroid(Polyline poly)
        {
            float cx = 0f;
            float cy = 0f;
            float cz = 0f;

            for (int i = 0; i < poly.Points.Count; i++)
            {
                Point3F pt = poly.Points[i].Point;
                cx += (float)pt.X;
                cy += (float)pt.Y;
                cz += (float)pt.Z;
            }

            int count = poly.Points.Count;
            return new Point3F(cx / count, cy / count, cz / count);
        }
    }
}