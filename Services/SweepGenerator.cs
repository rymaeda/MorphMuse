using CamBam.Geom;
using System;
using System.Collections.Generic;

namespace MorphMuse.Services
{
    public static class SweepGenerator
    {
        /// <summary>
        /// Generates a series of contours (cross-sections) by positioning and rotating the 'profile' curve along the 'rail' curve.
        /// </summary>
        public static List<List<Point3F>> GenerateSweepContours(List<Point3F> rail, List<Point3F> profile)
        {
            var contours = new List<List<Point3F>>();
            if (rail.Count < 2 || profile.Count < 2) return contours;

            // 1. Normalize the profile so that the first point is at the origin (0,0,0)
            Point3F profileOrigin = profile[0];
            var normalizedProfile = new List<Point3F>();
            foreach (var p in profile)
            {
                normalizedProfile.Add(new Point3F(p.X - profileOrigin.X, p.Y - profileOrigin.Y, p.Z - profileOrigin.Z));
            }

            // 2. Traverse the rail and position the profile at each point
            for (int i = 0; i < rail.Count; i++)
            {
                Point3F currentPos = rail[i];
                Vector3F tangent;

                // Calculate the tangent at the current rail point
                if (i < rail.Count - 1)
                {
                    tangent = Geometry3F.FromPoints(rail[i], rail[i + 1]);
                }
                else
                {
                    tangent = Geometry3F.FromPoints(rail[i - 1], rail[i]);
                }
                // Normalize tangent manually
                double tangentLength = Geometry3F.Length(tangent);
                if (tangentLength > 0)
                    tangent = new Vector3F(tangent.X / tangentLength, tangent.Y / tangentLength, tangent.Z / tangentLength);

                // Fallback for zero tangent
                if (tangent.X == 0 && tangent.Y == 0 && tangent.Z == 0) tangent = new Vector3F(0, 0, 1);

                // Create the orthonormal basis (simplified Frenet-Serret)
                // We want the shape to stay "upright" (vertical).
                // We use the Up vector (0,0,1) to define the vertical plane.
                Vector3F worldUp = new Vector3F(0, 0, 1);
                
                // If the tangent is nearly vertical, we change the auxiliary Up vector to avoid singularity
                if (Math.Abs(Vector3F.DotProduct(tangent, worldUp)) > 0.99)
                {
                    worldUp = new Vector3F(1, 0, 0);
                }

                // Local X axis (Normal): Perpendicular to the tangent and WorldUp
                Vector3F localX = Geometry3F.Cross(tangent, worldUp);
                double localXLength = Geometry3F.Length(localX);
                if (localXLength > 0)
                    localX = new Vector3F(localX.X / localXLength, localX.Y / localXLength, localX.Z / localXLength);
                // Fallback for zero localX
                if (localX.X == 0 && localX.Y == 0 && localX.Z == 0) localX = new Vector3F(1, 0, 0);

                // Local Y axis (Binormal): Perpendicular to the tangent and localX
                Vector3F localY = Geometry3F.Cross(localX, tangent);
                double localYLength = Geometry3F.Length(localY);
                if (localYLength > 0)
                    localY = new Vector3F(localY.X / localYLength, localY.Y / localYLength, localY.Z / localYLength);
                // Fallback for zero localY
                if (localY.X == 0 && localY.Y == 0 && localY.Z == 0) localY = new Vector3F(0, 1, 0);

                // Generate the transformed contour
                var contour = new List<Point3F>();
                foreach (var p in normalizedProfile)
                {
                    // Coordinates of the profile point (px, py, pz) in its own local system
                    double px = p.X;
                    double py = p.Y;
                    double pz = p.Z;

                    // Apply 90 degree rotation around the profile's X axis:
                    // A point (x, y, z) rotated 90 degrees around the X axis becomes (x, y', z') where:
                    // y' = y * cos(angle) - z * sin(angle)
                    // z' = y * sin(angle) + z * cos(angle)
                    // For 90 degrees (PI/2 radians):
                    // cos(PI/2) = 0
                    // sin(PI/2) = 1
                    // So:
                    // y' = -z
                    // z' = y

                    double rotatedPx = px;
                    double rotatedPy = -pz; // The original Z of the profile becomes negative Y
                    double rotatedPz = py;  // The original Y of the profile becomes Z

                    // Map the rotated profile coordinates to the rail coordinate system:
                    // rotatedPx -> along the localX (Normal) vector of the rail.
                    // rotatedPy -> along the localY (Binormal, pointing up) vector of the rail.
                    // rotatedPz -> along the tangent (depth) vector of the rail.

                    double worldX = rotatedPx * localX.X + rotatedPy * localY.X + rotatedPz * tangent.X + currentPos.X;
                    double worldY = rotatedPx * localX.Y + rotatedPy * localY.Y + rotatedPz * tangent.Y + currentPos.Y;
                    double worldZ = rotatedPx * localX.Z + rotatedPy * localY.Z + rotatedPz * tangent.Z + currentPos.Z;
                    
                    contour.Add(new Point3F((float)worldX, (float)worldY, (float)worldZ));
                }
                contours.Add(contour);
            }

            return contours;
        }
    }
}