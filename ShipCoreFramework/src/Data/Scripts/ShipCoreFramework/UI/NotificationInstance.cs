using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    public static class NotificationInstance
    {
        private static IMyHudNotification _notification;
        private static int _baseMs;
        private static string _font;

        public static void ShowNotification(string text, int ms = 1000, string font = "Red")
        {
            if (_notification == null || _font != font || _baseMs != ms)
            {
                _notification = MyAPIGateway.Utilities.CreateNotification(text, ms, font);
                _font = font;
                _baseMs = ms;
            }
            
            _notification.Text = text;
            
            _notification.ResetAliveTime();
            _notification.Hide();
            _notification.Show();
        }

        public static void Hide()
        {
            _notification?.Hide();
        }
    }
}