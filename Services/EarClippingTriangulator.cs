using CamBam.Geom;
using System;
using System.Collections.Generic;

namespace MorphMuse.Services
{
    public static class EarClippingTriangulator
    {
        /// <summary>
        /// Triangulates a simple polygon (can be concave) using the Ear Clipping algorithm.
        /// Supports polygons in any plane by projecting to 2D first.
        /// </summary>
        public static List<TriangleFace> Triangulate(List<Point3F> polygon, Point3FArray points, Dictionary<Point3F, int> indexMap)
        {
            var faces = new List<TriangleFace>();
            if (polygon.Count < 3) return faces;

            // Create a working copy to avoid modifying the original
            var workingPolygon = new List<Point3F>(polygon);

            // Remove duplicate closing point if present
            if (workingPolygon.Count > 1 && 
                PointsAreEqual(workingPolygon[0], workingPolygon[workingPolygon.Count - 1]))
            {
                workingPolygon.RemoveAt(workingPolygon.Count - 1);
            }

            if (workingPolygon.Count < 3) return faces;

            // Calculate the polygon's normal to determine the best projection plane
            Vector3F normal = CalculatePolygonNormal(workingPolygon);

            // Project 3D points to 2D for triangulation
            List<Point2D> projected = ProjectTo2D(workingPolygon, normal);

            // Create a list of indices to work with
            List<int> indices = new List<int>();
            for (int i = 0; i < workingPolygon.Count; i++)
            {
                indices.Add(i);
            }

            // Determine the orientation of the polygon (clockwise or counter-clockwise)
            bool isClockwise = IsClockwise2D(projected, indices);

            int iterations = 0;
            int maxIterations = workingPolygon.Count * workingPolygon.Count; // Safety limit

            while (indices.Count > 3 && iterations < maxIterations)
            {
                bool earFound = false;
                int n = indices.Count;

                for (int i = 0; i < n; i++)
                {
                    int prevIdx = indices[(i - 1 + n) % n];
                    int currIdx = indices[i];
                    int nextIdx = indices[(i + 1) % n];

                    if (IsEar2D(prevIdx, currIdx, nextIdx, indices, projected, isClockwise))
                    {
                        // Add the triangle using original 3D points
                        int ia = Geometry3F.AddPoint(workingPolygon[prevIdx], points, indexMap);
                        int ib = Geometry3F.AddPoint(workingPolygon[currIdx], points, indexMap);
                        int ic = Geometry3F.AddPoint(workingPolygon[nextIdx], points, indexMap);

                        faces.Add(new TriangleFace(ia, ib, ic));

                        // Remove the "ear" vertex
                        indices.RemoveAt(i);
                        earFound = true;
                        break;
                    }
                }

                if (!earFound)
                {
                    // Try reversing orientation if no ear found
                    if (iterations == 0)
                    {
                        isClockwise = !isClockwise;
                        iterations++;
                        continue;
                    }
                    break;
                }
                iterations++;
            }

            // Add the last remaining triangle
            if (indices.Count == 3)
            {
                int ia = Geometry3F.AddPoint(workingPolygon[indices[0]], points, indexMap);
                int ib = Geometry3F.AddPoint(workingPolygon[indices[1]], points, indexMap);
                int ic = Geometry3F.AddPoint(workingPolygon[indices[2]], points, indexMap);
                faces.Add(new TriangleFace(ia, ib, ic));
            }

            return faces;
        }

        /// <summary>
        /// Helper struct for 2D points during triangulation.
        /// </summary>
        private struct Point2D
        {
            public double X;
            public double Y;

            public Point2D(double x, double y)
            {
                X = x;
                Y = y;
            }
        }

        /// <summary>
        /// Calculates the average normal of the polygon using Newell's method.
        /// </summary>
        private static Vector3F CalculatePolygonNormal(List<Point3F> polygon)
        {
            double nx = 0, ny = 0, nz = 0;

            for (int i = 0; i < polygon.Count; i++)
            {
                Point3F current = polygon[i];
                Point3F next = polygon[(i + 1) % polygon.Count];

                nx += (current.Y - next.Y) * (current.Z + next.Z);
                ny += (current.Z - next.Z) * (current.X + next.X);
                nz += (current.X - next.X) * (current.Y + next.Y);
            }

            double length = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (length < 1e-10)
            {
                // Fallback to Z-up if polygon is degenerate
                return new Vector3F(0, 0, 1);
            }

            return new Vector3F(nx / length, ny / length, nz / length);
        }

