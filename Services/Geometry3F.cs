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
    }
}