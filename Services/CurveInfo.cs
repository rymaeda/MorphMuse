using CamBam.CAD;

namespace MorphMuse.Services
{
    /// <summary>
    /// Wrapper class to preserve original entity information (ID, Type) 
    /// even after conversion to Polyline
    /// </summary>
    public class CurveInfo
    {
        /// <summary>
        /// The polyline (possibly converted from another entity type)
        /// </summary>
        public Polyline Polyline { get; set; }

        /// <summary>
        /// Original entity ID (preserved from source entity)
        /// </summary>
        public int OriginalId { get; set; }

        /// <summary>
        /// Original entity type (e.g., "Spline", "Circle", "Arc", "Polyline")
        /// </summary>
        public string OriginalType { get; set; }

        public CurveInfo(Polyline polyline, int originalId, string originalType)
        {
            Polyline = polyline;
            OriginalId = originalId;
            OriginalType = originalType;
        }

        /// <summary>
        /// Gets a concise identification string matching CamBam tree view format
        /// Format: "Type (ID: xxx)"
        /// </summary>
        public string GetIdentification()
        {
            return $"{OriginalType} (ID: {OriginalId})";
        }
    }
}
