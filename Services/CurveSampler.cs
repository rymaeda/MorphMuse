using CamBam.CAD;
using CamBam.Geom;
using MorphMuse.Services;
using System;
using System.Collections.Generic;

public static class CurveSampler
{
    public static PointList CreatePointlistFromPolylineStepCopilot(Polyline curve, double StepMax)
    {
        var points = new PointList();

        // Clone and apply transformation, if any
        var polyline = (Polyline)curve.Clone();
        if (polyline.ApplyTransformation())
        {
            polyline.Transform = Matrix4x4F.Identity;
        }

        // Extract the segments and arcs from the polyline
        Entity[] primitives = polyline.ToPrimitives();

        foreach (var primitive in primitives)
        {
            if (primitive is Arc arc)
            {
                // Calculate the number of points based on the radius and StepMax
                double arcLength = Math.Abs(arc.Sweep) * Math.PI / 180.0 * arc.Radius;
                int steps = Math.Max(2, (int)Math.Ceiling(arcLength / StepMax));

                double angleStep = arc.Sweep / steps;
                for (int i = 0; i <= steps; i++)
                {
                    double angleDeg = arc.Start + i * angleStep;
                    double angleRad = angleDeg * Math.PI / 180.0;

                    double x = arc.Point.X + arc.Radius * Math.Cos(angleRad);
                    double y = arc.Point.Y + arc.Radius * Math.Sin(angleRad);
                    points.Add(new Point3F(x, y, arc.Point.Z));
                }
            }
            else if (primitive is Line line)
            {
                // Add only the endpoints of the line
                if (line.Points.Count > 0)
                {
                    points.Add(line.Points[0]);
                    points.Add(line.Points[line.Points.Count - 1]);
                }
            }
        }

        // Apply final transformation, if necessary
        if (!polyline.Transform.IsIdentity())
        {
            points.ApplyTransformation(polyline.Transform);
        }

        // Remove duplicate if curve is closed
        if (points.Points.Count > 1 && Point3F.Match(points.Points[0], points.Points[points.Points.Count - 1]))
        {
            points.Points.RemoveAt(points.Points.Count - 1);
        }

        return points;
    }
    public static PointList CreatePointlistFromPolylineStepCopilot2(Polyline curve, double StepMax)
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
    /// <summary>
    /// Generates sampled points from a set of ordered contours, using a generatrix and density parameters.
    /// Each contour is sampled according to its spacing and the provided density.
    /// </summary>
    /// <param name="orderedContours">Contours ordered by generatrix.</param>
    /// <param name="simplifiedGeratriz">Simplified generatrix points.</param>
    /// <param name="baseDensity">Base density for sampling.</param>
    /// <param name="minStep">Minimum allowed step size.</param>
    /// <returns>A list of lists of sampled points for each contour.</returns>
    public static List<List<Point3F>> GenerateSampledPointsFromContours(
        List<List<Polyline>> orderedContours,
        List<Point3F> simplifiedGeratriz,
        double baseDensity,
        double minStep)
    {
        var sampledCurves = new List<List<Point3F>>();

        // Iterate through each contour group.
        for (int i = 0; i < orderedContours.Count; i++)
        {
            List<Polyline> curves = orderedContours[i];
            double step = baseDensity;

            // Adjust the sampling step based on the spacing between generatrix points.
            if (i < simplifiedGeratriz.Count - 1)
            {
                Point3F p1 = simplifiedGeratriz[i];
                Point3F p2 = simplifiedGeratriz[i + 1];
                Vector3F delta = new Vector3F(p2.X - p1.X, p2.Y - p1.Y, p2.Z - p1.Z);

                double spacing = Geometry3F.Length(delta);
                step = spacing * baseDensity;
                // Ensure the step is not below the minimum allowed.
                if (step < minStep)
                {
                    step = minStep;
                }
            }

            // Sample each polyline in the current contour group.
            for (int j = 0; j < curves.Count; j++)
            {
                Polyline curve = curves[j];
                PointList rawPoints = CreatePointlistFromPolylineStepCopilot(curve, step);

                List<Point3F> converted = new List<Point3F>();

                // Convert the PointList to a standard List<Point3F>.
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