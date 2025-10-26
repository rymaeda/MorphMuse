using CamBam.CAD;
using CamBam.Geom;
using System;
using System.Collections.Generic;

namespace MorphMuse.Services
{
    public static class Geometry3F
    {
        public static Vector3F FromPoints(Point3F a, Point3F b)
        {
            return new Vector3F(b.X - a.X, b.Y - a.Y, b.Z - a.Z);
        }

        public static Vector3F Subtract(Point3F a, Point3F b)
        {
            return new Vector3F(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        public static Vector3F Cross(Vector3F a, Vector3F b)
        {
            return new Vector3F(
                a.Y * b.Z - a.Z * b.Y,
                a.Z * b.X - a.X * b.Z,
                a.X * b.Y - a.Y * b.X
            );
        }

        public static double Length(Vector3F v)
        {
            return Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
        }

        public static float Distance(Point3F a, Point3F b)
        {
            Vector3F delta = FromPoints(a, b);
            return (float)Length(delta);
        }
        public static Point3F GetCentroid(List<Point3F> points)
        {
            if (points == null || points.Count == 0)
                return new Point3F(0, 0, 0);

            float sumX = 0, sumY = 0, sumZ = 0;

            foreach (Point3F pt in points)
            {
                sumX += (float)pt.X;
                sumY += (float)pt.Y;
                sumZ += (float)pt.Z;
            }

            int count = points.Count;
            return new Point3F(sumX / count, sumY / count, sumZ / count);
        }
        public static double AngleBetween(Vector3F u, Vector3F v)
        {
            double dot = u.X * v.X + u.Y * v.Y + u.Z * v.Z;
            double lenU = Geometry3F.Length(u);
            double lenV = Geometry3F.Length(v);

            if (lenU < 1e-8 || lenV < 1e-8)
                return 0.0; // Vetores nulos ou quase nulos

            double cosTheta = dot / (lenU * lenV);
            cosTheta = Math.Max(-1.0, Math.Min(1.0, cosTheta)); // Clamping para evitar erros numéricos

            return Math.Acos(cosTheta); // Retorna em radianos
        }
        public static int AddPoint(Point3F p, Point3FArray points, Dictionary<Point3F, int> indexMap)
        {
            int index;
            if (!indexMap.TryGetValue(p, out index))
            {
                index = points.Count;
                points.Add(p);
                indexMap[p] = index;
            }
            return index;
        }
    }
}