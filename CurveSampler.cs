using CamBam.CAD;
using CamBam.Geom;
using MorphMuse.Services;
using System; // Add this using directive at the top of the file
using System.Collections.Generic;
using System.Linq;

public static class CurveSampler
{


    public static PointList CreatePointlistFromPolylineStepCopilot(Polyline curve, double StepMax)
    {
        var points = new PointList();

        // Aplica transformação, se houver
        var polyline = (Polyline)curve.Clone();
        if (polyline.ApplyTransformation())
        {
            polyline.Transform = Matrix4x4F.Identity;
        }

        double totalLength = polyline.GetPerimeter();
        double currentLength = 0.0;

        // Sempre adiciona o primeiro ponto
        points.Add(polyline.FirstPoint);

        // Amostragem contínua ao longo da curva
        while (currentLength + StepMax < totalLength)
        {
            currentLength += StepMax;
            double t = currentLength / totalLength;
            Point3F pt = polyline.GetParametricPoint(t);
            points.Add(pt);
        }

        // Sempre adiciona o último ponto
        points.Add(polyline.LastPoint);

        // Aplica transformação final, se necessário
        if (!polyline.Transform.IsIdentity())
        {
            points.ApplyTransformation(polyline.Transform);
        }

        // Remove duplicata se curva for fechada
        // Replace C# 8.0 index operator '^1' with explicit indexing for compatibility with C# 7.3
        if (points.Points.Count > 1 && Point3F.Match(points.Points[0], points.Points[points.Points.Count - 1]))
        {
            points.Points.RemoveAt(points.Points.Count - 1);
        }

        return points;
    }
    public static List<List<Point3F>> GenerateSampledPointsFromContours(
        List<List<Polyline>> orderedContours,
        List<Point3F> simplifiedGeratriz,
        double baseDensity,
        double minStep)
    {
        var sampledCurves = new List<List<Point3F>>();

        for (int i = 0; i < orderedContours.Count; i++)
        {
            List<Polyline> curves = orderedContours[i];
            double step = baseDensity;

            if (i < simplifiedGeratriz.Count - 1)
            {
                Point3F p1 = simplifiedGeratriz[i];
                Point3F p2 = simplifiedGeratriz[i + 1];
                Vector3F delta = new Vector3F(p2.X - p1.X, p2.Y - p1.Y, p2.Z - p1.Z);

                double spacing = Geometry3F.Length(delta);
                step = spacing * baseDensity;
                if (step < minStep)
                {
                    step = minStep;
                }
            }

            for (int j = 0; j < curves.Count; j++)
            {
                Polyline curve = curves[j];
                PointList rawPoints = CreatePointlistFromPolylineStepCopilot(curve, step);

                List<Point3F> converted = new List<Point3F>();

                for (int k = 0; k < rawPoints.Points.Count; k++)
                {
                    converted.Add(rawPoints.Points[k]);
                }

                sampledCurves.Add(converted);

            }
        }

        return sampledCurves;
    }
}