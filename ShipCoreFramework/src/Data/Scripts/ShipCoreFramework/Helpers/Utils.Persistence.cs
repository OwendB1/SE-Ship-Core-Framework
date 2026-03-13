using System;
using System.Text;
using Sandbox.ModAPI;

namespace ShipCoreFramework
{
    internal static partial class Utils
    {
        internal static T LoadFromSandbox<T>(string keyName)
        {
            string savedBlobB64;
            MyAPIGateway.Utilities.GetVariable(keyName, out savedBlobB64);
            return string.IsNullOrWhiteSpace(savedBlobB64)
                ? default(T)
                : MyAPIGateway.Utilities.SerializeFromXML<T>(
                    Encoding.UTF8.GetString(Convert.FromBase64String(savedBlobB64)));
        }

        internal static void SaveToSandbox<T>(string keyName, T item)
        {
            if (item == null) return;
            var encodedCore = Encoding.UTF8.GetBytes(MyAPIGateway.Utilities.SerializeToXML(item));
            MyAPIGateway.Utilities.SetVariable(keyName, Convert.ToBase64String(encodedCore));
        }
    }
}
