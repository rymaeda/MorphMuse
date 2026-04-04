using CamBam.CAD;
using CamBam.Geom;
using System.Collections.Generic;

namespace MorphMuse.Services
{
    internal static class LayerGenerator
    {
        public static List<Polyline> GenerateParallelClosedPolylines(
            Polyline closedBase,
            List<Point3F> openReferencePoints)
        {
            var contours = new List<Polyline>();

            foreach (Point3F refPt in openReferencePoints)
            {
                float offsetValue = (float)refPt.X;
                float zHeight = (float)refPt.Y;

                Polyline[] offsetLayers = closedBase.CreateOffsetPolyline(offsetValue, 0.01f);
                if (offsetLayers == null || offsetLayers.Length == 0)
                {
                    CamBam.ThisApplication.AddLogMessage($"offsetValue: {offsetValue} // offsetLayers.Length: {(offsetLayers != null ? offsetLayers.Length.ToString() : "nulo")}");
                    continue;
                }

                foreach (Polyline layer in offsetLayers)
                {
                    for (int i = 0; i < layer.Points.Count; i++)
                    {
                        PolylineItem item = layer.Points[i];
                        Point3F pt = item.Point;
                        pt.Z = zHeight;
                        item.Point = pt;
                        layer.Points[i] = item;
                    }

                    contours.Add(layer);
                }
            }

            CamBam.ThisApplication.AddLogMessage($"GenerateParallelOpenPolylines: Completed. Generated {contours.Count} contours from {openReferencePoints.Count} reference points");
            return contours;
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

            CamBam.ThisApplication.AddLogMessage($"GenerateParallelOpenPolylines: Processing {openReferencePoints.Count} reference points");

            foreach (Point3F refPt in openReferencePoints)
            {
                float offsetValue = (float)refPt.X;
                float zHeight = (float)refPt.Y;

                CamBam.ThisApplication.AddLogMessage($"  Attempting offset: X={offsetValue:F4}, Y(Height)={zHeight:F4}");

                Polyline[] offsetLayers = openBase.CreateOffsetPolyline(offsetValue, 0.01f);
                if (offsetLayers == null || offsetLayers.Length == 0)
                {
                    CamBam.ThisApplication.AddLogMessage($"    FAILED: offsetValue={offsetValue:F4} returned {(offsetLayers != null ? offsetLayers.Length.ToString() : "null")}");
                    continue;
                }

                CamBam.ThisApplication.AddLogMessage($"    SUCCESS: Generated {offsetLayers.Length} offset layer(s) at height {zHeight:F4}");

                foreach (Polyline layer in offsetLayers)
                {
                    for (int i = 0; i < layer.Points.Count; i++)
                    {
                        PolylineItem item = layer.Points[i];
                        Point3F pt = item.Point;
                        pt.Z = zHeight;
                        item.Point = pt;
                        layer.Points[i] = item;
                    }

                    contours.Add(layer);
                }
            }

            CamBam.ThisApplication.AddLogMessage($"GenerateParallelOpenPolylines: Completed. Generated {contours.Count} contours from {openReferencePoints.Count} reference points");
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