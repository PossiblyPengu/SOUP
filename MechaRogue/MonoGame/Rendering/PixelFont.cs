namespace MechaRogue.Rendering;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

/// <summary>
/// Minimal built-in pixel font — no content pipeline needed.
/// 5×7 characters rendered as textures.
/// </summary>
public class PixelFont
{
    private readonly Texture2D _pixel;
    private readonly Dictionary<char, bool[,]> _glyphs = new();
    public int CharWidth => 5;
    public int CharHeight => 7;
    public int Spacing => 1;

    public PixelFont(GraphicsDevice gd)
    {
        _pixel = new Texture2D(gd, 1, 1);
        _pixel.SetData(new[] { Color.White });
        BuildGlyphs();
    }

    public void DrawString(SpriteBatch sb, string text, Vector2 pos, Color color, int scale = 1)
    {
        float x = pos.X;
        float y = pos.Y;
        foreach (char ch in text)
        {
            if (ch == '\n') { y += (CharHeight + 2) * scale; x = pos.X; continue; }
            if (ch == ' ') { x += (CharWidth + Spacing) * scale; continue; }

            var upper = char.ToUpperInvariant(ch);
            if (_glyphs.TryGetValue(upper, out var glyph))
            {
                for (int r = 0; r < CharHeight; r++)
                    for (int c = 0; c < CharWidth; c++)
                        if (glyph[r, c])
                            sb.Draw(_pixel, new Rectangle((int)x + c * scale, (int)y + r * scale, scale, scale), color);
            }
            x += (CharWidth + Spacing) * scale;
        }
    }

    public Vector2 MeasureString(string text, int scale = 1)
    {
        if (string.IsNullOrEmpty(text)) return Vector2.Zero;
        var lines = text.Split('\n');
        float maxW = 0;
        foreach (var line in lines)
            maxW = Math.Max(maxW, line.Length * (CharWidth + Spacing) * scale);
        return new Vector2(maxW, lines.Length * (CharHeight + 2) * scale);
    }

    public void DrawStringCentered(SpriteBatch sb, string text, Vector2 center, Color color, int scale = 1)
    {
        var size = MeasureString(text, scale);
        DrawString(sb, text, center - size / 2, color, scale);
    }

    public void DrawStringWithShadow(SpriteBatch sb, string text, Vector2 pos, Color color, int scale = 1)
    {
        DrawString(sb, text, pos + new Vector2(scale, scale), Color.Black * 0.6f, scale);
        DrawString(sb, text, pos, color, scale);
    }

    private void BuildGlyphs()
    {
        // Compact encoding: each string is 7 rows of 5 binary chars
        void Add(char c, params string[] rows)
        {
            var g = new bool[7, 5];
            for (int r = 0; r < Math.Min(7, rows.Length); r++)
                for (int col = 0; col < Math.Min(5, rows[r].Length); col++)
                    g[r, col] = rows[r][col] == '#';
            _glyphs[c] = g;
        }

        Add('A', ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#");
        Add('B', "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####.");
        Add('C', ".###.", "#...#", "#....", "#....", "#....", "#...#", ".###.");
        Add('D', "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####.");
        Add('E', "#####", "#....", "#....", "####.", "#....", "#....", "#####");
        Add('F', "#####", "#....", "#....", "####.", "#....", "#....", "#....");
        Add('G', ".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".###.");
        Add('H', "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#");
        Add('I', ".###.", "..#..", "..#..", "..#..", "..#..", "..#..", ".###.");
        Add('J', "..###", "...#.", "...#.", "...#.", "#..#.", "#..#.", ".##..");
        Add('K', "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#");
        Add('L', "#....", "#....", "#....", "#....", "#....", "#....", "#####");
        Add('M', "#...#", "##.##", "#.#.#", "#...#", "#...#", "#...#", "#...#");
        Add('N', "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#");
        Add('O', ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###.");
        Add('P', "####.", "#...#", "#...#", "####.", "#....", "#....", "#....");
        Add('Q', ".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#");
        Add('R', "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#");
        Add('S', ".####", "#....", "#....", ".###.", "....#", "....#", "####.");
        Add('T', "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#..");
        Add('U', "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###.");
        Add('V', "#...#", "#...#", "#...#", "#...#", ".#.#.", ".#.#.", "..#..");
        Add('W', "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#");
        Add('X', "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#");
        Add('Y', "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#..");
        Add('Z', "#####", "....#", "...#.", "..#..", ".#...", "#....", "#####");
        Add('0', ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###.");
        Add('1', "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###.");
        Add('2', ".###.", "#...#", "....#", "..##.", ".#...", "#....", "#####");
        Add('3', ".###.", "#...#", "....#", "..##.", "....#", "#...#", ".###.");
        Add('4', "#...#", "#...#", "#...#", "#####", "....#", "....#", "....#");
        Add('5', "#####", "#....", "#....", "####.", "....#", "....#", "####.");
        Add('6', ".###.", "#....", "#....", "####.", "#...#", "#...#", ".###.");
        Add('7', "#####", "....#", "...#.", "..#..", "..#..", "..#..", "..#..");
        Add('8', ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###.");
        Add('9', ".###.", "#...#", "#...#", ".####", "....#", "....#", ".###.");
        Add('!', "..#..", "..#..", "..#..", "..#..", "..#..", ".....", "..#..");
        Add('?', ".###.", "#...#", "....#", "..##.", "..#..", ".....", "..#..");
        Add('.', ".....", ".....", ".....", ".....", ".....", ".....", "..#..");
        Add(',', ".....", ".....", ".....", ".....", ".....", "..#..", ".#...");
        Add(':', ".....", "..#..", ".....", ".....", ".....", "..#..", ".....");
        Add('-', ".....", ".....", ".....", ".###.", ".....", ".....", ".....");
        Add('+', ".....", "..#..", "..#..", "#####", "..#..", "..#..", ".....");
        Add('/', "....#", "...#.", "...#.", "..#..", ".#...", ".#...", "#....");
        Add('(', "..#..", ".#...", "#....", "#....", "#....", ".#...", "..#..");
        Add(')', "..#..", "...#.", "....#", "....#", "....#", "...#.", "..#..");
        Add('*', ".....", ".#.#.", "..#..", "#####", "..#..", ".#.#.", ".....");
        Add('[', ".##..", ".#...", ".#...", ".#...", ".#...", ".#...", ".##..");
        Add(']', "..##.", "...#.", "...#.", "...#.", "...#.", "...#.", "..##.");
        Add('#', ".#.#.", ".#.#.", "#####", ".#.#.", "#####", ".#.#.", ".#.#.");
        Add('%', "#...#", "...#.", "..#..", "..#..", "..#..", ".#...", "#...#");
        Add('>', "#....", ".#...", "..#..", "...#.", "..#..", ".#...", "#....");
        Add('<', "....#", "...#.", "..#..", ".#...", "..#..", "...#.", "....#");
        Add('=', ".....", ".....", "#####", ".....", "#####", ".....", ".....");
        Add('_', ".....", ".....", ".....", ".....", ".....", ".....", "#####");
        Add('\'', "..#..", "..#..", ".....", ".....", ".....", ".....", ".....");
        Add('"', ".#.#.", ".#.#.", ".....", ".....", ".....", ".....", ".....");
    }
}