        /// <summary>
        /// Projects 3D points to 2D by finding the best projection plane based on the polygon normal.
        /// </summary>
        private static List<Point2D> ProjectTo2D(List<Point3F> polygon, Vector3F normal)
        {
            var result = new List<Point2D>();

            // Find the dominant axis of the normal to choose projection plane
            double absX = Math.Abs(normal.X);
            double absY = Math.Abs(normal.Y);
            double absZ = Math.Abs(normal.Z);

            // Project to the plane perpendicular to the dominant axis
            if (absZ >= absX && absZ >= absY)
            {
                // Normal is mostly Z - project to XY plane
                foreach (var p in polygon)
                {
                    result.Add(new Point2D(p.X, p.Y));
                }
            }
            else if (absY >= absX && absY >= absZ)
            {
                // Normal is mostly Y - project to XZ plane
                foreach (var p in polygon)
                {
                    result.Add(new Point2D(p.X, p.Z));
                }
            }
            else
            {
                // Normal is mostly X - project to YZ plane
                foreach (var p in polygon)
                {
                    result.Add(new Point2D(p.Y, p.Z));
                }
            }

            return result;
        }

        /// <summary>
        /// Checks if the polygon is clockwise using the shoelace formula on 2D projected points.
        /// </summary>
        private static bool IsClockwise2D(List<Point2D> projected, List<int> indices)
        {
            double area = 0;
            int n = indices.Count;

            for (int i = 0; i < n; i++)
            {
                Point2D p1 = projected[indices[i]];
                Point2D p2 = projected[indices[(i + 1) % n]];
                area += (p2.X - p1.X) * (p2.Y + p1.Y);
            }

            return area > 0;
        }

        /// <summary>
        /// Determines if the vertex at currIdx forms an "ear" that can be clipped.
        /// </summary>
        private static bool IsEar2D(int pIdx, int cIdx, int nIdx, List<int> indices, List<Point2D> projected, bool isClockwise)
        {
            Point2D a = projected[pIdx];
            Point2D b = projected[cIdx];
            Point2D c = projected[nIdx];

            // 1. Check if the angle is convex relative to the polygon's interior
            if (!IsConvex2D(a, b, c, isClockwise)) return false;

            // 2. Check if any other polygon vertex is inside the triangle
            foreach (int idx in indices)
            {
                if (idx == pIdx || idx == cIdx || idx == nIdx) continue;
                if (IsPointInTriangle2D(projected[idx], a, b, c)) return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if vertex b forms a convex angle between a and c.
        /// </summary>
        private static bool IsConvex2D(Point2D a, Point2D b, Point2D c, bool isClockwise)
        {
            double crossProduct = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);

            // Use a small tolerance for numerical stability
            const double epsilon = 1e-10;

            if (isClockwise)
            {
                return crossProduct < -epsilon;
            }
            return crossProduct > epsilon;
        }

        /// <summary>
        /// Checks if point p is inside triangle (a, b, c) using barycentric coordinates.
        /// </summary>
        private static bool IsPointInTriangle2D(Point2D p, Point2D a, Point2D b, Point2D c)
        {
            double det = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);

            if (Math.Abs(det) < 1e-12) return false; // Degenerate triangle

            double baryA = ((b.Y - c.Y) * (p.X - c.X) + (c.X - b.X) * (p.Y - c.Y)) / det;
            double baryB = ((c.Y - a.Y) * (p.X - c.X) + (a.X - c.X) * (p.Y - c.Y)) / det;
            double baryC = 1.0 - baryA - baryB;

            // Point is inside if all barycentric coordinates are positive
            // Use small positive epsilon to handle edge cases (points exactly on edges)
            const double epsilon = 1e-10;
            return baryA > epsilon && baryB > epsilon && baryC > epsilon;
        }

        /// <summary>
        /// Checks if two 3D points are equal within tolerance.
        /// </summary>
        private static bool PointsAreEqual(Point3F a, Point3F b)
        {
            const double tolerance = 1e-6;
            return Math.Abs(a.X - b.X) < tolerance &&
                   Math.Abs(a.Y - b.Y) < tolerance &&
                   Math.Abs(a.Z - b.Z) < tolerance;
        }
    }
}