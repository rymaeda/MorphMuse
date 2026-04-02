using CamBam.Geom;
using System;
using System.Collections.Generic;

namespace MorphMuse.Services
{
    public static class SweepGenerator
    {
        /// <summary>
        /// Gera uma série de contornos (seções transversais) posicionando e rotacionando a curva 'profile' ao longo da curva 'rail'.
        /// </summary>
        public static List<List<Point3F>> GenerateSweepContours(List<Point3F> rail, List<Point3F> profile)
        {
            var contours = new List<List<Point3F>>();
            if (rail.Count < 2 || profile.Count < 2) return contours;

            // 1. Normalizar o profile para que o primeiro ponto seja a origem (0,0,0)
            // Isso garante que o profile "grude" no trilho pelo seu início.
            Point3F profileOrigin = profile[0];
            var normalizedProfile = new List<Point3F>();
            foreach (var p in profile)
            {
                normalizedProfile.Add(new Point3F(p.X - profileOrigin.X, p.Y - profileOrigin.Y, p.Z - profileOrigin.Z));
            }

            // 2. Percorrer o trilho e posicionar o profile em cada ponto
            for (int i = 0; i < rail.Count; i++)
            {
                Point3F currentPos = rail[i];
                Vector3F tangent;

                // Calcular a tangente no ponto atual do trilho
                if (i < rail.Count - 1)
                {
                    tangent = Geometry3F.FromPoints(rail[i], rail[i + 1]);
                }
                else
                {
                    tangent = Geometry3F.FromPoints(rail[i - 1], rail[i]);
                }

                // Normalizar a tangente
                double len = Geometry3F.Length(tangent);
                if (len > 1e-9)
                {
                    tangent = new Vector3F(tangent.X / len, tangent.Y / len, tangent.Z / len);
                }
                else
                {
                    tangent = new Vector3F(0, 0, 1); // Fallback
                }

                // Criar a matriz de transformação para alinhar o profile com a tangente
                // Usamos um sistema de coordenadas local (Frenet-Serret simplificado)
                // Z local = Tangente
                // X local = Vetor perpendicular à tangente (ex: projetado no plano XY)
                // Y local = Cross(Z, X)
                
                Vector3F up = new Vector3F(0, 0, 1);
                if (Math.Abs(tangent.Z) > 0.9) up = new Vector3F(1, 0, 0); // Evita singularidade se a tangente for vertical

                Vector3F localX = Geometry3F.Cross(up, tangent);
                double lenX = Geometry3F.Length(localX);
                if (lenX > 1e-9)
                {
                    localX = new Vector3F(localX.X / lenX, localX.Y / lenX, localX.Z / lenX);
                }
                else
                {
                    localX = new Vector3F(1, 0, 0);
                }

                Vector3F localY = Geometry3F.Cross(tangent, localX);

                // Gerar o contorno transformado
                var contour = new List<Point3F>();
                foreach (var p in normalizedProfile)
                {
                    // Transformação: P_world = P_local.x * localX + P_local.y * localY + P_local.z * tangent + currentPos
                    double worldX = p.X * localX.X + p.Y * localY.X + p.Z * tangent.X + currentPos.X;
                    double worldY = p.X * localX.Y + p.Y * localY.Y + p.Z * tangent.Y + currentPos.Y;
                    double worldZ = p.X * localX.Z + p.Y * localY.Z + p.Z * tangent.Z + currentPos.Z;
                    
                    contour.Add(new Point3F((float)worldX, (float)worldY, (float)worldZ));
                }
                contours.Add(contour);
            }

            return contours;
        }
    }
}
