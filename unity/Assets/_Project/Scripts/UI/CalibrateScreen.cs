using System;
using System.Collections.Generic;
using Fpv450.Input.Radio;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using UnityEngine;

namespace Fpv450.UI
{
    /// <summary>
    /// The Calibrate screen's wizard (p6-input-app 1.2, INP-02), plain IMGUI until the pilot judges the look at G7.
    /// The menu (1.4) opens it with the app's RadioReader in Reader; without one it opens its own on the first radio.
    /// After Start, the radio alone drives it: every report reaches the wizard through the reader's tap with its
    /// arrival stamp, so nothing depends on the frame rate. The live bars show each raw axis and the sticks as the
    /// flight will get them (the reader's map). A clean calibration is saved under the radio's name, made the active
    /// profile and put into the reader's map at once; one with an axis too narrow is not saved. Profiles are listed to
    /// pick the active one, and renamed with the keyboard if the pilot wants to, after the calibration. The Rates tab
    /// (1.3) edits the rates and the throttle curve (RatesPanel); it is offered whenever the wizard is not running.
    /// </summary>
    public sealed class CalibrateScreen : MonoBehaviour
    {
        /// <summary>The app's reader; null opens one here for as long as the screen is enabled.</summary>
        public RadioReader Reader;

        /// <summary>The Back button: the menu closes the screen.</summary>
        public event Action Closed;

        RadioReader _own;
        CalibrationStore _store;
        string _storeError;
        CalibrationWizard _wizard;
        ReportTap _tap;
        RadioReport _report, _latest = new RadioReport(0);
        bool _saved;
        string _rename = "";
        GUIStyle _title, _text, _small;
        Texture2D _white;
        RatesPanel _rates;
        int _tab;
        static readonly string[] Tabs = { "Radio", "Rates" };

        /// <summary>
        /// QA's finding 13: a radio that updates more slowly than this has a healthy step of EdgeTX's floor gain (1024 / 100
        /// counts) on the jump rule's fastest accepted sweep (its 10–90 % band in MinTransitUs), about 4.4 ms. That is an
        /// RF module setting the mixer's period, so the Done screen says so, for a refusal and a saved profile alike.
        /// </summary>
        public static readonly long SlowRadioUs = (long)Math.Round(2 * CalibrationWizard.PitfallStep * CalibrationWizard.MinTransitUs / (1 - 2 * CalibrationWizard.EndBand));

        /// <summary>The note for a radio that updates every updateUs, or null when it is quick enough.</summary>
        public static string SlowRadioNote(long updateUs) => updateUs > SlowRadioUs
            ? "The radio updates its USB output only every " + (updateUs / 1000.0).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
              + " ms (an RF module is on): switch it off for the sim."
            : null;

        // The app's reader, or one opened here on first use, so the menu can set Reader just after enabling the screen.
        RadioReader Radio
        {
            get
            {
                if (Reader != null) return Reader;
                if (_own == null)
                {
                    _own = new RadioReader(() => WinHid.OpenFirst(HostClock.Instance));
                    _own.Start();
                }
                return _own;
            }
        }

        void OnEnable()
        {
            try
            {
                _store = CalibrationStore.Load(Application.persistentDataPath);
            }
            catch (Exception e)
            {
                _store = new CalibrationStore(Application.persistentDataPath);
                _storeError = CalibrationStore.FileName + " could not be read (" + e.Message + "); a new calibration replaces it.";
            }
            _rates = new RatesPanel(Application.persistentDataPath);
        }

        void Start()
        {
            CalibrationProfile active = _store.ActiveProfile;
            if (active != null) Radio.Map = new CalibratedStickMap(active);
        }

        void OnDisable()
        {
            _rates?.Save();
            Stop();
            _own?.Stop();
            _own = null;
        }

        void Begin()
        {
            RadioReader r = Radio;
            _wizard = new CalibrationWizard(r.DeviceName, r.Source, r.Axes);
            _tap = new ReportTap(r.Axes.Length);
            _report = new RadioReport(r.Axes.Length);
            _saved = false;
            r.Tap = _tap;
        }

