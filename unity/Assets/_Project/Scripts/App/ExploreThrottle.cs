using System;
using Fpv450.Feed;
using Fpv450.Input.Radio;
using Fpv450.Sim.Control;
using Fpv450.UI;
using UnityEngine;

namespace Fpv450.App
{
    /// <summary>
    /// The noclip OSD's throttle element in Explore (D-080): what the radio's throttle stick reads, through the active
    /// calibration, by the flight's own formula (FlightOsd.ThrottlePercent from the saved min_check), as Betaflight's
    /// element reads the stick armed or not; 0 %, the stick down, with no radio. FarmlandExplore puts it beside NoclipFeed.
    /// </summary>
    public sealed class ExploreThrottle : MonoBehaviour
    {
        StubOsd osd;
        IStickMap map;
        double minCheck;
        RadioReport report = new RadioReport(0);

        void Start()
        {
            FpvCamera rig = GetComponent<FpvCamera>();
            osd = rig != null && rig.Signals is NoclipFeedSignals noclip ? noclip.Stub.Osd : null;
            string profile = Application.persistentDataPath;
            CalibrationProfile active = null;
            try
            {
                active = CalibrationStore.Load(profile).ActiveProfile;
            }
            catch (Exception)
            {
                // the flight's rule: an unreadable calibration is the radio's raw map
            }
            map = active != null ? new CalibratedStickMap(active) : new RawStickMap();
            minCheck = FcSettingsStore.Load(profile, out _).min_check;
        }

        // Before the rig lays the field's OSD out (FpvCamera's LateUpdate).
        void Update()
        {
            if (osd == null) return;
            RadioReader reader = AppRadio.Reader;
            StickSample s = StickSample.Neutral;
            if (reader.Connected)
            {
                if (report.Axes.Length != reader.Axes.Length) report = new RadioReport(reader.Axes.Length);
                if (reader.ReadLatest(report)) map.Map(report, reader.Axes, out s);
            }
            osd.ThrottlePercent = FlightOsd.ThrottlePercent(1000 + 1000 * s.Throttle, minCheck);
        }
    }
}
