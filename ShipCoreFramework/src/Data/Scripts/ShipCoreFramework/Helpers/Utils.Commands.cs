using System;
using System.Globalization;
using VRageMath;

namespace ShipCoreFramework
{
    internal static partial class Utils
    {
        internal static bool TryParseGpsFromArgs(string[] args, out Vector3D position)
        {
            position = new Vector3D();
            if (args == null || args.Length == 0) return false;

            var joined = string.Join(" ", args);
            var idx = joined.IndexOf("GPS:", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return false;

            var tail = joined.Substring(idx);
            var parts = tail.Split(':');
            if (parts.Length < 5) return false;

            double x;
            double y;
            double z;
            if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out x)) return false;
            if (!double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out y)) return false;
            if (!double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out z)) return false;

            position = new Vector3D(x, y, z);
            return true;
        }
    }
}
