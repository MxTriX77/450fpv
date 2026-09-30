using System;
using Godot;

/// The OSD: a 30 × 16 character grid, plus the receiver's own two text lines, as one texture the shader reads.
///
/// **This is the layout of `docs/reference-notes/video-feed.md` §5 with the two substitutions of D-014 and nothing
/// else changed** (PR-2): the airframe's brand name is `Svinorez 10 Opto`, and the armament indicator reads
/// `! SAFE !`. Neither is cosmetic — the first keeps a real manufacturer's brand out of the product, the second keeps
/// a training simulator from presenting itself as armed. Do not "tidy" either one away.
///
/// The arrangement comes from a stable-feed clip the pilot supplied on 2026-09-30 so the OSD could be checked, read
/// as positions only — no values are recorded here or anywhere in the repo (OPSEC), so every number and every string
/// below is invented to be plausible and only the arrangement is reproduced:
///
///   top-left      a three-character mode word
///   top-right     ALT, a value and a unit character; it changes frame to frame
///   bottom-left   a stacked column: the arming indicator, then a tiny two-line unit label beside an MM:SS timer,
///                 then a battery mark with the per-cell voltage, then a battery mark with the pack voltage, both
///                 carrying a Cyrillic ve as their unit character
///   bottom-centre the airframe name
///   bottom-right  two right-aligned numbers, the lower one carrying an `a` for amps
///   centre        a small reticle: a horizontal bar with a short vertical tick and a dot at the middle
///
/// This is narrower than §5's general description of A–H, which also allows a dotted artificial-horizon line; the
/// clip's airframe does not show one, so neither does this.
///
/// Altitude, distance, heading and attitude come from the camera. The rest are stand-ins on <see cref="FeedSignals"/>
/// and will come from the flight model. §7 names the whole grid as the interface: **the game-developer owns the
/// content**, the tech-artist owns putting it through the feed before the analog stages. When the game's OSD model
/// arrives it replaces <see cref="Build"/> and nothing else here changes.
public sealed class FeedOsd
{
    public const int Cols = 30, Rows = 16;
    /// The receiver's own two lines of text live in the rows after the OSD, so one texture carries both.
    public const int RxRow = Rows, TotalRows = Rows + 2;

    /// Warning elements blink: ≈7 recorded frames on, ≥5 off (measured on E), i.e. 12 fields on and 9 off.
    const int BlinkOnFields = 12, BlinkOffFields = 9;

    /// The airframe name, D-014, kept here exactly as the decision writes it. The character ROM's face is all
    /// capitals (it has no lowercase letters, only unit marks), so it reaches the screen upper-cased. That is the
    /// font, not an edit to the name.
    static readonly string Airframe = "Svinorez 10 Opto".ToUpperInvariant();
    /// The armament indicator, D-014.
    const string Armed = "! SAFE !";

    /// The receiver's two short lines at the top left, green, shown on the no-signal screen and for ≈7.3 s after a
    /// dropout recovers. The footage's text is not recorded (OPSEC), so this is invented and deliberately generic.
    static readonly string[] RxText = { "AV IN 1", "PAL 50" };

    /// One row per grid line. Row 7 is the centre, which stays clear apart from the reticle.
    const int RowTop = 0, RowCentre = 7;
    const int RowArmed = 10, RowLabelTop = 11, RowTimer = 12, RowCell = 13, RowPack = 14, RowBottom = 15;
    /// Where the readouts sit. Right-aligned fields are placed by their last column.
    const int ColLeft = 1, ColStack = 3, ColRightEnd = 28;

    /// Warning thresholds for the stand-in readouts. Real ones come from the flight model.
    const float LowCellVolts = 3.50f, LowLinkFraction = 0.35f;

    readonly char[,] _grid = new char[TotalRows, Cols];
    readonly Image _image = Image.CreateEmpty(Cols, TotalRows, false, Image.Format.Rf);
    readonly ImageTexture _texture;

    public FeedOsd()
    {
        _texture = ImageTexture.CreateFromImage(_image);
        for (int r = 0; r < RxText.Length; r++)
            Put(RxRow + r, 0, RxText[r]);
    }

    /// The codes texture: one cell per character, the OSD in rows 0–15 and the receiver's text in rows 16–17.
    public Texture2D Codes => _texture;

    /// Rebuilds the grid for one video field and uploads it.
    public void Build(FeedSignals s, int field)
    {
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
                _grid[r, c] = ' ';

        // Top-left: the three-character mode word. Top-right: ALT, the value and its unit.
        Put(RowTop, ColLeft, "ANG");
        PutRight(RowTop, ColRightEnd, $"ALT {Math.Max(0, (int)MathF.Round(s.Altitude))}m");

        // Centre: the reticle, a bar with a tick and a dot at the middle.
        Put(RowCentre, Cols / 2 - 3, "--^--");

        // Bottom-left: the stacked column, in the order the clip shows it.
        float cell = s.PackVolts / 6f;
        int seconds = (int)s.ElapsedSeconds;
        Put(RowArmed, ColLeft, Armed);
        Put(RowLabelTop, ColLeft, "[");
        Put(RowTimer, ColLeft, "]");
        Put(RowTimer, ColStack, $"{seconds / 60 % 100:00}:{seconds % 60:00}");
        Put(RowCell, ColLeft, "$");
        Put(RowCell, ColStack, $"{cell.ToString("0.00", Invariant)}в");
        Put(RowPack, ColLeft, "$");
        Put(RowPack, ColStack, $"{s.PackVolts.ToString("00.0", Invariant)}в");

        // Bottom-centre: the airframe name. Bottom-right: two right-aligned numbers, the lower one in amps.
        Put(RowBottom, (Cols - Airframe.Length) / 2, Airframe);
        PutRight(RowPack, ColRightEnd, $"{(int)MathF.Round(s.Distance)}");
        PutRight(RowBottom, ColRightEnd, $"{(int)MathF.Round(s.Amps)}a");

        // Warning text, blinking, over the airframe name — the message field of the bottom row, which is where §5
        // puts warnings.
        string warning = cell < LowCellVolts ? "BATT LOW" : s.LinkMargin < LowLinkFraction ? "LOW LINK" : null;
        if (warning != null && Blink(field))
        {
            for (int c = 0; c < Airframe.Length; c++)
                _grid[RowBottom, (Cols - Airframe.Length) / 2 + c] = ' ';
            Put(RowBottom, (Cols - warning.Length) / 2, warning);
        }

        Upload();
    }

    /// True while a blinking element is on.
    static bool Blink(int field) => field % (BlinkOnFields + BlinkOffFields) < BlinkOnFields;

    static readonly System.Globalization.CultureInfo Invariant = System.Globalization.CultureInfo.InvariantCulture;

    void Put(int row, int col, string text)
    {
        for (int i = 0; i < text.Length && col + i < Cols; i++)
            if (col + i >= 0)
                _grid[row, col + i] = text[i];
    }

    void PutRight(int row, int lastCol, string text) => Put(row, lastCol - text.Length + 1, text);

    void Upload()
    {
        for (int r = 0; r < TotalRows; r++)
            for (int c = 0; c < Cols; c++)
                _image.SetPixel(c, r, new Color(FeedFont.Code(_grid[r, c]), 0f, 0f));
        _texture.Update(_image);
    }
}
