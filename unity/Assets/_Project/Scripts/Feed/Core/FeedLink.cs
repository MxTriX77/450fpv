using System.Globalization;

namespace Fpv450.Feed
{
    /// <summary>
    /// The receiver's reading of the fiber link (p7d-tether 1.16; PHY-20, PHY-21, PHY-25; the analog-feed spec's "Link
    /// loss from the physics"): the physics' margin dips and break, read once per video field before the field is stepped,
    /// played through the port's own loss machine (<see cref="FeedLoss"/>, D-026). Everything it shows is the port's or
    /// the physics': the tell, the outage's look and the terminal sequences are the port's draws; when a dip begins and
    /// ends, when the line breaks and what did it are the physics'.
    /// <list type="bullet">
    /// <item>A dip (the margin below the threshold until back above it plus the hysteresis, the physics' rule) is the
    /// recovering dropout of N12, from the field it began in to the field it ended in, the picture back clean in the
    /// next (<see cref="FeedLoss.HoldDropout"/>). A dropout already running on Godot's timer (D-085) is joined, never
    /// restarted or cut short.</item>
    /// <item>A break is the terminal loss (<see cref="FeedLoss.StartTerminal"/>, K§11.3's branch): a margin that fell
    /// through the threshold a field or more before (a dip on at the last reading) has faded, one that vanished within
    /// the field is abrupt. Blue until reset: until the line is whole again, which is a new flight's.</item>
    /// </list>
    /// Each loss is put into words with its physics cause for the log (<see cref="Apply"/>'s return); the review overlay
    /// prints the same causes beside the link's state (<see cref="FeedSignalLog.Value"/>).
    /// </summary>
    public sealed class FeedLink
    {
        int _dips;
        bool _inDip, _broken;

        /// Reads the link once before a field is stepped, and starts, holds or ends the loss it calls for. Returns what
        /// happened, for the log, or null when nothing did. Reading the same signals again changes nothing.
        public string Apply(IFeedSignals s, FeedLoss loss, int field)
        {
            if (s.LinkState == LinkState.Broken)
            {
                if (_broken)
                    return null;
                _broken = true;
                bool abrupt = !_inDip;
                _inDip = false;
                loss.StartTerminal(abrupt);
                return string.Format(CultureInfo.InvariantCulture,
                    "field {0}: the fiber broke{1}, the margin {2}: {3}", field,
                    s.TetherBreakMechanism.Length > 0 ? " by " + s.TetherBreakMechanism : "",
                    abrupt ? "gone within the field" : "already in a dip",
                    loss.Staged ? "the staged loss to no signal" : "the hard cut to no signal");
            }

            string said = null;
            if (_broken || s.LinkDips < _dips)
            {
                // A new line (a new flight's): the terminal blue goes, the dips count from the new flight's.
                bool wasBroken = _broken;
                _broken = _inDip = false;
                _dips = 0;
                loss.Reset();
                if (wasBroken)
                    said = string.Format(CultureInfo.InvariantCulture, "field {0}: the line is whole again (a new flight): the picture back", field);
            }

            bool began = s.LinkDips > _dips;
            if (began || (s.LinkInDip && !_inDip))
            {
                loss.HoldDropout();
                said = string.Format(CultureInfo.InvariantCulture, "field {0}: link dip {1}{2}: a recovering dropout{3}", field,
                    s.LinkDips, s.LinkLossCause.Length > 0 ? " on " + s.LinkLossCause : "",
                    s.LinkInDip ? "" : ", the dip already over");
            }
            if (!s.LinkInDip && (_inDip || began))
            {
                loss.ReleaseDropout();
                if (!began)
                    said = string.Format(CultureInfo.InvariantCulture, "field {0}: link dip {1} over: the picture back clean next field", field, s.LinkDips);
            }
            _inDip = s.LinkInDip;
            _dips = s.LinkDips;
            return said;
        }
    }
}
