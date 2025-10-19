using CamBam;
using CamBam.CAD;
using CamBam.Geom;
using CamBam.UI;
using MorphMuse.Services;
using System;
using System.Collections.Generic;

namespace PluginSettings
{
    public class SettingsManager
    {
        public enum Units
        {
            Millimeters = 0,
            Inches = 1,
            Centimeters = 2,
            Meters = 3,
            Thousandths = 4,
            Unknown = -1
        }

        public Units CurrentUnits { get; private set; } = Units.Millimeters;

        // Default values stored in millimeters
        private readonly double defaultDouglasPeuckerTolerance = 0.001;
        private readonly double defaultSamplingStep = 0.005;

        // Get current units from CamBam
        public static Units GetUnits()
        {
            try
            {
                return (Units)CamBamUI.MainUI.ActiveView.CADFile.DrawingUnits;
            }
            catch
            {
                return Units.Unknown;
            }
        }

        public class AdaptiveParameters
        {
            public double DouglasPeuckerTolerance { get; set; }
            public double SamplingStepClosedPoly { get; set; }
        }

        // Calculates the diagonal of the bounding box for the guide curve using Polyline.GetExtrema
        public double BoundingBoxDiagonal(Polyline guideCurve)
        {
            Point3F min = new Point3F();
            Point3F max = new Point3F();

            // Use Polyline's GetExtrema method to find the bounding box corners
            guideCurve.GetExtrema(ref min, ref max);

            // Calculate the diagonal distance between min and max points
            double diagonal = Point3F.Distance(min, max);
            return diagonal; // drawing units
        }

        public AdaptiveParameters GetSmartAdaptiveParameters(Polyline guideCurve)
        {
            double diagonal = BoundingBoxDiagonal(guideCurve); // units of the drawing
            double diagonalMm = ConvertToMillimeters(diagonal, GetUnits());
            double stepResolution = CamBamConfig.Defaults.STEPResolution;

            // Normalized scale from 0.0 to 1.0
            // Values above this will use the maximum tolerances
            double scale = Math.Min(1.0, diagonalMm / 1000.0);

            // Between these ranges
            double dpMin = 0.01;
            double dpMax = 0.3;
            double stepMin = 0.2;
            double stepMax = 2.0;

            double dpTolerance = Lerp(dpMin, dpMax, scale);
            double samplingStep = Lerp(stepMin, stepMax, scale);

            // Clamping with cambam STEPResolution
            dpTolerance = Clamp(dpTolerance, stepResolution / 10, stepResolution);
            samplingStep = Clamp(samplingStep, stepResolution, 10 * stepResolution);

            return new AdaptiveParameters
            {
                DouglasPeuckerTolerance = dpTolerance,
                SamplingStepClosedPoly = samplingStep
            };
        }

        // Interpolates linearly between min and max based on t (0.0 to 1.0)
        private double Lerp(double min, double max, double t)
        {
            return min + (max - min) * t;
        }
        // Calculates tolerance and step adaptively based on the object size

        // Utility function to clamp values
        public static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public static double ConvertFromMillimeters(double valueInMm, Units targetUnits)
        {
            switch (targetUnits)
            {
                case Units.Inches:
                    return valueInMm / 25.4;
                case Units.Centimeters:
                    return valueInMm / 10.0;
                case Units.Meters:
                    return valueInMm / 1000.0;
                default: // Millimeters or unknown
                    return valueInMm;
            }
        }

        public static double ConvertToMillimeters(double value, Units sourceUnits)
        {
            switch (sourceUnits)
            {
                case Units.Inches:
                    return value * 25.4;
                case Units.Centimeters:
                    return value * 10.0;
                case Units.Meters:
                    return value * 1000.0;
                default:
                    return value; // already in mm
            }
        }

        public AdaptiveParameters GetDefaultAdaptiveParameters()
        {
            return new AdaptiveParameters
            {
                DouglasPeuckerTolerance = defaultDouglasPeuckerTolerance,
                SamplingStepClosedPoly = defaultSamplingStep
            };
        }
    }
}