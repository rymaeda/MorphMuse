using CamBam.CAD;
using CamBam.Geom;
using System.Collections.Generic;

namespace MorphMuse.Services
{
    internal class OpenPolylineProcessor
    {
        public List<Point3F> SimplifiedPoints { get; private set; }

        public OpenPolylineProcessor(Polyline openPolyline, double arcTolerance, double simplifyTolerance)
        {
            //CheckOpenPolylineStart(openPolyline);
            SimplifiedPoints = new List<Point3F>();

            if (openPolyline != null && !openPolyline.Closed && openPolyline.Points.Count > 0)
            {
                // 1. Arcs removal
                Polyline linearized = openPolyline.RemoveArcs(arcTolerance);

                // 2. Define origin as first point
                Point3F origin = linearized.Points[0].Point;

                // 3. Extract and translate points to origin
                List<Point3F> translatedPoints = new List<Point3F>();
                foreach (PolylineItem item in linearized.Points)
                {
                    Point3F pt = item.Point;
                    Point3F relative = new Point3F(
                        pt.X - origin.X,
                        pt.Y - origin.Y,
                        pt.Z - origin.Z
                    );
                    translatedPoints.Add(relative);
                }

                // 4. Apply Douglas-Peucker simplification
                SimplifiedPoints = PolylineSimplifier.SimplifyDouglasPeucker(translatedPoints, simplifyTolerance);
            }
        }

        public static Polyline ReversePolylineIfNeeded(Polyline polyline, bool reverseDirection)
        {
            if (!reverseDirection || polyline == null)
                return polyline;

            // Clone to avoid mutating the original
            var reversed = (Polyline)polyline.Clone();
            var items = reversed.Points.ToArray();
            System.Array.Reverse(items);
            for (int i = 0; i < items.Length; i++)
            {
                reversed.Points[i] = items[i];
            }
            return reversed;
        }
    }
}
