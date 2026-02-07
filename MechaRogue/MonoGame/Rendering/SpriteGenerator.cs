namespace MechaRogue.Rendering;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MechaRogue.Models;

/// <summary>
/// Generates pixel-art Medabot sprite textures at runtime from a 16×20 template.
/// Each zone (head, body, arms, legs) is colored based on model palette.
/// Destroyed parts turn gray. KO gets a dark overlay.
/// </summary>
public static class SpriteGenerator
{
    // 0=transparent, 1=head, 2=body, 3=rightArm, 4=leftArm, 5=legs, 6=outline
    private static readonly int[,] Template = BuildTemplate();

    private static int[,] BuildTemplate()
    {
        var t = new int[20, 16];

        // Head (rows 0-4, cols 4-11) with outline
        for (int r = 0; r < 5; r++)
            for (int c = 4; c < 12; c++)
                t[r, c] = 1;
        // Eyes
        t[2, 5] = 0; t[2, 6] = 0; t[2, 9] = 0; t[2, 10] = 0;
        // Head outline top
        for (int c = 4; c < 12; c++) t[0, c] = 6;
        t[1, 4] = 6; t[1, 11] = 6;
        t[2, 4] = 6; t[2, 11] = 6;
        t[3, 4] = 6; t[3, 11] = 6;
        t[4, 4] = 6; t[4, 11] = 6;

        // Neck/body core (rows 5-6, cols 5-10)
        for (int r = 5; r < 7; r++)
            for (int c = 5; c < 11; c++)
                t[r, c] = 2;

        // Body torso (rows 7-11, cols 5-10)
        for (int r = 7; r < 12; r++)
            for (int c = 5; c < 11; c++)
                t[r, c] = 2;

        // Right arm (rows 7-12, cols 11-14)
        for (int r = 7; r < 13; r++)
            for (int c = 11; c < 15; c++)
                t[r, c] = 3;

        // Left arm (rows 7-12, cols 1-4)
        for (int r = 7; r < 13; r++)
            for (int c = 1; c < 5; c++)
                t[r, c] = 4;

        // Legs (rows 12-19, cols 4-7 left, 9-11 right)
        for (int r = 12; r < 20; r++)
        {
            for (int c = 4; c < 7; c++) t[r, c] = 5;
            for (int c = 9; c < 12; c++) t[r, c] = 5;
        }

        return t;
    }

    private static readonly Color DestroyedColor = new(0x30, 0x36, 0x3D);
    private static readonly Color BodyColor = new(0x48, 0x52, 0x5C);
    private static readonly Color OutlineColor = new(0x10, 0x12, 0x14);

    /// <summary>Color palettes per model prefix: [primary, secondary, accent].</summary>
    public static readonly Dictionary<string, Color[]> Palettes = new()
    {
        ["KBT"] = [new(0xD2, 0x99, 0x22), new(0xB8, 0x80, 0x10), new(0xE0, 0xA8, 0x30)],
        ["KWG"] = [new(0x58, 0xA6, 0xFF), new(0x1F, 0x6F, 0xEB), new(0x79, 0xC0, 0xFF)],
        ["CAT"] = [new(0xBC, 0x8C, 0xFF), new(0x8B, 0x5C, 0xF6), new(0xD0, 0xAA, 0xFF)],
        ["TOT"] = [new(0x3F, 0xB9, 0x50), new(0x23, 0x8B, 0x2F), new(0x56, 0xD3, 0x64)],
        ["NAS"] = [new(0xFF, 0x7B, 0x72), new(0xE0, 0x55, 0x4D), new(0xFF, 0xA0, 0x98)],
        ["DOG"] = [new(0x8B, 0x94, 0x9E), new(0x6E, 0x76, 0x81), new(0xA8, 0xB2, 0xBC)],
        ["STG"] = [new(0xF8, 0x51, 0x49), new(0xD0, 0x30, 0x28), new(0xFF, 0x78, 0x70)],
    };

    /// <summary>
    /// Create a Texture2D for the given Medabot at native 16×20 resolution.
    /// Scale it up with point filtering when drawing.
    /// </summary>
    public static Texture2D CreateTexture(GraphicsDevice gd, Medabot bot)
    {
        var palette = GetPalette(bot.ModelId);
        bool headDead = bot.Head.IsDestroyed;
        bool rArmDead = bot.RightArm.IsDestroyed;
        bool lArmDead = bot.LeftArm.IsDestroyed;
        bool legsDead = bot.Legs.IsDestroyed;

        var tex = new Texture2D(gd, 16, 20);
        var pixels = new Color[16 * 20];

        for (int r = 0; r < 20; r++)
        {
            for (int c = 0; c < 16; c++)
            {
                int zone = Template[r, c];
                pixels[r * 16 + c] = zone switch
                {
                    0 => Color.Transparent,
                    1 => headDead ? DestroyedColor : palette[0],
                    2 => BodyColor,
                    3 => rArmDead ? DestroyedColor : palette[1],
                    4 => lArmDead ? DestroyedColor : palette[2],
                    5 => legsDead ? DestroyedColor : palette[0],
                    6 => OutlineColor,
                    _ => Color.Transparent
                };
            }
        }

        // KO overlay
        if (bot.IsKnockedOut)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i] != Color.Transparent)
                    pixels[i] = Color.Lerp(pixels[i], new Color(0, 0, 0, 200), 0.5f);
            }
            // X eyes
            void SetPixel(int pr, int pc, Color clr)
            {
                if (pr >= 0 && pr < 20 && pc >= 0 && pc < 16)
                    pixels[pr * 16 + pc] = clr;
            }
            var red = Color.Red;
            SetPixel(2, 5, red); SetPixel(2, 6, red);
            SetPixel(1, 5, red); SetPixel(3, 6, red);
            SetPixel(3, 5, red); SetPixel(1, 6, red);
            SetPixel(2, 9, red); SetPixel(2, 10, red);
            SetPixel(1, 9, red); SetPixel(3, 10, red);
            SetPixel(3, 9, red); SetPixel(1, 10, red);
        }

        tex.SetData(pixels);
        return tex;
    }

    private static Color[] GetPalette(string modelId)
    {
        var prefix = modelId.Length >= 3 ? modelId[..3] : "KBT";
        return Palettes.TryGetValue(prefix, out var p) ? p : [BodyColor, BodyColor, BodyColor];
    }
}
