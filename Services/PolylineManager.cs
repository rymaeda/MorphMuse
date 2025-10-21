using CamBam.CAD;
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
        public int CounterOpenP { get; private set; }
        public int CounterClosedP { get; private set; }

        public PolylineManager(Polyline closed, Polyline open)
        {
            ClosedPoly = closed;
            OpenPoly = open;
        }

        public static bool TryCreateFromSelection(out PolylineManager manager)
        {
            manager = null;

            GetPolylinesFromSelection(out List<Polyline> closedPolys, out List<Polyline> openPolys);

            int closedCount = closedPolys.Count;
            int openCount = openPolys.Count;

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
                CamBam.ThisApplication.AddLogMessage($"Open Polyline: X-Offset={MaxOpenPolyAmplitude:F4}");

                float MaxNegativeOffset = FindMaxSafeNegativeOffsetBinarySearch(closed);
                CamBam.ThisApplication.AddLogMessage($"Closed Polyline: MaxOffset={MaxNegativeOffset:F4}");

                if (MaxOpenPolyAmplitude < MaxNegativeOffset)
                {
                    MessageBox.Show(TextTranslation.Translate(
                        $"Warning: The open polyline's effective amplitude along the X axis\nexceeds the maximum negative offset of the closed polyline ({MaxNegativeOffset:F4}).\nThis plugin can't deal with this yet."));
                    return false;
                }

                return true;
            }
            else if (closedCount == 1 && openCount == 2)
            {
                MessageBox.Show(TextTranslation.Translate("Selected one closed Polyline and two and just two open Polylines."));
            }
            else if (closedCount == 0 && openCount == 2)
            {
                MessageBox.Show(TextTranslation.Translate("Selected two and just two open Polylines."));
                return false;
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
                if (ent == null)
                    continue;

                Entity clone = (Entity)ent.Clone(); // Clonagem segura via Entity

                switch (clone)
                {
                    case Polyline poly:
                        if (poly.Closed) closedPolys.Add(poly);
                        else openPolys.Add(poly);
                        break;

                    case Circle circle:
                        {
                            var poly = circle.ToPolyline();
                            if (poly != null) closedPolys.Add(poly);
                        }
                        break;

                    case Arc arc:
                        {
                            var poly = arc.ToPolyline();
                            if (poly != null) openPolys.Add(poly);
                        }
                        break;

                    case Line line:
                        {
                            var poly = line.ToPolyline();
                            if (poly != null) openPolys.Add(poly);
                        }
                        break;

                    case Spline spline:
                        {
                            var poly = spline.ToPolyline(0.01); // tolerância ajustável
                            if (poly != null) openPolys.Add(poly);
                        }
                        continue; // já tratou Region, pula para o próximo
                }
            }
        }
        public static bool ValidateSelection(out PolylineManager selectionManager)
        {
            if (!PolylineManager.TryCreateFromSelection(out selectionManager))
                return false;
            return true;
        }

        public static float FindMaxSafeNegativeOffsetBinarySearch(Polyline closedBase, float tolerance = 0.01f)
        {
            SizeF amplitude = GetAmplitudeXY(closedBase);
            float minOffset = -Math.Min(amplitude.Width, amplitude.Height); // limite inferior
            float maxOffset = 0f;
            float safeOffset = 0f;

            int iteration = 0;

            while (Math.Abs(maxOffset - minOffset) > tolerance)
            {
                iteration++;
                float mid = (minOffset + maxOffset) / 2f;
                Polyline[] offsetResult = closedBase.CreateOffsetPolyline(mid, 0.01f);

                bool isValid = offsetResult != null &&
                               offsetResult.Length == 1 &&
                               offsetResult[0].Points.Count >= 3;

                if (isValid)// Binary search criterion
                {
                    safeOffset = mid; // update safe offset
                    maxOffset = mid; // update upper bound
                }
                else
                {
                    minOffset = mid; // update lower bound
                }
            }
            return safeOffset;
        }

        // Method to get the amplitude along X and Y axis for the closed polyline
        public static SizeF GetAmplitudeXY(Polyline polyline)
        {
            PointF min = new PointF();
            PointF max = new PointF();
            polyline.GetExtents(ref min, ref max);
            return new SizeF(max.X - min.X, max.Y - min.Y);
        }

        // Method to get the effective amplitude along X axis for the open polyline
        public static float GetOpenPolyEffectiveAmplitudeX(Polyline poly)
        {
            if (poly == null || poly.Points.Count < 2)
                return 0; // or throw an exception

            float xStart = (float)poly.Points[0].Point.X;
            float xEnd = (float)poly.Points[poly.Points.Count - 1].Point.X;
            return (xEnd - xStart); 
        }
    }
}
