using System.Collections.Generic;
using UnityEngine;

namespace Fpv450.UI
{
    public enum PauseChoice { None, Resume, Restart, End }
    public enum SummaryChoice { None, FlyAgain, Setup, Menu }

    /// <summary>
    /// What the app draws over the flight (APP-04), never through the feed: a one-line hint at the foot while the
    /// pilot has not armed yet, the pause, and the post-flight summary read from the log (design 7).
    /// </summary>
    public static class FlightScreens
    {
        /// <summary>A quiet line at the foot of the picture.</summary>
        public static void Hint(string text)
        {
            AppStyle.Begin();
            var content = new GUIContent(text);
            Vector2 size = AppStyle.Pill.CalcSize(content);
            GUI.Label(new Rect(Mathf.Round((Screen.width - size.x) / 2), Screen.height - size.y - AppStyle.S(40), size.x, size.y), content, AppStyle.Pill);
        }

        public static PauseChoice Pause()
        {
            AppStyle.Begin();
            AppStyle.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, 0.55f));
            float w = AppStyle.S(460), h = AppStyle.S(360);
            var panel = new Rect(Mathf.Round((Screen.width - w) / 2), Mathf.Round((Screen.height - h) / 2), w, h);
            AppStyle.Fill(panel, AppStyle.Panel);
            float pad = AppStyle.S(40);
            GUILayout.BeginArea(new Rect(panel.x + pad, panel.y + pad, panel.width - 2 * pad, panel.height - 2 * pad));
            GUILayout.Label("Paused", AppStyle.Heading);
            GUILayout.Space(AppStyle.S(24));
            PauseChoice choice = PauseChoice.None;
            if (GUILayout.Button("Resume", AppStyle.Primary, GUILayout.Height(AppStyle.S(60)))) choice = PauseChoice.Resume;
            GUILayout.Space(AppStyle.S(6));
            if (GUILayout.Button("Restart", AppStyle.Secondary, GUILayout.Height(AppStyle.S(54)))) choice = PauseChoice.Restart;
            if (GUILayout.Button("End flight", AppStyle.Secondary, GUILayout.Height(AppStyle.S(54)))) choice = PauseChoice.End;
            GUILayout.EndArea();
            return choice;
        }

        /// <summary>The summary: the end as a title, its lines (PostFlight.Lines), then what next. Lines null: still reading.</summary>
        public static SummaryChoice Summary(string title, IList<string> lines)
        {
            AppStyle.Begin();
            AppStyle.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, 0.6f));
            float w = Mathf.Min(AppStyle.S(1100), Screen.width - 32), h = Mathf.Min(AppStyle.S(640), Screen.height - 32);
            var panel = new Rect(Mathf.Round((Screen.width - w) / 2), Mathf.Round((Screen.height - h) / 2), w, h);
            AppStyle.Fill(panel, AppStyle.Panel);
            AppStyle.Fill(new Rect(panel.x, panel.y, AppStyle.S(6), panel.height), AppStyle.Accent);
            float pad = AppStyle.S(48);
            GUILayout.BeginArea(new Rect(panel.x + pad, panel.y + pad * 0.8f, panel.width - 2 * pad, panel.height - 1.6f * pad));
            GUILayout.Label(title, AppStyle.Heading);
            GUILayout.Space(AppStyle.S(18));
            if (lines == null) GUILayout.Label("Reading the flight's log…", AppStyle.Body);
            else
                for (int i = 0; i < lines.Count; i++)
                    GUILayout.Label(lines[i], i == 0 ? AppStyle.Body : AppStyle.Small);
            GUILayout.FlexibleSpace();
            SummaryChoice choice = SummaryChoice.None;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Menu", AppStyle.Secondary, GUILayout.Width(AppStyle.S(180)), GUILayout.Height(AppStyle.S(60)))) choice = SummaryChoice.Menu;
            if (GUILayout.Button("Setup", AppStyle.Secondary, GUILayout.Width(AppStyle.S(180)), GUILayout.Height(AppStyle.S(60)))) choice = SummaryChoice.Setup;
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Fly again", AppStyle.Primary, GUILayout.Width(AppStyle.S(300)), GUILayout.Height(AppStyle.S(60)))) choice = SummaryChoice.FlyAgain;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            return choice;
        }
    }
}
