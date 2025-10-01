using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    public static class NotificationInstance
    {
        private static IMyHudNotification _noot;

        public static void ShowNotification(string text, int ms = 1000, string font = "Red")
        {
            if (_noot == null) _noot = MyAPIGateway.Utilities.CreateNotification("", ms, font);
            if (_noot.Text == text) return;
            
            _noot.Text = text;
            _noot.AliveTime = ms;   // reset lifetime
            _noot.Show();           // redraws the SAME notification line
        }

        public static void HideNfzWarning()
        {
            _noot?.Hide();
        }
    }
}