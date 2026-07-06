using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    internal static class NotificationInstance
    {
        private static IMyHudNotification _notification;
        private static int _baseMs;
        private static string _font;
        private static string _countdownKey;
        private static string _countdownText;
        private static string _countdownFont;
        private static int _countdownEndTick;
        private static int _lastCountdownSecond = -1;

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

        public static void StartCountdown(string key, string text, int seconds, string font = "Red")
        {
            if (string.IsNullOrWhiteSpace(key) || seconds <= 0) return;

            _countdownKey = key;
            _countdownText = string.IsNullOrWhiteSpace(text) ? "Countdown" : text;
            _countdownFont = string.IsNullOrWhiteSpace(font) ? "Red" : font;
            _countdownEndTick = Session.CurrentTick + (seconds * 60);
            _lastCountdownSecond = -1;
            RunCountdownTick();
        }

        public static void CancelCountdown(string key)
        {
            if (!string.IsNullOrWhiteSpace(key) && !string.Equals(_countdownKey, key))
                return;

            _countdownKey = null;
            _countdownText = null;
            _countdownFont = null;
            _countdownEndTick = 0;
            _lastCountdownSecond = -1;
            Hide();
        }

        public static void RunCountdownTick()
        {
            if (string.IsNullOrWhiteSpace(_countdownKey)) return;

            var ticksRemaining = _countdownEndTick - Session.CurrentTick;
            if (ticksRemaining <= 0)
            {
                CancelCountdown(_countdownKey);
                return;
            }

            var secondsRemaining = (ticksRemaining + 59) / 60;
            if (secondsRemaining == _lastCountdownSecond) return;

            _lastCountdownSecond = secondsRemaining;
            ShowNotification(_countdownText + " " + secondsRemaining + "s", 1200, _countdownFont);
        }

        public static void Hide()
        {
            _notification?.Hide();
        }
    }
}
