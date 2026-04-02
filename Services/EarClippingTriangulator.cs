using CamBam.Geom;
using System;
using System.Collections.Generic;

namespace MorphMuse.Services
{
    public static class EarClippingTriangulator
    {
        /// <summary>
        /// Triangula um polígono simples (pode ser côncavo) usando o algoritmo de Ear Clipping.
        /// Assume que todos os pontos estão no mesmo plano.
        /// </summary>
        public static List<TriangleFace> Triangulate(List<Point3F> polygon, Point3FArray points, Dictionary<Point3F, int> indexMap)
        {
            var faces = new List<TriangleFace>();
            if (polygon.Count < 3) return faces;

            // Criar uma lista de índices para trabalhar
            List<int> indices = new List<int>();
            for (int i = 0; i < polygon.Count; i++)
            {
                indices.Add(i);
            }

            // Determinar a orientação do polígono (sentido horário ou anti-horário)
            // Usamos a soma das áreas assinadas (Shoelace formula)
            bool isClockwise = IsClockwise(polygon);

            int iterations = 0;
            while (indices.Count > 3 && iterations < 2000) // Guardrail aumentado
            {
                bool earFound = false;
                for (int i = 0; i < indices.Count; i++)
                {
                    int prevIdx = indices[(i - 1 + indices.Count) % indices.Count];
                    int currIdx = indices[i];
                    int nextIdx = indices[(i + 1) % indices.Count];

                    if (IsEar(prevIdx, currIdx, nextIdx, indices, polygon, isClockwise))
                    {
                        // Adicionar o triângulo
                        int ia = Geometry3F.AddPoint(polygon[prevIdx], points, indexMap);
                        int ib = Geometry3F.AddPoint(polygon[currIdx], points, indexMap);
                        int ic = Geometry3F.AddPoint(polygon[nextIdx], points, indexMap);
                        
                        // Orientação ajustada para normais corretas (ia, ib, ic)
                        // O usuário mencionou que precisou trocar b e c, então usamos a ordem que gera a normal correta.
                        faces.Add(new TriangleFace(ia, ib, ic));

                        // Remover o vértice "orelha"
                        indices.RemoveAt(i);
                        earFound = true;
                        break;
                    }
                }

                if (!earFound) break; 
                iterations++;
            }

            // Adicionar o último triângulo restante
            if (indices.Count == 3)
            {
                int ia = Geometry3F.AddPoint(polygon[indices[0]], points, indexMap);
                int ib = Geometry3F.AddPoint(polygon[indices[1]], points, indexMap);
                int ic = Geometry3F.AddPoint(polygon[indices[2]], points, indexMap);
                faces.Add(new TriangleFace(ia, ib, ic));
            }

            return faces;
        }

        private static bool IsEar(int pIdx, int cIdx, int nIdx, List<int> indices, List<Point3F> polygon, bool isClockwise)
        {
            Point3F a = polygon[pIdx];
            Point3F b = polygon[cIdx];
            Point3F c = polygon[nIdx];

            // 1. Verificar se o ângulo é convexo em relação ao interior do polígono
            if (!IsConvex(a, b, c, isClockwise)) return false;

            // 2. Verificar se algum outro ponto do polígono está dentro do triângulo (a, b, c)
            // Usamos uma margem de erro pequena (epsilon) para evitar problemas de precisão numérica
            foreach (int idx in indices)
            {
                if (idx == pIdx || idx == cIdx || idx == nIdx) continue;
                if (IsPointInTriangle(polygon[idx], a, b, c)) return false;
            }

            return true;
        }

        private static bool IsConvex(Point3F a, Point3F b, Point3F c, bool isClockwise)
        {
            // Produto vetorial 2D (z-component)
            double crossProduct = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
            
            // Se for anti-horário (isClockwise = false), crossProduct > 0 é convexo
            // Se for horário (isClockwise = true), crossProduct < 0 é convexo
            if (isClockwise) return crossProduct < -1e-9;
            return crossProduct > 1e-9;
        }

        private static bool IsClockwise(List<Point3F> polygon)
        {
            double area = 0;
            for (int i = 0; i < polygon.Count; i++)
            {
                Point3F p1 = polygon[i];
                Point3F p2 = polygon[(i + 1) % polygon.Count];
                area += (p2.X - p1.X) * (p2.Y + p1.Y);
            }
            // Em coordenadas de tela (Y para baixo), area > 0 é horário. 
            // Em coordenadas cartesianas padrão (CamBam), area > 0 é horário.
            return area > 0;
        }

        private static bool IsPointInTriangle(Point3F p, Point3F a, Point3F b, Point3F c)
        {
            // Coordenadas baricêntricas com tolerância para precisão numérica
            double det = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
            if (Math.Abs(det) < 1e-12) return false; // Triângulo degenerado

            double baryA = ((b.Y - c.Y) * (p.X - c.X) + (c.X - b.X) * (p.Y - c.Y)) / det;
            double baryB = ((c.Y - a.Y) * (p.X - c.X) + (a.X - c.X) * (p.Y - c.Y)) / det;
            double baryC = 1 - baryA - baryB;

            double epsilon = -1e-9;
            return baryA > epsilon && baryB > epsilon && baryC > epsilon;
        }
    }
}
