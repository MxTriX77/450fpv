using System;
using System.Collections.Generic;
using System.Globalization;
using Fpv450.App.Session;
using Fpv450.Sim.Wind;
using UnityEngine;

namespace Fpv450.UI
{
    public enum SetupChoice { None, Fly, Back }

    /// <summary>
    /// The setup (APP-03, PRD-04): the payload, the legs (off: the launch stand), the time of day, the wind, the rain
    /// and the start point, and nothing else; then Fly. It edits Choices in place; the caller saves them on Fly.
    /// </summary>
    public sealed class SetupScreen
    {
        static readonly CultureInfo C = CultureInfo.InvariantCulture;
        static readonly string[] Legs = { "On", "Off · launch stand" };
        static readonly string[] Winds = { "Calm", "Windy", "Severe" };
        static readonly WindLevel[] WindLevels = { WindLevel.Calm, WindLevel.Windy, WindLevel.Severe };
        static readonly string[] Rains = { "Sunny", "Rain", "Severe rain" };

        public FlightChoices Choices = new FlightChoices();
        public StartPoint[] Starts = { StartPoint.Origin };

        /// <summary>Notes to show under the choices (a file that could not be read, why the last flight did not start).</summary>
        public readonly List<string> Notes = new List<string>();

        public SetupChoice Draw(bool overFlight)
        {
            AppStyle.Begin();
            if (overFlight) AppStyle.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, 0.6f));
            else AppStyle.DrawGround();

            float w = Mathf.Min(AppStyle.S(1040), Screen.width - 32), h = Mathf.Min(AppStyle.S(820), Screen.height - 32);
            var panel = new Rect(Mathf.Round((Screen.width - w) / 2), Mathf.Round((Screen.height - h) / 2), w, h);
            AppStyle.Fill(panel, AppStyle.Panel);
            float pad = AppStyle.S(48);
            GUILayout.BeginArea(new Rect(panel.x + pad, panel.y + pad * 0.8f, panel.width - 2 * pad, panel.height - 1.6f * pad));
            GUILayout.Label("Train", AppStyle.Heading);
            GUILayout.Space(AppStyle.S(24));

            FlightChoices c = Choices;
            Row("Payload", () =>
            {
                if (GUILayout.Button("−", AppStyle.Segment, GUILayout.Width(AppStyle.S(52)), GUILayout.Height(AppStyle.S(44))))
                    c.Payload = Step(c.Payload, -1);
                float v = GUILayout.HorizontalSlider((float)c.Payload, 0f, (float)FlightChoices.PayloadMax, GUILayout.ExpandWidth(true));
                if (Math.Abs(v - c.Payload) > 1e-4) c.Payload = Math.Round(v / FlightChoices.PayloadStep) * FlightChoices.PayloadStep;
                if (GUILayout.Button("+", AppStyle.Segment, GUILayout.Width(AppStyle.S(52)), GUILayout.Height(AppStyle.S(44))))
                    c.Payload = Step(c.Payload, +1);
                GUILayout.Label(c.Payload.ToString("0.0", C) + " kg", AppStyle.Value, GUILayout.Width(AppStyle.S(110)), GUILayout.Height(AppStyle.S(44)));
            });
            Row("Legs", () => c.Legs = AppStyle.Segments(c.Legs ? 0 : 1, Legs, AppStyle.S(44)) == 0);
            Row("Time of day", () =>
            {
                float v = GUILayout.HorizontalSlider((float)c.TimeOfDay, 0f, 24f - 1f / 60, GUILayout.ExpandWidth(true));
                if (Math.Abs(v - c.TimeOfDay) > 1e-4) c.TimeOfDay = Math.Min(Math.Round(v * 60) / 60, 24 - 1.0 / 60);
                GUILayout.Label(Clock(c.TimeOfDay), AppStyle.Value, GUILayout.Width(AppStyle.S(110)), GUILayout.Height(AppStyle.S(44)));
            });
            Row("Wind", () => c.Wind = WindLevels[AppStyle.Segments(Math.Max(0, Array.IndexOf(WindLevels, c.Wind)), Winds, AppStyle.S(44))]);
            Row("Rain", () => c.Rain = (Rain)AppStyle.Segments((int)c.Rain, Rains, AppStyle.S(44)));
            Row("Start", () =>
            {
                var names = new string[Starts.Length];
                int at = 0;
                for (int i = 0; i < Starts.Length; i++)
                {
                    names[i] = Title(Starts[i].Name);
                    if (Starts[i].Name == c.StartPoint) at = i;
                }
                c.StartPoint = Starts[AppStyle.Segments(at, names, AppStyle.S(44))].Name;
            });

            foreach (string note in Notes)
            {
                GUILayout.Space(AppStyle.S(6));
                GUILayout.Label(note, AppStyle.Small);
            }
            GUILayout.FlexibleSpace();
            SetupChoice choice = SetupChoice.None;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Back", AppStyle.Secondary, GUILayout.Width(AppStyle.S(180)), GUILayout.Height(AppStyle.S(60)))) choice = SetupChoice.Back;
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Fly", AppStyle.Primary, GUILayout.Width(AppStyle.S(300)), GUILayout.Height(AppStyle.S(60)))) choice = SetupChoice.Fly;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            return choice;
        }

        static void Row(string label, Action control)
        {
            GUILayout.BeginHorizontal(GUILayout.Height(AppStyle.S(64)));
            GUILayout.Label(label, AppStyle.Label, GUILayout.Width(AppStyle.S(200)), GUILayout.Height(AppStyle.S(52)));
            GUILayout.BeginVertical();
            GUILayout.Space(AppStyle.S(4));
            GUILayout.BeginHorizontal();
            control();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        static double Step(double payload, int by) =>
            Math.Max(0, Math.Min(FlightChoices.PayloadMax, Math.Round(payload / FlightChoices.PayloadStep + by) * FlightChoices.PayloadStep));

        /// <summary>Hours as a 24-hour clock, HH:MM.</summary>
        public static string Clock(double hours)
        {
            int minutes = (int)Math.Round(hours * 60);
            return (minutes / 60 % 24).ToString("00", C) + ":" + (minutes % 60).ToString("00", C);
        }

        static string Title(string name) => string.IsNullOrEmpty(name) ? name : char.ToUpperInvariant(name[0]) + name.Substring(1);
    }
}
