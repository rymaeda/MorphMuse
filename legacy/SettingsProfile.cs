using PluginSettings;

namespace MorphMuse.model
{
    public class SettingsProfile
    {
        public string Name { get; set; } = "default";
        public SettingsManager.Units Units { get; set; } = SettingsManager.Units.Millimeters;
        public float Tolerance { get; set; } = 0.01f;
        public float OffsetStep { get; set; } = 0.5f;
        public bool UseAdvancedMode { get; set; } = false;
    }
}