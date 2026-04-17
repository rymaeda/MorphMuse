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

        public static int AddPoint(Point3F p, Point3FArray points, Dictionary<Point3F, int> indexMap)
        {
            if (!indexMap.TryGetValue(p, out int index))
            {
                index = points.Count;
                points.Add(p);
                indexMap[p] = index;
            }
            return index;
        }
    }
}