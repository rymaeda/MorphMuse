using PluginSettings;
using System;

namespace MorphMuse.Services
{
    // Pure data class for configuration
    public class SettingsProfile
    {
        public string Name { get; set; } = "default";
        public SettingsManager.Units Units { get; set; } = SettingsManager.Units.Millimeters;
        public float Tolerance { get; set; } = 0.01f;
        public float DouglasPeuckerTolerance { get; set; } = 0.001f;
        public float SamplingStepClosedPoly { get; set; } = 0.05f;
        public float OffsetStep { get; set; } = 0.5f;
        public float BaseDensity { get; set; } = 0.5f;
        public float MinimumSamplingStep { get; set; } = 0.1f;
        public float DegeneracyThreshold { get; set; } = 1e-6f;
        public float VectorLengthThreshold { get; set; } = 1e-8f;
        public int MaxLogPoints { get; set; } = 5;
        public int MinimumCurveLengthForAlignment { get; set; } = 3;
        public bool UseAdvancedMode { get; set; } = false;
    }
}