        void Stop()
        {
            RadioReader r = Reader ?? _own;
            if (r != null && r.Tap == _tap) r.Tap = null;
            _tap = null;
        }

        void Update()
        {
            if (_wizard == null || _tap == null) return;
            if (!Radio.Connected)
            {
                Stop();
                _wizard = null;
                return;
            }
            while (_tap.TryRead(_report)) _wizard.Feed(_report);
            if (_wizard.Step != CalibrationStep.Done) return;
            Stop();
            if (_wizard.Failed) return;
            _store.Put(_wizard.Profile);
            try
            {
                _store.Save();
                _storeError = null;
            }
            catch (Exception e)
            {
                _storeError = "The profile could not be saved: " + e.Message;
            }
            Radio.Map = new CalibratedStickMap(_wizard.Profile);
            _rename = _wizard.Profile.Name;
            _saved = true;
        }

        // ---- Drawing ----

        void Styles()
        {
            if (_white != null) return;
            _white = Texture2D.whiteTexture;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, wordWrap = true };
            _text = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            _title.normal.textColor = _text.normal.textColor = Color.white;
            _small.normal.textColor = AppStyle.Ink(new Color(0.75f, 0.75f, 0.75f));
        }

        void OnGUI()
        {
            AppStyle.Begin();
            Styles();
            AppStyle.DrawGround();
            float w = Mathf.Min(900, Screen.width - 32);
            GUILayout.BeginArea(new Rect((Screen.width - w) / 2, 24, w, Screen.height - 48));
            GUILayout.Label("Calibrate", _title);
            if (_wizard != null && _wizard.Step != CalibrationStep.Done) _tab = 0;
            else
            {
                GUILayout.BeginHorizontal(GUILayout.Width(320));
                _tab = AppStyle.Segments(_tab, Tabs, 36);
                GUILayout.EndHorizontal();
                GUILayout.Space(12);
            }
            if (_tab == 1) _rates.Draw(w);
            else
            {
                RadioReader r = Radio;
                if (!r.Connected)
                {
                    GUILayout.Label("Connect the radio by USB in joystick mode.", _text);
                }
                else
                {
                    GUILayout.Label(r.DeviceName, _small);
                    GUILayout.Space(12);
                    if (_wizard == null) Idle();
                    else if (_wizard.Step != CalibrationStep.Done) Running();
                    else Finished();
                    GUILayout.Space(16);
                    Bars(r);
                }
                if (_storeError != null) GUILayout.Label(_storeError, _small);
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Back", GUILayout.Width(160), GUILayout.Height(36)))
            {
                _wizard = null;
                Stop();
                _rates.Save();
                Closed?.Invoke();
            }
            GUILayout.EndArea();
        }

        void Idle()
        {
            GUILayout.Label("About a minute, with the radio alone after Start.", _text);
            if (GUILayout.Button("Start", GUILayout.Width(160), GUILayout.Height(40))) Begin();
            Profiles();
        }

        void Running()
        {
            int step = (int)_wizard.Step + 1, steps = (int)CalibrationStep.Done;
            GUILayout.Label("Step " + step + " of " + steps, _small);
            GUILayout.Label(_wizard.Prompt, _title);
        }

        void Finished()
        {
            GUILayout.Label(_wizard.Prompt, _title);
            CalibrationProfile p = _wizard.Profile;
            GUILayout.Label("Channels " + p.Order + ", arm on " + (p.Arm.Control == ArmControl.Axis ? "channel " + (p.Arm.Index + 1) : "button " + (p.Arm.Index + 1)) + ".", _text);
            foreach (CalibrationFinding f in _wizard.Findings) GUILayout.Label("• " + f.Message, _text);
            string slow = SlowRadioNote(_wizard.UpdateUs);
            if (slow != null) GUILayout.Label("• " + slow, _text);
            if (_saved)
            {
                GUILayout.Label("Saved as the active profile.", _small);
                GUILayout.BeginHorizontal();
                _rename = GUILayout.TextField(_rename, GUILayout.Width(320), GUILayout.Height(30));
                if (GUILayout.Button("Rename", GUILayout.Width(120), GUILayout.Height(30)) && _rename.Trim().Length > 0 && _rename != p.Name)
                {
                    _store.Profiles.RemoveAll(x => x.Name == p.Name);
                    p.Name = _rename.Trim();
                    _store.Put(p);
                    Save();
                }
                GUILayout.EndHorizontal();
            }
            if (GUILayout.Button("Calibrate again", GUILayout.Width(200), GUILayout.Height(36))) Begin();
        }

        void Profiles()
        {
            if (_store.Profiles.Count == 0) return;
            GUILayout.Space(12);
            GUILayout.Label("Profiles", _small);
            foreach (CalibrationProfile p in _store.Profiles)
            {
                bool active = p.Name == _store.Active;
                if (GUILayout.Toggle(active, " " + p.Name + "  (" + p.Order + ")", GUILayout.Height(26)) && !active)
                {
                    _store.Active = p.Name;
                    Radio.Map = new CalibratedStickMap(p);
                    Save();
                }
            }
        }

        void Save()
        {
            try
            {
                _store.Save();
            }
            catch (Exception e)
            {
                _storeError = "The profiles could not be saved: " + e.Message;
            }
        }

        // The live bars: every raw axis over its logical range, then the sticks as the flight gets them.
        void Bars(RadioReader r)
        {
            RadioAxis[] axes = r.Axes;
            if (_latest.Axes.Length != axes.Length) _latest = new RadioReport(axes.Length);
            if (!r.ReadLatest(_latest)) return;
            GUILayout.Label("Raw", _small);
            for (int i = 0; i < axes.Length; i++)
            {
                float v = axes[i].Max > axes[i].Min ? (_latest.Axes[i] - axes[i].Min) / (float)(axes[i].Max - axes[i].Min) : 0;
                Bar("ch" + (i + 1), v, false, _latest.Axes[i].ToString());
            }
            var on = new List<string>();
            for (int b = 0; b < 64; b++)
                if (((_latest.Buttons >> b) & 1) != 0) on.Add((b + 1).ToString());
            GUILayout.Label("Buttons on: " + (on.Count == 0 ? "none" : string.Join(", ", on)), _small);

            r.Map.Map(_latest, axes, out StickSample s);
            GUILayout.Space(8);
            GUILayout.Label("Calibrated", _small);
            Bar("Roll", (float)s.Roll, true, s.Roll.ToString("+0.00;-0.00"));
            Bar("Pitch", (float)s.Pitch, true, s.Pitch.ToString("+0.00;-0.00"));
            Bar("Yaw", (float)s.Yaw, true, s.Yaw.ToString("+0.00;-0.00"));
            Bar("Throttle", (float)s.Throttle, false, s.Throttle.ToString("0.00"));
            GUILayout.Label(s.Arm ? "ARMED" : "disarmed", _text);
        }

        // centred: −1 to +1 from the middle; otherwise 0 to 1 from the left.
        void Bar(string label, float v, bool centred, string value)
        {
            Rect row = GUILayoutUtility.GetRect(10, 22, GUILayout.ExpandWidth(true));
            GUI.Label(new Rect(row.x, row.y, 90, row.height), label, _small);
            var track = new Rect(row.x + 96, row.y + 5, row.width - 96 - 80, row.height - 10);
            GUI.color = new Color(0.22f, 0.22f, 0.24f);
            GUI.DrawTexture(track, _white);
            GUI.color = new Color(0.85f, 0.85f, 0.85f);
            v = Mathf.Clamp(v, centred ? -1 : 0, 1);
            float mid = track.x + track.width / 2;
            Rect fill = centred
                ? new Rect(Mathf.Min(mid, mid + v * track.width / 2), track.y, Mathf.Abs(v) * track.width / 2, track.height)
                : new Rect(track.x, track.y, v * track.width, track.height);
            GUI.DrawTexture(fill, _white);
            GUI.color = Color.white;
            GUI.Label(new Rect(track.xMax + 8, row.y, 72, row.height), value, _small);
        }
    }
}
