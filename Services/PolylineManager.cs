using CamBam.CAD;
using CamBam.Geom;
using CamBam.UI;
using CamBam.Util;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace MorphMuse.Services
{
    internal class PolylineManager
    {
        public Polyline ClosedPoly { get; private set; }
        public Polyline OpenPoly { get; private set; }
        public List<Polyline> SelectedOpenPolys { get; private set; } // Nova lista para múltiplas polilinhas abertas
        public int CounterOpenP { get; private set; }
        public int CounterClosedP { get; private set; }

        public PolylineManager(Polyline closed, Polyline open)
        {
            ClosedPoly = closed;
            OpenPoly = open;
            SelectedOpenPolys = new List<Polyline>();
            if (open != null) SelectedOpenPolys.Add(open);
        }

        public PolylineManager(List<Polyline> openPolys)
        {
            SelectedOpenPolys = openPolys;
            CounterOpenP = openPolys.Count;
            CounterClosedP = 0;
        }

        public static bool TryCreateFromSelection(out PolylineManager manager)
        {
            manager = null;

            GetPolylinesFromSelection(out List<Polyline> closedPolys, out List<Polyline> openPolys);

            int closedCount = closedPolys.Count;
            int openCount = openPolys.Count;

            // Cenário A: 1 Fechada + 1 Aberta (Volume com tampa)
            if (closedCount == 1 && openCount == 1)
            {
                Polyline closed = closedPolys[0];
                Polyline open = openPolys[0];

                manager = new PolylineManager(closed, open)
                {
                    CounterClosedP = closedCount,
                    CounterOpenP = openCount
                };

                float MaxOpenPolyAmplitude = GetOpenPolyEffectiveAmplitudeX(open);
                float MaxNegativeOffset = FindMaxSafeNegativeOffsetBinarySearch(closed);

                if (MaxOpenPolyAmplitude < MaxNegativeOffset)
                {
                    MessageBox.Show(TextTranslation.Translate(
                        $"Warning: The open polyline's effective amplitude along the X axis\nexceeds the maximum negative offset of the closed polyline ({MaxNegativeOffset:F4}).\nThis plugin can't deal with this yet."));
                    return false;
                }

                return true;
            }
            // Cenário B: 2 Abertas (Superfície lateral apenas)
            else if (closedCount == 0 && openCount == 2)
            {
                manager = new PolylineManager(openPolys);
                return true;
            }
            else
            {
                MessageBox.Show(TextTranslation.Translate(
                    "Invalid Selection. Please select either:\n1. One closed and one open polyline (for volume with cap)\n2. Two open polylines (for surface only)"),
                    "Invalid Selection",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            return false;
        }

        public static void GetPolylinesFromSelection(out List<Polyline> closedPolys, out List<Polyline> openPolys)
        {
            closedPolys = new List<Polyline>();
            openPolys = new List<Polyline>();

            ICADView view = CamBamUI.MainUI.ActiveView;

            foreach (object entObj in view.SelectedEntities)
            {
                Entity ent = entObj as Entity;
                if (ent == null) continue;

                Entity clone = (Entity)ent.Clone();

                switch (clone)
                {
                    case Polyline poly:
                        if (poly.CanConvertToPolylines == true)
                            poly = poly.ConvertToPolylines(true)[0];
                        if (poly.Closed) closedPolys.Add(poly);
                        else openPolys.Add(poly);
                        break;

                    case Circle circle:
                        var polyCircle = circle.ToPolyline();
                        if (polyCircle != null) closedPolys.Add(polyCircle);
                        break;

                    case Arc arc:
                        var polyArc = arc.ToPolyline();
                        if (polyArc != null) openPolys.Add(polyArc);
                        break;

                    case Line line:
                        var polyLine = line.ToPolyline();
                        if (polyLine != null) openPolys.Add(polyLine);
                        break;

                    case Spline spline:
                        var polySpline = spline.ToPolyline(0.01);
                        if (polySpline.Closed) closedPolys.Add(polySpline);
                        else openPolys.Add(polySpline);
                        break;
                }
            }
        }

        public static bool ValidateSelection(out PolylineManager selectionManager)
        {
            return TryCreateFromSelection(out selectionManager);
        }

        public static float FindMaxSafeNegativeOffsetBinarySearch(Polyline closedBase, float tolerance = 0.01f)
        {
            SizeF amplitude = GetAmplitudeXY(closedBase);
            float minOffset = -Math.Min(amplitude.Width, amplitude.Height);
            float maxOffset = 0f;
            float safeOffset = 0f;

            while (Math.Abs(maxOffset - minOffset) > tolerance)
            {
                float mid = (minOffset + maxOffset) / 2f;
                Polyline[] offsetResult = closedBase.CreateOffsetPolyline(mid, 0.01f);

                bool isValid = offsetResult != null &&
                               offsetResult.Length == 1 &&
                               offsetResult[0].Points.Count >= 3;

                if (isValid)
                {
                    safeOffset = mid;
                    maxOffset = mid;
                }
                else
                {
                    minOffset = mid;
                }
            }
            return safeOffset;
        }

        public static SizeF GetAmplitudeXY(Polyline polyline)
        {
            PointF min = new PointF();
            PointF max = new PointF();
            polyline.GetExtents(ref min, ref max);
            return new SizeF(max.X - min.X, max.Y - min.Y);
        }

        public static float GetOpenPolyEffectiveAmplitudeX(Polyline poly)
        {
            if (poly == null || poly.Points.Count < 2) return 0;
            float xStart = (float)poly.Points[0].Point.X;
            float xEnd = (float)poly.Points[poly.Points.Count - 1].Point.X;
            return (xEnd - xStart);
        }
    }
}
