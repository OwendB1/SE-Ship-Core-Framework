namespace ShipCoreFramework
{
    internal static class TextUtils
    {
        internal const float CharWidth = 20;
        internal const float BaseLineHeight = 30f;

        internal static float GetLineHeight(float scale = 1f)
        {
            return BaseLineHeight * scale;
        }

        internal static float GetTextWidth(string text, float scale = 1f)
        {
            return text.Length * CharWidth * scale;
        }

        internal static float GetTextHeight(string text, float scale = 1f)
        {
            return NumLines(text) * GetLineHeight(scale);
        }

        private static int NumLines(string text)
        {
            var charDiff = text.Length - text.Replace("\n", string.Empty).Length;
            return charDiff + 1;
        }
    }
}
