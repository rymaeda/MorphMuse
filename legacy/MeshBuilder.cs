using CamBam.CAD;
using CamBam.Geom;
using System;
using System.Collections.Generic;
using MorphMuse.Services;

namespace MorphMuse
{
    internal class MeshBuilder
    {
        public static List<List<Point3F>> GenerateSampledCurvesFromGeratriz(
        List<List<Polyline>> orderedContours,
        List<Point3F> simplifiedGeratriz,
        double baseDensity = 0.5,
        double minStep = 0.1)
        {
            var sampledCurves = new List<List<Point3F>>();

            for (int i = 0; i < orderedContours.Count; i++)
            {
                var curves = orderedContours[i];
                double step = baseDensity;

                if (i < simplifiedGeratriz.Count - 1)
                {
                    // Corrected: Use Geometry3F.FromPoints instead of subtraction
                    double spacing = Geometry3F.Length(Geometry3F.FromPoints(simplifiedGeratriz[i], simplifiedGeratriz[i + 1]));
                    step = Math.Max(minStep, spacing * baseDensity);
                }

                foreach (var curve in curves)
                {
                    Vector3F delta = Geometry3F.FromPoints(simplifiedGeratriz[i], simplifiedGeratriz[i + 1]);
                    double spacing = Geometry3F.Length(delta);
                    double stp = Math.Max(minStep, spacing * baseDensity);

                    PointList rawPoints = PointListUtils.CreatePointlistFromPolylineStep(curve, stp);
                    List<Point3F> sampled = ConvertPointList(rawPoints);
                    sampledCurves.Add(sampled);
                }
            }

            return sampledCurves;
        }

        public static List<Point3F> ConvertPointList(PointList pointList)
        {
            var result = new List<Point3F>();
            for (int i = 0; i < pointList.Points.Count; i++)
            {
                result.Add(pointList.Points[i]);
            }
            return result;
        }
    }
}
