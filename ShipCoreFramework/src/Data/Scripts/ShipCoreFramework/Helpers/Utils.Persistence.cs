using System;
using System.Text;
using Sandbox.ModAPI;

namespace ShipCoreFramework
{
    internal static partial class Utils
    {
        internal static bool TryLoadFromSandbox<T>(string keyName, out T item)
        {
            string savedBlobB64;
            if (!MyAPIGateway.Utilities.GetVariable(keyName, out savedBlobB64) || string.IsNullOrWhiteSpace(savedBlobB64))
            {
                item = default(T);
                return false;
            }

            item = MyAPIGateway.Utilities.SerializeFromXML<T>(
                Encoding.UTF8.GetString(Convert.FromBase64String(savedBlobB64)));
            return true;
        }
    }
}
