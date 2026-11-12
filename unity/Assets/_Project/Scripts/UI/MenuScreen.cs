using UnityEngine;

namespace Fpv450.UI
{
    public enum MenuChoice { None, Play, Calibrate, Explore, Exit }

    /// <summary>
    /// The main menu (APP-02, PRD-05, D-100): Play, Calibrate and Exit, and Explore in a review build. The name and the drone
    /// at the left, the items under them, and a quiet line at the foot saying which radio is in.
    /// </summary>
    public static class MenuScreen
    {
        /// <summary>The items in their order, each with its quiet hint (null: none): Play opens the carousel of House and
        /// Village (w10-play-maps 1.1), Explore the farmland, Village, in noclip.</summary>
        public static (MenuChoice Choice, string Name, string Hint)[] Items(bool review) => review
            ? new (MenuChoice, string, string)[] { (MenuChoice.Play, "Play", PlayHint), (MenuChoice.Calibrate, "Calibrate", null), (MenuChoice.Explore, "Explore", "Village in noclip"), (MenuChoice.Exit, "Exit", null) }
            : new (MenuChoice, string, string)[] { (MenuChoice.Play, "Play", PlayHint), (MenuChoice.Calibrate, "Calibrate", null), (MenuChoice.Exit, "Exit", null) };

        const string PlayHint = "House or Village";

        public static MenuChoice Draw(bool review, string radio, string version)
        {
            AppStyle.Begin();
            AppStyle.DrawGround();
            float x = AppStyle.S(150), w = AppStyle.S(560);
            float y = Mathf.Round(Screen.height * 0.24f);
            GUI.Label(new Rect(x, y, Screen.width - x, AppStyle.S(80)), "450FPV", AppStyle.Title);
            y += AppStyle.S(82);
            AppStyle.Fill(new Rect(x + AppStyle.S(4), y, AppStyle.S(64), AppStyle.S(3)), AppStyle.Accent);
            y += AppStyle.S(16);
            GUI.Label(new Rect(x, y, Screen.width - x, AppStyle.S(30)), "Svinoriz 10 Opto · heavy fiber-optic cargo FPV", AppStyle.Label);
            y += AppStyle.S(84);

            MenuChoice choice = MenuChoice.None;
            float h = AppStyle.S(64);
            foreach ((MenuChoice item, string name, string hint) in Items(review))
            {
                if (Item(new Rect(x - AppStyle.S(28), y, w, h), name, hint)) choice = item;
                y += h;
            }

            float foot = Screen.height - AppStyle.S(64);
            GUI.Label(new Rect(x, foot, Screen.width * 0.6f, AppStyle.S(30)), radio, AppStyle.Small);
            var right = new GUIStyle(AppStyle.Small) { alignment = TextAnchor.UpperRight };
            GUI.Label(new Rect(Screen.width * 0.5f, foot, Screen.width * 0.5f - x, AppStyle.S(30)), version, right);
            return choice;
        }

        static bool Item(Rect r, string text, string hint = null)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            if (hover) AppStyle.Fill(new Rect(r.x, r.y + AppStyle.S(14), AppStyle.S(4), r.height - AppStyle.S(28)), AppStyle.Accent);
            bool pressed = GUI.Button(r, text, AppStyle.MenuItem);
            // The hint in a quiet column after the longest item, on the item's line.
            if (hint != null) GUI.Label(new Rect(r.x + AppStyle.S(210), r.y, Screen.width - r.x - AppStyle.S(210), r.height), hint, AppStyle.Label);
            return pressed;
        }
    }
}
