using CamBam.CAD;
using CamBam.Geom;
using System.Collections.Generic;

namespace MorphMuse.Services
{
    public static class OffsetUtils
    {
        public static List<List<Polyline>> GenerateOffsetLayersRecursive(
            List<Polyline> basePolylines,
            float offsetStep,
            int maxLayers,
            float tolerance)
        {
            var result = new List<List<Polyline>>();
            GenerateOffsetLayersRecursiveInternal(basePolylines, offsetStep, maxLayers, tolerance, result, 0);
            return result;
        }

        private static void GenerateOffsetLayersRecursiveInternal(
            List<Polyline> currentPolylines,
            float offsetStep,
            int maxLayers,
            float tolerance,
            List<List<Polyline>> result,
            int currentLayer)
        {
            if (currentLayer >= maxLayers || currentPolylines == null || currentPolylines.Count == 0)
                return;

            result.Add(currentPolylines);

            var nextLayerPolylines = new List<Polyline>();
            foreach (var poly in currentPolylines)
            {
                // Offset each polyline; may return multiple polylines due to singularities
                Polyline[] offsets = poly.CreateOffsetPolyline(offsetStep, tolerance);
                if (offsets != null && offsets.Length > 0)
                {
                    nextLayerPolylines.AddRange(offsets);
                }
            }

            // Recursively process the next layer
            GenerateOffsetLayersRecursiveInternal(nextLayerPolylines, offsetStep, maxLayers, tolerance, result, currentLayer + 1);
        }
    }
}