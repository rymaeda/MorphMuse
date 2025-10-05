using CamBam.Geom;
using System.Collections.Generic;

namespace MorphMuse.Model
{
    internal class Triangle
    {
        public Point3F A { get; set; }
        public Point3F B { get; set; }
        public Point3F C { get; set; }

        public Triangle(Point3F a, Point3F b, Point3F c)
        {
            this.A = a;
            this.B = b;
            this.C = c;
        }
    }

    internal class MeshGenerator
    {
        public List<Triangle> GenerateMesh(List<List<Point3F>> layers)
        {
            var triangles = new List<Triangle>();

            for (int i = 0; i < layers.Count - 1; i++)
            {
                var lower = layers[i];
                var upper = layers[i + 1];

                int count = System.Math.Min(lower.Count, upper.Count);

                for (int j = 0; j < count - 1; j++)
                {
                    // Triângulo 1
                    triangles.Add(new Triangle(
                        lower[j],
                        upper[j],
                        upper[j + 1]
                    ));

                    // Triângulo 2
                    triangles.Add(new Triangle(
                        lower[j],
                        upper[j + 1],
                        lower[j + 1]
                    ));
                }

                // Fecha o loop se for polilinha fechada
                if (lower.Count > 2 && upper.Count > 2)
                {
                    triangles.Add(new Triangle(
                        lower[count - 1],
                        upper[count - 1],
                        upper[0]
                    ));

                    triangles.Add(new Triangle(
                        lower[count - 1],
                        upper[0],
                        lower[0]
                    ));
                }
            }

            return triangles;
        }
    }
}