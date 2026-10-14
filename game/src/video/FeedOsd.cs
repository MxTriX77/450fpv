using System;
using System.Globalization;
using Godot;

/// The OSD: a 30 × 16 character grid, plus the receiver's own two text lines, as one texture.
///
/// **This is the layout of the pilot's own footage, measured off it at 1:1**, with the two substitutions of D-014 and
/// nothing else changed (PR-2): the airframe's brand name is `Svinorez 10 Opto`, and the armament indicator reads
/// `! SAFE !`. Neither is cosmetic — the first keeps a real manufacturer's brand out of the product, the second keeps
/// a training simulator from presenting itself as armed. Do not "tidy" either one away. The footage is
/// OPSEC-restricted and never enters the repo: what is recorded here is geometry, and every value is invented.
///
/// **The grid is not the picture.** Measured at 1920 × 1080, a character cell is 67.3 × 71.0 px and the grid is inset
/// half a cell from the top left, so 30 × 16 cells cover 2019 × 1136 px — wider and taller than the picture. The last
/// two columns and the last two rows fall outside it, exactly as they do on the aircraft, and nothing is placed
/// there. Mapping the grid to the picture instead would shrink every cell by 5 % and move every readout. Do not
/// "fix" it to 1/30 × 1/16 of the screen.
///
/// Row and column assignment, all measured:
///
///   row 0    mode word at columns 0–2; the altitude label, its value and the unit ending at column 26
///   row 7    the centre reticle, three cells at columns 13–15
///   row 10   the arming indicator at columns 0–7
///   row 11   the two-line unit label at column 0, the MM:SS timer at columns 1–5
///   row 12   a battery mark, the per-cell voltage, the airframe name, and a right-aligned number
///   row 13   a battery mark, the pack voltage, a right-aligned number and its amps unit
///
/// The footage's own name is three characters shorter than `Svinorez 10 Opto`, so the upper number sits two columns
/// further right than the footage's to keep a clear cell either side of the name. That is the only position this
/// layout moves, and D-014's longer name is why.
///
/// Altitude comes from the camera. The rest are stand-ins on <see cref="FeedSignals"/> and will come from the flight
/// model. §7 of the video-feed notes names the whole grid as the interface: **the game-developer owns the content**,
/// the tech-artist owns putting it through the feed before the analog stages. When the game's OSD model arrives it
/// replaces <see cref="Build"/> and nothing else here changes.
public sealed class FeedOsd
{
    public const int Cols = 30, Rows = 16;
    /// The receiver's own two lines of text live in the rows after the OSD, so one texture carries both.
    public const int RxRow = Rows, TotalRows = Rows + 2;

    /// The character cell and the grid origin, as fractions of the picture. Measured: 67.3 × 71.0 px at 1920 × 1080,
    /// inset half a cell. The shader reads these, so this is the only place they are written down.
    public const float CellW = 67.3f / 1920f, CellH = 71.0f / 1080f;
    public const float OriginX = 33.7f / 1920f, OriginY = 33.5f / 1080f;

    /// Warning elements blink: ≈7 recorded frames on, ≥5 off (measured on clip E), i.e. 12 fields on and 9 off.
    const int BlinkOnFields = 12, BlinkOffFields = 9;

    /// The airframe name, D-014, kept here exactly as the decision writes it. The character ROM's face is all
    /// capitals (it has no lowercase letters, only unit marks), so it reaches the screen upper-cased. That is the
    /// font, not an edit to the name.
    static readonly string Airframe = "Svinorez 10 Opto".ToUpperInvariant();
    /// The armament indicator, D-014. Eight cells, which is what the footage's own indicator occupies exactly.
    const string Armed = "! SAFE !";
    /// The three-character flight-mode word, as the footage draws it.
    const string Mode = "AIR";

    /// The receiver's two short lines at the top left, green, shown on the no-signal screen and the receiver's snow only,
    /// never over the picture (PR-4 correction). The footage's text is not recorded (OPSEC), so this is invented and
    /// deliberately generic.
    static readonly string[] RxText = { "AV IN 1", "PAL 50" };

    const int RowTop = 0, RowReticle = 7, RowArmed = 10, RowTimer = 11, RowCell = 12, RowPack = 13;
    const int ColLeft = 0, ColStack = 1, ColReticle = 13;
    const int ColName = 7, ColAltEnd = 26, ColUpperEnd = 27, ColLowerEnd = 26, ColAmps = 27;

    /// Warning thresholds for the stand-in readouts. Real ones come from the flight model.
    const float LowCellVolts = 3.50f, LowLinkFraction = 0.35f;

    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

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

        // Top row: the mode word at the left, the altitude label, value and unit at the right.
        Put(RowTop, ColLeft, Mode);
        PutRight(RowTop, ColAltEnd, $">{Math.Max(0, (int)MathF.Round(s.Altitude))}m");

        // Centre: the reticle, a bar with a lozenge at the middle, drawn across three cells.
        Put(RowReticle, ColReticle, "{|}");

        // The bottom-left stack.
        float cell = s.PackVolts / 6f;
        int seconds = (int)s.ElapsedSeconds;
        Put(RowArmed, ColLeft, Armed);
        Put(RowTimer, ColLeft, "[");
        Put(RowTimer, ColStack, $"{seconds / 60 % 100:00}:{seconds % 60:00}");
        Put(RowCell, ColLeft, "$");
        Put(RowCell, ColStack, $"{cell.ToString("0.00", Invariant)}в");
        Put(RowPack, ColLeft, "$");
        Put(RowPack, ColStack, $"{s.PackVolts.ToString("00.0", Invariant)}в");

        // The airframe name shares the per-cell voltage's row, and the two right-aligned numbers sit one per row,
        // the lower one carrying the amps unit.
        Put(RowCell, ColName, Airframe);
        PutRight(RowCell, ColUpperEnd, (s.Distance / 1000f).ToString("0.00", Invariant));
        PutRight(RowPack, ColLowerEnd, s.Amps.ToString("00.00", Invariant));
        Put(RowPack, ColAmps, "a");

        // Warning text, blinking, over the airframe name — the message field of that row.
        string warning = cell < LowCellVolts ? "BATT LOW" : s.LinkMargin < LowLinkFraction ? "LOW LINK" : null;
        if (warning != null && Blink(field))
        {
            for (int c = 0; c < Airframe.Length; c++)
                _grid[RowCell, ColName + c] = ' ';
            Put(RowCell, ColName + (Airframe.Length - warning.Length) / 2, warning);
        }

        Upload();
    }

    /// True while a blinking element is on.
    static bool Blink(int field) => field % (BlinkOnFields + BlinkOffFields) < BlinkOnFields;

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
