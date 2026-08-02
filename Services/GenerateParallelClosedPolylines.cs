using CamBam.Geom;
using System.Collections.Generic;
using System.Linq;

// Add this using directive if Polyline is defined in CamBam.CAD
using CamBam.CAD;

namespace MorphMuse.Services
{
    internal static class LayerGenerator
    {
        /// <summary>
        /// Minimum ratio (relative to the base polyline's area) a resulting offset
        /// contour must have to be considered valid. Offset algorithms can produce
        /// spurious near-zero-area loops near narrow or concave sections of the
        /// base curve; these artifacts are filtered out to avoid generating
        /// disconnected/spurious meshes in the final surface.
        /// </summary>
        private const double MinRelativeAreaRatio = 0.001; // 0.1% of the base area

        public static List<Polyline> GenerateParallelClosedPolylines(
            Polyline closedBase,
            List<Point3F> openReferencePoints)
        {
            var contours = new List<Polyline>();

            // Reference area used to filter out spurious tiny offset artifacts.
            double baseArea = CalculatePolylineArea(closedBase);

            foreach (Point3F refPt in openReferencePoints)
            {
                float offsetValue = (float)refPt.X;
                float zHeight = (float)refPt.Y;

                Polyline[] offsetLayers = closedBase.CreateOffsetPolyline(offsetValue, 0.01f);
                if (offsetLayers == null || offsetLayers.Length == 0)
                {
                    CamBam.ThisApplication.AddLogMessage($"[LayerGenerator] Offset failed at offsetValue={offsetValue}: no contours returned.");
                    continue;
                }

                // Filter out spurious near-zero-area loops before applying Z height.
                var validLayers = FilterSpuriousContours(offsetLayers, baseArea);

                if (validLayers.Count == 0)
                {
                    CamBam.ThisApplication.AddLogMessage($"[LayerGenerator] offsetValue={offsetValue}: all {offsetLayers.Length} offset contour(s) discarded as spurious (area too small).");
                    continue;
                }

                if (validLayers.Count < offsetLayers.Length)
                {
                    CamBam.ThisApplication.AddLogMessage($"[LayerGenerator] offsetValue={offsetValue}: discarded {offsetLayers.Length - validLayers.Count} spurious contour(s) out of {offsetLayers.Length}.");
                }

                foreach (Polyline layer in validLayers)
                {
                    // Replace PolylineItem usage with direct manipulation of the Points list
                    for (int i = 0; i < layer.Points.Count; i++)
                    {
                        var pt = layer.Points[i];
                        pt.Point = new Point3F(pt.Point.X, pt.Point.Y, zHeight);
                        layer.Points[i] = pt;
                    }

                    contours.Add(layer);
                }
            }

            return contours;
        }

        /// <summary>
        /// Filters out offset result contours whose area is negligible compared to the
        /// base polyline's area. These tiny loops are numerical artifacts of the offset
        /// algorithm near narrow or concave regions, not meaningful cross-sections.
        /// </summary>
        private static List<Polyline> FilterSpuriousContours(Polyline[] offsetLayers, double baseArea)
        {
            var result = new List<Polyline>();

            // If we couldn't determine a meaningful base area, fall back to accepting
            // every contour (preserves legacy behavior in edge cases).
            if (baseArea <= 0)
            {
                result.AddRange(offsetLayers);
                return result;
            }

            double minArea = baseArea * MinRelativeAreaRatio;

            foreach (var layer in offsetLayers)
            {
                // PolylineItemList does not support LINQ directly; use ToArray() to enumerate.
                var pts = layer.Points.ToArray().Select(p => p.Point).ToList();
                double area = Geometry3F.CalculatePolygonArea2D(pts);

                if (area >= minArea)
                {
                    result.Add(layer);
                }
            }

            return result;
        }

        /// <summary>
        /// Calculates the 2D area of a closed base polyline, sampling its points
        /// directly (arcs are approximated by their control points, which is
        /// sufficient for area comparison/filtering purposes).
        /// </summary>
        private static double CalculatePolylineArea(Polyline polyline)
        {
            if (polyline == null || polyline.Points.Count < 3) return 0;

            // PolylineItemList does not support LINQ directly; use ToArray() to enumerate.
            var pts = polyline.Points.ToArray().Select(p => p.Point).ToList();
            return Geometry3F.CalculatePolygonArea2D(pts);
        }

        public static List<List<Polyline>> GenerateContoursByGeratrizOrder(
            Polyline closedBase,
            List<Point3F> openReferencePoints)
        {
            var orderedContours = new List<List<Polyline>>();

            foreach (var refPt in openReferencePoints)
            {
                var curves = GenerateParallelClosedPolylines(closedBase, new List<Point3F> { refPt });
                orderedContours.Add(curves);
            }

            return orderedContours;
        }

        public static List<Polyline> GenerateParallelOpenPolylines(
            Polyline openBase,
            List<Point3F> openReferencePoints)
        {
            var contours = new List<Polyline>();

            foreach (Point3F refPt in openReferencePoints)
            {
                float offsetValue = (float)refPt.X;
                float zHeight = (float)refPt.Y;

                Polyline[] offsetLayers = openBase.CreateOffsetPolyline(offsetValue, 0.01f);
                if (offsetLayers == null || offsetLayers.Length == 0)
                {
                    CamBam.ThisApplication.AddLogMessage($"[LayerGenerator] Open offset failed at offsetValue={offsetValue:F4}: no contours returned.");
                    continue;
                }

                foreach (Polyline layer in offsetLayers)
                {
                    for (int i = 0; i < layer.Points.Count; i++)
                    {
                        var pt = layer.Points[i];
                        pt.Point = new Point3F(pt.Point.X, pt.Point.Y, zHeight);
                        layer.Points[i] = pt;
                    }

                    contours.Add(layer);
                }
            }

            return contours;
        }

        public static List<List<Polyline>> GenerateParallelOpenPolylinesByGeratrizOrder(
            Polyline openBase,
            List<Point3F> openReferencePoints)
        {
            var orderedContours = new List<List<Polyline>>();

            foreach (var refPt in openReferencePoints)
            {
                var curves = GenerateParallelOpenPolylines(openBase, new List<Point3F> { refPt });
                orderedContours.Add(curves);
            }

            return orderedContours;
        }
    }
}