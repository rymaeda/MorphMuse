using CamBam.CAD;
using CamBam.UI;
using CamBam.Util;
using CamBam.Geom;
using System;
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

        private PolylineManager(Polyline closed, Polyline open)
        {
            ClosedPoly = closed;
            OpenPoly = open;
        }

        public static bool TryCreateFromSelection(out PolylineManager manager)
        {
            manager = null;
            ICADView view = CamBamUI.MainUI.ActiveView;

            if (view.SelectedEntities.Length == 0)
            {
                MessageBox.Show(TextTranslation.Translate("No selection."));
                return false;
            }

            Polyline closed = null;
            Polyline open = null;
            int closedCount = 0;
            int openCount = 0;

            foreach (Entity ent in view.SelectedEntities)
            {
                if (ent is Polyline poly)
                {
                    if (poly.Closed)
                    {
                        closed = poly;
                        closedCount++;
                    }
                    else
                    {
                        open = poly;
                        openCount++;
                    }
                }
            }

            if (closedCount == 1 && openCount == 1)
            {
                manager = new PolylineManager(closed, open)
                {
                    CounterClosedP = closedCount,
                    CounterOpenP = openCount
                };
                float MaxOpenPolyAmplitude= GetOpenPolyEffectiveAmplitudeX(open);
                CamBam.ThisApplication.AddLogMessage($"Open Polyline: X-Offset={MaxOpenPolyAmplitude:F4}");
                float MaxNegativeOffset = FindMaxSafeNegativeOffsetBinarySearch(closed);
                CamBam.ThisApplication.AddLogMessage($"Closed Polyline: MaxOffset={MaxNegativeOffset:F4}");
                if (MaxOpenPolyAmplitude < MaxNegativeOffset)
                {
                    MessageBox.Show(TextTranslation.Translate("Warning: The open polyline's effective amplitude along the X axis\nexceeds the maximum negative offset of the closed polyline ({.\nThis plugin can't deal this yet."));
                    return false;
                }
                return true;
            }

            //MessageBox.Show(TextTranslation.Translate("Please, select one and just one closed Polyline and\none and just one open Polyline."));
            return false;
        }
        public static bool ValidateSelection(out PolylineManager selectionManager)
        {
            if (!PolylineManager.TryCreateFromSelection(out selectionManager))
            {
                MessageBox.Show("Select at least one open polyline and one closed polyline.");
                return false;
            }
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
