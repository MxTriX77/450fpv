using UnityEngine;

namespace Fpv450.UI
{
    public enum MenuChoice { None, Train, Calibrate, Explore, Exit }

    /// <summary>
    /// The main menu (APP-02, PRD-05): Train, Calibrate and Exit, and Explore in a review build. The name and the drone
    /// at the left, the items under them, and a quiet line at the foot saying which radio is in.
    /// </summary>
    public static class MenuScreen
    {
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
            if (Item(new Rect(x - AppStyle.S(28), y, w, h), "Train")) choice = MenuChoice.Train;
            y += h;
            if (Item(new Rect(x - AppStyle.S(28), y, w, h), "Calibrate")) choice = MenuChoice.Calibrate;
            y += h;
            if (review)
            {
                if (Item(new Rect(x - AppStyle.S(28), y, w, h), "Explore")) choice = MenuChoice.Explore;
                y += h;
            }
            if (Item(new Rect(x - AppStyle.S(28), y, w, h), "Exit")) choice = MenuChoice.Exit;

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
