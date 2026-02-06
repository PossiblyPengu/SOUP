namespace MechaRogue.Models;

/// <summary>
/// Defines a pixel-based sprite as a 2D grid of color indices.
/// </summary>
public class PixelSprite
{
    /// <summary>Width in pixels.</summary>
    public int Width { get; init; }
    
    /// <summary>Height in pixels.</summary>
    public int Height { get; init; }
    
    /// <summary>
    /// Pixel data as color palette indices.
    /// 0 = transparent, 1-9 = palette colors.
    /// </summary>
    public string[] Rows { get; init; } = [];
    
    /// <summary>
    /// Color palette mapping indices to hex colors.
    /// </summary>
    public Dictionary<char, string> Palette { get; init; } = new();
}

/// <summary>
/// Provides pre-defined mech sprites.
/// </summary>
public static class MechSprites
{
    // Palette: 
    // 0 = transparent
    // 1 = dark outline
    // 2 = primary color (body)
    // 3 = secondary color (accent)  
    // 4 = highlight
    // 5 = visor/eyes
    // 6 = shadow
    // 7 = metal/gray
    // 8 = warning/red
    // 9 = energy/cyan
    
    private static readonly Dictionary<char, string> PlayerPalette = new()
    {
        { '0', "transparent" },
        { '1', "#1a1a2e" },    // Dark outline
        { '2', "#3b82f6" },    // Blue body
        { '3', "#60a5fa" },    // Light blue accent
        { '4', "#93c5fd" },    // Highlight
        { '5', "#22d3ee" },    // Cyan visor
        { '6', "#1e3a5f" },    // Shadow
        { '7', "#6b7280" },    // Metal gray
        { '8', "#ef4444" },    // Warning red
        { '9', "#06b6d4" },    // Energy cyan
    };
    
    private static readonly Dictionary<char, string> EnemyPalette = new()
    {
        { '0', "transparent" },
        { '1', "#1a1a2e" },    // Dark outline
        { '2', "#dc2626" },    // Red body
        { '3', "#f87171" },    // Light red accent
        { '4', "#fca5a5" },    // Highlight
        { '5', "#fbbf24" },    // Yellow visor
        { '6', "#7f1d1d" },    // Shadow
        { '7', "#6b7280" },    // Metal gray
        { '8', "#000000" },    // Black
        { '9', "#f59e0b" },    // Orange energy
    };
    
    private static readonly Dictionary<char, string> GoldPalette = new()
    {
        { '0', "transparent" },
        { '1', "#1a1a2e" },    // Dark outline
        { '2', "#f59e0b" },    // Gold body
        { '3', "#fbbf24" },    // Light gold accent
        { '4', "#fde68a" },    // Highlight
        { '5', "#22d3ee" },    // Cyan visor
        { '6', "#92400e" },    // Shadow
        { '7', "#6b7280" },    // Metal gray
        { '8', "#ef4444" },    // Warning red
        { '9', "#06b6d4" },    // Energy cyan
    };
    
    // Metabee-style beetle mech (16x16)
    private static readonly string[] BeetleSprite =
    [
        "0000011111100000",
        "0001155555511000",
        "0011233333321100",
        "0012233443322100",
        "0122334444332210",
        "1123345555433211",
        "1123345555433211",
        "0122334444332210",
        "0017733333377100",
        "0177111111117710",
        "1771122221117771",
        "1771177771117771",
        "0177177771177710",
        "0017711111177100",
        "0001771001771000",
        "0001110001110000",
    ];
    
    // Rokusho-style samurai mech (16x16)
    private static readonly string[] SamuraiSprite =
    [
        "0000111111110000",
        "0001144444411000",
        "0011222222221100",
        "0112255555522110",
        "1122233333222211",
        "1223334443332211",
        "1223344444332211",
        "0122333333322110",
        "0012277772221100",
        "0017777777771000",
        "0177122221177100",
        "1771177771177710",
        "1771177771177710",
        "0171111111117100",
        "0011770001177000",
        "0001110001110000",
    ];
    
    // Heavy tank mech (16x16)
    private static readonly string[] TankSprite =
    [
        "0000000000000000",
        "0001111111111000",
        "0012222222222100",
        "0122333333332210",
        "1223355555533211",
        "1223355555533211",
        "1223333333333211",
        "1222222222222211",
        "1277777777777211",
        "1277777777777211",
        "1271111111117211",
        "0177777777777100",
        "0177177777177100",
        "0117711111177100",
        "0011111111111000",
        "0000000000000000",
    ];
    
    // Light scout mech (16x16)
    private static readonly string[] ScoutSprite =
    [
        "0000001111000000",
        "0000115555110000",
        "0001123332211000",
        "0011233333321100",
        "0012234443322100",
        "0012234443322100",
        "0011233333321100",
        "0001177771711000",
        "0001711117110000",
        "0017112211171000",
        "0171177771117100",
        "0171177771117100",
        "0017111111171000",
        "0001770001770000",
        "0001710001710000",
        "0001100001100000",
    ];
    
    // Sniper mech (16x16)
    private static readonly string[] SniperSprite =
    [
        "0000000111110000",
        "0000011555511000",
        "0000112233211000",
        "0001123333321100",
        "0011233443322110",
        "1112234444322111",
        "1777233333277711",
        "0177722222771100",
        "0011777777110000",
        "0017112211710000",
        "0171177771171000",
        "0171177771171000",
        "0017111111710000",
        "0001770001770000",
        "0001710001710000",
        "0001100001100000",
    ];
    
    // Boss mech (16x16)
    private static readonly string[] BossSprite =
    [
        "0111111111111110",
        "1155555555555511",
        "1523333333333251",
        "1233445555443321",
        "1233455555543321",
        "1233455885543321",
        "1233455885543321",
        "1233455555543321",
        "1223344444433221",
        "1177733333377711",
        "1771122221117711",
        "1771177771117711",
        "1771177771117711",
        "0171111111111710",
        "0117770001777100",
        "0011100001110000",
    ];
    
    public static PixelSprite GetPlayerSprite(string mechName)
    {
        var rows = mechName.ToLowerInvariant() switch
        {
            "metabee" => BeetleSprite,
            "rokusho" => SamuraiSprite,
            "sumilidon" => TankSprite,
            _ when mechName.Contains("Scout") => ScoutSprite,
            _ when mechName.Contains("Sniper") => SniperSprite,
            _ => BeetleSprite
        };
        
        return new PixelSprite
        {
            Width = 16,
            Height = 16,
            Rows = rows,
            Palette = PlayerPalette
        };
    }
    
    public static PixelSprite GetEnemySprite(string mechName, int floor)
    {
        var rows = floor switch
        {
            >= 7 => BossSprite,
            >= 5 => TankSprite,
            >= 3 => SamuraiSprite,
            _ when mechName.Contains("Scout") => ScoutSprite,
            _ when mechName.Contains("Sniper") => SniperSprite,
            _ => BeetleSprite
        };
        
        var palette = floor >= 7 ? GoldPalette : EnemyPalette;
        
        return new PixelSprite
        {
            Width = 16,
            Height = 16,
            Rows = rows,
            Palette = palette
        };
    }
}
