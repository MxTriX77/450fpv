using System;
using Godot;

/// The OSD: a 30 × 16 character grid, plus the receiver's own two text lines, as one texture the shader reads.
///
/// **This is the layout of `docs/reference-notes/video-feed.md` §5 with the two substitutions of D-014 and nothing
/// else changed** (PR-2): the airframe's brand name is `Svinorez 10 Opto`, and the armament indicator reads
/// `! SAFE !`. Neither is cosmetic — the first keeps a real manufacturer's brand out of the product, the second keeps
/// a training simulator from presenting itself as armed. Do not "tidy" either one away.
///
/// §5 places the fields but records no values, on purpose (OPSEC): "readouts sit along the edges and in the corners,
/// and the centre stays clear apart from a small crosshair or aircraft symbol and, optionally, a dotted
/// artificial-horizon line. The top edge holds arming or status text, a heading or name field, and height or distance
/// readouts. The left side holds a flight-mode or status word, the bottom-left corner the battery readouts, and the
/// bottom row a name or message line, timer, current and link-quality readouts, and warning text." Every number and
/// every string below is therefore invented to be plausible, and the arrangement is what is reproduced.
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

    /// The airframe name, D-014. Sixteen characters, which is the bottom row's message field exactly.
    const string Airframe = "Svinorez 10 Opto";
    /// The armament indicator, D-014.
    const string Armed = "! SAFE !";

    /// The receiver's two short lines at the top left, green, shown on the no-signal screen and for ≈7.3 s after a
    /// dropout recovers. The footage's text is not recorded (OPSEC), so this is invented and deliberately generic.
    static readonly string[] RxText = { "AV IN 1", "PAL 50" };

    /// One row per grid line. Row 7 is the centre, which stays clear apart from the crosshair.
    const int RowTop = 0, RowDistance = 1, RowMode = 3, RowCentre = 7;
    const int RowPack = 12, RowCell = 13, RowPercent = 14, RowBottom = 15;
    /// Where the readouts end, counting from the left. Right-aligned fields are placed by their last column.
    const int ColLeft = 1, ColHeadingEnd = 17, ColHeightEnd = 27;
    const int ColTimer = 17, ColCurrentEnd = 25, ColLinkEnd = 29;

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

        // Top edge: arming or status text, the heading, and the height and distance readouts.
        Put(RowTop, ColLeft, Armed);
        PutRight(RowTop, ColHeadingEnd, $"{Wrap(s.HeadingDeg):000}°");
        PutRight(RowTop, ColHeightEnd, $"{Math.Max(0, (int)MathF.Round(s.Altitude))}m");
        PutRight(RowDistance, ColHeightEnd, $"{(int)MathF.Round(s.Distance)}m");

        // Left side: the flight-mode word.
        Put(RowMode, ColLeft, "ANGL");

        // Centre: the crosshair, and the dotted artificial horizon around it.
        Put(RowCentre, Cols / 2 - 2, "-");
        Put(RowCentre, Cols / 2 - 1, "+");
        Put(RowCentre, Cols / 2, "-");
        Horizon(s.PitchDeg, s.RollDeg);

        // Bottom-left corner: the battery readouts.
        float cell = s.PackVolts / 6f;
        Put(RowPack, ColLeft, $"{s.PackVolts.ToString("00.0", System.Globalization.CultureInfo.InvariantCulture)}V");
        Put(RowCell, ColLeft, $"{cell.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}V");
        Put(RowPercent, ColLeft, $"{Percent(cell)}%");

        // Bottom row: the name or message line, the timer, the current and the link quality.
        int seconds = (int)s.ElapsedSeconds;
        Put(RowBottom, 0, Airframe);
        Put(RowBottom, ColTimer, $"{seconds / 60 % 100:00}:{seconds % 60:00}");
        PutRight(RowBottom, ColCurrentEnd, $"{(int)MathF.Round(s.Amps)}A");
        PutRight(RowBottom, ColLinkEnd, $"{(int)MathF.Round(s.LinkMargin * 100f)}%");

        // Warning text, blinking, in the bottom row's message field — which is how the real one shows warnings and
        // why that field is described as "a name or message line".
        string warning = cell < LowCellVolts ? "BATT LOW" : s.LinkMargin < LowLinkFraction ? "LOW LINK" : null;
        if (warning != null && Blink(field))
        {
            for (int c = 0; c < Airframe.Length; c++)
                _grid[RowBottom, c] = ' ';
            Put(RowBottom, (Airframe.Length - warning.Length) / 2, warning);
        }

        Upload();
    }

    /// The dotted artificial-horizon line, straight because the OSD is inserted after the lens (A f236). It pivots on
    /// the centre with roll and rides up and down with pitch; the two centre columns stay clear for the crosshair.
    void Horizon(float pitchDeg, float rollDeg)
    {
        const float DegreesPerRow = 6f, CellAspect = 0.95f;
        float centre = RowCentre + 0.5f + pitchDeg / DegreesPerRow;
        float slope = MathF.Tan(Mathf.DegToRad(rollDeg)) * CellAspect;
        for (int c = 3; c < Cols - 3; c++)
        {
            if (c >= Cols / 2 - 3 && c <= Cols / 2 + 1)
                continue;
            int row = (int)MathF.Round(centre + (c - (Cols - 1) / 2f) * slope - 0.5f);
            if (row >= 1 && row < Rows - 1 && _grid[row, c] == ' ')
                _grid[row, c] = '~';
        }
    }

    /// True while a blinking element is on.
    static bool Blink(int field) => field % (BlinkOnFields + BlinkOffFields) < BlinkOnFields;

    static int Wrap(float degrees) => ((int)MathF.Round(degrees) % 360 + 360) % 360;

    /// A stand-in state of charge from the cell voltage: 3.3 V empty, 4.2 V full.
    static int Percent(float cell) => Math.Clamp((int)MathF.Round((cell - 3.3f) / 0.9f * 100f), 0, 100);

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
