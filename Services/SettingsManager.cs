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
        //private readonly double defaultSmoothing = 0.025;

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
            public double SamplingStep { get; set; }
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

        // Calculates tolerance and step adaptively based on the object size
        public AdaptiveParameters GetAdaptiveParametersFromGuideCurve(Polyline GuideCurve)
        {
            double diagonal = BoundingBoxDiagonal(GuideCurve); // drawing units
            var config = new CamBamConfig();
            double CambamStepResolution = config.STEPResolution;
            CamBam.ThisApplication.AddLogMessage($"STEPResolution: {CambamStepResolution}.");

            double dpTolerance = Clamp(diagonal * 0.0005, 0.005, CambamStepResolution); // Douglas-Peucker tolerance
            double samplingStep = Clamp(diagonal * 0.001, 0.001, CambamStepResolution/5); // Step size of arc discretization

            return new AdaptiveParameters
            {
                DouglasPeuckerTolerance = dpTolerance,
                SamplingStep = samplingStep
            };
        }

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

        public AdaptiveParameters GetDefaultAdaptiveParameters()
        {
            return new AdaptiveParameters
            {
                DouglasPeuckerTolerance = defaultDouglasPeuckerTolerance,
                SamplingStep = defaultSamplingStep
            };
        }
    }
}