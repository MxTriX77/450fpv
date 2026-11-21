using UnityEngine;

namespace Fpv450.UI
{
    public enum MenuChoice { None, Play, Calibrate, Exit }

    /// <summary>
    /// The main menu (APP-02, PRD-05, D-100): Play, Calibrate and Exit in every build (w18-menu-cleanup: no Explore). The
    /// name and the drone's name at the left, the items under them, and a quiet line at the foot saying which radio is in.
    /// No text describes an item (w18-menu-cleanup).
    /// </summary>
    public static class MenuScreen
    {
        /// <summary>The items in their order, each by its name alone.</summary>
        public static (MenuChoice Choice, string Name)[] Items() =>
            new (MenuChoice, string)[] { (MenuChoice.Play, "Play"), (MenuChoice.Calibrate, "Calibrate"), (MenuChoice.Exit, "Exit") };

        /// <summary>The line under the title: the drone's name.</summary>
        public const string Subtitle = "Svinoriz 10 Opto";

        public static MenuChoice Draw(string radio, string version)
        {
            AppStyle.Begin();
            AppStyle.DrawGround();
            float x = AppStyle.S(150), w = AppStyle.S(560);
            float y = Mathf.Round(Screen.height * 0.24f);
            GUI.Label(new Rect(x, y, Screen.width - x, AppStyle.S(80)), "450FPV", AppStyle.Title);
            y += AppStyle.S(82);
            AppStyle.Fill(new Rect(x + AppStyle.S(4), y, AppStyle.S(64), AppStyle.S(3)), AppStyle.Accent);
            y += AppStyle.S(16);
            GUI.Label(new Rect(x, y, Screen.width - x, AppStyle.S(30)), Subtitle, AppStyle.Label);
            y += AppStyle.S(84);

            MenuChoice choice = MenuChoice.None;
            float h = AppStyle.S(64);
            foreach ((MenuChoice item, string name) in Items())
            {
                if (Item(new Rect(x - AppStyle.S(28), y, w, h), name)) choice = item;
                y += h;
            }

            float foot = Screen.height - AppStyle.S(64);
            GUI.Label(new Rect(x, foot, Screen.width * 0.6f, AppStyle.S(30)), radio, AppStyle.Small);
            var right = new GUIStyle(AppStyle.Small) { alignment = TextAnchor.UpperRight };
            GUI.Label(new Rect(Screen.width * 0.5f, foot, Screen.width * 0.5f - x, AppStyle.S(30)), version, right);
            return choice;
        }

        static bool Item(Rect r, string text)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            if (hover) AppStyle.Fill(new Rect(r.x, r.y + AppStyle.S(14), AppStyle.S(4), r.height - AppStyle.S(28)), AppStyle.Accent);
            return GUI.Button(r, text, AppStyle.MenuItem);
        }
    }
}
