using System;
using Fpv450.Input.Radio;
using Fpv450.Sim.Control;
using Fpv450.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fpv450.App
{
    /// <summary>
    /// Play (w10-play-maps 1.1, D-100): the carousel of PlayMaps (MapCarousel), each card with its still when the build
    /// carries one. The keyboard (← →, then Enter or Space), the mouse (MapCarousel's clicks) and the radio's sticks (roll
    /// moves, pitch forward chooses: StickNav, through the pilot's calibration, or the uncalibrated AETR without one) move
    /// and choose; Esc and Back return to the menu (AppFlow).
    /// </summary>
    public sealed class PlayScreen
    {
        const string Hint = "← →  or the roll stick  ·  Enter or pitch forward to choose  ·  Esc back";

        readonly MapCarousel _carousel = new MapCarousel();
        readonly StickNav _sticks = new StickNav();
        IStickMap _map = new RawStickMap();
        RadioReport _report = new RadioReport(0);

        public PlayMap Selected => PlayMaps.All[_carousel.Selected];

        /// <summary>Opens on the map; the stills are read the first time, and the radio's map from the active profile.</summary>
        public void Open(PlayMap at)
        {
            if (_carousel.Cards.Length == 0)
                _carousel.Cards = Array.ConvertAll(PlayMaps.All, m => new MapCard { Name = m.Name, Line = m.Line, Still = PlayMaps.LoadStill(PlayMaps.StillPath(m)) });
            _carousel.Show(Array.IndexOf(PlayMaps.All, at));
            _sticks.Reset();
            CalibrationProfile profile = null;
            try
            {
                profile = CalibrationStore.Load(Application.persistentDataPath).ActiveProfile;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[App] Play reads the radio uncalibrated: " + e.Message);
            }
            _map = profile != null ? new CalibratedStickMap(profile) : new RawStickMap();
        }

        /// <summary>Once a frame (AppFlow's Update): the keys' and the sticks' moves; true when one chose the card.</summary>
        public bool Update()
        {
            bool choose = false;
            Keyboard k = Keyboard.current;
            if (k != null)
            {
                if (k.leftArrowKey.wasPressedThisFrame) _carousel.Move(-1);
                if (k.rightArrowKey.wasPressedThisFrame) _carousel.Move(+1);
                choose = k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame;
            }
            RadioReader radio = AppRadio.Reader;
            RadioAxis[] axes = radio.Axes;
            if (_report.Axes.Length != axes.Length) _report = new RadioReport(axes.Length);
            if (radio.Connected && radio.ReadLatest(_report))
            {
                _map.Map(_report, axes, out StickSample s);
                _carousel.Move(_sticks.Step(s.Roll, s.Pitch, out bool forward));
                choose |= forward;
            }
            else _sticks.Reset();
            return choose;
        }

        /// <summary>OnGUI's: the carousel, and the mouse's choice.</summary>
        public CarouselChoice Draw() => _carousel.Draw(Hint);
    }
}
