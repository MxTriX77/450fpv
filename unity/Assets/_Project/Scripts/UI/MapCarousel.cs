using System;
using UnityEngine;

namespace Fpv450.UI
{
    public enum CarouselChoice { None, Choose, Back }

    /// <summary>A map's card: its name, a line on what it is, and its still (null: a plain card).</summary>
    public sealed class MapCard
    {
        public string Name, Line;
        public Texture Still;
    }

    /// <summary>
    /// Play's carousel (w10-play-maps 1.1, D-100, APP-02): one card per map, the selected one large in the middle with its
    /// name and line under it, its neighbours smaller and dimmed at the sides, sliding when the selection moves, and a dash
    /// per card under it for where it is; Back and Choose at the foot, as the setup's Back and Fly. A click on a side card
    /// moves to it, on the middle one chooses it. The keys and the radio's sticks are the caller's (Move, once a frame).
    /// </summary>
    public sealed class MapCarousel
    {
        /// <summary>A side card's size against the middle one's, and how dark it is drawn.</summary>
        const float SideScale = 0.62f, SideDim = 0.6f;

        public MapCard[] Cards = new MapCard[0];
        public int Selected { get; private set; }

        float _shown; // where the slide is, in cards; it eases to Selected

        /// <summary>One card left (−1) or right (+1), staying within the cards.</summary>
        public void Move(int by) => Selected = Clamp(Selected + by);

        /// <summary>Selects a card at once, without the slide (the screen opening).</summary>
        public void Show(int index) => _shown = Selected = Clamp(index);

        int Clamp(int i) => Math.Max(0, Math.Min(Cards.Length - 1, i));

        public CarouselChoice Draw(string hint)
        {
            AppStyle.Begin();
            AppStyle.DrawGround();
            if (Event.current.type == EventType.Repaint)
                _shown = Mathf.Abs(Selected - _shown) < 0.002f ? Selected : Mathf.Lerp(_shown, Selected, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));

            float x = AppStyle.S(150), y = Mathf.Round(Screen.height * 0.08f);
            GUI.Label(new Rect(x, y, Screen.width - x, AppStyle.S(80)), "Play", AppStyle.Title);
            AppStyle.Fill(new Rect(x + AppStyle.S(4), y + AppStyle.S(82), AppStyle.S(64), AppStyle.S(3)), AppStyle.Accent);

            CarouselChoice choice = CarouselChoice.None;
            float w = Mathf.Round(Mathf.Min(AppStyle.S(960), Screen.width * 0.6f)), h = Mathf.Round(w * 9f / 16f);
            float cx = Screen.width * 0.5f, cy = Mathf.Round(Screen.height * 0.46f);
            float step = w * 0.5f + AppStyle.S(56) + w * SideScale * 0.5f;
            // The farthest first, so the middle card is drawn over its neighbours.
            var order = new int[Cards.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, (a, b) => Mathf.Abs(b - _shown).CompareTo(Mathf.Abs(a - _shown)));
            foreach (int i in order)
            {
                float d = i - _shown, off = Mathf.Min(1f, Mathf.Abs(d)), s = Mathf.Lerp(1f, SideScale, off);
                var r = new Rect(Mathf.Round(cx + d * step - w * s * 0.5f), Mathf.Round(cy - h * s * 0.5f), Mathf.Round(w * s), Mathf.Round(h * s));
                Card(Cards[i], r);
                if (off > 0f) AppStyle.Fill(r, new Color(0f, 0f, 0f, SideDim * off));
                else if (r.Contains(Event.current.mousePosition)) Frame(r, AppStyle.S(3), AppStyle.Accent);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                {
                    if (i == Selected) choice = CarouselChoice.Choose;
                    else Selected = i;
                }
            }

            if (Cards.Length > 0)
            {
                MapCard card = Cards[Selected];
                float below = cy + h * 0.5f + AppStyle.S(26);
                GUI.Label(new Rect(0, below, Screen.width, AppStyle.S(44)), card.Name, Centred(AppStyle.Heading));
                GUI.Label(new Rect(0, below + AppStyle.S(50), Screen.width, AppStyle.S(30)), card.Line, Centred(AppStyle.Label));
                float dash = AppStyle.S(28), gap = AppStyle.S(10), dashes = Cards.Length * dash + (Cards.Length - 1) * gap;
                for (int i = 0; i < Cards.Length; i++)
                    AppStyle.Fill(new Rect(Mathf.Round(cx - dashes * 0.5f + i * (dash + gap)), below + AppStyle.S(104), dash, AppStyle.S(4)),
                        i == Selected ? AppStyle.Accent : AppStyle.Control);
            }

            float foot = Screen.height - AppStyle.S(124), button = AppStyle.S(60);
            if (GUI.Button(new Rect(x, foot, AppStyle.S(180), button), "Back", AppStyle.Secondary)) choice = CarouselChoice.Back;
            if (GUI.Button(new Rect(Screen.width - x - AppStyle.S(300), foot, AppStyle.S(300), button), "Choose", AppStyle.Primary)) choice = CarouselChoice.Choose;
            GUI.Label(new Rect(0, foot, Screen.width, button), hint, Centred(AppStyle.Small));
            return choice;
        }

        // The still over the card's ground, whole (letterboxed when its shape is not the card's), or a plain card with the
        // map's name.
        static void Card(MapCard card, Rect r)
        {
            if (card.Still != null)
            {
                AppStyle.Fill(r, AppStyle.GroundLow);
                GUI.DrawTexture(r, card.Still, ScaleMode.ScaleToFit);
                return;
            }
            AppStyle.Fill(r, AppStyle.Control);
            GUIStyle name = Centred(AppStyle.Title);
            name.fontSize = Mathf.RoundToInt(AppStyle.Title.fontSize * r.width / AppStyle.S(960));
            name.normal.textColor = AppStyle.Ink(AppStyle.Muted);
            GUI.Label(r, card.Name, name);
        }

        static void Frame(Rect r, float t, Color c)
        {
            AppStyle.Fill(new Rect(r.x, r.y, r.width, t), c);
            AppStyle.Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            AppStyle.Fill(new Rect(r.x, r.y, t, r.height), c);
            AppStyle.Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        static GUIStyle Centred(GUIStyle from) => new GUIStyle(from) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
    }

    /// <summary>
    /// The radio's sticks on Play's carousel (w10-play-maps 1.1): the roll stick past Push moves one card its way, the pitch
    /// stick forward past Push chooses, each once; then both sticks must come back within Rest of the centre before the
    /// next. A stick already pushed when the screen opens (Reset) does nothing until it has been centred, so a pilot still
    /// holding pitch from the last screen chooses nothing.
    /// </summary>
    public sealed class StickNav
    {
        public const double Push = 0.6, Rest = 0.25;

        bool _ready;

        public void Reset() => _ready = false;

        /// <summary>The move (−1, 0 or +1) for the sticks now (−1 to +1, + right and forward); choose: pitch forward.</summary>
        public int Step(double roll, double pitch, out bool choose)
        {
            choose = false;
            if (!_ready)
            {
                _ready = Math.Abs(roll) < Rest && Math.Abs(pitch) < Rest;
                return 0;
            }
            int move = roll >= Push ? 1 : roll <= -Push ? -1 : 0;
            choose = move == 0 && pitch >= Push;
            if (move != 0 || choose) _ready = false;
            return move;
        }
    }
}
