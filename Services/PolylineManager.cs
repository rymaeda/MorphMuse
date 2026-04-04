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
        public Polyline OpenRailPoly { get; private set; }
        public Polyline OpenFormPoly { get; private set; }
        public int CounterOpenP { get; private set; }
        public int CounterClosedP { get; private set; }

        public PolylineManager(Polyline closed, Polyline open)
        {
            ClosedPoly = closed;
            OpenPoly = open;
        }

        public PolylineManager(Polyline openRail, Polyline openForm, bool isTwoOpen)
        {
            OpenRailPoly = openRail;
            OpenFormPoly = openForm;
            OpenPoly = openForm; // For compatibility with existing code that expects OpenPoly as the form
        }

        public static bool TryCreateFromSelection(out PolylineManager manager)
        {
            manager = null;

            GetCurveInfoFromSelection(out List<CurveInfo> closedCurves, out List<CurveInfo> openCurves);

            int closedCount = closedCurves.Count;
            int openCount = openCurves.Count;

            if (closedCount == 1 && openCount == 1)
            {
                Polyline closed = closedCurves[0].Polyline;
                Polyline open = openCurves[0].Polyline;

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
                using (var dialog = new CurveSelectionDialog(openCurves))
                {
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        manager = new PolylineManager(dialog.SelectedRail.Polyline, dialog.SelectedForm.Polyline, true)
                        {
                            CounterClosedP = closedCount,
                            CounterOpenP = openCount
                        };
                        return true;
                    }
                }
                return false;
            }

            return false;
        }

        public static void GetPolylinesFromSelection(out List<Polyline> closedPolys, out List<Polyline> openPolys)
        {
            closedPolys = new List<Polyline>();
            openPolys = new List<Polyline>();
        }

        public static void GetCurveInfoFromSelection(out List<CurveInfo> closedCurves, out List<CurveInfo> openCurves)
        {
            closedCurves = new List<CurveInfo>();
            openCurves = new List<CurveInfo>();

            ICADView view = CamBamUI.MainUI.ActiveView;

            foreach (object entObj in view.SelectedEntities)
            {
                Entity ent = entObj as Entity;
                if (ent == null)
                    continue;

                // Work with the original entity, not a clone, to preserve ID and properties
                switch (ent)
                {
                    case Polyline poly:
                        {
                            int originalId = poly.ID;
                            string originalType = poly.PrimitiveType;
                            CamBam.ThisApplication.AddLogMessage($"Found Polyline: ID={originalId}, Type={originalType}, Closed={poly.Closed}, Points={poly.Points.Count}");
                            
                            if (poly.CanConvertToPolylines == true)
                            {
                                var converted = poly.ConvertToPolylines(true);
                                if (converted != null && converted.Length > 0)
                                    poly = converted[0];
                            }
                            
                            var curveInfo = new CurveInfo(poly, originalId, originalType);
                            if (poly.Closed) closedCurves.Add(curveInfo);
                            else openCurves.Add(curveInfo);
                        }
                        break;

                    case Circle circle:
                        {
                            int originalId = circle.ID;
                            string originalType = circle.PrimitiveType;
                            CamBam.ThisApplication.AddLogMessage($"Found Circle: ID={originalId}, Type={originalType}");
                            var poly = circle.ToPolyline();
                            if (poly != null)
                            {
                                var curveInfo = new CurveInfo(poly, originalId, originalType);
                                closedCurves.Add(curveInfo);
                            }
                        }
                        break;

                    case Arc arc:
                        {
                            int originalId = arc.ID;
                            string originalType = arc.PrimitiveType;
                            CamBam.ThisApplication.AddLogMessage($"Found Arc: ID={originalId}, Type={originalType}");
                            var poly = arc.ToPolyline();
                            if (poly != null)
                            {
                                var curveInfo = new CurveInfo(poly, originalId, originalType);
                                openCurves.Add(curveInfo);
                            }
                        }
                        break;

                    case Line line:
                        {
                            int originalId = line.ID;
                            string originalType = line.PrimitiveType;
                            CamBam.ThisApplication.AddLogMessage($"Found Line: ID={originalId}, Type={originalType}");
                            var poly = line.ToPolyline();
                            if (poly != null)
                            {
                                var curveInfo = new CurveInfo(poly, originalId, originalType);
                                openCurves.Add(curveInfo);
                            }
                        }
                        break;

                    case Spline spline:
                        {
                            int originalId = spline.ID;
                            string originalType = spline.PrimitiveType;
                            CamBam.ThisApplication.AddLogMessage($"Found Spline: ID={originalId}, Type={originalType}");
                            var poly = spline.ToPolyline(0.01); // tolerancia ajustavel
                            if (poly != null)
                            {
                                var curveInfo = new CurveInfo(poly, originalId, originalType);
                                if (poly.Closed) closedCurves.Add(curveInfo);
                                else openCurves.Add(curveInfo);
                            }
                        }
                        break;
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
