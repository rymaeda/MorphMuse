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
    public class PolylineManager
    {
        public Polyline ClosedPoly { get; private set; }
        public List<Polyline> OpenPoly { get; private set; } = new List<Polyline>(); // ✅ list of polylines
        public List<Polyline> OpenPoly2 { get; private set; } = new List<Polyline>(); // ✅ list of polylines
        public int CounterOpenP { get; private set; }
        public int CounterClosedP { get; private set; }

        private PolylineManager() { } // private default constructor

        public static PolylineManager CreateFromOpenPair(Polyline open1, Polyline open2)
        {
            return new PolylineManager
            {
                OpenPoly = new List<Polyline> { open1 },
                OpenPoly2 = new List<Polyline> { open2 },
                CounterOpenP = 2,
                CounterClosedP = 0
            };
        }

        public static PolylineManager CreateFromOpenAndClosed(Polyline open, Polyline closed)
        {
            return new PolylineManager
            {
                OpenPoly = new List<Polyline> { open },
                ClosedPoly = closed,
                CounterOpenP = 1,
                CounterClosedP = 1
            };
        }
        public static bool TryCreateFromSelection(out PolylineManager manager, out Enum mode)
        {
            manager = null;
            ICADView view = CamBamUI.MainUI.ActiveView;
            mode= null;

            if (view.SelectedEntities.Length == 0)
            {
                MessageBox.Show("No selection.");
                return false;
            }

            List<Polyline> openPolys = new List<Polyline>();
            List<Polyline> closedPolys = new List<Polyline>();

            foreach (Entity ent in view.SelectedEntities)
            {
                if (ent is Polyline poly)
                {
                    if (poly.Closed)
                        closedPolys.Add(poly);
                    else
                        openPolys.Add(poly);
                }
            }

            // Valid case 1: two open polylines
            if (openPolys.Count == 2 && closedPolys.Count == 0)
            {
                manager = CreateFromOpenPair(openPolys[0], openPolys[1]);
                CamBam.ThisApplication.AddLogMessage("[Copilot] Two open polylines selected.");
                mode = SurfaceMode.GeneratrizOpen; // Set mode to indicate two open polylines
                return true;
            }

            // Valid case 2: one open and one closed polyline
            if (openPolys.Count == 1 && closedPolys.Count == 1)
            {
                manager = CreateFromOpenAndClosed(openPolys[0], closedPolys[0]);
                CamBam.ThisApplication.AddLogMessage("[Copilot] One open and one closed polyline selected.");
                mode = SurfaceMode.GeneratrizClosed;
                return true;
            }

            MessageBox.Show("Please select either:\n- Two open polylines\n- One open and one closed polyline.");
            mode = null;
            return false;
        }
        public static bool ValidateSelection(out PolylineManager selectionManager)
        {
            if (!PolylineManager.TryCreateFromSelection(out selectionManager, out Enum mode))
            {
                MessageBox.Show("Select at least one open polyline and one closed polyline\n" +
                                "OR two open polylines.");
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
