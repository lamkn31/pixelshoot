using TMPro;

namespace Wayfu.Lamkn
{
    public static class Utils
    {
        #region TMP Text

        public static void SetTextSafe(TMP_Text tmp, string value)
        {
            if (tmp == null) return;
            if (tmp.text == value) return;
            tmp.SetText(value);
        }

        public static void SetTextSafe(TMP_Text tmp, int value)
        {
            if (tmp == null) return;
            string s = value.ToString();
            if (tmp.text == s) return;
            tmp.SetText(s);
        }

        #endregion
    }
}
