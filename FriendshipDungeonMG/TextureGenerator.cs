using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FriendshipDungeonMG;

/// <summary>
/// Generates procedural pixel art textures at runtime for the dungeon crawler.
/// Duke Nukem 3D / Build Engine style with raycasting support.
/// </summary>
public class TextureGenerator
{
    private GraphicsDevice _graphicsDevice;
    private Random _random;

    public Texture2D PixelTexture { get; private set; } = null!;
    public Texture2D WallTexture { get; private set; } = null!;
    public Texture2D FloorTexture { get; private set; } = null!;
    public Texture2D CeilingTexture { get; private set; } = null!;

    // Texture data for raycasting pixel access
    private Color[] _wallData = null!;
    private Color[] _floorData = null!;
    private Color[] _ceilingData = null!;
    private const int TextureSize = 64;

    // Cached sprite/enemy textures
    private Dictionary<EnemyType, Texture2D> _enemyTextures = new();
    private Dictionary<EnemyType, Color[]> _enemyTextureData = new();
    private Dictionary<SpriteType, Texture2D> _spriteTextures = new();
    private Dictionary<SpriteType, Color[]> _spriteTextureData = new();
    private Dictionary<WeaponType, Texture2D> _weaponTextures = new();

    public TextureGenerator(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice;
        _random = new Random(42); // Consistent seed for reproducible textures
    }

    public void GenerateAllTextures()
    {
        PixelTexture = CreatePixelTexture();
        
        // Generate wall, floor, ceiling with stored pixel data
        _wallData = new Color[TextureSize * TextureSize];
        _floorData = new Color[TextureSize * TextureSize];
        _ceilingData = new Color[TextureSize * TextureSize];
        
        WallTexture = CreateWallTexture(TextureSize, TextureSize, _wallData);
        FloorTexture = CreateFloorTexture(TextureSize, TextureSize, _floorData);
        CeilingTexture = CreateCeilingTexture(TextureSize, TextureSize, _ceilingData);

        // Pre-generate all enemy textures
        foreach (EnemyType type in Enum.GetValues<EnemyType>())
        {
            var data = new Color[64 * 64];
            var tex = CreateEnemyTexture(type, 64, 64, data);
            _enemyTextures[type] = tex;
            _enemyTextureData[type] = data;
        }

        // Pre-generate sprite textures
        foreach (SpriteType type in Enum.GetValues<SpriteType>())
        {
            var data = new Color[64 * 64];
            var tex = CreateSpriteTexture(type, 64, 64, data);
            _spriteTextures[type] = tex;
            _spriteTextureData[type] = data;
        }

        // Pre-generate weapon textures (64x64 low-res, scaled up 4x for chunky pixels)
        foreach (WeaponType type in Enum.GetValues<WeaponType>())
        {
            var tex = CreateWeaponTexture(type, 64, 64);
            _weaponTextures[type] = tex;
        }
    }

    // Fast pixel access for raycasting
    public Color GetWallPixel(int x, int y) => _wallData[(y & (TextureSize - 1)) * TextureSize + (x & (TextureSize - 1))];
    public Color GetFloorPixel(int x, int y) => _floorData[(y & (TextureSize - 1)) * TextureSize + (x & (TextureSize - 1))];
    public Color GetCeilingPixel(int x, int y) => _ceilingData[(y & (TextureSize - 1)) * TextureSize + (x & (TextureSize - 1))];
    
    public Texture2D GetEnemyTexture(EnemyType type) => _enemyTextures[type];
    public Texture2D GetSpriteTexture(SpriteType type) => _spriteTextures[type];
    public Texture2D GetWeaponTexture(WeaponType type) => _weaponTextures[type];
    
    public Color GetTexturePixel(Texture2D tex, int x, int y)
    {
        // For enemy/sprite textures, look up from cached data
        foreach (var kvp in _enemyTextures)
        {
            if (kvp.Value == tex)
                return _enemyTextureData[kvp.Key][y * 64 + x];
        }
        foreach (var kvp in _spriteTextures)
        {
            if (kvp.Value == tex)
                return _spriteTextureData[kvp.Key][y * 64 + x];
        }
        return Color.Magenta; // Debug color
    }

    private Texture2D CreatePixelTexture()
    {
        var texture = new Texture2D(_graphicsDevice, 1, 1);
        texture.SetData(new[] { Color.White });
        return texture;
    }

    private Texture2D CreateWallTexture(int width, int height, Color[] data)
    {
        var texture = new Texture2D(_graphicsDevice, width, height);

        // Creepy flesh-stone walls with embedded faces and eyes
        Color[] stone = {
            new Color(75, 55, 65),   // Base purple-grey
            new Color(65, 45, 55),   // Shadow
            new Color(85, 65, 75),   // Highlight
            new Color(55, 35, 45),   // Deep shadow
            new Color(95, 75, 80),   // Light
        };
        
        Color[] flesh = {
            new Color(140, 90, 95),  // Flesh highlight
            new Color(120, 70, 80),  // Flesh mid
            new Color(95, 55, 65),   // Flesh shadow
        };
        
        Color mortarDark = new Color(25, 18, 22);
        Color mortarLight = new Color(40, 30, 35);
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int brickRow = y / 10;
                int offset = (brickRow % 2) * 8;
                int brickCol = (x + offset) / 14;
                
                int localX = (x + offset) % 14;
                int localY = y % 10;
                
                bool isMortar = (localY < 2) || (localX < 2);
                
                int hash = (brickRow * 17 + brickCol * 31) % 100;
                int v = ((x * 7 + y * 13) % 17) - 8;
                
                if (isMortar)
                {
                    // Mortar with occasional gross ooze
                    bool hasOoze = hash % 23 == 0 && localY < 2;
                    if (hasOoze)
                        data[y * width + x] = new Color(60 + v, 90 + v, 55 + v); // Green ooze
                    else
                        data[y * width + x] = localY == 0 ? mortarDark : mortarLight;
                }
                else
                {
                    // Base stone with 3D shading
                    float fx = (localX - 7f) / 7f;
                    float fy = (localY - 5f) / 5f;
                    float light = -fx * 0.3f - fy * 0.5f + 0.5f;
                    light = Math.Clamp(light, 0, 1);
                    
                    int stoneIdx = (int)((1 - light) * (stone.Length - 1));
                    stoneIdx = Math.Clamp(stoneIdx, 0, stone.Length - 1);
                    Color c = stone[stoneIdx];
                    
                    // CREEPY: Embedded watching eye (rare)
                    if (hash == 42)
                    {
                        int eyeCx = 7, eyeCy = 5;
                        float eyeDist = MathF.Sqrt((localX - eyeCx) * (localX - eyeCx) + (localY - eyeCy) * (localY - eyeCy));
                        if (eyeDist < 3.5f)
                        {
                            if (eyeDist < 1.5f)
                                c = new Color(20, 10, 15); // Pupil
                            else if (eyeDist < 2.5f)
                                c = new Color(180, 160, 120); // Iris
                            else
                                c = new Color(220, 210, 200); // Sclera
                        }
                    }
                    // SILLY: Embedded smiley face (rare)
                    else if (hash == 77)
                    {
                        int fcx = 7, fcy = 5;
                        // Eyes
                        if ((Math.Abs(localX - fcx + 2) < 1 && Math.Abs(localY - fcy + 1) < 1) ||
                            (Math.Abs(localX - fcx - 2) < 1 && Math.Abs(localY - fcy + 1) < 1))
                            c = new Color(30, 20, 25);
                        // Smile
                        float smileDist = MathF.Sqrt((localX - fcx) * (localX - fcx) + (localY - fcy - 1) * (localY - fcy - 1));
                        if (smileDist > 2 && smileDist < 3.5f && localY > fcy)
                            c = new Color(40, 25, 30);
                    }
                    // Flesh patches growing on stone
                    else if (hash % 11 == 0)
                    {
                        float fleshy = MathF.Sin(localX * 0.5f + localY * 0.3f);
                        if (fleshy > 0.5f)
                        {
                            int fi = (int)((fleshy - 0.5f) * 4);
                            fi = Math.Clamp(fi, 0, flesh.Length - 1);
                            c = flesh[fi];
                        }
                    }
                    // Blood drip
                    else if (hash % 17 == 0 && localX > 5 && localX < 9)
                    {
                        float drip = (float)localY / 10f;
                        c = LerpColor(new Color(120, 35, 40), new Color(60, 20, 25), drip);
                    }
                    // Cracks
                    else if ((x * 11 + y * 23) % 89 == 0)
                    {
                        c = new Color(25, 18, 22);
                    }
                    
                    // Add subtle noise
                    c = new Color(
                        Math.Clamp(c.R + v, 0, 255),
                        Math.Clamp(c.G + v, 0, 255),
                        Math.Clamp(c.B + v, 0, 255)
                    );
                    
                    data[y * width + x] = c;
                }
            }
        }

        texture.SetData(data);
        return texture;
    }

    private Texture2D CreateFloorTexture(int width, int height, Color[] data)
    {
        var texture = new Texture2D(_graphicsDevice, width, height);

        // Creepy checkered floor with pentagram hints and teeth
        Color[] darkTile = {
            new Color(45, 35, 40),   // Dark base
            new Color(35, 25, 30),   // Darker
            new Color(55, 45, 50),   // Less dark
        };
        
        Color[] lightTile = {
            new Color(70, 55, 60),   // Light base
            new Color(60, 45, 50),   // Medium
            new Color(80, 65, 70),   // Lighter
        };
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int tileX = x / 16;
                int tileY = y / 16;
                int localX = x % 16;
                int localY = y % 16;
                bool isDark = (tileX + tileY) % 2 == 0;
                
                int v = ((x * 7 + y * 11) % 11) - 5;
                int hash = (tileX * 13 + tileY * 29) % 100;
                
                // 3D tile shading (raised edges)
                float edgeDist = Math.Min(Math.Min(localX, 15 - localX), Math.Min(localY, 15 - localY));
                float edgeLight = Math.Min(1, edgeDist / 3f);
                
                Color[] palette = isDark ? darkTile : lightTile;
                int idx = (int)((1 - edgeLight) * (palette.Length - 1));
                idx = Math.Clamp(idx, 0, palette.Length - 1);
                Color c = palette[idx];
                
                // CREEPY: Scratches (something was dragged here)
                if (hash == 33 && localY > 6 && localY < 10)
                {
                    float scratch = MathF.Sin(localX * 0.8f);
                    if (scratch > 0.7f)
                        c = new Color(25, 18, 22);
                }
                
                // SILLY: Tiny teeth embedded in floor
                if (hash == 66)
                {
                    float toothDist = MathF.Sqrt((localX - 8) * (localX - 8) + (localY - 8) * (localY - 8));
                    if (toothDist < 3)
                    {
                        float toothLight = 1 - toothDist / 3f;
                        c = LerpColor(new Color(180, 170, 150), new Color(220, 215, 200), toothLight);
                    }
                }
                
                // Pentagram hints (just subtle red lines occasionally)
                float centerX = (localX - 8f) / 8f;
                float centerY = (localY - 8f) / 8f;
                float angle = MathF.Atan2(centerY, centerX);
                float dist = MathF.Sqrt(centerX * centerX + centerY * centerY);
                
                if (hash == 13 && dist > 0.5f && dist < 0.7f)
                {
                    // Star point alignment
                    float starAngle = (angle + MathF.PI) % (MathF.PI * 2 / 5);
                    if (starAngle < 0.15f || starAngle > (MathF.PI * 2 / 5) - 0.15f)
                        c = LerpColor(c, new Color(100, 40, 45), 0.4f);
                }
                
                // Blood pool spreading
                if (hash == 88)
                {
                    float poolDist = MathF.Sqrt((localX - 10) * (localX - 10) + (localY - 10) * (localY - 10));
                    if (poolDist < 5)
                    {
                        float poolFade = poolDist / 5f;
                        c = LerpColor(new Color(80, 25, 30), c, poolFade);
                    }
                }
                
                // Deep cracks
                if ((x * 11 + y * 17) % 97 == 0)
                {
                    c = new Color(15, 10, 12);
                }
                
                // Noise
                c = new Color(
                    Math.Clamp(c.R + v, 0, 255),
                    Math.Clamp(c.G + v, 0, 255),
                    Math.Clamp(c.B + v, 0, 255)
                );
                
                data[y * width + x] = c;
            }
        }

        texture.SetData(data);
        return texture;
    }

    private Texture2D CreateCeilingTexture(int width, int height, Color[] data)
    {
        var texture = new Texture2D(_graphicsDevice, width, height);

        // Creepy organic ceiling with hanging things and dripping goo
        Color[] ceiling = {
            new Color(25, 28, 35),   // Dark blue-grey
            new Color(35, 38, 45),   // Medium
            new Color(45, 48, 55),   // Light
            new Color(20, 22, 28),   // Deep shadow
        };
        
        Color oozeGreen = new Color(55, 85, 50);
        Color oozeDark = new Color(35, 55, 35);
        Color fleshPink = new Color(110, 75, 80);
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int v = ((x * 13 + y * 7) % 17) - 8;
                int hash = (x / 8 * 17 + y / 8 * 31) % 100;
                
                // Organic bumpy pattern
                float bump = MathF.Sin(x * 0.4f) * MathF.Sin(y * 0.35f);
                bump += MathF.Sin(x * 0.15f + y * 0.12f) * 0.5f;
                
                float light = (bump + 1.5f) / 3f;
                light = Math.Clamp(light, 0, 1);
                
                int idx = (int)((1 - light) * (ceiling.Length - 1));
                idx = Math.Clamp(idx, 0, ceiling.Length - 1);
                Color c = ceiling[idx];
                
                // Dripping green goo
                if ((x * 13 + y * 7) % 67 == 0)
                {
                    c = oozeGreen;
                }
                
                // CREEPY: Hanging tendrils
                if (hash == 25)
                {
                    int localY = y % 8;
                    int localX = x % 8;
                    if (localX > 2 && localX < 6 && localY > 3)
                    {
                        float tendril = (localY - 3f) / 5f;
                        c = LerpColor(fleshPink, new Color(70, 45, 50), tendril);
                    }
                }
                
                // SILLY: Upside-down smiley embedded in ceiling
                if (hash == 50)
                {
                    int lx = x % 8, ly = y % 8;
                    int fcx = 4, fcy = 4;
                    // Upside down eyes (at bottom since inverted)
                    if ((Math.Abs(lx - fcx + 1) < 1 && Math.Abs(ly - fcy + 1) < 1) ||
                        (Math.Abs(lx - fcx - 1) < 1 && Math.Abs(ly - fcy + 1) < 1))
                        c = new Color(140, 120, 80); // Yellow eyes
                    // Upside down frown (looks like smile from below)
                    float smileDist = MathF.Sqrt((lx - fcx) * (lx - fcx) + (ly - fcy + 2) * (ly - fcy + 2));
                    if (smileDist > 1.5f && smileDist < 2.5f && ly < fcy)
                        c = new Color(20, 15, 20);
                }
                
                // Cobwebs in corners
                float cornerDist = Math.Min(Math.Min(x, width - x), Math.Min(y, height - y));
                if (cornerDist < 12)
                {
                    float webChance = (x * 7 + y * 11) % 5;
                    if (webChance == 0 && cornerDist < 8)
                    {
                        float webFade = cornerDist / 8f;
                        c = LerpColor(new Color(80, 80, 85), c, webFade);
                    }
                }
                
                // Mold patches
                if (hash % 7 == 0)
                {
                    float moldPattern = MathF.Sin(x * 0.6f + y * 0.4f);
                    if (moldPattern > 0.8f)
                        c = LerpColor(c, oozeDark, 0.5f);
                }
                
                // Mysterious stains
                if (hash == 77)
                {
                    int lx = x % 8, ly = y % 8;
                    float stainDist = MathF.Sqrt((lx - 4) * (lx - 4) + (ly - 4) * (ly - 4));
                    if (stainDist < 3)
                    {
                        float stainFade = stainDist / 3f;
                        c = LerpColor(new Color(60, 35, 40), c, stainFade);
                    }
                }
                
                // Noise
                c = new Color(
                    Math.Clamp(c.R + v, 0, 255),
                    Math.Clamp(c.G + v, 0, 255),
                    Math.Clamp(c.B + v, 0, 255)
                );
                
                data[y * width + x] = c;
            }
        }

        texture.SetData(data);
        return texture;
    }

    private Texture2D CreateSpriteTexture(SpriteType type, int width, int height, Color[] data)
    {
        var texture = new Texture2D(_graphicsDevice, width, height);
        
        // Initialize with transparency
        for (int i = 0; i < data.Length; i++)
            data[i] = Color.Transparent;

        switch (type)
        {
            case SpriteType.Stairs:
                DrawStairs(data, width, height);
                break;
            case SpriteType.Chest:
                DrawChest(data, width, height);
                break;
            case SpriteType.Trap:
                DrawTrap(data, width, height);
                break;
            case SpriteType.Shrine:
                DrawShrine(data, width, height);
                break;
            case SpriteType.Torch:
                DrawTorch(data, width, height);
                break;
            case SpriteType.Pillar:
                DrawPillar(data, width, height);
                break;
        }

        texture.SetData(data);
        return texture;
    }

    private void DrawStairs(Color[] data, int w, int h)
    {
        int cx = w / 2;
        
        // Creepy spiral staircase descending into darkness
        Color[] stone = {
            new Color(90, 75, 80),
            new Color(70, 55, 62),
            new Color(50, 38, 45),
            new Color(35, 25, 32)
        };
        
        // Steps with 3D shading
        for (int step = 0; step < 5; step++)
        {
            int y = h - 8 - step * 11;
            int offset = step * 5;
            
            // Step surface (lit from above-left)
            for (int sy = 0; sy < 8; sy++)
            {
                for (int sx = 0; sx < 28; sx++)
                {
                    float light = 1 - (float)sy / 8f;
                    light *= 1 - (float)step / 6f; // Darker as we go down
                    int idx = (int)((1 - light) * (stone.Length - 1));
                    idx = Math.Clamp(idx, 0, stone.Length - 1);
                    SetPixelSafe(data, w, h, cx - 18 + offset + sx, y + sy, stone[idx]);
                }
            }
            
            // Worn edge
            for (int ex = 0; ex < 28; ex++)
            {
                if ((ex + step) % 4 == 0)
                    SetPixelSafe(data, w, h, cx - 18 + offset + ex, y, stone[3]);
            }
        }
        
        // The dark void below (something down there...)
        for (int vy = 0; vy < 14; vy++)
        {
            for (int vx = 0; vx < 20; vx++)
            {
                float dist = MathF.Sqrt((vx - 10) * (vx - 10) + (vy - 7) * (vy - 7));
                if (dist < 10)
                {
                    float darkness = dist / 10f;
                    Color c = LerpColor(new Color(5, 2, 8), new Color(25, 18, 28), darkness);
                    SetPixelSafe(data, w, h, cx + 5 + vx, h - 58 + vy, c);
                }
            }
        }
        
        // SILLY: Two tiny eyes peering from darkness
        SetPixelSafe(data, w, h, cx + 12, h - 53, new Color(180, 160, 80));
        SetPixelSafe(data, w, h, cx + 18, h - 53, new Color(180, 160, 80));
    }

    private void DrawChest(Color[] data, int w, int h)
    {
        int cx = w / 2, cy = h / 2;
        
        // Creepy mimic-style chest with teeth
        Color[] wood = {
            new Color(140, 85, 45),
            new Color(110, 65, 32),
            new Color(80, 48, 25),
            new Color(55, 32, 18)
        };
        Color[] metal = {
            new Color(100, 95, 105),
            new Color(75, 70, 80),
            new Color(50, 45, 55)
        };
        Color teeth = new Color(220, 210, 190);
        Color teethShadow = new Color(180, 165, 145);
        Color tongue = new Color(150, 65, 75);
        
        // Chest body with 3D shading
        for (int py = 0; py < 28; py++)
        {
            for (int px = 0; px < 40; px++)
            {
                float nx = (px - 20f) / 20f;
                float ny = (py - 14f) / 14f;
                float light = -nx * 0.3f + (1 - ny) * 0.6f + 0.3f;
                light = Math.Clamp(light, 0, 1);
                
                int idx = (int)((1 - light) * (wood.Length - 1));
                idx = Math.Clamp(idx, 0, wood.Length - 1);
                SetPixelSafe(data, w, h, cx - 20 + px, cy - 8 + py, wood[idx]);
            }
        }
        
        // Lid with slight opening (it's watching...)
        for (int py = 0; py < 14; py++)
        {
            for (int px = 0; px < 42; px++)
            {
                float light = 0.8f - (float)py / 14f * 0.5f;
                int idx = (int)((1 - light) * (wood.Length - 1));
                idx = Math.Clamp(idx, 0, wood.Length - 1);
                SetPixelSafe(data, w, h, cx - 21 + px, cy - 20 + py, wood[idx]);
            }
        }
        
        // Metal bands
        for (int band = 0; band < 2; band++)
        {
            int bandY = band == 0 ? cy - 12 : cy + 8;
            for (int bx = 0; bx < 44; bx++)
            {
                float light = 0.8f - MathF.Abs(bx - 22f) / 30f;
                int idx = (int)((1 - light) * (metal.Length - 1));
                idx = Math.Clamp(idx, 0, metal.Length - 1);
                for (int by = 0; by < 4; by++)
                    SetPixelSafe(data, w, h, cx - 22 + bx, bandY + by, metal[idx]);
            }
        }
        
        // CREEPY: Teeth in the gap!
        for (int t = 0; t < 8; t++)
        {
            int tx = cx - 16 + t * 5;
            // Top teeth (hanging down)
            for (int ty = 0; ty < 4; ty++)
            {
                int tw = 2 - ty / 2;
                Color c = ty < 2 ? teeth : teethShadow;
                for (int dx = -tw; dx <= tw; dx++)
                    SetPixelSafe(data, w, h, tx + dx, cy - 8 + ty, c);
            }
        }
        
        // SILLY: One eye peeking from gap
        FillEllipse(data, w, h, cx + 8, cy - 6, 4, 3, new Color(200, 180, 120));
        FillEllipse(data, w, h, cx + 8, cy - 5, 2, 2, new Color(30, 20, 25));
        SetPixelSafe(data, w, h, cx + 7, cy - 7, Color.White);
        
        // Tongue lolling out
        for (int ty = 0; ty < 6; ty++)
        {
            int tw = 3 - ty / 2;
            for (int tx = -tw; tx <= tw; tx++)
            {
                float lightT = 1 - (float)ty / 6f;
                Color c = LerpColor(new Color(100, 45, 55), tongue, lightT);
                SetPixelSafe(data, w, h, cx - 5 + tx, cy - 3 + ty, c);
            }
        }
        
        // Lock (it's a nose!)
        FillEllipse(data, w, h, cx, cy + 2, 4, 5, new Color(180, 160, 50));
        FillEllipse(data, w, h, cx, cy + 2, 2, 3, new Color(140, 120, 40));
    }

    private void DrawTrap(Color[] data, int w, int h)
    {
        int cx = w / 2, cy = h / 2;
        
        // Bone spikes with eyeballs impaled
        Color[] bone = {
            new Color(230, 220, 200),
            new Color(200, 185, 165),
            new Color(165, 150, 130),
            new Color(120, 105, 90)
        };
        Color blood = new Color(130, 30, 35);
        Color bloodDark = new Color(80, 20, 25);
        
        // Spikes
        for (int i = 0; i < 5; i++)
        {
            int sx = cx - 22 + i * 11;
            int spikeH = 22 + (i % 2) * 4;
            
            for (int y = 0; y < spikeH; y++)
            {
                float t = (float)y / spikeH;
                int width = (int)((1 - t) * 4);
                
                for (int dx = -width; dx <= width; dx++)
                {
                    float light = 1 - MathF.Abs((float)dx / (width + 1));
                    light *= 0.5f + (1 - t) * 0.5f;
                    int idx = (int)((1 - light) * (bone.Length - 1));
                    idx = Math.Clamp(idx, 0, bone.Length - 1);
                    SetPixelSafe(data, w, h, sx + dx, cy + 18 - y, bone[idx]);
                }
            }
            
            // Blood drip
            if (i % 2 == 0)
            {
                for (int by = 0; by < 8; by++)
                {
                    float fade = (float)by / 8f;
                    Color c = LerpColor(blood, bloodDark, fade);
                    SetPixelSafe(data, w, h, sx - 1 + by % 2, cy - spikeH + 20 + by, c);
                }
            }
        }
        
        // SILLY: Impaled eyeball on middle spike
        int eyeY = cy - 5;
        FillEllipse(data, w, h, cx, eyeY, 6, 5, new Color(220, 200, 180));
        FillEllipse(data, w, h, cx + 1, eyeY + 1, 3, 3, new Color(100, 60, 70));
        FillEllipse(data, w, h, cx + 1, eyeY + 1, 2, 2, new Color(30, 15, 20));
        SetPixelSafe(data, w, h, cx - 1, eyeY - 2, Color.White);
        
        // Blood pool
        for (int py = 0; py < 8; py++)
        {
            for (int px = 0; px < 40; px++)
            {
                float dist = MathF.Sqrt((px - 20) * (px - 20) + (py - 4) * (py - 4) * 4);
                if (dist < 18 && py > 2)
                {
                    float poolLight = 1 - dist / 18f;
                    Color c = LerpColor(bloodDark, blood, poolLight * 0.5f);
                    SetPixelSafe(data, w, h, cx - 20 + px, cy + 22 + py, c);
                }
            }
        }
    }

    private void DrawShrine(Color[] data, int w, int h)
    {
        int cx = w / 2, cy = h / 2;
        
        // Eldritch shrine with floating orb and tentacles
        Color[] stone = {
            new Color(85, 75, 95),
            new Color(65, 55, 75),
            new Color(45, 38, 55),
            new Color(30, 25, 40)
        };
        Color[] orb = {
            new Color(180, 255, 180),
            new Color(120, 220, 130),
            new Color(70, 180, 90),
            new Color(40, 120, 60)
        };
        Color tentacle = new Color(100, 70, 110);
        
        // Stone pedestal with 3D
        for (int py = 0; py < 25; py++)
        {
            for (int px = 0; px < 34; px++)
            {
                float nx = (px - 17f) / 17f;
                float ny = (py - 12f) / 12f;
                float light = -nx * 0.4f + (1 - ny) * 0.5f + 0.3f;
                light = Math.Clamp(light, 0, 1);
                
                int idx = (int)((1 - light) * (stone.Length - 1));
                idx = Math.Clamp(idx, 0, stone.Length - 1);
                SetPixelSafe(data, w, h, cx - 17 + px, cy + 2 + py, stone[idx]);
            }
        }
        
        // Small tentacles around base
        for (int t = 0; t < 4; t++)
        {
            int tx = cx - 12 + t * 8;
            for (int ty = 0; ty < 10; ty++)
            {
                float wave = MathF.Sin(ty * 0.5f + t) * 2;
                int tw = 2 - ty / 4;
                for (int dx = -tw; dx <= tw; dx++)
                {
                    float tlight = 1 - MathF.Abs((float)dx / (tw + 1));
                    Color c = LerpColor(new Color(60, 40, 70), tentacle, tlight);
                    SetPixelSafe(data, w, h, tx + dx + (int)wave, cy + 25 - ty, c);
                }
            }
        }
        
        // Glowing orb with sphere shading
        for (int oy = -12; oy <= 12; oy++)
        {
            for (int ox = -12; ox <= 12; ox++)
            {
                float dist = MathF.Sqrt(ox * ox + oy * oy);
                if (dist > 11) continue;
                
                float nx = ox / 11f;
                float ny = oy / 11f;
                float nz = MathF.Sqrt(Math.Max(0, 1 - nx * nx - ny * ny));
                float light = -nx * 0.3f - ny * 0.4f + nz * 0.8f;
                light = (light + 1) / 2f;
                
                int idx = (int)((1 - light) * (orb.Length - 1));
                idx = Math.Clamp(idx, 0, orb.Length - 1);
                SetPixelSafe(data, w, h, cx + ox, cy - 16 + oy, orb[idx]);
            }
        }
        
        // SILLY: Face in orb
        SetPixelSafe(data, w, h, cx - 3, cy - 18, new Color(40, 80, 50));
        SetPixelSafe(data, w, h, cx + 3, cy - 18, new Color(40, 80, 50));
        // Smile
        for (int sx = -3; sx <= 3; sx++)
        {
            int sy = Math.Abs(sx) / 2;
            SetPixelSafe(data, w, h, cx + sx, cy - 13 + sy, new Color(40, 80, 50));
        }
        
        // Glow halo
        for (int angle = 0; angle < 360; angle += 15)
        {
            float rad = angle * MathF.PI / 180;
            for (int r = 12; r < 16; r++)
            {
                int px = cx + (int)(MathF.Cos(rad) * r);
                int py = cy - 16 + (int)(MathF.Sin(rad) * r);
                float fade = (r - 12f) / 4f;
                Color c = LerpColor(orb[0], Color.Transparent, fade);
                if (c.A > 30) SetPixelSafe(data, w, h, px, py, c);
            }
        }
    }

    private void DrawTorch(Color[] data, int w, int h)
    {
        int cx = w / 2, cy = h / 2;
        
        // Creepy bone torch with green flame
        Color[] bone = {
            new Color(220, 210, 190),
            new Color(185, 170, 150),
            new Color(145, 130, 115),
            new Color(100, 88, 75)
        };
        Color[] flame = {
            new Color(200, 255, 180),
            new Color(150, 230, 120),
            new Color(100, 200, 80),
            new Color(60, 150, 50),
            new Color(30, 100, 35)
        };
        
        // Bone handle with 3D cylinder shading
        for (int py = 0; py < 32; py++)
        {
            for (int px = -4; px <= 4; px++)
            {
                float light = 1 - MathF.Abs(px / 4f);
                light = MathF.Pow(light, 0.6f);
                int idx = (int)((1 - light) * (bone.Length - 1));
                idx = Math.Clamp(idx, 0, bone.Length - 1);
                SetPixelSafe(data, w, h, cx + px, cy + py, bone[idx]);
            }
            
            // Joint rings
            if (py % 10 < 2)
            {
                for (int px = -5; px <= 5; px++)
                    SetPixelSafe(data, w, h, cx + px, cy + py, bone[3]);
            }
        }
        
        // Skull mount at top
        for (int sy = -8; sy <= 4; sy++)
        {
            for (int sx = -6; sx <= 6; sx++)
            {
                float dist = MathF.Sqrt(sx * sx + sy * sy * 1.5f);
                if (dist > 7) continue;
                
                float light = 1 - dist / 7f;
                int idx = (int)((1 - light) * (bone.Length - 1));
                idx = Math.Clamp(idx, 0, bone.Length - 1);
                SetPixelSafe(data, w, h, cx + sx, cy - 2 + sy, bone[idx]);
            }
        }
        // Eye sockets
        FillEllipse(data, w, h, cx - 3, cy - 3, 2, 3, new Color(30, 25, 35));
        FillEllipse(data, w, h, cx + 3, cy - 3, 2, 3, new Color(30, 25, 35));
        // Nose hole
        SetPixelSafe(data, w, h, cx, cy + 1, new Color(40, 35, 45));
        
        // Green ghostly flame
        for (int fy = 0; fy < 18; fy++)
        {
            float t = (float)fy / 18f;
            int fw = (int)((1 - t * t) * 10);
            
            for (int fx = -fw; fx <= fw; fx++)
            {
                float flicker = MathF.Sin(fy * 0.4f + fx * 0.3f) * 0.2f;
                float light = 1 - MathF.Abs((float)fx / (fw + 1));
                light *= 1 - t;
                light += flicker;
                light = Math.Clamp(light, 0, 1);
                
                int idx = (int)((1 - light) * (flame.Length - 1));
                idx = Math.Clamp(idx, 0, flame.Length - 1);
                SetPixelSafe(data, w, h, cx + fx, cy - 8 - fy, flame[idx]);
            }
        }
        
        // SILLY: Face in flame
        if (true) // Always show
        {
            SetPixelSafe(data, w, h, cx - 2, cy - 18, new Color(255, 255, 220));
            SetPixelSafe(data, w, h, cx + 2, cy - 18, new Color(255, 255, 220));
            // Wavy mouth
            for (int mx = -2; mx <= 2; mx++)
            {
                int my = (mx + 2) % 2;
                SetPixelSafe(data, w, h, cx + mx, cy - 14 + my, new Color(255, 255, 220));
            }
        }
    }

    private void DrawPillar(Color[] data, int w, int h)
    {
        int cx = w / 2;
        
        // Creepy pillar with faces trying to push through
        Color[] stone = {
            new Color(90, 80, 85),
            new Color(70, 60, 68),
            new Color(50, 42, 50),
            new Color(35, 28, 35)
        };
        Color flesh = new Color(130, 95, 100);
        
        // Main pillar with cylinder shading
        for (int py = 8; py < h - 8; py++)
        {
            for (int px = -11; px <= 11; px++)
            {
                float light = 1 - MathF.Abs(px / 11f);
                light = MathF.Pow(light, 0.5f);
                int idx = (int)((1 - light) * (stone.Length - 1));
                idx = Math.Clamp(idx, 0, stone.Length - 1);
                SetPixelSafe(data, w, h, cx + px, py, stone[idx]);
            }
        }
        
        // Capital (top decoration)
        for (int py = 0; py < 10; py++)
        {
            int capWidth = 13 + (10 - py) / 3;
            for (int px = -capWidth; px <= capWidth; px++)
            {
                float light = 1 - MathF.Abs((float)px / capWidth) * 0.5f;
                light *= 1 - (float)py / 12f * 0.3f;
                int idx = (int)((1 - light) * (stone.Length - 1));
                idx = Math.Clamp(idx, 0, stone.Length - 1);
                SetPixelSafe(data, w, h, cx + px, py, stone[idx]);
            }
        }
        
        // Base
        for (int py = 0; py < 10; py++)
        {
            int baseWidth = 13 + py / 3;
            for (int px = -baseWidth; px <= baseWidth; px++)
            {
                float light = 1 - MathF.Abs((float)px / baseWidth) * 0.5f;
                light *= 0.7f + (float)py / 15f * 0.3f;
                int idx = (int)((1 - light) * (stone.Length - 1));
                idx = Math.Clamp(idx, 0, stone.Length - 1);
                SetPixelSafe(data, w, h, cx + px, h - 10 + py, stone[idx]);
            }
        }
        
        // CREEPY: Face pushing through stone
        int faceY = h / 2;
        // Bulging forehead
        for (int fy = -8; fy <= 5; fy++)
        {
            for (int fx = -7; fx <= 7; fx++)
            {
                float dist = MathF.Sqrt(fx * fx + fy * fy * 0.8f);
                if (dist > 8) continue;
                
                float bulge = 1 - dist / 8f;
                bulge = MathF.Pow(bulge, 1.5f);
                
                Color c = LerpColor(stone[1], flesh, bulge * 0.6f);
                SetPixelSafe(data, w, h, cx + fx, faceY + fy, c);
            }
        }
        
        // Sunken eyes
        FillEllipse(data, w, h, cx - 3, faceY - 2, 2, 2, stone[3]);
        FillEllipse(data, w, h, cx + 3, faceY - 2, 2, 2, stone[3]);
        
        // Open mouth (screaming)
        for (int my = 0; my < 4; my++)
        {
            int mw = 3 - my / 2;
            for (int mx = -mw; mx <= mw; mx++)
                SetPixelSafe(data, w, h, cx + mx, faceY + 3 + my, stone[3]);
        }
        
        // SILLY: Second smaller face above
        int face2Y = faceY - 22;
        FillEllipse(data, w, h, cx, face2Y, 5, 4, LerpColor(stone[1], flesh, 0.3f));
        SetPixelSafe(data, w, h, cx - 2, face2Y - 1, stone[3]);
        SetPixelSafe(data, w, h, cx + 2, face2Y - 1, stone[3]);
        // Tiny smile
        SetPixelSafe(data, w, h, cx - 1, face2Y + 2, stone[3]);
        SetPixelSafe(data, w, h, cx, face2Y + 2, stone[3]);
        SetPixelSafe(data, w, h, cx + 1, face2Y + 2, stone[3]);
    }

    private void FillRect(Color[] data, int w, int h, int x, int y, int width, int height, Color color)
    {
        for (int py = y; py < y + height; py++)
            for (int px = x; px < x + width; px++)
                SetPixelSafe(data, w, h, px, py, color);
    }

    public Texture2D CreateEnemyTexture(EnemyType type, int width, int height, Color[] data)
    {
        var texture = new Texture2D(_graphicsDevice, width, height);

        // Initialize with transparency
        for (int i = 0; i < data.Length; i++)
            data[i] = Color.Transparent;

        switch (type)
        {
            case EnemyType.SmileDog:
                DrawSmileDog(data, width, height);
                break;
            case EnemyType.MeatChild:
                DrawMeatChild(data, width, height);
                break;
            case EnemyType.GrandmasTwin:
                DrawGrandmasTwin(data, width, height);
                break;
            case EnemyType.ManInWall:
                DrawManInWall(data, width, height);
                break;
            case EnemyType.FriendlyHelper:
                DrawFriendlyHelper(data, width, height);
                break;
            case EnemyType.YourReflection:
                DrawYourReflection(data, width, height);
                break;
            case EnemyType.ItsListening:
                DrawItsListening(data, width, height);
                break;
            case EnemyType.TheHost:
                DrawTheHost(data, width, height);
                break;
        }

        texture.SetData(data);
        return texture;
    }

    private void DrawSmileDog(Color[] data, int w, int h)
    {
        int cx = w / 2, cy = h / 2;
        
        // Body (tan ellipse)
        FillEllipse(data, w, h, cx, cy + 10, 30, 20, new Color(210, 180, 140));
        
        // Head
        FillEllipse(data, w, h, cx, cy - 10, 25, 20, new Color(210, 180, 140));
        
        // Wide creepy smile
        for (int x = cx - 15; x <= cx + 15; x++)
        {
            int smileY = cy - 5 + (int)(8 * Math.Sin((x - cx + 15) * Math.PI / 30));
            SetPixelSafe(data, w, h, x, smileY, Color.DarkRed);
            SetPixelSafe(data, w, h, x, smileY + 1, new Color(40, 0, 0));
        }
        
        // Teeth
        for (int i = 0; i < 6; i++)
        {
            int tx = cx - 12 + i * 5;
            for (int ty = cy - 5; ty < cy; ty++)
                SetPixelSafe(data, w, h, tx, ty, Color.White);
        }
        
        // Eyes (wide and staring)
        FillEllipse(data, w, h, cx - 10, cy - 18, 6, 8, Color.White);
        FillEllipse(data, w, h, cx + 10, cy - 18, 6, 8, Color.White);
        FillEllipse(data, w, h, cx - 10, cy - 17, 3, 5, Color.Black);
        FillEllipse(data, w, h, cx + 10, cy - 17, 3, 5, Color.Black);
        
        // Red around eyes
        DrawEllipse(data, w, h, cx - 10, cy - 18, 6, 8, Color.Red);
        DrawEllipse(data, w, h, cx + 10, cy - 18, 6, 8, Color.Red);
    }

    private void DrawMeatChild(Color[] data, int w, int h)
    {
        int cx = w / 2, cy = h / 2;
        
        // Fleshy body
        FillEllipse(data, w, h, cx, cy + 5, 25, 35, new Color(200, 100, 100));
        
        // Veins
        for (int i = 0; i < 5; i++)
        {
            int startX = cx - 15 + i * 8;
            for (int y = cy - 10; y < cy + 30; y += 2)
            {
                SetPixelSafe(data, w, h, startX + (y % 3), y, new Color(120, 40, 40));
            }
        }
        
        // Head
        FillEllipse(data, w, h, cx, cy - 25, 15, 12, new Color(180, 120, 120));
        
        // Sad eyes
        FillEllipse(data, w, h, cx - 6, cy - 27, 4, 3, Color.Black);
        FillEllipse(data, w, h, cx + 6, cy - 27, 4, 3, Color.Black);
    }

    private void DrawGrandmasTwin(Color[] data, int w, int h)
    {
        int cx = w / 2, cy = h / 2;
        
        // Dress
        for (int y = cy - 5; y < h - 5; y++)
        {
            int width = 15 + (y - cy + 5) / 3;
            for (int x = cx - width; x <= cx + width; x++)
                SetPixelSafe(data, w, h, x, y, new Color(80, 60, 80));
        }
        
        // Face
        FillEllipse(data, w, h, cx, cy - 20, 18, 20, new Color(240, 230, 240));
        
        // Hair
        for (int i = 0; i < 7; i++)
        {
            int hx = cx - 12 + i * 4;
            for (int hy = cy - 38; hy < cy - 10; hy++)
                SetPixelSafe(data, w, h, hx, hy, new Color(180, 180, 190));
        }
        
        // Black pit eyes
        FillEllipse(data, w, h, cx - 7, cy - 22, 5, 6, Color.Black);
        FillEllipse(data, w, h, cx + 7, cy - 22, 5, 6, Color.Black);
        
        // Thin smile
        for (int x = cx - 8; x <= cx + 8; x++)
        {
            int smileY = cy - 12 + (int)(3 * Math.Sin((x - cx + 8) * Math.PI / 16));
            SetPixelSafe(data, w, h, x, smileY, new Color(150, 100, 100));
        }
    }

    private void DrawManInWall(Color[] data, int w, int h)
    {
        int cx = w / 2, cy = h / 2;
        
        // Ghostly torso emerging from wall
        for (int y = cy - 10; y < h; y++)
        {
            int alpha = Math.Max(50, 200 - (y - cy + 10) * 3);
            int width = 20 - (y - cy + 10) / 5;
            for (int x = cx - width; x <= cx + width; x++)
                SetPixelSafe(data, w, h, x, y, new Color(60, 60, 70, alpha));
        }
        
        // Reaching arm
        for (int i = 0; i < 30; i++)
        {
            int ax = cx + 15 + i;
            int ay = cy - 10 - i / 3;
            FillEllipse(data, w, h, ax, ay, 4, 4, new Color(70, 70, 80));
        }
        
        // Fingers
        for (int f = 0; f < 5; f++)
        {
            for (int i = 0; i < 10; i++)
            {
                int fx = cx + 45 + i;
                int fy = cy - 20 + f * 4 + i / 3;
                SetPixelSafe(data, w, h, fx, fy, new Color(60, 60, 70));
            }
        }
        
        // Face
        FillEllipse(data, w, h, cx, cy - 25, 14, 16, new Color(50, 50, 60));
        
        // Single glowing eye
        FillEllipse(data, w, h, cx, cy - 25, 5, 6, Color.White);
        FillEllipse(data, w, h, cx, cy - 25, 2, 3, new Color(150, 150, 200));
    }

    private void DrawFriendlyHelper(Color[] data, int w, int h)
    {
        int cx = w / 2, cy = h / 2;
        
        // Bright yellow body
        FillEllipse(data, w, h, cx, cy + 10, 25, 35, Color.LightYellow);
        
        // Head
        FillEllipse(data, w, h, cx, cy - 20, 22, 22, Color.LightYellow);
        DrawEllipse(data, w, h, cx, cy - 20, 22, 22, Color.Gold);
        
        // Wide creepy smile
        for (int x = cx - 15; x <= cx + 15; x++)
        {
            int smileY = cy - 12 + (int)(10 * Math.Sin((x - cx + 15) * Math.PI / 30));
            SetPixelSafe(data, w, h, x, smileY, Color.Black);
            SetPixelSafe(data, w, h, x, smileY + 1, new Color(255, 150, 150));
        }
        
        // Unblinking eyes
        FillEllipse(data, w, h, cx - 8, cy - 25, 6, 8, Color.White);
        FillEllipse(data, w, h, cx + 8, cy - 25, 6, 8, Color.White);
        DrawEllipse(data, w, h, cx - 8, cy - 25, 6, 8, Color.Red);
        DrawEllipse(data, w, h, cx + 8, cy - 25, 6, 8, Color.Red);
        FillEllipse(data, w, h, cx - 8, cy - 25, 2, 4, Color.Black);
        FillEllipse(data, w, h, cx + 8, cy - 25, 2, 4, Color.Black);
    }

    private void DrawYourReflection(Color[] data, int w, int h)
    {
        int cx = w / 2, cy = h / 2;
        
        // Translucent ghostly body
        FillEllipse(data, w, h, cx, cy + 5, 22, 40, new Color(150, 100, 200, 150));
        
        // Face
        FillEllipse(data, w, h, cx, cy - 30, 18, 20, new Color(200, 200, 220, 180));
        
        // Mirror-like eyes
        FillEllipse(data, w, h, cx - 7, cy - 32, 5, 6, Color.White);
        FillEllipse(data, w, h, cx + 7, cy - 32, 5, 6, Color.White);
        FillEllipse(data, w, h, cx - 7, cy - 32, 2, 3, Color.Black);
        FillEllipse(data, w, h, cx + 7, cy - 32, 2, 3, Color.Black);
    }

    private void DrawItsListening(Color[] data, int w, int h)
    {
        int cx = w / 2, cy = h / 2;
        
        // Dark mass
        FillEllipse(data, w, h, cx, cy, 35, 45, Color.Black);
        
        // Multiple ears scattered around
        var rand = new Random(123);
        for (int i = 0; i < 6; i++)
        {
            int ex = cx - 25 + rand.Next(50);
            int ey = cy - 30 + rand.Next(60);
            FillEllipse(data, w, h, ex, ey, 8, 10, new Color(60, 40, 50));
            FillEllipse(data, w, h, ex, ey, 3, 4, Color.Black); // Canal
        }
        
        // Single red eye in center
        FillEllipse(data, w, h, cx, cy, 6, 8, Color.Red);
        FillEllipse(data, w, h, cx, cy, 2, 4, Color.Black);
    }

    private void DrawTheHost(Color[] data, int w, int h)
    {
        int cx = w / 2, cy = h / 2;
        
        // Massive crimson body
        FillEllipse(data, w, h, cx, cy + 5, 45, 50, Color.Crimson);
        
        // Horns
        for (int i = 0; i < 5; i++)
        {
            int hx = cx - 20 + i * 10;
            for (int hy = 5; hy < 25; hy++)
            {
                int width = Math.Max(1, 4 - hy / 6);
                for (int dx = -width; dx <= width; dx++)
                    SetPixelSafe(data, w, h, hx + dx, cy - 35 - hy + (i % 2) * 8, new Color(40, 20, 20));
            }
        }
        
        // Multiple eyes (2 rows of 3)
        for (int row = 0; row < 2; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                int ex = cx - 18 + col * 18;
                int ey = cy - 15 + row * 15;
                FillEllipse(data, w, h, ex, ey, 5, 6, Color.Yellow);
                FillEllipse(data, w, h, ex, ey, 2, 3, Color.Black);
            }
        }
        
        // Gaping mouth with teeth
        FillEllipse(data, w, h, cx, cy + 25, 20, 12, Color.Black);
        for (int i = 0; i < 7; i++)
        {
            int tx = cx - 15 + i * 5;
            for (int ty = cy + 18; ty < cy + 26; ty++)
                SetPixelSafe(data, w, h, tx, ty, Color.White);
        }
    }

    private void FillEllipse(Color[] data, int w, int h, int cx, int cy, int rx, int ry, Color color)
    {
        for (int y = cy - ry; y <= cy + ry; y++)
        {
            for (int x = cx - rx; x <= cx + rx; x++)
            {
                float dx = (x - cx) / (float)rx;
                float dy = (y - cy) / (float)ry;
                if (dx * dx + dy * dy <= 1)
                    SetPixelSafe(data, w, h, x, y, color);
            }
        }
    }

    private void DrawEllipse(Color[] data, int w, int h, int cx, int cy, int rx, int ry, Color color)
    {
        for (int angle = 0; angle < 360; angle += 5)
        {
            double rad = angle * Math.PI / 180;
            int x = cx + (int)(rx * Math.Cos(rad));
            int y = cy + (int)(ry * Math.Sin(rad));
            SetPixelSafe(data, w, h, x, y, color);
        }
    }

    private void SetPixelSafe(Color[] data, int w, int h, int x, int y, Color color)
    {
        if (x >= 0 && x < w && y >= 0 && y < h)
            data[y * w + x] = color;
    }

    // ==================== WEAPON TEXTURES ====================
    
    private Texture2D CreateWeaponTexture(WeaponType type, int w, int h)
    {
        var texture = new Texture2D(_graphicsDevice, w, h);
        var data = new Color[w * h];
        
        // Start with transparent
        for (int i = 0; i < data.Length; i++)
            data[i] = Color.Transparent;

        bool isOrganic = false;
        
        switch (type)
        {
            case WeaponType.SharpCrayon:
                DrawSharpCrayonSprite(data, w, h);
                isOrganic = false; // Wax crayon
                break;
            case WeaponType.TeddyMaw:
                DrawTeddyMawSprite(data, w, h);
                isOrganic = true; // Fleshy bear mouth
                break;
            case WeaponType.JackInTheGun:
                DrawJackInTheGunSprite(data, w, h);
                isOrganic = false; // Metal box
                break;
            case WeaponType.MyFirstNailer:
                DrawMyFirstNailerSprite(data, w, h);
                isOrganic = false; // Plastic toy
                break;
            case WeaponType.SippyCannon:
                DrawSippyCannonSprite(data, w, h);
                isOrganic = true; // Something organic inside
                break;
            case WeaponType.MusicBoxDancer:
                DrawMusicBoxDancerSprite(data, w, h);
                isOrganic = true; // The ballerina is... organic now
                break;
        }

        // Apply pixel art enhancements
        ApplyWeaponEnhancements(data, w, h, isOrganic);

        texture.SetData(data);
        return texture;
    }

    #region Duke Nukem 3D Style Weapon Sprites
    
    // ========== SHARED DUKE3D STYLE PALETTES ==========
    private static readonly Color[] Duke3DSkin = {
        new Color(255, 210, 170),  // Highlight - very bright
        new Color(245, 190, 150),  // Light
        new Color(220, 165, 125),  // Mid-light
        new Color(190, 135, 100),  // Base
        new Color(155, 105, 75),   // Mid-dark
        new Color(115, 75, 50),    // Dark
        new Color(80, 50, 35),     // Deep shadow
        new Color(50, 30, 20)      // Outline
    };
    
    private static readonly Color[] Duke3DMetal = {
        new Color(255, 255, 255),  // Specular highlight
        new Color(220, 225, 235),  // Bright
        new Color(180, 190, 205),  // Light
        new Color(140, 150, 170),  // Mid
        new Color(100, 110, 130),  // Dark
        new Color(65, 75, 95),     // Deep
        new Color(40, 45, 60),     // Shadow
        new Color(20, 25, 35)      // Outline
    };
    
    private static readonly Color[] Duke3DFlesh = {
        new Color(255, 180, 170),  // Highlight
        new Color(230, 145, 135),  // Light
        new Color(195, 115, 110),  // Mid-light
        new Color(165, 85, 85),    // Base
        new Color(130, 60, 65),    // Mid-dark
        new Color(95, 40, 45),     // Dark
        new Color(65, 25, 30),     // Deep
        new Color(35, 15, 18)      // Outline
    };
    
    private static readonly Color[] Duke3DBone = {
        new Color(255, 252, 240),  // Highlight
        new Color(245, 238, 215),  // Light
        new Color(225, 215, 185),  // Mid-light
        new Color(200, 188, 155),  // Base
        new Color(170, 155, 125),  // Mid-dark
        new Color(135, 120, 95),   // Dark
        new Color(95, 80, 60),     // Deep
        new Color(55, 45, 35)      // Outline
    };
    
    private static readonly Color[] Duke3DWood = {
        new Color(140, 95, 60),    // Highlight
        new Color(115, 75, 45),    // Light
        new Color(90, 58, 35),     // Mid-light
        new Color(70, 45, 25),     // Base
        new Color(52, 32, 18),     // Mid-dark
        new Color(38, 22, 12),     // Dark
        new Color(25, 15, 8),      // Deep
        new Color(15, 8, 4)        // Outline
    };
    
    // Duke3D style helper: Draw chunky, bold arm from screen corner
    private void DrawDuke3DArm(Color[] data, int w, int h, int endX, int endY, float scale)
    {
        int steps = (int)(200 * scale);
        float startRadius = 140 * scale;
        float endRadius = 55 * scale;
        
        for (int i = 0; i < steps; i++)
        {
            float t = (float)i / steps;
            // Start from bottom-right corner, curve to weapon
            int cx = (int)(w + 80 * scale - t * (w + 80 * scale - endX));
            int cy = (int)(h + 120 * scale - t * (h + 120 * scale - endY));
            float radius = startRadius + (endRadius - startRadius) * t;
            
            DrawDuke3DLitCircle(data, w, h, cx, cy, (int)radius, Duke3DSkin, true);
        }
    }
    
    // Duke3D style: Bold lit circle with optional outline - PIXEL ART VERSION
    private void DrawDuke3DLitCircle(Color[] data, int w, int h, int cx, int cy, int radius, Color[] palette, bool outline = false)
    {
        int r2 = radius * radius;
        int outerR2 = (radius + 1) * (radius + 1);
        int innerR2 = (radius - 2) * (radius - 2);
        
        for (int dy = -radius - 1; dy <= radius + 1; dy++)
        {
            for (int dx = -radius - 1; dx <= radius + 1; dx++)
            {
                int dist2 = dx * dx + dy * dy;
                
                // Outline - 2px thick dark edge
                if (outline && dist2 > innerR2 && dist2 <= outerR2 && dist2 > r2 - radius)
                {
                    SetPixelSafe(data, w, h, cx + dx, cy + dy, palette[palette.Length - 1]);
                    continue;
                }
                
                if (dist2 > r2) continue;
                
                // Pixelated lighting - divide into discrete bands
                // Light from top-left
                int lightBand = 3 - (dx + dy) / (radius / 3 + 1);
                lightBand = Math.Clamp(lightBand, 0, palette.Length - 2);
                
                SetPixelSafe(data, w, h, cx + dx, cy + dy, palette[lightBand]);
            }
        }
    }
    
    // Duke3D style: Bold finger with knuckles
    private void DrawDuke3DFinger(Color[] data, int w, int h, int startX, int startY, int length, int width, float curveFactor, Color[] palette)
    {
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / length;
            int fx = startX - i;
            int fy = startY + (int)(MathF.Pow(t, 1.5f) * curveFactor);
            int fWidth = (int)(width * (1 - t * 0.35f));
            if (fWidth < 4) fWidth = 4;
            
            // Draw finger segment with cylindrical shading
            for (int dy = -fWidth; dy <= fWidth; dy++)
            {
                for (int dx = -3; dx <= 3; dx++)
                {
                    float ny = (float)dy / fWidth;
                    float light = 0.5f - ny * 0.5f;
                    light = MathF.Pow(Math.Clamp(light, 0, 1), 0.6f);
                    
                    int idx = (int)((1 - light) * (palette.Length - 2));
                    idx = Math.Clamp(idx, 0, palette.Length - 2);
                    
                    SetPixelSafe(data, w, h, fx + dx, fy + dy, palette[idx]);
                }
            }
            
            // Knuckle wrinkles
            if (i == length / 3 || i == 2 * length / 3)
            {
                for (int dy = -fWidth + 2; dy <= fWidth - 2; dy++)
                {
                    SetPixelSafe(data, w, h, fx, fy + dy, palette[5]);
                    SetPixelSafe(data, w, h, fx + 1, fy + dy, palette[6]);
                }
            }
        }
        
        // Fingernail at tip
        int tipX = startX - length + 4;
        int tipY = startY + (int)curveFactor;
        int nailW = width / 2;
        int nailH = width / 3;
        
        for (int ny = -nailH; ny <= nailH; ny++)
        {
            for (int nx = -nailW; nx <= 2; nx++)
            {
                float dist = MathF.Sqrt((float)(nx * nx) / (nailW * nailW + 1) + (float)(ny * ny) / (nailH * nailH + 1));
                if (dist > 1) continue;
                Color nc = dist < 0.4f ? new Color(255, 250, 245) : new Color(235, 220, 210);
                SetPixelSafe(data, w, h, tipX + nx, tipY + ny, nc);
            }
        }
    }
    
    // Duke3D style: Bold thumb
    private void DrawDuke3DThumb(Color[] data, int w, int h, int cx, int cy, int length, int width, Color[] palette)
    {
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / length;
            int tx = cx - (int)(t * length * 0.4f);
            int ty = cy - (int)(t * length);
            int tWidth = (int)(width * (1 - t * 0.3f));
            
            DrawDuke3DLitCircle(data, w, h, tx, ty, tWidth, palette, false);
        }
        
        // Thumbnail
        int tipX = cx - (int)(length * 0.4f);
        int tipY = cy - length + 5;
        for (int ny = -width / 3; ny <= width / 4; ny++)
        {
            for (int nx = -width / 2; nx <= 3; nx++)
            {
                float dist = MathF.Sqrt((float)(nx * nx) / (width * width / 4 + 1) + (float)(ny * ny) / (width * width / 9 + 1));
                if (dist > 1) continue;
                Color nc = dist < 0.4f ? new Color(255, 250, 245) : new Color(235, 220, 210);
                SetPixelSafe(data, w, h, tipX + nx, tipY + ny, nc);
            }
        }
    }

    private void DrawSharpCrayonSprite(Color[] data, int w, int h)
    {
        // === UNCANNY WEAPON - A child's crayon sharpened to a lethal point ===
        void P(int x, int y, Color c) { if (x >= 0 && x < w && y >= 0 && y < h) data[y * w + x] = c; }
        
        // Burnt Sienna crayon colors (brown-orange, classic crayon)
        Color C0 = new Color(255, 180, 140); // Highlight
        Color C1 = new Color(205, 125, 90);  // Light
        Color C2 = new Color(160, 85, 55);   // Mid (Burnt Sienna)
        Color C3 = new Color(120, 60, 35);   // Dark
        Color C4 = new Color(80, 40, 20);    // Shadow
        
        // Paper wrapper colors (aged/torn)
        Color P0 = new Color(240, 235, 220); // Clean paper
        Color P1 = new Color(200, 190, 170); // Aged
        Color P2 = new Color(160, 150, 130); // Dirty
        Color PB = new Color(30, 20, 15);    // Paper text (black)
        
        // Blood stains
        Color B0 = new Color(140, 30, 30);
        Color B1 = new Color(100, 20, 20);
        Color B2 = new Color(60, 10, 10);
        
        // === CRAYON BODY (cylindrical, slightly warped from use) ===
        for (int cx = 10; cx <= 52; cx++)
        {
            int baseY = 30;
            int radius = 9;
            // Slight waviness like it's been gripped hard
            float warp = MathF.Sin(cx * 0.2f) * 1.5f;
            
            for (int cy = (int)(baseY - radius + warp); cy <= (int)(baseY + radius + warp); cy++)
            {
                float normY = (cy - baseY - warp) / radius;
                // Cylindrical shading
                float light = MathF.Sqrt(1 - normY * normY);
                int shade = (int)((1 - light) * 4);
                shade = Math.Clamp(shade, 0, 4);
                Color c = shade == 0 ? C1 : shade == 1 ? C2 : shade == 2 ? C3 : C4;
                P(cx, cy, c);
            }
        }
        
        // === SHARPENED TIP (lethally pointed) ===
        for (int tx = 0; tx < 12; tx++)
        {
            int tipY = 30;
            int tipRad = 9 - (tx * 9 / 12);
            if (tipRad < 1) tipRad = 1;
            
            for (int ty = tipY - tipRad; ty <= tipY + tipRad; ty++)
            {
                float normY = (float)(ty - tipY) / tipRad;
                float light = MathF.Sqrt(1 - normY * normY);
                int shade = (int)((1 - light) * 4) + tx / 4;
                shade = Math.Clamp(shade, 0, 4);
                Color c = shade == 0 ? C0 : shade == 1 ? C1 : shade == 2 ? C2 : shade == 3 ? C3 : C4;
                P(10 - tx, ty, c);
            }
        }
        // Extremely sharp point
        P(-2, 30, C3); P(-3, 30, C4);
        
        // === PAPER WRAPPER (torn and aged) ===
        for (int px = 24; px <= 48; px++)
        {
            int paperY = 30;
            int paperRad = 10;
            // Torn edge at one side
            bool isTornEdge = px < 28 && (px + 30) % 3 == 0;
            
            if (!isTornEdge)
            {
                for (int py = paperY - paperRad; py <= paperY + paperRad; py++)
                {
                    float normY = (float)(py - paperY) / paperRad;
                    if (MathF.Abs(normY) > 0.9f) continue;
                    
                    float light = MathF.Sqrt(1 - normY * normY);
                    int shade = (int)((1 - light) * 2);
                    Color c = shade == 0 ? P0 : shade == 1 ? P1 : P2;
                    P(px, py, c);
                }
            }
        }
        
        // === CRAYOLA-STYLE TEXT "BURNT SIENNA" (barely readable) ===
        // Simplified "BURNT" text
        for (int tx = 28; tx <= 32; tx++) P(tx, 26, PB); // B
        P(28, 27, PB); P(28, 28, PB); P(28, 29, PB); P(30, 28, PB);
        
        // === CREEPY CHILD'S SCRAWL on wrapper ===
        // "HELP" scratched into the wrapper faintly
        P(35, 33, B1); P(35, 34, B1); P(35, 35, B1); P(36, 34, B1); // H
        P(37, 35, B1); // E
        P(38, 33, B1); P(38, 34, B1); P(38, 35, B1); // L
        P(40, 33, B1); P(40, 34, B1); // P
        
        // === BLOOD STAINS (dried, old) ===
        // On the tip
        P(2, 28, B0); P(3, 29, B1); P(4, 31, B0); P(5, 32, B1);
        P(0, 30, B2); P(1, 31, B1); P(2, 32, B0);
        // Drip down the side
        P(15, 36, B1); P(15, 37, B0); P(14, 38, B1); P(15, 39, B2);
        P(16, 40, B1); P(15, 41, B2);
        
        // === BITE MARKS (a child chewed on this) ===
        for (int bx = 50; bx <= 52; bx++)
        {
            P(bx, 25, C4); P(bx, 35, C4);
        }
        
        // === ONE UNBLINKING EYE (scratched into the wax at the back) ===
        // Eye carved into the flat back of the crayon
        for (int ey = -3; ey <= 3; ey++)
            for (int ex = -4; ex <= 4; ex++)
                if (ex*ex/2 + ey*ey <= 9) P(54 + ex, 30 + ey, P0);
        // Iris (unsettling brown)
        P(54, 29, C4); P(55, 29, C4); P(54, 30, C4); P(55, 30, C4);
        P(53, 30, C3); P(56, 30, C3);
        // It's watching
        P(54, 30, PB); P(55, 30, PB);
    }
    
    // Simple rectangle fill for chunky pixel art
    private void FillRect(Color[] data, int stride, int x1, int y1, int x2, int y2, Color c)
    {
        for (int y = y1; y < y2; y++)
            for (int x = x1; x < x2; x++)
                if (x >= 0 && x < stride && y >= 0 && y < stride)
                    data[y * stride + x] = c;
    }
    
    // Helper: Draw a shaded pixel art rectangle (no circles!)
    private void DrawPixelRect(Color[] data, int w, int h, int x1, int y1, int x2, int y2, 
        Color main, Color light, Color dark)
    {
        // Clamp to bounds
        x1 = Math.Max(0, x1);
        y1 = Math.Max(0, y1);
        x2 = Math.Min(w - 1, x2);
        y2 = Math.Min(h - 1, y2);
        
        int rw = x2 - x1;
        int rh = y2 - y1;
        if (rw <= 0 || rh <= 0) return;
        
        for (int y = y1; y <= y2; y++)
        {
            for (int x = x1; x <= x2; x++)
            {
                // Simple 3-band shading: top light, middle main, bottom dark
                Color c;
                int band = (y - y1) * 3 / (rh + 1);
                if (band == 0) c = light;
                else if (band == 1) c = main;
                else c = dark;
                
                // Left edge slightly lighter
                if (x < x1 + 3) c = light;
                // Right edge slightly darker  
                if (x > x2 - 3) c = dark;
                
                SetPixelSafe(data, w, h, x, y, c);
            }
        }
        
        // 1px dark outline
        for (int x = x1; x <= x2; x++)
        {
            SetPixelSafe(data, w, h, x, y1, new Color(20, 15, 10));
            SetPixelSafe(data, w, h, x, y2, new Color(20, 15, 10));
        }
        for (int y = y1; y <= y2; y++)
        {
            SetPixelSafe(data, w, h, x1, y, new Color(20, 15, 10));
            SetPixelSafe(data, w, h, x2, y, new Color(20, 15, 10));
        }
    }
    #endregion

    // Simple lit circle for body parts
    private void DrawSimpleLitCircle(Color[] data, int w, int h, int cx, int cy, int radius, 
        Color light, Color midLight, Color mid, Color midDark, Color dark)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                if (dist > radius) continue;
                
                // Simple gradient: top-left bright, bottom-right dark
                float lightVal = 0.5f - (dx + dy) / (radius * 3f);
                lightVal = Math.Clamp(lightVal, 0, 1);
                
                Color c;
                if (lightVal > 0.7f) c = light;
                else if (lightVal > 0.5f) c = midLight;
                else if (lightVal > 0.3f) c = mid;
                else if (lightVal > 0.15f) c = midDark;
                else c = dark;
                
                SetPixelSafe(data, w, h, cx + dx, cy + dy, c);
            }
        }
    }
    
    // === SHARED HELPER: Draw 3D-shaded arm (used by multiple weapons) ===
    private void DrawArm3D(Color[] data, int w, int h, int x1, int y1, int x2, int y2, float r1, float r2, Color[] palette)
    {
        int steps = 80;
        for (int i = 0; i < steps; i++)
        {
            float t = (float)i / steps;
            int cx = (int)(x1 + (x2 - x1) * t);
            int cy = (int)(y1 + (y2 - y1) * t);
            float radius = r1 + (r2 - r1) * t;
            
            for (int dy = (int)-radius; dy <= (int)radius; dy++)
            {
                for (int dx = (int)-radius; dx <= (int)radius; dx++)
                {
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    if (dist > radius) continue;
                    
                    float lightX = -dx / radius;
                    float lightY = -dy / radius;
                    float light = (lightX * 0.5f + lightY * 0.7f + 0.5f);
                    light = MathF.Pow(Math.Clamp(light, 0, 1), 0.8f);
                    
                    int colorIdx = (int)((1 - light) * (palette.Length - 1));
                    colorIdx = Math.Clamp(colorIdx, 0, palette.Length - 1);
                    
                    SetPixelSafe(data, w, h, cx + dx, cy + dy, palette[colorIdx]);
                }
            }
        }
    }
    
    // === SHARED HELPER: Draw 3D sphere (used by multiple weapons) ===
    private void Draw3DSphere(Color[] data, int w, int h, int cx, int cy, int rx, int ry, Color[] palette)
    {
        for (int y = -ry; y <= ry; y++)
        {
            for (int x = -rx; x <= rx; x++)
            {
                float nx = (float)x / rx;
                float ny = (float)y / ry;
                float dist = MathF.Sqrt(nx * nx + ny * ny);
                if (dist > 1) continue;
                
                float nz = MathF.Sqrt(1 - dist * dist);
                float light = -nx * 0.4f - ny * 0.6f + nz * 0.7f;
                light = (light + 1) / 2;
                light = MathF.Pow(Math.Clamp(light, 0, 1), 0.7f);
                
                int colorIdx = (int)((1 - light) * (palette.Length - 1));
                colorIdx = Math.Clamp(colorIdx, 0, palette.Length - 1);
                
                SetPixelSafe(data, w, h, cx + x, cy + y, palette[colorIdx]);
            }
        }
    }
    
    // === SHARED HELPER: Draw curved finger (used by multiple weapons) ===
    private void DrawCurvedFinger3D(Color[] data, int w, int h, int startX, int startY, int length, float radius, Color[] palette)
    {
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / length;
            float angle = t * 2.5f - 0.3f;
            
            int fx = startX - (int)(MathF.Cos(angle) * 25);
            int fy = startY + (int)(MathF.Sin(angle) * 12);
            float r = radius * (1 - t * 0.35f);
            
            for (int dy = (int)-r; dy <= (int)r; dy++)
            {
                for (int dx = (int)-r; dx <= (int)r; dx++)
                {
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    if (dist > r) continue;
                    
                    float light = (1 - dist / r) * 0.6f + MathF.Cos(angle - 1) * 0.4f;
                    light = Math.Clamp(light, 0, 1);
                    
                    int colorIdx = (int)((1 - light) * (palette.Length - 1));
                    colorIdx = Math.Clamp(colorIdx, 0, palette.Length - 1);
                    
                    SetPixelSafe(data, w, h, fx + dx, fy + dy, palette[colorIdx]);
                }
            }
        }
    }
    
    // === SHARED HELPER: Draw thumb (used by multiple weapons) ===
    private void DrawThumb3D(Color[] data, int w, int h, int cx, int cy, Color[] palette)
    {
        for (int i = 0; i < 35; i++)
        {
            float t = (float)i / 35;
            int tx = cx - (int)(t * 35);
            int ty = cy + (int)(MathF.Sin(t * 2) * 15);
            float r = 10 - t * 3;
            
            for (int dy = (int)-r; dy <= (int)r; dy++)
            {
                for (int dx = (int)-r; dx <= (int)r; dx++)
                {
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    if (dist > r) continue;
                    
                    float light = (1 - dist / r) * 0.7f + (1 - t) * 0.3f;
                    int colorIdx = (int)((1 - light) * (palette.Length - 1));
                    colorIdx = Math.Clamp(colorIdx, 0, palette.Length - 1);
                    
                    SetPixelSafe(data, w, h, tx + dx, ty + dy, palette[colorIdx]);
                }
            }
        }
    }
    
    // === HELPER: Cross-guard ===
    private void Draw3DGuard(Color[] data, int w, int h, int cx, int cy, int width, int height, Color[] palette)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = -width / 2; x <= width / 2; x++)
            {
                float nx = (float)x / (width / 2);
                float ny = (float)y / height;
                
                // Curved surface
                float curve = MathF.Sin(ny * MathF.PI);
                float light = (1 - MathF.Abs(nx)) * curve;
                light = 0.3f + light * 0.7f;
                
                int colorIdx = (int)((1 - light) * (palette.Length - 1));
                colorIdx = Math.Clamp(colorIdx, 0, palette.Length - 1);
                
                SetPixelSafe(data, w, h, cx + x, cy + y, palette[colorIdx]);
            }
        }
    }
    
    // === HELPER: Blade with bevel ===
    private void Draw3DBlade(Color[] data, int w, int h, int cx, int top, int bot, int maxWidth, Color[] palette)
    {
        for (int y = top; y <= bot; y++)
        {
            float yProgress = (float)(y - top) / (bot - top);
            int halfWidth = (int)(3 + yProgress * (maxWidth - 3));
            
            for (int x = -halfWidth; x <= halfWidth; x++)
            {
                float nx = (float)x / halfWidth;
                
                // Double bevel - light on left edge, shadow on right
                float light;
                if (nx < -0.7f) // Left edge highlight
                    light = 0.95f;
                else if (nx < 0) // Left bevel
                    light = 0.7f + nx * 0.3f;
                else // Right bevel
                    light = 0.7f - nx * 0.4f;
                
                // Add gradient toward tip
                light += (1 - yProgress) * 0.15f;
                light = Math.Clamp(light, 0, 1);
                
                int colorIdx = (int)((1 - light) * (palette.Length - 1));
                colorIdx = Math.Clamp(colorIdx, 0, palette.Length - 1);
                
                SetPixelSafe(data, w, h, cx + x, y, palette[colorIdx]);
            }
        }
        
        // Blade tip
        for (int i = 0; i < 20; i++)
        {
            float t = (float)i / 20;
            int tipW = (int)(3 * (1 - t));
            int tipY = top - i;
            
            for (int x = -tipW; x <= tipW; x++)
            {
                float light = 0.8f - MathF.Abs((float)x / (tipW + 1)) * 0.3f;
                light = Math.Clamp(light, 0.3f, 1);
                
                int colorIdx = (int)((1 - light) * (palette.Length - 1));
                colorIdx = Math.Clamp(colorIdx, 0, palette.Length - 1);
                
                SetPixelSafe(data, w, h, cx + x, tipY, palette[colorIdx]);
            }
        }
    }
    
    // === HELPER: Blood drop ===
    private void DrawBloodDrop(Color[] data, int w, int h, int cx, int cy, int size, Color dark, Color mid)
    {
        for (int dy = 0; dy < size * 2; dy++)
        {
            int dropWidth = dy < size ? (int)(size * MathF.Sin((float)dy / size * MathF.PI / 2)) : 
                                        Math.Max(1, size - (dy - size) / 2);
            for (int dx = -dropWidth; dx <= dropWidth; dx++)
            {
                float light = 1 - MathF.Abs((float)dx / (dropWidth + 1));
                Color c = LerpColor(dark, mid, light * 0.6f);
                SetPixelSafe(data, w, h, cx + dx, cy + dy, c);
            }
        }
    }
    
    private void DrawDetailedRust(Color[] data, int w, int h, int cx, int cy, Color rust1, Color rust2, Color rust3, int size)
    {
        var rand = new Random(cx * 100 + cy);
        for (int i = 0; i < size * 2; i++)
        {
            int rx = cx + rand.Next(-size / 2, size / 2 + 1);
            int ry = cy + rand.Next(-size / 3, size / 3 + 1);
            float dist = (float)Math.Sqrt(Math.Pow(rx - cx, 2) + Math.Pow(ry - cy, 2)) / (size / 2f);
            Color c = dist < 0.3f ? rust3 : (dist < 0.6f ? rust2 : rust1);
            SetPixelSafe(data, w, h, rx, ry, c);
            if (rand.Next(3) == 0)
                SetPixelSafe(data, w, h, rx + 1, ry, LerpColor(c, rust1, 0.5f));
        }
    }
    
    private void DrawBloodDrip(Color[] data, int w, int h, int x, int startY, int length, Color blood1, Color blood2, Color blood3, Color wet)
    {
        var rand = new Random(x * 50 + startY);
        int currentX = x;
        
        for (int i = 0; i < length; i++)
        {
            float progress = (float)i / length;
            // Drip gets thinner
            int thickness = Math.Max(1, (int)(3 * (1f - progress * 0.6f)));
            
            // Slight wandering
            if (rand.Next(4) == 0) currentX += rand.Next(-1, 2);
            
            Color c = progress < 0.2f ? wet : LerpColor(blood1, blood3, progress);
            
            for (int t = -thickness / 2; t <= thickness / 2; t++)
            {
                float tNorm = Math.Abs(t) / (float)(thickness / 2 + 1);
                Color tc = LerpColor(c, blood2, tNorm * 0.5f);
                SetPixelSafe(data, w, h, currentX + t, startY + i, tc);
            }
        }
        // Droplet at end
        FillEllipse(data, w, h, currentX, startY + length + 3, 3, 4, blood2);
        SetPixelSafe(data, w, h, currentX - 1, startY + length + 1, wet);
    }
    
    private void DrawRustPatch(Color[] data, int w, int h, int cx, int cy, Color rust1, Color rust2)
    {
        var rand = new Random(cx * 100 + cy);
        for (int i = 0; i < 12; i++)
        {
            int rx = cx + rand.Next(-4, 5);
            int ry = cy + rand.Next(-3, 4);
            Color c = rand.Next(2) == 0 ? rust1 : rust2;
            SetPixelSafe(data, w, h, rx, ry, c);
        }
    }
    
    private Color LerpColor(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Color(
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t),
            (int)(a.A + (b.A - a.A) * t)
        );
    }

    #region Advanced Pixel Art Tools
    
    /// <summary>
    /// Apply ordered (Bayer) dithering for smoother gradients with limited colors
    /// </summary>
    private void ApplyOrderedDithering(Color[] data, int w, int h, int levels = 4)
    {
        // 4x4 Bayer matrix for ordered dithering
        float[,] bayerMatrix = {
            { 0f/16f,  8f/16f,  2f/16f, 10f/16f },
            { 12f/16f, 4f/16f, 14f/16f,  6f/16f },
            { 3f/16f, 11f/16f,  1f/16f,  9f/16f },
            { 15f/16f, 7f/16f, 13f/16f,  5f/16f }
        };
        
        float levelStep = 255f / levels;
        
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                if (data[idx].A == 0) continue;
                
                float threshold = bayerMatrix[x % 4, y % 4] - 0.5f;
                
                int r = (int)Math.Clamp(data[idx].R + threshold * levelStep, 0, 255);
                int g = (int)Math.Clamp(data[idx].G + threshold * levelStep, 0, 255);
                int b = (int)Math.Clamp(data[idx].B + threshold * levelStep, 0, 255);
                
                // Quantize to levels
                r = (int)(Math.Round(r / levelStep) * levelStep);
                g = (int)(Math.Round(g / levelStep) * levelStep);
                b = (int)(Math.Round(b / levelStep) * levelStep);
                
                data[idx] = new Color(r, g, b, data[idx].A);
            }
        }
    }
    
    /// <summary>
    /// Add pixel art style outlines (dark edges)
    /// </summary>
    private void ApplyOutline(Color[] data, int w, int h, Color outlineColor, int thickness = 1)
    {
        Color[] original = new Color[data.Length];
        Array.Copy(data, original, data.Length);
        
        for (int y = thickness; y < h - thickness; y++)
        {
            for (int x = thickness; x < w - thickness; x++)
            {
                int idx = y * w + x;
                if (original[idx].A == 0)
                {
                    // Check if any neighbor is non-transparent
                    bool hasNeighbor = false;
                    for (int dy = -thickness; dy <= thickness && !hasNeighbor; dy++)
                    {
                        for (int dx = -thickness; dx <= thickness && !hasNeighbor; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int ni = (y + dy) * w + (x + dx);
                            if (ni >= 0 && ni < original.Length && original[ni].A > 128)
                                hasNeighbor = true;
                        }
                    }
                    if (hasNeighbor)
                        data[idx] = outlineColor;
                }
            }
        }
    }
    
    /// <summary>
    /// Hue shifting in shadows (classic pixel art technique - shadows shift toward purple/blue)
    /// </summary>
    private void ApplyHueShiftShadows(Color[] data, int w, int h, float shiftAmount = 0.1f)
    {
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i].A == 0) continue;
            
            // Calculate luminance
            float lum = (data[i].R * 0.299f + data[i].G * 0.587f + data[i].B * 0.114f) / 255f;
            
            // In dark areas, shift hue toward blue/purple
            if (lum < 0.5f)
            {
                float shift = (0.5f - lum) * 2 * shiftAmount;
                int r = (int)Math.Clamp(data[i].R * (1 - shift * 0.3f), 0, 255);
                int g = (int)Math.Clamp(data[i].G * (1 - shift * 0.2f), 0, 255);
                int b = (int)Math.Clamp(data[i].B + shift * 30, 0, 255);
                data[i] = new Color(r, g, b, data[i].A);
            }
        }
    }
    
    /// <summary>
    /// Add specular highlights for shiny surfaces
    /// </summary>
    private void ApplySpecularHighlights(Color[] data, int w, int h, float intensity = 0.3f, float threshold = 0.7f)
    {
        for (int y = 1; y < h - 1; y++)
        {
            for (int x = 1; x < w - 1; x++)
            {
                int idx = y * w + x;
                if (data[idx].A == 0) continue;
                
                float lum = (data[idx].R * 0.299f + data[idx].G * 0.587f + data[idx].B * 0.114f) / 255f;
                
                if (lum > threshold)
                {
                    // Add white highlight
                    float highlightStrength = (lum - threshold) / (1 - threshold) * intensity;
                    int r = (int)Math.Clamp(data[idx].R + 255 * highlightStrength, 0, 255);
                    int g = (int)Math.Clamp(data[idx].G + 255 * highlightStrength, 0, 255);
                    int b = (int)Math.Clamp(data[idx].B + 255 * highlightStrength, 0, 255);
                    data[idx] = new Color(r, g, b, data[idx].A);
                }
            }
        }
    }
    
    /// <summary>
    /// Add ambient occlusion - darken areas near edges and corners
    /// </summary>
    private void ApplyAmbientOcclusion(Color[] data, int w, int h, int radius = 3, float strength = 0.3f)
    {
        Color[] original = new Color[data.Length];
        Array.Copy(data, original, data.Length);
        
        for (int y = radius; y < h - radius; y++)
        {
            for (int x = radius; x < w - radius; x++)
            {
                int idx = y * w + x;
                if (original[idx].A == 0) continue;
                
                // Count nearby transparent pixels (edges)
                int edgeCount = 0;
                int totalChecked = 0;
                
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int ni = (y + dy) * w + (x + dx);
                        if (ni >= 0 && ni < original.Length)
                        {
                            totalChecked++;
                            if (original[ni].A < 128)
                                edgeCount++;
                        }
                    }
                }
                
                // Darken based on proximity to edges
                float occlusion = (float)edgeCount / totalChecked * strength;
                if (occlusion > 0.01f)
                {
                    int r = (int)(data[idx].R * (1 - occlusion));
                    int g = (int)(data[idx].G * (1 - occlusion));
                    int b = (int)(data[idx].B * (1 - occlusion));
                    data[idx] = new Color(r, g, b, data[idx].A);
                }
            }
        }
    }
    
    /// <summary>
    /// Add film grain / noise for texture
    /// </summary>
    private void ApplyNoiseGrain(Color[] data, int w, int h, float intensity = 0.05f, int seed = 42)
    {
        var rand = new Random(seed);
        
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i].A == 0) continue;
            
            float noise = ((float)rand.NextDouble() - 0.5f) * 2 * intensity * 255;
            
            int r = (int)Math.Clamp(data[i].R + noise, 0, 255);
            int g = (int)Math.Clamp(data[i].G + noise, 0, 255);
            int b = (int)Math.Clamp(data[i].B + noise, 0, 255);
            
            data[i] = new Color(r, g, b, data[i].A);
        }
    }
    
    /// <summary>
    /// Add sub-surface scattering effect for skin/organic materials
    /// </summary>
    private void ApplySubsurfaceScattering(Color[] data, int w, int h, Color scatterColor, float intensity = 0.15f)
    {
        Color[] original = new Color[data.Length];
        Array.Copy(data, original, data.Length);
        
        // Simple blur + tint for SSS approximation
        for (int y = 2; y < h - 2; y++)
        {
            for (int x = 2; x < w - 2; x++)
            {
                int idx = y * w + x;
                if (original[idx].A == 0) continue;
                
                // Sample neighbors
                int sumR = 0, sumG = 0, sumB = 0, count = 0;
                for (int dy = -2; dy <= 2; dy++)
                {
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        int ni = (y + dy) * w + (x + dx);
                        if (original[ni].A > 0)
                        {
                            sumR += original[ni].R;
                            sumG += original[ni].G;
                            sumB += original[ni].B;
                            count++;
                        }
                    }
                }
                
                if (count > 0)
                {
                    // Blend original with blurred + scatter tint
                    float avgR = sumR / (float)count;
                    float avgG = sumG / (float)count;
                    float avgB = sumB / (float)count;
                    
                    // Add scatter color influence
                    avgR = avgR * (1 - intensity) + scatterColor.R * intensity;
                    avgG = avgG * (1 - intensity) + scatterColor.G * intensity;
                    avgB = avgB * (1 - intensity) + scatterColor.B * intensity;
                    
                    // Blend with original
                    int r = (int)Math.Clamp(data[idx].R * 0.7f + avgR * 0.3f, 0, 255);
                    int g = (int)Math.Clamp(data[idx].G * 0.7f + avgG * 0.3f, 0, 255);
                    int b = (int)Math.Clamp(data[idx].B * 0.7f + avgB * 0.3f, 0, 255);
                    
                    data[idx] = new Color(r, g, b, data[idx].A);
                }
            }
        }
    }
    
    /// <summary>
    /// Anti-aliasing for smoother edges
    /// </summary>
    private void ApplyAntiAliasing(Color[] data, int w, int h)
    {
        Color[] original = new Color[data.Length];
        Array.Copy(data, original, data.Length);
        
        for (int y = 1; y < h - 1; y++)
        {
            for (int x = 1; x < w - 1; x++)
            {
                int idx = y * w + x;
                
                // Check if this is an edge pixel (has both transparent and opaque neighbors)
                bool hasTransparent = false;
                bool hasOpaque = false;
                
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int ni = (y + dy) * w + (x + dx);
                        if (original[ni].A < 64) hasTransparent = true;
                        else if (original[ni].A > 192) hasOpaque = true;
                    }
                }
                
                // If edge pixel, blend with neighbors
                if (hasTransparent && hasOpaque && original[idx].A > 128)
                {
                    int sumR = original[idx].R * 4;
                    int sumG = original[idx].G * 4;
                    int sumB = original[idx].B * 4;
                    int sumA = original[idx].A * 4;
                    int weight = 4;
                    
                    // Cardinal neighbors
                    int[] dx = { 0, 0, -1, 1 };
                    int[] dy = { -1, 1, 0, 0 };
                    
                    for (int i = 0; i < 4; i++)
                    {
                        int ni = (y + dy[i]) * w + (x + dx[i]);
                        if (original[ni].A > 32)
                        {
                            sumR += original[ni].R;
                            sumG += original[ni].G;
                            sumB += original[ni].B;
                            sumA += original[ni].A;
                            weight++;
                        }
                    }
                    
                    data[idx] = new Color(sumR / weight, sumG / weight, sumB / weight, sumA / weight);
                }
            }
        }
    }
    
    /// <summary>
    /// Add rim lighting effect (backlight glow on edges)
    /// </summary>
    private void ApplyRimLighting(Color[] data, int w, int h, Color rimColor, float intensity = 0.4f, int direction = 1)
    {
        Color[] original = new Color[data.Length];
        Array.Copy(data, original, data.Length);
        
        for (int y = 1; y < h - 1; y++)
        {
            for (int x = 1; x < w - 1; x++)
            {
                int idx = y * w + x;
                if (original[idx].A == 0) continue;
                
                // Check edge on the "back" side (opposite to light direction)
                int checkX = x + direction;
                int checkY = y + direction;
                
                bool isEdge = false;
                if (checkX >= 0 && checkX < w && checkY >= 0 && checkY < h)
                {
                    int edgeIdx = checkY * w + checkX;
                    if (original[edgeIdx].A < 64)
                        isEdge = true;
                }
                
                if (isEdge)
                {
                    int r = (int)Math.Clamp(data[idx].R + rimColor.R * intensity, 0, 255);
                    int g = (int)Math.Clamp(data[idx].G + rimColor.G * intensity, 0, 255);
                    int b = (int)Math.Clamp(data[idx].B + rimColor.B * intensity, 0, 255);
                    data[idx] = new Color(r, g, b, data[idx].A);
                }
            }
        }
    }
    
    /// <summary>
    /// Apply all enhancements to a weapon sprite
    /// </summary>
    private void ApplyWeaponEnhancements(Color[] data, int w, int h, bool isOrganic)
    {
        // For pixel art style - minimal post-processing, no anti-aliasing
        // Just add a slight outline for definition
        ApplyPixelArtOutline(data, w, h);
    }
    
    // Simple black outline for pixel art definition
    private void ApplyPixelArtOutline(Color[] data, int w, int h)
    {
        var outline = new Color[w * h];
        Color outlineColor = new Color(20, 15, 10);
        
        for (int y = 1; y < h - 1; y++)
        {
            for (int x = 1; x < w - 1; x++)
            {
                int idx = y * w + x;
                if (data[idx].A > 128) continue; // Skip non-transparent
                
                // Check if adjacent to non-transparent pixel
                bool hasNeighbor = 
                    data[idx - 1].A > 128 ||
                    data[idx + 1].A > 128 ||
                    data[idx - w].A > 128 ||
                    data[idx + w].A > 128;
                
                if (hasNeighbor)
                    outline[idx] = outlineColor;
            }
        }
        
        // Apply outline
        for (int i = 0; i < data.Length; i++)
        {
            if (outline[i].A > 0)
                data[i] = outline[i];
        }
    }

    #endregion

    private void DrawTeddyMawSprite(Color[] data, int w, int h)
    {
        // === UNCANNY WEAPON - Mr. Huggles: A teddy bear whose mouth opens too wide ===
        void P(int x, int y, Color c) { if (x >= 0 && x < w && y >= 0 && y < h) data[y * w + x] = c; }
        
        // Faded brown teddy fur (loved too much)
        Color F0 = new Color(200, 175, 145); // Highlight (worn)
        Color F1 = new Color(170, 140, 105); // Light
        Color F2 = new Color(140, 110, 75);  // Mid
        Color F3 = new Color(105, 80, 50);   // Dark
        Color F4 = new Color(70, 50, 30);    // Shadow
        
        // Inner mouth (fleshy, wrong)
        Color M0 = new Color(180, 80, 90);   // Gum highlight
        Color M1 = new Color(140, 50, 60);   // Flesh
        Color M2 = new Color(100, 30, 40);   // Dark flesh
        Color M3 = new Color(50, 15, 20);    // Throat void
        
        // Button eye (one missing)
        Color BTN = new Color(20, 15, 10);
        Color THR = new Color(180, 60, 60);  // Thread (red, like blood)
        
        // Stitches
        Color STH = new Color(80, 60, 40);
        
        Color BK = new Color(10, 5, 5);
        
        // === TEDDY BODY (round, huggable, worn) ===
        int bodyCx = 38, bodyCy = 40;
        for (int by = -16; by <= 16; by++)
        {
            for (int bx = -14; bx <= 14; bx++)
            {
                if (bx*bx/196f + by*by/256f > 1) continue;
                float normX = (float)bx / 14;
                float normY = (float)by / 16;
                float light = 0.5f - normX * 0.3f - normY * 0.2f;
                int shade = (int)((1 - light) * 4);
                shade = Math.Clamp(shade, 0, 4);
                Color c = shade == 0 ? F0 : shade == 1 ? F1 : shade == 2 ? F2 : shade == 3 ? F3 : F4;
                P(bodyCx + bx, bodyCy + by, c);
            }
        }
        
        // === HEAD (tilted, staring) ===
        int headCx = 18, headCy = 26;
        for (int hy = -12; hy <= 12; hy++)
        {
            for (int hx = -11; hx <= 11; hx++)
            {
                if (hx*hx + hy*hy > 144) continue;
                float normX = (float)hx / 11;
                float normY = (float)hy / 12;
                float light = 0.5f - normX * 0.3f - normY * 0.2f;
                int shade = (int)((1 - light) * 4);
                shade = Math.Clamp(shade, 0, 4);
                Color c = shade == 0 ? F0 : shade == 1 ? F1 : shade == 2 ? F2 : shade == 3 ? F3 : F4;
                P(headCx + hx, headCy + hy, c);
            }
        }
        
        // === EARS (round, floppy) ===
        for (int ey = -5; ey <= 5; ey++)
            for (int ex = -5; ex <= 5; ex++)
                if (ex*ex + ey*ey <= 25) {
                    P(8 + ex, 16 + ey, F2);
                    P(28 + ex, 16 + ey, F2);
                }
        // Inner ear
        for (int ey = -3; ey <= 3; ey++)
            for (int ex = -3; ex <= 3; ex++)
                if (ex*ex + ey*ey <= 9) {
                    P(8 + ex, 16 + ey, M1);
                    P(28 + ex, 16 + ey, M1);
                }
        
        // === THE MAW (opens too wide, unhinging) ===
        // Upper jaw lifted
        for (int mx = -8; mx <= 8; mx++)
        {
            int jawY = 30 - Math.Abs(mx) / 3;
            for (int my = jawY; my >= jawY - 4; my--)
                P(headCx + mx, my, F3);
        }
        // Lower jaw dropped WAY down
        for (int mx = -8; mx <= 8; mx++)
        {
            int jawY = 36 + Math.Abs(mx) / 2;
            for (int my = 32; my <= jawY; my++)
                P(headCx + mx, my, F3);
        }
        // The void inside (dark throat)
        for (int my = 28; my <= 38; my++)
        {
            int mawWidth = 6 - Math.Abs(my - 33) / 2;
            for (int mx = -mawWidth; mx <= mawWidth; mx++)
            {
                float depth = 1 - MathF.Abs(mx) / (mawWidth + 1f);
                Color c = depth > 0.6f ? M3 : depth > 0.3f ? M2 : M1;
                P(headCx + mx, my, c);
            }
        }
        // Teeth (baby teeth, too many)
        for (int tx = -5; tx <= 5; tx += 2)
        {
            P(headCx + tx, 29, Color.White);
            P(headCx + tx, 30, new Color(240, 235, 220));
            P(headCx + tx, 36, Color.White);
            P(headCx + tx, 35, new Color(240, 235, 220));
        }
        
        // === BUTTON EYE (one remaining, staring) ===
        for (int ey = -4; ey <= 4; ey++)
            for (int ex = -4; ex <= 4; ex++)
                if (ex*ex + ey*ey <= 16) P(24 + ex, 22 + ey, BTN);
        // Shine
        P(22, 20, new Color(60, 55, 50));
        
        // === MISSING EYE (just thread and void) ===
        // Empty socket
        for (int ey = -3; ey <= 3; ey++)
            for (int ex = -3; ex <= 3; ex++)
                if (ex*ex + ey*ey <= 9) P(12 + ex, 22 + ey, M3);
        // Dangling thread
        P(12, 26, THR); P(11, 27, THR); P(12, 28, THR); P(10, 29, THR);
        P(11, 30, THR); P(9, 31, THR);
        
        // === STITCHES (coming undone) ===
        for (int sy = 0; sy < 20; sy += 3)
        {
            P(38, 32 + sy, STH); P(39, 33 + sy, STH);
        }
        // Loose thread
        P(40, 50, STH); P(42, 51, STH); P(43, 53, STH); P(45, 54, STH);
        
        // === ARM (reaching toward you) ===
        for (int ax = 0; ax < 14; ax++)
        {
            int armY = 36 + ax / 2;
            for (int ay = -4; ay <= 4; ay++)
                P(52 + ax, armY + ay, F2);
        }
        // Paw
        for (int py = -5; py <= 5; py++)
            for (int px = -5; px <= 5; px++)
                if (px*px + py*py <= 25) P(66 + px, 44 + py, F1);
        // Paw pad
        for (int py = -2; py <= 2; py++)
            for (int px = -2; px <= 2; px++)
                if (px*px + py*py <= 4) P(66 + px, 44 + py, M1);
        
        // === SOMETHING DRIPPING from the mouth ===
        Color DRIP = new Color(120, 40, 50);
        P(18, 40, DRIP); P(17, 42, DRIP); P(18, 44, DRIP); 
        P(16, 46, DRIP); P(17, 48, DRIP);
    }
    
    // Duke3D style vein helper
    private void DrawDuke3DVein(Color[] data, int w, int h, int x1, int y1, int x2, int y2, float s)
    {
        Color[] vein = { new Color(150, 50, 60), new Color(110, 35, 45), new Color(75, 20, 30) };
        int steps = Math.Max(Math.Abs(x2 - x1), Math.Abs(y2 - y1));
        float veinW = 5 * s;
        
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            int x = x1 + (int)((x2 - x1) * t);
            int y = y1 + (int)((y2 - y1) * t) + (int)(MathF.Sin(i * 0.3f / s) * 3 * s);
            
            for (int dy = (int)-veinW; dy <= (int)veinW; dy++)
            {
                float light = 1 - MathF.Abs(dy) / (veinW + 1);
                int idx = (int)((1 - light) * 2);
                SetPixelSafe(data, w, h, x, y + dy, vein[Math.Clamp(idx, 0, 2)]);
            }
        }
    }
    
    // Scaled helper methods
    private void DrawArm3DScaled(Color[] data, int w, int h, int x1, int y1, int x2, int y2, float r1, float r2, Color[] palette)
    {
        int steps = Math.Max(80, (int)(160 * (w / 256f)));
        for (int i = 0; i < steps; i++)
        {
            float t = (float)i / steps;
            int cx = (int)(x1 + (x2 - x1) * t);
            int cy = (int)(y1 + (y2 - y1) * t);
            float radius = r1 + (r2 - r1) * t;
            
            for (int dy = (int)-radius; dy <= (int)radius; dy++)
            {
                for (int dx = (int)-radius; dx <= (int)radius; dx++)
                {
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    if (dist > radius) continue;
                    
                    float lightX = -dx / radius;
                    float lightY = -dy / radius;
                    float light = (lightX * 0.5f + lightY * 0.7f + 0.5f);
                    light = MathF.Pow(Math.Clamp(light, 0, 1), 0.8f);
                    
                    int colorIdx = (int)((1 - light) * (palette.Length - 1));
                    colorIdx = Math.Clamp(colorIdx, 0, palette.Length - 1);
                    
                    SetPixelSafe(data, w, h, cx + dx, cy + dy, palette[colorIdx]);
                }
            }
        }
    }
    
    private void Draw3DSphereScaled(Color[] data, int w, int h, int cx, int cy, int rx, int ry, Color[] palette)
    {
        for (int y = -ry; y <= ry; y++)
        {
            for (int x = -rx; x <= rx; x++)
            {
                float nx = (float)x / rx;
                float ny = (float)y / ry;
                float dist = MathF.Sqrt(nx * nx + ny * ny);
                if (dist > 1) continue;
                
                float nz = MathF.Sqrt(1 - dist * dist);
                float light = -nx * 0.4f - ny * 0.6f + nz * 0.7f;
                light = (light + 1) / 2;
                light = MathF.Pow(Math.Clamp(light, 0, 1), 0.7f);
                
                int colorIdx = (int)((1 - light) * (palette.Length - 1));
                colorIdx = Math.Clamp(colorIdx, 0, palette.Length - 1);
                
                SetPixelSafe(data, w, h, cx + x, cy + y, palette[colorIdx]);
            }
        }
    }
    
    private void DrawOrganicFinger3DScaled(Color[] data, int w, int h, int startX, int startY, int length, float radius, Color[] skin, Color[] flesh)
    {
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / length;
            float angle = t * 2.2f;
            
            int fx = startX - (int)(MathF.Cos(angle) * 44 * (w / 256f));
            int fy = startY + (int)(MathF.Sin(angle) * 20 * (w / 256f));
            float r = radius * (1 - t * 0.3f);
            
            float fleshBlend = Math.Clamp(t * 1.5f, 0, 1);
            
            for (int dy = (int)-r; dy <= (int)r; dy++)
            {
                for (int dx = (int)-r; dx <= (int)r; dx++)
                {
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    if (dist > r) continue;
                    
                    float light = (1 - dist / r) * 0.6f + MathF.Cos(angle - 1) * 0.4f;
                    light = Math.Clamp(light, 0, 1);
                    
                    int skinIdx = (int)((1 - light) * (skin.Length - 1));
                    skinIdx = Math.Clamp(skinIdx, 0, skin.Length - 1);
                    int fleshIdx = (int)((1 - light) * (flesh.Length - 1));
                    fleshIdx = Math.Clamp(fleshIdx, 0, flesh.Length - 1);
                    
                    Color c = LerpColor(skin[skinIdx], flesh[fleshIdx], fleshBlend);
                    SetPixelSafe(data, w, h, fx + dx, fy + dy, c);
                }
            }
        }
    }
    
    private void DrawThumb3DScaled(Color[] data, int w, int h, int cx, int cy, Color[] palette, float s)
    {
        for (int i = 0; i < (int)(70 * s); i++)
        {
            float t = (float)i / (70 * s);
            int tx = cx - (int)(t * 70 * s);
            int ty = cy + (int)(MathF.Sin(t * 2) * 30 * s);
            float r = (20 - t * 6) * s;
            
            for (int dy = (int)-r; dy <= (int)r; dy++)
            {
                for (int dx = (int)-r; dx <= (int)r; dx++)
                {
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    if (dist > r) continue;
                    
                    float light = (1 - dist / r) * 0.7f + (1 - t) * 0.3f;
                    int colorIdx = (int)((1 - light) * (palette.Length - 1));
                    colorIdx = Math.Clamp(colorIdx, 0, palette.Length - 1);
                    
                    SetPixelSafe(data, w, h, tx + dx, ty + dy, palette[colorIdx]);
                }
            }
        }
    }
    
    private void DrawOrganicBody3DScaled(Color[] data, int w, int h, int cx, int cy, int rx, int ry, Color[] palette)
    {
        for (int y = -ry; y <= ry; y++)
        {
            for (int x = -rx; x <= rx; x++)
            {
                float nx = (float)x / rx;
                float ny = (float)y / ry;
                float dist = MathF.Sqrt(nx * nx + ny * ny);
                if (dist > 1) continue;
                
                float nz = MathF.Sqrt(1 - dist * dist);
                float light = -nx * 0.35f - ny * 0.55f + nz * 0.75f;
                light = (light + 1) / 2;
                light = MathF.Pow(Math.Clamp(light, 0, 1), 0.65f);
                
                int colorIdx = (int)((1 - light) * (palette.Length - 1));
                colorIdx = Math.Clamp(colorIdx, 0, palette.Length - 1);
                
                SetPixelSafe(data, w, h, cx + x, cy + y, palette[colorIdx]);
            }
        }
    }
    
    private void DrawVein3DScaled(Color[] data, int w, int h, int x1, int y1, int x2, int y2, Color[] palette, float s)
    {
        int steps = Math.Max(Math.Abs(x2 - x1), Math.Abs(y2 - y1));
        float veinWidth = 4 * s;
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            int x = x1 + (int)((x2 - x1) * t);
            int y = y1 + (int)((y2 - y1) * t) + (int)(MathF.Sin(i * 0.4f / s) * 4 * s);
            
            for (int dy = (int)-veinWidth; dy <= (int)veinWidth; dy++)
            {
                float light = 1 - MathF.Abs(dy) / (veinWidth + 1);
                int idx = (int)((1 - light) * (palette.Length - 1));
                idx = Math.Clamp(idx, 0, palette.Length - 1);
                SetPixelSafe(data, w, h, x, y + dy, palette[idx]);
            }
        }
    }
    
    private void DrawPustule3DScaled(Color[] data, int w, int h, int cx, int cy, int radius, Color[] palette)
    {
        Color pusYellow = new Color(230, 220, 160);
        Color pusHighlight = new Color(255, 250, 220);
        
        for (int y = -radius - 1; y <= radius + 1; y++)
        {
            for (int x = -radius - 1; x <= radius + 1; x++)
            {
                float dist = MathF.Sqrt(x * x + y * y);
                if (dist > radius + 1) continue;
                SetPixelSafe(data, w, h, cx + x, cy + y, palette[palette.Length - 2]);
            }
        }
        
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                float dist = MathF.Sqrt(x * x + y * y);
                if (dist > radius) continue;
                
                float nz = MathF.Sqrt(1 - (dist / radius) * (dist / radius));
                float light = -x / (float)radius * 0.3f - y / (float)radius * 0.5f + nz * 0.8f;
                light = (light + 1) / 2;
                
                Color c = light > 0.8f ? pusHighlight : (light > 0.5f ? pusYellow : palette[(int)((1 - light) * 2)]);
                SetPixelSafe(data, w, h, cx + x, cy + y, c);
            }
        }
    }
    
    private void Draw3DTriggerScaled(Color[] data, int w, int h, int cx, int cy, int length, Color[] palette)
    {
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / length;
            int tx = cx;
            int ty = cy + i;
            float r = 6 * (w / 256f) * (1 - t * 0.3f);
            
            for (int dy = (int)-r; dy <= (int)r; dy++)
            {
                for (int dx = (int)-r; dx <= (int)r; dx++)
                {
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    if (dist > r) continue;
                    
                    float light = 0.5f - dx / r * 0.4f;
                    int idx = (int)((1 - light) * (palette.Length - 1));
                    idx = Math.Clamp(idx, 0, palette.Length - 1);
                    SetPixelSafe(data, w, h, tx + dx, ty + dy, palette[idx]);
                }
            }
        }
    }
    
    // === HELPER: Draw organic finger that merges with flesh ===
    private void DrawOrganicFinger3D(Color[] data, int w, int h, int startX, int startY, int length, float radius, Color[] skin, Color[] flesh)
    {
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / length;
            float angle = t * 2.2f;
            
            int fx = startX - (int)(MathF.Cos(angle) * 22);
            int fy = startY + (int)(MathF.Sin(angle) * 10);
            float r = radius * (1 - t * 0.3f);
            
            // Blend from skin to flesh along finger
            float fleshBlend = Math.Clamp(t * 1.5f, 0, 1);
            
            for (int dy = (int)-r; dy <= (int)r; dy++)
            {
                for (int dx = (int)-r; dx <= (int)r; dx++)
                {
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    if (dist > r) continue;
                    
                    float light = (1 - dist / r) * 0.6f + MathF.Cos(angle - 1) * 0.4f;
                    light = Math.Clamp(light, 0, 1);
                    
                    // Get skin color
                    int skinIdx = (int)((1 - light) * (skin.Length - 1));
                    skinIdx = Math.Clamp(skinIdx, 0, skin.Length - 1);
                    
                    // Get flesh color
                    int fleshIdx = (int)((1 - light) * (flesh.Length - 1));
                    fleshIdx = Math.Clamp(fleshIdx, 0, flesh.Length - 1);
                    
                    Color c = LerpColor(skin[skinIdx], flesh[fleshIdx], fleshBlend);
                    SetPixelSafe(data, w, h, fx + dx, fy + dy, c);
                }
            }
        }
    }
    
    // === HELPER: Organic body with 3D sphere shading ===
    private void DrawOrganicBody3D(Color[] data, int w, int h, int cx, int cy, int rx, int ry, Color[] palette)
    {
        for (int y = -ry; y <= ry; y++)
        {
            for (int x = -rx; x <= rx; x++)
            {
                float nx = (float)x / rx;
                float ny = (float)y / ry;
                float dist = MathF.Sqrt(nx * nx + ny * ny);
                if (dist > 1) continue;
                
                // Sphere normal for organic roundness
                float nz = MathF.Sqrt(1 - dist * dist);
                
                // Light from top-left-front
                float light = -nx * 0.35f - ny * 0.55f + nz * 0.75f;
                light = (light + 1) / 2;
                light = MathF.Pow(Math.Clamp(light, 0, 1), 0.65f);
                
                int colorIdx = (int)((1 - light) * (palette.Length - 1));
                colorIdx = Math.Clamp(colorIdx, 0, palette.Length - 1);
                
                SetPixelSafe(data, w, h, cx + x, cy + y, palette[colorIdx]);
            }
        }
    }
    
    // === HELPER: 3D Vein ===
    private void DrawVein3D(Color[] data, int w, int h, int x1, int y1, int x2, int y2, Color[] palette)
    {
        int steps = Math.Max(Math.Abs(x2 - x1), Math.Abs(y2 - y1));
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            int x = x1 + (int)((x2 - x1) * t);
            int y = y1 + (int)((y2 - y1) * t) + (int)(MathF.Sin(i * 0.4f) * 2);
            
            // Vein is a small raised cylinder
            for (int dy = -2; dy <= 2; dy++)
            {
                float light = 1 - MathF.Abs(dy) / 2.5f;
                int idx = (int)((1 - light) * (palette.Length - 1));
                idx = Math.Clamp(idx, 0, palette.Length - 1);
                SetPixelSafe(data, w, h, x, y + dy, palette[idx]);
            }
        }
    }
    
    // === HELPER: 3D Pustule ===
    private void DrawPustule3D(Color[] data, int w, int h, int cx, int cy, int radius, Color[] palette)
    {
        Color pusYellow = new Color(230, 220, 160);
        Color pusHighlight = new Color(255, 250, 220);
        
        // Base (sunken area)
        for (int y = -radius - 1; y <= radius + 1; y++)
        {
            for (int x = -radius - 1; x <= radius + 1; x++)
            {
                float dist = MathF.Sqrt(x * x + y * y);
                if (dist > radius + 1) continue;
                SetPixelSafe(data, w, h, cx + x, cy + y, palette[palette.Length - 2]);
            }
        }
        
        // Raised blister
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                float dist = MathF.Sqrt(x * x + y * y);
                if (dist > radius) continue;
                
                float light = 1 - dist / radius;
                light = MathF.Pow(light, 0.5f);
                
                Color c = LerpColor(palette[2], pusYellow, light * 0.8f);
                if (x < 0 && y < 0 && dist < radius * 0.4f)
                    c = LerpColor(c, pusHighlight, 0.5f);
                
                SetPixelSafe(data, w, h, cx + x, cy + y, c);
            }
        }
    }
    
    // === HELPER: 3D Bone trigger ===
    private void Draw3DTrigger(Color[] data, int w, int h, int cx, int cy, int length, Color[] bone)
    {
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / length;
            int ty = cy + i;
            int width = (int)(5 * (1 - t * 0.4f));
            
            for (int dx = -width; dx <= width; dx++)
            {
                float light = 1 - MathF.Abs((float)dx / (width + 1));
                light = light * 0.7f + 0.3f;
                
                int idx = (int)((1 - light) * (bone.Length - 1));
                idx = Math.Clamp(idx, 0, bone.Length - 1);
                
                SetPixelSafe(data, w, h, cx + dx, ty, bone[idx]);
            }
        }
    }
    
    private void DrawOrganicVein(Color[] data, int w, int h, int x1, int y1, int x2, int y2, Color dark, Color mid, int thickness)
    {
        int steps = Math.Max(Math.Abs(x2 - x1), Math.Abs(y2 - y1));
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            int x = x1 + (int)((x2 - x1) * t);
            int y = y1 + (int)((y2 - y1) * t);
            // Slight waviness
            x += (int)(Math.Sin(i * 0.5) * 2);
            
            for (int th = -thickness; th <= thickness; th++)
            {
                float dist = Math.Abs(th) / (float)thickness;
                Color c = LerpColor(dark, mid, 1 - dist);
                SetPixelSafe(data, w, h, x, y + th, c);
            }
        }
    }

    private void DrawJackInTheGunSprite(Color[] data, int w, int h)
    {
        // === UNCANNY WEAPON - Jack-in-the-Box with a gun barrel instead of a clown ===
        void P(int x, int y, Color c) { if (x >= 0 && x < w && y >= 0 && y < h) data[y * w + x] = c; }
        
        // Faded circus colors (old, sun-bleached)
        Color RED = new Color(200, 70, 70);
        Color REDD = new Color(150, 45, 45);
        Color YEL = new Color(240, 210, 100);
        Color YELD = new Color(190, 160, 70);
        Color BLU = new Color(100, 130, 180);
        Color BLUD = new Color(60, 90, 140);
        
        // Metal (gun barrel, crank)
        Color M0 = new Color(180, 180, 190);
        Color M1 = new Color(130, 130, 145);
        Color M2 = new Color(85, 85, 100);
        Color M3 = new Color(45, 45, 60);
        
        // Wood box (chipped paint)
        Color W0 = new Color(160, 130, 90);
        Color W1 = new Color(120, 95, 60);
        Color W2 = new Color(80, 60, 35);
        
        // Clown face (emerging from spring, twisted)
        Color SKIN = new Color(255, 220, 200);
        Color SKIND = new Color(200, 160, 140);
        Color NOSE = new Color(255, 80, 80);
        
        Color BK = new Color(15, 10, 10);
        
        // === THE BOX (slightly open, lid ajar) ===
        // Front face
        for (int by = 32; by <= 58; by++)
        {
            for (int bx = 28; bx <= 58; bx++)
            {
                int stripe = ((bx - 28) / 5 + (by - 32) / 5) % 2;
                Color c = stripe == 0 ? (bx < 43 ? RED : REDD) : (bx < 43 ? YEL : YELD);
                P(bx, by, c);
            }
        }
        // Side of box
        for (int by = 34; by <= 58; by++)
        {
            for (int bx = 58; bx <= 64; bx++)
            {
                P(bx, by, W1);
            }
        }
        // Box edge highlight
        for (int by = 32; by <= 58; by++) P(28, by, W0);
        for (int bx = 28; bx <= 58; bx++) P(bx, 58, W2);
        
        // === LID (open, hinged at back) ===
        for (int ly = 24; ly <= 32; ly++)
        {
            for (int lx = 28 - (32 - ly); lx <= 58 - (32 - ly) / 2; lx++)
            {
                int stripe = ((lx - 20) / 5 + (ly - 24) / 3) % 2;
                Color c = stripe == 0 ? BLU : BLUD;
                P(lx, ly, c);
            }
        }
        
        // === THE SPRING (coiled, emerging) ===
        for (int sy = 0; sy < 20; sy++)
        {
            int springX = 20 - sy / 2;
            float coil = MathF.Sin(sy * 0.8f) * 4;
            for (int sw = -3; sw <= 3; sw++)
            {
                float light = 1 - MathF.Abs(sw) / 4f;
                Color c = LerpColor(M2, M0, light);
                P((int)(springX + coil + sw), 30 + sy / 2, c);
            }
        }
        
        // === CLOWN HEAD (twisted, wrong) ===
        int clownX = 10, clownY = 24;
        // Face (pale)
        for (int cy = -8; cy <= 8; cy++)
            for (int cx = -7; cx <= 7; cx++)
                if (cx*cx + cy*cy <= 56) {
                    float light = 0.5f - cx * 0.05f - cy * 0.03f;
                    Color c = light > 0.4f ? SKIN : SKIND;
                    P(clownX + cx, clownY + cy, c);
                }
        // Red nose (too big)
        for (int ny = -3; ny <= 3; ny++)
            for (int nx = -3; nx <= 3; nx++)
                if (nx*nx + ny*ny <= 9) P(clownX + nx, clownY + ny, NOSE);
        
        // === BUT WHERE THE EYES SHOULD BE: GUN BARRELS ===
        // Left barrel
        for (int bx = 0; bx < 12; bx++)
        {
            for (int by = -2; by <= 2; by++)
            {
                float light = 1 - MathF.Abs(by) / 3f;
                Color c = LerpColor(M3, M1, light);
                P(clownX - 4 - bx, clownY - 3 + by, c);
            }
        }
        // Right barrel
        for (int bx = 0; bx < 12; bx++)
        {
            for (int by = -2; by <= 2; by++)
            {
                float light = 1 - MathF.Abs(by) / 3f;
                Color c = LerpColor(M3, M1, light);
                P(clownX - 4 - bx, clownY + 3 + by, c);
            }
        }
        // Dark barrel holes
        P(-4, clownY - 3, BK); P(-3, clownY - 3, BK);
        P(-4, clownY + 3, BK); P(-3, clownY + 3, BK);
        
        // Clown smile (frozen, too wide)
        for (int mx = -5; mx <= 5; mx++)
        {
            int smileY = clownY + 5 + Math.Abs(mx) / 2;
            P(clownX + mx, smileY, BK);
            P(clownX + mx, smileY + 1, new Color(100, 30, 40));
        }
        
        // === CRANK HANDLE (on side of box) ===
        int crankX = 62, crankY = 46;
        // Arm
        for (int ca = 0; ca < 8; ca++)
            P(crankX + ca, crankY, M1);
        // Handle
        for (int ch = -4; ch <= 4; ch++)
            P(crankX + 8, crankY + ch, M0);
        
        // === MUZZLE FLASH (something just fired) ===
        Color FLASH = new Color(255, 240, 150);
        P(-6, clownY - 3, FLASH); P(-7, clownY - 4, FLASH); P(-7, clownY - 2, FLASH);
        P(-6, clownY + 3, FLASH); P(-7, clownY + 4, FLASH); P(-7, clownY + 2, FLASH);
        
        // === MUSIC NOTE (the song is playing) ===
        Color NOTE = new Color(60, 40, 80);
        P(8, 12, NOTE); P(9, 11, NOTE); P(10, 11, NOTE);
        P(8, 13, NOTE); P(8, 14, NOTE); P(8, 15, NOTE);
        for (int ny = -2; ny <= 2; ny++)
            for (int nx = -2; nx <= 2; nx++)
                if (nx*nx + ny*ny <= 4) P(6 + nx, 16 + ny, NOTE);
    }
    
    // Duke3D style eye socket
    private void DrawDuke3DEyeSocket(Color[] data, int w, int h, int cx, int cy, int size, Color dark)
    {
        // Dark cavity
        FillEllipse(data, w, h, cx, cy, size, size - 2, dark);
        FillEllipse(data, w, h, cx + 2, cy + 2, size / 3, size / 3, new Color(15, 10, 8));
        
        // Rim highlight
        for (int a = 0; a < 360; a += 15)
        {
            float angle = a * MathF.PI / 180;
            int rx = cx + (int)(MathF.Cos(angle) * size);
            int ry = cy + (int)(MathF.Sin(angle) * (size - 2));
            if (a > 180 && a < 300)
                SetPixelSafe(data, w, h, rx, ry, Duke3DBone[0]);
        }
    }
    
    // Duke3D style vertebra
    private void DrawDuke3DVertebra(Color[] data, int w, int h, int cx, int cy, int rx, int ry, float s, Color cavity)
    {
        // Main vertebra body
        for (int y = -ry; y <= ry; y++)
        {
            for (int x = -rx; x <= rx; x++)
            {
                float nx = (float)x / rx;
                float ny = (float)y / ry;
                float dist = MathF.Sqrt(nx * nx + ny * ny);
                if (dist > 1) continue;
                
                float nz = MathF.Sqrt(1 - dist * dist);
                float light = -nx * 0.3f - ny * 0.5f + nz * 0.7f;
                light = (light + 1) / 2;
                light = MathF.Pow(Math.Clamp(light, 0, 1), 0.6f);
                
                int idx = (int)((1 - light) * (Duke3DBone.Length - 2));
                idx = Math.Clamp(idx, 0, Duke3DBone.Length - 2);
                
                SetPixelSafe(data, w, h, cx + x, cy + y, Duke3DBone[idx]);
            }
        }
        
        // Spinal hole
        FillEllipse(data, w, h, cx, cy, rx / 2, ry / 2, cavity);
        
        // Spinous process (pointy bit)
        for (int i = 0; i < (int)(12 * s); i++)
        {
            int py = cy - ry - i;
            int pW = Math.Max(1, (int)((6 - i * 0.4f / s) * s));
            for (int dx = -pW; dx <= pW; dx++)
                SetPixelSafe(data, w, h, cx + dx, py, Duke3DBone[2]);
        }
    }

    // === HELPER: 3D Bone body ===
    private void DrawBoneBody3D(Color[] data, int w, int h, int cx, int cy, int rx, int ry, Color[] palette)
    {
        for (int y = -ry; y <= ry; y++)
        {
            for (int x = -rx; x <= rx; x++)
            {
                float nx = (float)x / rx;
                float ny = (float)y / ry;
                float dist = MathF.Sqrt(nx * nx + ny * ny);
                if (dist > 1) continue;
                
                float nz = MathF.Sqrt(1 - dist * dist);
                
                // Bone has harder specular than skin
                float light = -nx * 0.3f - ny * 0.5f + nz * 0.8f;
                light = (light + 1) / 2;
                light = MathF.Pow(Math.Clamp(light, 0, 1), 0.55f);
                
                // Add bone surface texture (micro-bumps)
                float texture = MathF.Sin(x * 0.3f) * MathF.Cos(y * 0.25f) * 0.08f;
                light += texture;
                light = Math.Clamp(light, 0, 1);
                
                int colorIdx = (int)((1 - light) * (palette.Length - 1));
                colorIdx = Math.Clamp(colorIdx, 0, palette.Length - 1);
                
                SetPixelSafe(data, w, h, cx + x, cy + y, palette[colorIdx]);
            }
        }
    }
    
    // === HELPER: Eye socket ===
    private void DrawEyeSocket(Color[] data, int w, int h, int cx, int cy, int size, Color dark, Color rim)
    {
        // Outer rim
        for (int y = -size; y <= size; y++)
        {
            for (int x = -size; x <= size; x++)
            {
                float dist = MathF.Sqrt(x * x + y * y);
                if (dist > size || dist < size - 2) continue;
                
                float light = 1 - MathF.Abs(dist - size + 1) / 2;
                Color c = LerpColor(dark, rim, light * 0.5f);
                SetPixelSafe(data, w, h, cx + x, cy + y, c);
            }
        }
        
        // Deep cavity
        FillEllipse(data, w, h, cx, cy, size - 2, size - 1, dark);
        FillEllipse(data, w, h, cx + 1, cy + 1, size / 3, size / 3, new Color(15, 10, 8));
    }
    
    // === HELPER: Vertebra ===
    private void DrawVertebra3D(Color[] data, int w, int h, int cx, int cy, int rx, int ry, Color[] bone, Color cavity)
    {
        // Main body
        for (int y = -ry; y <= ry; y++)
        {
            for (int x = -rx; x <= rx; x++)
            {
                float nx = (float)x / rx;
                float ny = (float)y / ry;
                float dist = MathF.Sqrt(nx * nx + ny * ny);
                if (dist > 1) continue;
                
                float nz = MathF.Sqrt(1 - dist * dist);
                float light = -nx * 0.3f - ny * 0.5f + nz * 0.7f;
                light = (light + 1) / 2;
                
                // Ridge in center
                if (MathF.Abs(ny) < 0.25f) light *= 0.85f;
                
                int idx = (int)((1 - light) * (bone.Length - 1));
                idx = Math.Clamp(idx, 0, bone.Length - 1);
                
                SetPixelSafe(data, w, h, cx + x, cy + y, bone[idx]);
            }
        }
        
        // Spinal process (top spike)
        for (int i = 0; i < 10; i++)
        {
            int sw = Math.Max(1, 5 - i / 2);
            float light = 0.5f + (float)i / 20;
            int idx = (int)((1 - light) * (bone.Length - 1));
            idx = Math.Clamp(idx, 0, bone.Length - 1);
            
            for (int dx = -sw; dx <= sw; dx++)
                SetPixelSafe(data, w, h, cx + dx, cy - ry - i, bone[idx]);
        }
        
        // Central foramen
        FillEllipse(data, w, h, cx, cy, 4, 5, cavity);
    }
    
    // === HELPER: Molar tooth ===
    private void DrawMolarTooth3D(Color[] data, int w, int h, int cx, int cy, int size, Color[] tooth, Color hole)
    {
        for (int y = -size; y <= size; y++)
        {
            for (int x = -size; x <= size; x++)
            {
                float dist = MathF.Sqrt(x * x + y * y);
                if (dist > size) continue;
                
                float light = 1 - dist / size;
                light = MathF.Pow(light, 0.6f);
                // Top-left highlight
                light += (x < 0 && y < 0) ? 0.15f : 0;
                light = Math.Clamp(light, 0, 1);
                
                int idx = (int)((1 - light) * (tooth.Length - 1));
                idx = Math.Clamp(idx, 0, tooth.Length - 1);
                
                SetPixelSafe(data, w, h, cx + x, cy + y, tooth[idx]);
            }
        }
        
        // Root hole
        FillEllipse(data, w, h, cx, cy, 2, 2, hole);
    }
    
    // === HELPER: Jawbone handle ===
    private void DrawJawboneHandle3D(Color[] data, int w, int h, int cx, int cy, int length, Color[] bone, Color[] tooth)
    {
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / length;
            int hy = cy + i;
            int width = (int)(14 - t * 5);
            
            for (int dx = -width / 2; dx <= width / 2; dx++)
            {
                float nx = (float)dx / (width / 2 + 1);
                float light = 0.5f - nx * 0.35f;
                light += (1 - t) * 0.15f;
                light = Math.Clamp(light, 0.1f, 0.95f);
                
                int idx = (int)((1 - light) * (bone.Length - 1));
                idx = Math.Clamp(idx, 0, bone.Length - 1);
                
                SetPixelSafe(data, w, h, cx + dx, hy, bone[idx]);
            }
        }
        
        // Teeth along grip
        for (int t = 0; t < 5; t++)
        {
            int ty = cy + 6 + t * 8;
            DrawSmallTooth3D(data, w, h, cx - 6, ty, tooth);
            DrawSmallTooth3D(data, w, h, cx + 4, ty, tooth);
        }
    }
    
    // === HELPER: Small tooth ===
    private void DrawSmallTooth3D(Color[] data, int w, int h, int cx, int cy, Color[] tooth)
    {
        for (int i = 0; i < 6; i++)
        {
            int tw = Math.Max(1, 3 - i / 2);
            float light = 0.9f - (float)i / 8;
            int idx = (int)((1 - light) * (tooth.Length - 1));
            idx = Math.Clamp(idx, 0, tooth.Length - 1);
            
            for (int dx = -tw; dx <= tw; dx++)
                SetPixelSafe(data, w, h, cx + dx, cy + i, tooth[idx]);
        }
    }
    
    // === HELPER: Finger bone ===
    private void DrawFingerBone3D(Color[] data, int w, int h, int cx, int cy, int length, int width, Color[] bone)
    {
        // Shaft
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / length;
            int bw = (int)(width * (0.6f + 0.4f * MathF.Sin(t * MathF.PI)));
            
            for (int dx = -bw; dx <= bw; dx++)
            {
                float light = 1 - MathF.Abs((float)dx / (bw + 1));
                light = light * 0.7f + 0.3f;
                
                int idx = (int)((1 - light) * (bone.Length - 1));
                idx = Math.Clamp(idx, 0, bone.Length - 1);
                
                SetPixelSafe(data, w, h, cx + dx, cy + i, bone[idx]);
            }
        }
        
        // Knuckle at top
        Draw3DSphere(data, w, h, cx, cy - 2, width + 2, width, bone);
    }
    
    // === HELPER: Mini skull emblem ===
    private void DrawMiniSkull(Color[] data, int w, int h, int cx, int cy, Color[] bone, Color cavity)
    {
        // Head
        for (int y = -8; y <= 5; y++)
        {
            for (int x = -7; x <= 7; x++)
            {
                float nx = (float)x / 7;
                float ny = (float)(y + 2) / 8;
                float dist = MathF.Sqrt(nx * nx + ny * ny);
                if (dist > 1) continue;
                
                float light = 0.6f - nx * 0.2f - ny * 0.3f;
                int idx = (int)((1 - light) * (bone.Length - 1));
                idx = Math.Clamp(idx, 0, bone.Length - 1);
                
                SetPixelSafe(data, w, h, cx + x, cy + y, bone[idx]);
            }
        }
        
        // Eye sockets
        FillEllipse(data, w, h, cx - 3, cy - 2, 2, 3, cavity);
        FillEllipse(data, w, h, cx + 3, cy - 2, 2, 3, cavity);
        
        // Nose
        SetPixelSafe(data, w, h, cx, cy + 1, cavity);
        
        // Teeth
        for (int t = 0; t < 4; t++)
            SetPixelSafe(data, w, h, cx - 3 + t * 2, cy + 4, bone[1]);
    }

    private void DrawMyFirstNailerSprite(Color[] data, int w, int h)
    {
        // === UNCANNY WEAPON - Fisher-Price style "My First Nailer" - real nails ===
        void P(int x, int y, Color c) { if (x >= 0 && x < w && y >= 0 && y < h) data[y * w + x] = c; }
        
        // Cheerful toy colors (primary colors, but wrong)
        Color RED = new Color(230, 80, 80);
        Color REDD = new Color(180, 50, 50);
        Color YEL = new Color(255, 230, 100);
        Color YELD = new Color(210, 180, 60);
        Color BLU = new Color(100, 160, 230);
        Color BLUD = new Color(60, 120, 180);
        Color GRN = new Color(100, 200, 100);
        Color GRND = new Color(60, 150, 60);
        
        // Metal nails (real, rusty, dangerous)
        Color N0 = new Color(180, 175, 165);
        Color N1 = new Color(140, 130, 120);
        Color N2 = new Color(100, 90, 80);
        Color RUST = new Color(160, 80, 50);
        
        // Blood
        Color BLD = new Color(140, 30, 30);
        
        Color BK = new Color(20, 15, 10);
        Color WHT = new Color(255, 250, 240);
        
        // === MAIN BODY (chunky, rounded toy shape) ===
        for (int by = 20; by <= 44; by++)
        {
            int bodyWidth = 18 - Math.Abs(by - 32) / 3;
            for (int bx = 30 - bodyWidth; bx <= 30 + bodyWidth; bx++)
            {
                float light = 0.5f - (bx - 30) * 0.02f - (by - 32) * 0.015f;
                Color c = light > 0.45f ? RED : light > 0.35f ? REDD : new Color(140, 35, 35);
                P(bx, by, c);
            }
        }
        
        // === BARREL (yellow tube, but a REAL nail sticks out) ===
        for (int bx = 2; bx <= 18; bx++)
        {
            for (int by = 28; by <= 36; by++)
            {
                float light = 0.5f - (by - 32) * 0.08f;
                Color c = light > 0.4f ? YEL : YELD;
                P(bx, by, c);
            }
        }
        // Barrel opening
        for (int by = 29; by <= 35; by++) { P(0, by, BK); P(1, by, BK); }
        
        // === REAL NAIL sticking out (too sharp, too real) ===
        // Nail shaft
        for (int nx = -10; nx <= 0; nx++)
        {
            int nailY = 32;
            P(nx, nailY - 1, N0); P(nx, nailY, N1); P(nx, nailY + 1, N2);
        }
        // Nail point (sharp!)
        P(-11, 32, N1); P(-12, 32, N2); P(-13, 32, BK);
        // Rust spots
        P(-5, 31, RUST); P(-7, 33, RUST); P(-3, 32, RUST);
        // Blood on tip
        P(-11, 31, BLD); P(-12, 32, BLD); P(-11, 33, BLD);
        
        // === NAIL MAGAZINE (visible through window) ===
        for (int my = 18; my <= 26; my++)
        {
            for (int mx = 24; mx <= 36; mx++)
            {
                P(mx, my, BLU);
            }
        }
        // Window showing nails inside
        for (int wy = 20; wy <= 24; wy++)
            for (int wx = 26; wx <= 34; wx++)
                P(wx, wy, new Color(220, 230, 240));
        // Nails visible inside
        for (int n = 0; n < 4; n++)
        {
            int nx = 27 + n * 2;
            P(nx, 21, N1); P(nx, 22, N1); P(nx, 23, N1);
            P(nx, 24, N2);
        }
        
        // === HANDLE (blue, chunky grip) ===
        for (int hy = 44; hy <= 60; hy++)
        {
            int hw = 8 - (hy - 44) / 4;
            if (hw < 4) hw = 4;
            for (int hx = 32 - hw; hx <= 32 + hw; hx++)
            {
                float light = 0.5f - (hx - 32) * 0.04f;
                Color c = light > 0.4f ? BLU : BLUD;
                P(hx, hy, c);
            }
        }
        
        // === TRIGGER (green, big for small hands) ===
        for (int ty = 46; ty <= 54; ty++)
        {
            int tw = 4 - (ty - 46) / 3;
            for (int tx = 36; tx <= 36 + tw; tx++)
            {
                P(tx, ty, ty < 50 ? GRN : GRND);
            }
        }
        
        // === "MY FIRST NAILER" LABEL ===
        // White label background
        for (int ly = 34; ly <= 42; ly++)
            for (int lx = 20; lx <= 42; lx++)
                P(lx, ly, WHT);
        // Cheerful star
        P(24, 36, YEL); P(23, 37, YEL); P(25, 37, YEL); P(24, 38, YEL);
        P(22, 38, YEL); P(26, 38, YEL); P(24, 39, YEL); P(24, 40, YEL);
        // Text hint "MY 1ST"
        for (int tx = 28; tx <= 40; tx += 2) P(tx, 37, RED);
        for (int tx = 28; tx <= 40; tx += 3) P(tx, 39, RED);
        
        // === SMILEY FACE (but wrong) ===
        int smileX = 50, smileY = 30;
        // Face circle
        for (int sy = -6; sy <= 6; sy++)
            for (int sx = -6; sx <= 6; sx++)
                if (sx*sx + sy*sy <= 36) P(smileX + sx, smileY + sy, YEL);
        // Eyes (one winking... or damaged?)
        P(smileX - 2, smileY - 2, BK); P(smileX - 3, smileY - 2, BK);
        // X for other eye (broken)
        P(smileX + 2, smileY - 3, BK); P(smileX + 4, smileY - 1, BK);
        P(smileX + 2, smileY - 1, BK); P(smileX + 4, smileY - 3, BK);
        // Smile (too wide)
        for (int mx = -3; mx <= 3; mx++)
            P(smileX + mx, smileY + 3 + Math.Abs(mx) / 2, BK);
        
        // === WARNING LABEL (small, ignored) ===
        P(48, 42, WHT); P(49, 42, WHT); P(50, 42, WHT); P(51, 42, WHT);
        P(49, 43, BK); // !
        P(49, 45, BK);
        
        // === BLOOD SPLATTER (someone already used this) ===
        P(14, 40, BLD); P(16, 41, BLD); P(13, 42, BLD);
        P(46, 26, BLD); P(48, 27, BLD);
    }
    
    // === HELPER: Gloved finger ===
    private void DrawGlovedFinger3D(Color[] data, int w, int h, int startX, int startY, int length, float radius, Color[] glove)
    {
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / length;
            float angle = t * 2.0f;
            
            int fx = startX - (int)(MathF.Cos(angle) * 20);
            int fy = startY + (int)(MathF.Sin(angle) * 10);
            float r = radius * (1 - t * 0.25f);
            
            // Knuckle bumps
            bool isKnuckle = (i % 10) < 3;
            if (isKnuckle) r *= 1.1f;
            
            for (int dy = (int)-r; dy <= (int)r; dy++)
            {
                for (int dx = (int)-r; dx <= (int)r; dx++)
                {
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    if (dist > r) continue;
                    
                    float light = (1 - dist / r) * 0.6f + MathF.Cos(angle - 0.8f) * 0.4f;
                    light = Math.Clamp(light, 0, 1);
                    
                    int idx = (int)((1 - light) * (glove.Length - 1));
                    idx = Math.Clamp(idx, 0, glove.Length - 1);
                    
                    SetPixelSafe(data, w, h, fx + dx, fy + dy, glove[idx]);
                }
            }
        }
    }
    
    // === HELPER: Industrial body with beveled edges ===
    private void DrawIndustrialBody3D(Color[] data, int w, int h, int cx, int cy, int rx, int ry, Color[] main, Color[] edge)
    {
        for (int y = -ry; y <= ry; y++)
        {
            for (int x = -rx; x <= rx; x++)
            {
                // Check if we're in the main rectangle area
                bool inBody = MathF.Abs(x) < rx - 3 && MathF.Abs(y) < ry - 3;
                
                if (!inBody)
                {
                    // Edge area - use metal bevels
                    float edgeDist = MathF.Max(MathF.Abs(x) - rx + 6, MathF.Abs(y) - ry + 6);
                    if (edgeDist > 6) continue;
                    
                    float light = 1 - edgeDist / 6;
                    // Top-left edges brighter
                    if (x < 0 || y < 0) light += 0.2f;
                    light = Math.Clamp(light, 0, 1);
                    
                    int idx = (int)((1 - light) * (edge.Length - 1));
                    idx = Math.Clamp(idx, 0, edge.Length - 1);
                    
                    SetPixelSafe(data, w, h, cx + x, cy + y, edge[idx]);
                }
                else
                {
                    // Main body - use yellow with lighting
                    float ny = (float)y / ry;
                    float light = 0.6f - ny * 0.3f;
                    // Left side highlight
                    if (x < -rx / 2) light += 0.15f;
                    light = Math.Clamp(light, 0.1f, 0.95f);
                    
                    int idx = (int)((1 - light) * (main.Length - 1));
                    idx = Math.Clamp(idx, 0, main.Length - 1);
                    
                    SetPixelSafe(data, w, h, cx + x, cy + y, main[idx]);
                }
            }
        }
    }
    
    // === HELPER: Hazard stripes ===
    private void DrawHazardStripes(Color[] data, int w, int h, int x, int y, int width, int height, Color[] yellow, Color black)
    {
        for (int dy = 0; dy < height; dy++)
        {
            for (int dx = 0; dx < width; dx++)
            {
                int stripe = (dx + dy) / 8;
                if (stripe % 2 == 0)
                {
                    // Black stripe with slight 3D variation
                    float variation = ((dx * 3 + dy * 7) % 10) / 40f;
                    Color c = LerpColor(black, new Color(50, 50, 55), variation);
                    SetPixelSafe(data, w, h, x + dx, y + dy, c);
                }
            }
        }
    }
    
    // === HELPER: Nail magazine ===
    private void DrawNailMagazine3D(Color[] data, int w, int h, int cx, int cy, int rx, int ry, Color[] metal)
    {
        for (int y = -ry / 2; y <= ry / 2; y++)
        {
            for (int x = -rx / 2; x <= rx / 2; x++)
            {
                float nx = (float)x / (rx / 2);
                float ny = (float)y / (ry / 2);
                
                float light = 0.5f - ny * 0.3f - nx * 0.15f;
                // Ridged texture
                if ((x + rx / 2) % 6 < 2) light -= 0.15f;
                light = Math.Clamp(light, 0.05f, 0.95f);
                
                int idx = (int)((1 - light) * (metal.Length - 1));
                idx = Math.Clamp(idx, 0, metal.Length - 1);
                
                SetPixelSafe(data, w, h, cx + x, cy + y, metal[idx]);
            }
        }
    }
    
    // === HELPER: Single nail ===
    private void DrawNail3D(Color[] data, int w, int h, int cx, int cy, int length, Color[] metal)
    {
        // Shaft
        for (int i = 0; i < length; i++)
        {
            float light = 0.7f - (float)i / length * 0.3f;
            int idx = (int)((1 - light) * (metal.Length - 1));
            idx = Math.Clamp(idx, 0, metal.Length - 1);
            
            SetPixelSafe(data, w, h, cx, cy + i, metal[idx]);
            SetPixelSafe(data, w, h, cx + 1, cy + i, metal[Math.Min(idx + 1, metal.Length - 1)]);
        }
        
        // Head
        for (int hx = -2; hx <= 2; hx++)
        {
            for (int hy = 0; hy < 3; hy++)
            {
                float light = 0.8f - (float)hy / 4;
                int idx = (int)((1 - light) * (metal.Length - 1));
                idx = Math.Clamp(idx, 0, metal.Length - 1);
                
                SetPixelSafe(data, w, h, cx + hx, cy - 2 + hy, metal[idx]);
            }
        }
    }
    
    // === HELPER: Rubber grip ===
    private void DrawRubberGrip3D(Color[] data, int w, int h, int cx, int cy, int length, int width, Color rubber)
    {
        Color rubberLight = new Color(65, 65, 70);
        Color rubberDark = new Color(30, 30, 35);
        
        for (int y = 0; y < length; y++)
        {
            float yProg = (float)y / length;
            for (int x = -width / 2; x <= width / 2; x++)
            {
                float nx = (float)x / (width / 2);
                float light = 0.5f - nx * 0.35f;
                
                // Grip ridges
                if (y % 5 < 2) light -= 0.2f;
                light = Math.Clamp(light, 0.1f, 0.9f);
                
                Color c = LerpColor(rubberDark, rubberLight, light);
                SetPixelSafe(data, w, h, cx + x, cy + y, c);
            }
        }
    }
    
    // === HELPER: Metal trigger ===
    private void DrawMetalTrigger3D(Color[] data, int w, int h, int cx, int cy, int length, Color[] metal)
    {
        for (int y = 0; y < length; y++)
        {
            int tw = 8 - y / 3;
            for (int x = 0; x < tw; x++)
            {
                float light = (float)x / tw * 0.5f + 0.3f;
                int idx = (int)((1 - light) * (metal.Length - 1));
                idx = Math.Clamp(idx, 0, metal.Length - 1);
                
                SetPixelSafe(data, w, h, cx + x, cy + y, metal[idx]);
            }
        }
    }
    
    // === HELPER: Phillips screw ===
    private void DrawPhillipsScrew(Color[] data, int w, int h, int cx, int cy, Color[] metal)
    {
        // Screw body
        for (int dy = -3; dy <= 3; dy++)
        {
            for (int dx = -3; dx <= 3; dx++)
            {
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                if (dist > 3) continue;
                
                float light = 1 - dist / 3.5f;
                // Top-left highlight
                if (dx < 0 && dy < 0) light += 0.2f;
                light = Math.Clamp(light, 0, 1);
                
                int idx = (int)((1 - light) * (metal.Length - 1));
                idx = Math.Clamp(idx, 0, metal.Length - 1);
                
                SetPixelSafe(data, w, h, cx + dx, cy + dy, metal[idx]);
            }
        }
        
        // Phillips cross
        SetPixelSafe(data, w, h, cx, cy, metal[4]);
        SetPixelSafe(data, w, h, cx - 1, cy, metal[4]);
        SetPixelSafe(data, w, h, cx + 1, cy, metal[4]);
        SetPixelSafe(data, w, h, cx, cy - 1, metal[4]);
        SetPixelSafe(data, w, h, cx, cy + 1, metal[4]);
    }
    
    private void DrawBloodSplatter(Color[] data, int w, int h, int cx, int cy, Color blood, Color bloodDark)
    {
        var rand = new Random(cx * 100 + cy);
        for (int i = 0; i < 15; i++)
        {
            int bx = cx + rand.Next(-6, 7);
            int by = cy + rand.Next(-4, 5);
            Color c = rand.Next(2) == 0 ? blood : bloodDark;
            SetPixelSafe(data, w, h, bx, by, c);
        }
    }

    private void DrawSippyCannonSprite(Color[] data, int w, int h)
    {
        // === UNCANNY WEAPON - Sippy Cup with twin barrels, leaking something wrong ===
        void P(int x, int y, Color c) { if (x >= 0 && x < w && y >= 0 && y < h) data[y * w + x] = c; }
        
        // Pastel baby colors (faded, stained)
        Color PINK = new Color(255, 180, 200);
        Color PINKD = new Color(220, 140, 160);
        Color PINKDD = new Color(180, 100, 120);
        Color BLU = new Color(180, 210, 240);
        Color BLUD = new Color(140, 170, 200);
        
        // The liquid inside (not milk)
        Color LIQ = new Color(80, 30, 40);
        Color LIQD = new Color(50, 15, 25);
        
        // Plastic
        Color PL0 = new Color(240, 240, 245);
        Color PL1 = new Color(200, 200, 210);
        Color PL2 = new Color(160, 160, 175);
        
        Color BK = new Color(15, 10, 12);
        
        // === MAIN CUP BODY (round, baby-safe) ===
        int cupX = 36, cupY = 34;
        for (int cy = -18; cy <= 18; cy++)
        {
            int cupWidth = (int)(14 * MathF.Sqrt(1 - (cy * cy) / 400f));
            for (int cx = -cupWidth; cx <= cupWidth; cx++)
            {
                float light = 0.5f - cx * 0.025f - cy * 0.015f;
                Color c = light > 0.45f ? PINK : light > 0.35f ? PINKD : PINKDD;
                P(cupX + cx, cupY + cy, c);
            }
        }
        
        // === TWIN SPOUTS (barrels) ===
        // Top spout
        for (int sx = -14; sx <= 0; sx++)
        {
            for (int sy = -3; sy <= 3; sy++)
            {
                float light = 0.5f - sy * 0.12f;
                Color c = light > 0.4f ? BLU : BLUD;
                P(22 + sx, 26 + sy, c);
            }
        }
        // Bottom spout
        for (int sx = -14; sx <= 0; sx++)
        {
            for (int sy = -3; sy <= 3; sy++)
            {
                float light = 0.5f - sy * 0.12f;
                Color c = light > 0.4f ? BLU : BLUD;
                P(22 + sx, 42 + sy, c);
            }
        }
        // Spout holes (dark)
        for (int sy = -2; sy <= 2; sy++) { P(6, 26 + sy, BK); P(7, 26 + sy, BK); }
        for (int sy = -2; sy <= 2; sy++) { P(6, 42 + sy, BK); P(7, 42 + sy, BK); }
        
        // === LIQUID DRIPPING from spouts (not milk) ===
        // Top drip
        P(4, 27, LIQ); P(3, 28, LIQ); P(2, 30, LIQD); P(3, 32, LIQ);
        P(1, 34, LIQD); P(2, 36, LIQ);
        // Bottom drip
        P(4, 44, LIQ); P(2, 45, LIQD); P(3, 47, LIQ); P(1, 49, LIQD);
        
        // === HANDLES (baby grip handles on sides) ===
        // Top handle
        for (int ha = 0; ha < 10; ha++)
        {
            float angle = ha * 0.15f - 0.75f;
            int hx = cupX + (int)(MathF.Cos(angle) * 18);
            int hy = cupY - 8 + (int)(MathF.Sin(angle) * 8);
            for (int ht = -3; ht <= 3; ht++)
                P(hx, hy + ht, PINK);
        }
        // Bottom handle
        for (int ha = 0; ha < 10; ha++)
        {
            float angle = ha * 0.15f + 0.75f;
            int hx = cupX + (int)(MathF.Cos(angle) * 18);
            int hy = cupY + 8 + (int)(MathF.Sin(angle) * 8);
            for (int ht = -3; ht <= 3; ht++)
                P(hx, hy + ht, PINK);
        }
        
        // === LID (semi-transparent, see the liquid) ===
        for (int ly = -6; ly <= 6; ly++)
        {
            int lidWidth = 16 - Math.Abs(ly);
            for (int lx = -lidWidth; lx <= lidWidth; lx++)
            {
                float light = 0.6f - lx * 0.02f;
                Color c = light > 0.5f ? PL0 : light > 0.4f ? PL1 : PL2;
                P(cupX + lx, cupY - 14 + ly, c);
            }
        }
        // Dark liquid visible through lid
        for (int vy = -3; vy <= 3; vy++)
            for (int vx = -8; vx <= 8; vx++)
                if (vx*vx + vy*vy < 50) P(cupX + vx, cupY - 14 + vy, LIQD);
        
        // === CUTE DECORATION (bunny face, but wrong) ===
        int bunX = cupX, bunY = cupY;
        // Bunny face circle
        for (int by = -6; by <= 6; by++)
            for (int bx = -6; bx <= 6; bx++)
                if (bx*bx + by*by <= 36) P(bunX + bx, bunY + by, PL0);
        // Ears
        P(bunX - 4, bunY - 8, PL0); P(bunX - 4, bunY - 9, PL0); P(bunX - 4, bunY - 10, PL0);
        P(bunX + 4, bunY - 8, PL0); P(bunX + 4, bunY - 9, PL0); P(bunX + 4, bunY - 10, PL0);
        P(bunX - 4, bunY - 9, PINKD); // Inner ear
        P(bunX + 4, bunY - 9, PINKD);
        // Eyes (X's - dead bunny)
        P(bunX - 3, bunY - 2, BK); P(bunX - 1, bunY - 2, BK);
        P(bunX - 2, bunY - 1, BK); P(bunX - 2, bunY - 3, BK);
        P(bunX + 3, bunY - 2, BK); P(bunX + 1, bunY - 2, BK);
        P(bunX + 2, bunY - 1, BK); P(bunX + 2, bunY - 3, BK);
        // Nose
        P(bunX, bunY + 1, PINKD);
        // Mouth (frown)
        P(bunX - 2, bunY + 3, BK); P(bunX - 1, bunY + 4, BK);
        P(bunX, bunY + 4, BK); P(bunX + 1, bunY + 4, BK); P(bunX + 2, bunY + 3, BK);
        
        // === STAINS on the cup (old, used) ===
        Color STAIN = new Color(160, 140, 130);
        P(cupX + 8, cupY + 10, STAIN); P(cupX + 9, cupY + 11, STAIN);
        P(cupX - 10, cupY + 5, STAIN); P(cupX - 11, cupY + 6, STAIN);
        
        // === "NO-SPILL" LABEL (ironic) ===
        for (int ly = 48; ly <= 52; ly++)
            for (int lx = 28; lx <= 44; lx++)
                P(lx, ly, PL0);
        // Text hint
        for (int tx = 30; tx <= 42; tx += 2) P(tx, 50, PINKD);
        
        // === SOMETHING moving inside (bubbles? eyes?) ===
        for (int ey = -2; ey <= 2; ey++)
            for (int ex = -2; ex <= 2; ex++)
                if (ex*ex + ey*ey <= 4) P(cupX + 4 + ex, cupY - 14 + ey, new Color(200, 180, 160));
        P(cupX + 4, cupY - 14, BK); // Pupil???
    }
    
    // Duke3D style screaming mouth
    private void DrawDuke3DScreamingMouth(Color[] data, int w, int h, int cx, int cy, int size, float s)
    {
        // Dark maw
        FillEllipse(data, w, h, cx, cy, size, size, new Color(25, 10, 12));
        FillEllipse(data, w, h, cx, cy, size - (int)(4 * s), size - (int)(4 * s), new Color(10, 3, 5));
        
        // Bold triangular teeth
        for (int t = 0; t < 10; t++)
        {
            float angle = t * MathF.PI / 5;
            int toothLen = (int)(12 * s);
            
            for (int len = 0; len < toothLen; len++)
            {
                float lt = (float)len / toothLen;
                int tx = cx + (int)(MathF.Cos(angle) * (size + (int)(4 * s) - len));
                int ty = cy + (int)(MathF.Sin(angle) * (size + (int)(4 * s) - len));
                int toothW = Math.Max(1, (int)((6 - lt * 5) * s));
                
                float light = MathF.Cos(angle - 0.5f) * 0.3f + 0.7f - lt * 0.3f;
                int idx = (int)((1 - light) * 3);
                
                for (int dx = -toothW; dx <= toothW; dx++)
                {
                    for (int dy = -toothW / 2; dy <= toothW / 2; dy++)
                        SetPixelSafe(data, w, h, tx + dx, ty + dy, Duke3DBone[Math.Clamp(idx, 0, 3)]);
                }
            }
        }
        
        // Gum line
        for (int g = 0; g < 360; g += 15)
        {
            float angle = g * MathF.PI / 180;
            int gx = cx + (int)(MathF.Cos(angle) * (size + (int)(8 * s)));
            int gy = cy + (int)(MathF.Sin(angle) * (size + (int)(8 * s)));
            SetPixelSafe(data, w, h, gx, gy, Duke3DFlesh[3]);
            SetPixelSafe(data, w, h, gx + 1, gy, Duke3DFlesh[4]);
        }
    }
    
    // Duke3D style eye
    private void DrawDuke3DEye(Color[] data, int w, int h, int cx, int cy, int size, Color[] eye)
    {
        // Eye socket
        for (int y = -size - 3; y <= size + 3; y++)
        {
            for (int x = -size - 3; x <= size + 3; x++)
            {
                float dist = MathF.Sqrt(x * x + y * y);
                if (dist > size + 3 || dist < size - 2) continue;
                SetPixelSafe(data, w, h, cx + x, cy + y, Duke3DFlesh[5]);
            }
        }
        
        // Sclera with bold shading
        for (int y = -size; y <= size; y++)
        {
            for (int x = -size; x <= size; x++)
            {
                float nx = (float)x / size;
                float ny = (float)y / size;
                float dist = MathF.Sqrt(nx * nx + ny * ny);
                if (dist > 1) continue;
                
                float nz = MathF.Sqrt(1 - dist * dist);
                float light = -nx * 0.3f - ny * 0.5f + nz * 0.8f;
                light = (light + 1) / 2;
                
                Color c = LerpColor(new Color(200, 200, 185), eye[0], light);
                
                // Bloodshot veins
                float vein = MathF.Sin(nx * 12 + ny * 8);
                if (vein > 0.85f) c = LerpColor(c, new Color(180, 80, 80), 0.5f);
                
                SetPixelSafe(data, w, h, cx + x, cy + y, c);
            }
        }
        
        // Bold iris
        int irisSize = size * 2 / 3;
        for (int y = -irisSize; y <= irisSize; y++)
        {
            for (int x = -irisSize; x <= irisSize; x++)
            {
                float dist = MathF.Sqrt(x * x + y * y);
                if (dist > irisSize) continue;
                
                float light = 0.5f - (float)x / irisSize * 0.3f - (float)y / irisSize * 0.2f;
                Color c = LerpColor(eye[2], eye[1], Math.Clamp(light, 0, 1));
                SetPixelSafe(data, w, h, cx + x - 2, cy + y, c);
            }
        }
        
        // Pupil
        int pupilSize = size / 3;
        FillEllipse(data, w, h, cx - 2, cy, pupilSize, pupilSize, eye[3]);
        
        // Highlight
        SetPixelSafe(data, w, h, cx - size / 3, cy - size / 3, Color.White);
        SetPixelSafe(data, w, h, cx - size / 3 + 1, cy - size / 3, new Color(220, 220, 220));
    }
    
    // === HELPER: Screaming mouth ===
    private void DrawScreamingMouth(Color[] data, int w, int h, int cx, int cy, int size, Color[] tooth, Color[] flesh)
    {
        // Dark maw
        FillEllipse(data, w, h, cx, cy, size, size, new Color(20, 8, 10));
        FillEllipse(data, w, h, cx, cy, size - 3, size - 3, new Color(8, 2, 4));
        
        // Teeth ring - each tooth as a small 3D cone
        for (int t = 0; t < 10; t++)
        {
            float angle = t * MathF.PI / 5;
            int tx = cx + (int)(MathF.Cos(angle) * (size + 3));
            int ty = cy + (int)(MathF.Sin(angle) * (size + 3));
            
            // Each tooth pointing inward
            for (int len = 0; len < 7; len++)
            {
                float lt = (float)len / 7;
                int ttx = tx - (int)(MathF.Cos(angle) * len * 0.8f);
                int tty = ty - (int)(MathF.Sin(angle) * len * 0.8f);
                int toothW = Math.Max(1, 3 - len / 3);
                
                // Lighting based on angle
                float light = MathF.Cos(angle - 0.5f) * 0.3f + 0.7f - lt * 0.3f;
                int idx = (int)((1 - light) * (tooth.Length - 1));
                idx = Math.Clamp(idx, 0, tooth.Length - 1);
                
                for (int dx = -toothW; dx <= toothW; dx++)
                    SetPixelSafe(data, w, h, ttx + dx, tty, tooth[idx]);
            }
        }
        
        // Gum line
        for (int g = 0; g < 360; g += 20)
        {
            float angle = g * MathF.PI / 180;
            int gx = cx + (int)(MathF.Cos(angle) * (size + 6));
            int gy = cy + (int)(MathF.Sin(angle) * (size + 6));
            SetPixelSafe(data, w, h, gx, gy, flesh[2]);
            SetPixelSafe(data, w, h, gx + 1, gy, flesh[3]);
        }
    }
    
    // === HELPER: Living eye with 3D shading ===
    private void DrawLivingEye3D(Color[] data, int w, int h, int cx, int cy, int size, Color[] eye)
    {
        // Eye socket (sunken)
        for (int y = -size - 2; y <= size + 2; y++)
        {
            for (int x = -size - 2; x <= size + 2; x++)
            {
                float dist = MathF.Sqrt(x * x + y * y);
                if (dist > size + 2 || dist < size - 1) continue;
                SetPixelSafe(data, w, h, cx + x, cy + y, new Color(90, 50, 55));
            }
        }
        
        // Sclera with sphere shading
        for (int y = -size; y <= size; y++)
        {
            for (int x = -size; x <= size; x++)
            {
                float nx = (float)x / size;
                float ny = (float)y / size;
                float dist = MathF.Sqrt(nx * nx + ny * ny);
                if (dist > 1) continue;
                
                float nz = MathF.Sqrt(1 - dist * dist);
                float light = -nx * 0.3f - ny * 0.5f + nz * 0.8f;
                light = (light + 1) / 2;
                
                // Slightly yellowed white
                Color c = LerpColor(new Color(200, 200, 185), eye[0], light);
                
                // Bloodshot veins
                float vein = MathF.Sin(nx * 12 + ny * 8);
                if (vein > 0.85f) c = LerpColor(c, new Color(180, 80, 80), 0.4f);
                
                SetPixelSafe(data, w, h, cx + x, cy + y, c);
            }
        }
        
        // Iris
        int irisSize = size / 2;
        for (int y = -irisSize - 1; y <= irisSize + 1; y++)
        {
            for (int x = -irisSize - 1; x <= irisSize + 1; x++)
            {
                float dist = MathF.Sqrt(x * x + y * y);
                if (dist > irisSize + 1) continue;
                
                float light = 1 - dist / (irisSize + 2);
                Color c = LerpColor(eye[2], eye[1], light);
                SetPixelSafe(data, w, h, cx - 1 + x, cy + y, c);
            }
        }
        
        // Pupil
        FillEllipse(data, w, h, cx - 1, cy, irisSize / 2, irisSize / 2 + 1, eye[3]);
        
        // Highlight
        SetPixelSafe(data, w, h, cx - 2, cy - 2, new Color(255, 255, 250));
        SetPixelSafe(data, w, h, cx - 1, cy - 2, new Color(240, 240, 235));
    }
    
    // === HELPER: Ribbed muscle ===
    private void DrawRibbedMuscle3D(Color[] data, int w, int h, int cx, int cy, int width, int height, Color[] flesh)
    {
        int ribs = 8;
        int ribWidth = width / ribs;
        
        for (int r = 0; r < ribs; r++)
        {
            int rx = cx + r * ribWidth;
            bool isRaised = r % 2 == 0;
            
            for (int y = 0; y < height; y++)
            {
                float yProg = (float)y / height;
                float curve = MathF.Sin(yProg * MathF.PI);
                
                for (int x = 0; x < ribWidth - 1; x++)
                {
                    float xProg = (float)x / (ribWidth - 1);
                    float ribCurve = MathF.Sin(xProg * MathF.PI);
                    
                    float light = curve * 0.5f + ribCurve * 0.3f + (isRaised ? 0.2f : 0f);
                    light = Math.Clamp(light, 0, 1);
                    
                    int idx = (int)((1 - light) * (flesh.Length - 1));
                    idx = Math.Clamp(idx, 0, flesh.Length - 1);
                    
                    SetPixelSafe(data, w, h, rx + x, cy + y, flesh[idx]);
                }
            }
        }
    }
    
    // === HELPER: Bone stock ===
    private void DrawBoneStock3D(Color[] data, int w, int h, int cx, int cy, int width, int height, Color[] bone)
    {
        for (int y = -height / 2; y <= height / 2; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float ny = (float)y / (height / 2);
                float nx = (float)x / width;
                
                // Curved cross-section
                float curve = MathF.Sin((ny + 1) / 2 * MathF.PI);
                float light = curve * 0.5f + nx * 0.3f + 0.2f;
                
                // Cracks/age marks
                if (((x + y) * 7) % 31 == 0) light -= 0.2f;
                light = Math.Clamp(light, 0.1f, 0.95f);
                
                int idx = (int)((1 - light) * (bone.Length - 1));
                idx = Math.Clamp(idx, 0, bone.Length - 1);
                
                SetPixelSafe(data, w, h, cx + x, cy + y, bone[idx]);
            }
        }
    }

    private void DrawMusicBoxDancerSprite(Color[] data, int w, int h)
    {
        // === UNCANNY WEAPON - Broken Music Box Ballerina that never stops dancing ===
        void P(int x, int y, Color c) { if (x >= 0 && x < w && y >= 0 && y < h) data[y * w + x] = c; }
        
        // Faded porcelain ballerina colors
        Color SKIN = new Color(255, 235, 225);
        Color SKIND = new Color(230, 200, 190);
        Color SKINDD = new Color(180, 150, 140);
        
        // Pink tutu (faded, torn)
        Color TUTU = new Color(255, 180, 200);
        Color TUTUD = new Color(220, 140, 160);
        Color TUTUDD = new Color(180, 100, 120);
        
        // Gold accents (tarnished)
        Color GOLD = new Color(220, 180, 100);
        Color GOLDD = new Color(170, 130, 60);
        
        // Music box wood (old, cracked)
        Color WOOD = new Color(120, 80, 50);
        Color WOODD = new Color(80, 50, 30);
        Color WOODDD = new Color(50, 30, 15);
        
        // Cracks and damage
        Color CRACK = new Color(40, 25, 15);
        Color RUST = new Color(140, 80, 50);
        
        Color BK = new Color(10, 5, 5);
        
        // === MUSIC BOX BASE (ornate, damaged) ===
        for (int by = 46; by <= 62; by++)
        {
            int boxWidth = 24 - (62 - by) / 4;
            for (int bx = 32 - boxWidth; bx <= 32 + boxWidth; bx++)
            {
                float light = 0.5f - (bx - 32) * 0.015f - (by - 54) * 0.02f;
                Color c = light > 0.45f ? WOOD : light > 0.35f ? WOODD : WOODDD;
                P(bx, by, c);
            }
        }
        // Gold trim (tarnished)
        for (int tx = 10; tx <= 54; tx++)
        {
            P(tx, 46, GOLD); P(tx, 47, GOLDD);
            P(tx, 61, GOLDD); P(tx, 62, GOLD);
        }
        // Cracks in wood
        P(20, 50, CRACK); P(21, 51, CRACK); P(22, 52, CRACK); P(21, 53, CRACK);
        P(44, 52, CRACK); P(45, 53, CRACK); P(44, 54, CRACK);
        
        // === THE BALLERINA (porcelain, pose frozen mid-pirouette) ===
        int balX = 32, balY = 28;
        
        // Torso
        for (int ty = -8; ty <= 2; ty++)
        {
            int torsoW = 4 - Math.Abs(ty + 3) / 3;
            for (int tx = -torsoW; tx <= torsoW; tx++)
            {
                float light = 0.5f - tx * 0.08f;
                Color c = light > 0.4f ? SKIN : SKIND;
                P(balX + tx, balY + ty, c);
            }
        }
        
        // Tutu (frayed, spinning forever)
        for (int tutY = 0; tutY <= 8; tutY++)
        {
            int tutW = 10 + tutY;
            for (int tutX = -tutW; tutX <= tutW; tutX++)
            {
                // Ragged edge
                bool isEdge = Math.Abs(tutX) > tutW - 3 || tutY > 5;
                if (isEdge && (tutX + tutY) % 3 == 0) continue; // Torn gaps
                
                float light = 0.5f - tutX * 0.03f;
                Color c = light > 0.45f ? TUTU : light > 0.35f ? TUTUD : TUTUDD;
                P(balX + tutX, balY + tutY, c);
            }
        }
        
        // Arms (one broken, dangling at wrong angle)
        // Good arm (extended gracefully)
        for (int a = 0; a < 12; a++)
        {
            int ax = balX - 4 - a;
            int ay = balY - 6 + a / 4;
            P(ax, ay, SKIN); P(ax, ay + 1, SKIND);
        }
        // Broken arm (snapped, hanging)
        for (int a = 0; a < 8; a++)
        {
            int ax = balX + 4 + a / 2;
            int ay = balY - 4 + a; // Hanging down wrong
            P(ax, ay, SKIND); P(ax + 1, ay, SKINDD);
        }
        // Exposed joint where arm broke
        P(balX + 5, balY - 3, RUST); P(balX + 6, balY - 3, BK);
        
        // Head (tilted, staring)
        for (int hy = -6; hy <= 2; hy++)
            for (int hx = -4; hx <= 4; hx++)
                if (hx*hx + hy*hy <= 20) {
                    float light = 0.5f - hx * 0.08f - hy * 0.05f;
                    Color c = light > 0.4f ? SKIN : SKIND;
                    P(balX + hx, balY - 12 + hy, c);
                }
        // Hair bun
        for (int by2 = -4; by2 <= 0; by2++)
            for (int bx2 = -3; bx2 <= 3; bx2++)
                if (bx2*bx2 + by2*by2 <= 10) P(balX + bx2, balY - 18 + by2, WOODD);
        
        // Face (frozen smile, unblinking eyes)
        // Eyes (too wide, won't close)
        P(balX - 2, balY - 13, BK); P(balX - 1, balY - 13, BK);
        P(balX + 1, balY - 13, BK); P(balX + 2, balY - 13, BK);
        // Tiny pupils (always watching)
        P(balX - 2, balY - 13, new Color(80, 60, 40));
        P(balX + 2, balY - 13, new Color(80, 60, 40));
        // Painted smile (cracked)
        P(balX - 2, balY - 10, new Color(200, 100, 100));
        P(balX - 1, balY - 9, new Color(200, 100, 100));
        P(balX, balY - 9, new Color(200, 100, 100));
        P(balX + 1, balY - 9, new Color(200, 100, 100));
        P(balX + 2, balY - 10, new Color(200, 100, 100));
        // Crack through face
        P(balX + 1, balY - 15, CRACK); P(balX + 2, balY - 14, CRACK);
        P(balX + 2, balY - 12, CRACK); P(balX + 3, balY - 11, CRACK);
        
        // Legs (one in arabesque, one spinning mechanically)
        // Extended leg
        for (int l = 0; l < 16; l++)
        {
            int lx = balX - 10 - l / 2;
            int ly = balY + 8 + l / 4;
            P(lx, ly, SKIN); P(lx, ly + 1, SKIND);
        }
        // Ballet slipper
        P(balX - 18, balY + 12, TUTUD); P(balX - 19, balY + 12, TUTUD);
        P(balX - 20, balY + 13, TUTUDD);
        // Supporting leg
        for (int l = 0; l < 20; l++)
        {
            int ly = balY + 8 + l;
            P(balX, ly, SKIN); P(balX + 1, ly, SKIND);
        }
        // Point shoe
        P(balX, balY + 28, TUTUD); P(balX + 1, balY + 28, TUTUD);
        
        // === MUSIC KEY (stuck, broken) ===
        int keyX = 54, keyY = 54;
        for (int ky = -4; ky <= 4; ky++)
        {
            P(keyX, keyY + ky, GOLD);
            P(keyX + 1, keyY + ky, GOLDD);
        }
        // Key handle
        for (int kx = 0; kx < 8; kx++)
            P(keyX + 2 + kx, keyY, GOLDD);
        P(keyX + 10, keyY - 2, GOLD); P(keyX + 10, keyY - 1, GOLD);
        P(keyX + 10, keyY, GOLD); P(keyX + 10, keyY + 1, GOLD);
        P(keyX + 10, keyY + 2, GOLD);
        
        // === MUSIC NOTES (ethereal, haunting) ===
        Color NOTE = new Color(180, 150, 200);
        Color NOTED = new Color(140, 110, 160);
        // Notes floating up
        P(8, 22, NOTE); P(9, 21, NOTE); P(10, 21, NOTED);
        P(8, 23, NOTE); P(8, 24, NOTE);
        for (int ny = -2; ny <= 1; ny++)
            for (int nx = -2; nx <= 1; nx++)
                if (nx*nx + ny*ny <= 3) P(6 + nx, 25 + ny, NOTE);
        
        P(56, 26, NOTE); P(57, 25, NOTED);
        P(56, 27, NOTE); P(56, 28, NOTE);
        
        // === THE PLATFORM (spins forever) ===
        for (int py = 42; py <= 46; py++)
        {
            int platW = 8 + (46 - py);
            for (int px = -platW; px <= platW; px++)
            {
                float light = 0.6f - px * 0.03f;
                Color c = light > 0.5f ? GOLD : GOLDD;
                P(balX + px, py, c);
            }
        }
        // Spinning motion blur
        P(balX - 12, 44, new Color(255, 200, 220, 128));
        P(balX + 12, 44, new Color(255, 200, 220, 128));
        
        // === BROKEN MECHANISM visible inside ===
        P(28, 58, RUST); P(30, 59, RUST); P(32, 58, RUST);
        P(34, 59, RUST); P(36, 58, RUST);
        // Gears
        for (int gy = -2; gy <= 2; gy++)
            for (int gx = -2; gx <= 2; gx++)
                if ((gx + gy) % 2 == 0) P(32 + gx, 58 + gy, GOLDD);
    }
    
    // === HELPER: Corrupted arm ===
    private void DrawCorruptedArm3D(Color[] data, int w, int h, int x1, int y1, int x2, int y2, float r1, float r2, Color[] palette)
    {
        int steps = 80;
        for (int i = 0; i < steps; i++)
        {
            float t = (float)i / steps;
            int cx = (int)(x1 + (x2 - x1) * t);
            int cy = (int)(y1 + (y2 - y1) * t);
            float radius = r1 + (r2 - r1) * t;
            
            for (int dy = (int)-radius; dy <= (int)radius; dy++)
            {
                for (int dx = (int)-radius; dx <= (int)radius; dx++)
                {
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    if (dist > radius) continue;
                    
                    float lightX = -dx / radius;
                    float lightY = -dy / radius;
                    float light = (lightX * 0.5f + lightY * 0.7f + 0.5f);
                    
                    // Add visible veins under pale skin
                    float vein = MathF.Sin(cx * 0.1f + cy * 0.08f);
                    if (vein > 0.85f) light -= 0.2f;
                    
                    light = MathF.Pow(Math.Clamp(light, 0, 1), 0.8f);
                    
                    int colorIdx = (int)((1 - light) * (palette.Length - 1));
                    colorIdx = Math.Clamp(colorIdx, 0, palette.Length - 1);
                    
                    SetPixelSafe(data, w, h, cx + dx, cy + dy, palette[colorIdx]);
                }
            }
        }
    }
    
    // === HELPER: Corrupted finger ===
    private void DrawCorruptedFinger3D(Color[] data, int w, int h, int startX, int startY, int length, float radius, Color[] skin, Color[] voidC)
    {
        int joints = 4; // Too many joints
        
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / length;
            float angle = t * 2.5f;
            
            int fx = startX - (int)(MathF.Cos(angle) * 26);
            int fy = startY + (int)(MathF.Sin(angle) * 12);
            float r = radius * (1 - t * 0.3f);
            
            // Extra joint shadows
            bool isJoint = (i % (length / joints)) < 3;
            
            for (int dy = (int)-r; dy <= (int)r; dy++)
            {
                for (int dx = (int)-r; dx <= (int)r; dx++)
                {
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    if (dist > r) continue;
                    
                    float light = (1 - dist / r) * 0.6f + MathF.Cos(angle - 1) * 0.4f;
                    
                    // Corruption spreading toward tips
                    float corruption = t * 0.4f;
                    if (isJoint) light -= 0.15f;
                    light = Math.Clamp(light, 0, 1);
                    
                    int skinIdx = (int)((1 - light) * (skin.Length - 1));
                    skinIdx = Math.Clamp(skinIdx, 0, skin.Length - 1);
                    
                    // Blend with void at tips
                    int voidIdx = Math.Min((int)(corruption * voidC.Length), voidC.Length - 1);
                    Color c = LerpColor(skin[skinIdx], voidC[voidIdx], corruption * 0.5f);
                    
                    SetPixelSafe(data, w, h, fx + dx, fy + dy, c);
                }
            }
        }
    }
    
    // === HELPER: Dark nail ===
    private void DrawDarkNail3D(Color[] data, int w, int h, int cx, int cy, int length, Color[] voidC)
    {
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / length;
            int nw = Math.Max(1, 3 - i / 3);
            
            float light = 0.7f - t * 0.4f;
            int idx = (int)((1 - light) * (voidC.Length - 1));
            idx = Math.Clamp(idx, 0, voidC.Length - 1);
            
            for (int dx = -nw; dx <= nw; dx++)
                SetPixelSafe(data, w, h, cx - i, cy + dx, voidC[idx]);
        }
    }
    
    // === HELPER: Void body with cosmic swirls ===
    private void DrawVoidBody3D(Color[] data, int w, int h, int cx, int cy, int rx, int ry, Color[] palette)
    {
        for (int y = -ry; y <= ry; y++)
        {
            for (int x = -rx; x <= rx; x++)
            {
                float nx = (float)x / rx;
                float ny = (float)y / ry;
                float dist = MathF.Sqrt(nx * nx + ny * ny);
                if (dist > 1) continue;
                
                float nz = MathF.Sqrt(1 - dist * dist);
                float angle = MathF.Atan2(ny, nx);
                
                // Swirling void pattern
                float swirl = MathF.Sin(angle * 3 + dist * 6);
                
                float light = -nx * 0.3f - ny * 0.5f + nz * 0.7f + swirl * 0.1f;
                light = (light + 1) / 2;
                light = MathF.Pow(Math.Clamp(light, 0, 1), 0.6f);
                
                int colorIdx = (int)((1 - light) * (palette.Length - 1));
                colorIdx = Math.Clamp(colorIdx, 0, palette.Length - 1);
                
                SetPixelSafe(data, w, h, cx + x, cy + y, palette[colorIdx]);
            }
        }
    }
    
    // === HELPER: Cosmic shimmer ===
    private void DrawCosmicShimmer(Color[] data, int w, int h, int cx, int cy, int rx, int ry, Color pink, Color blue)
    {
        Random rand = new Random(123);
        for (int i = 0; i < 30; i++)
        {
            float angle = rand.NextSingle() * MathF.PI * 2;
            float r = rand.NextSingle() * 0.85f;
            
            int px = cx + (int)(MathF.Cos(angle) * r * rx);
            int py = cy + (int)(MathF.Sin(angle) * r * ry);
            
            Color shimmer = rand.Next(2) == 0 ? pink : blue;
            SetPixelSafe(data, w, h, px, py, LerpColor(shimmer, Color.White, 0.3f));
        }
    }
    
    // === HELPER: Friendly smile ===
    private void DrawFriendlySmile(Color[] data, int w, int h, int cx, int cy, Color[] eye, Color star)
    {
        // Two friendly eyes
        for (int e = 0; e < 2; e++)
        {
            int ex = cx + (e == 0 ? -7 : 7);
            int ey = cy - 9;
            
            // Eye with 3D shading
            for (int y = -6; y <= 6; y++)
            {
                for (int x = -5; x <= 5; x++)
                {
                    float dist = MathF.Sqrt(x * x + y * y);
                    if (dist > 5) continue;
                    
                    float light = 1 - dist / 6;
                    Color c = LerpColor(eye[1], eye[0], light);
                    SetPixelSafe(data, w, h, ex + x, ey + y, c);
                }
            }
            
            // Iris
            FillEllipse(data, w, h, ex, ey + 1, 3, 4, eye[2]);
            FillEllipse(data, w, h, ex, ey + 1, 2, 2, eye[3]);
            
            // Highlight
            SetPixelSafe(data, w, h, ex - 1, ey - 2, star);
        }
        
        // The smile (too wide, too friendly)
        Color smileLight = new Color(220, 200, 180);
        Color smileDark = new Color(160, 140, 120);
        
        for (int sx = -14; sx <= 14; sx++)
        {
            float curve = MathF.Pow(MathF.Abs(sx) / 14f, 2) * 7;
            int sy = cy + 3 + (int)curve;
            
            for (int th = 0; th < 3; th++)
            {
                float light = 1 - (float)th / 3;
                Color c = LerpColor(smileDark, smileLight, light);
                SetPixelSafe(data, w, h, cx + sx, sy + th, c);
            }
        }
        
        // Upturned ends
        SetPixelSafe(data, w, h, cx - 14, cy + 8, smileLight);
        SetPixelSafe(data, w, h, cx + 14, cy + 8, smileLight);
    }
    
    // === HELPER: Eldritch eye with proper 3D ===
    private void DrawEldritchEye3D(Color[] data, int w, int h, int cx, int cy, int size, Color[] eye, Color[] voidC)
    {
        // Socket
        for (int y = -size - 2; y <= size + 2; y++)
        {
            for (int x = -size - 2; x <= size + 2; x++)
            {
                float dist = MathF.Sqrt(x * x + y * y);
                if (dist > size + 2 || dist < size) continue;
                SetPixelSafe(data, w, h, cx + x, cy + y, voidC[5]);
            }
        }
        
        // Sclera with sphere shading (yellowish ancient)
        for (int y = -size; y <= size; y++)
        {
            for (int x = -size; x <= size; x++)
            {
                float nx = (float)x / size;
                float ny = (float)y / size;
                float dist = MathF.Sqrt(nx * nx + ny * ny);
                if (dist > 1) continue;
                
                float nz = MathF.Sqrt(1 - dist * dist);
                float light = -nx * 0.3f - ny * 0.5f + nz * 0.8f;
                light = (light + 1) / 2;
                
                Color c = LerpColor(eye[1], eye[0], light);
                SetPixelSafe(data, w, h, cx + x, cy + y, c);
            }
        }
        
        // Red iris staring at you
        int irisSize = size / 2;
        FillEllipse(data, w, h, cx - 1, cy, irisSize + 1, irisSize + 1, eye[2]);
        FillEllipse(data, w, h, cx - 1, cy, irisSize, irisSize, eye[3]);
        
        // Highlight
        SetPixelSafe(data, w, h, cx - 2, cy - 2, Color.White);
    }
    
    // === HELPER: 3D Tendril ===
    private void DrawTendril3D(Color[] data, int w, int h, int startX, int startY, int length, Color[] voidC)
    {
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / length;
            float wave = MathF.Sin(i * 0.35f) * 6;
            
            int tx = startX + i;
            int ty = startY + (int)wave;
            int thickness = Math.Max(1, (int)(4 * (1 - t)));
            
            for (int th = -thickness; th <= thickness; th++)
            {
                float light = 1 - MathF.Abs((float)th / (thickness + 1));
                light = light * 0.7f + 0.3f;
                
                int idx = (int)((1 - light) * (voidC.Length - 1));
                idx = Math.Clamp(idx, 0, voidC.Length - 1);
                
                SetPixelSafe(data, w, h, tx, ty + th, voidC[idx]);
            }
        }
    }
    
    // === HELPER: Cosmic star ===
    private void DrawCosmicStar(Color[] data, int w, int h, int cx, int cy, Color bright, Color[] voidC)
    {
        // 4-point star
        SetPixelSafe(data, w, h, cx, cy, bright);
        SetPixelSafe(data, w, h, cx - 1, cy, LerpColor(bright, voidC[3], 0.5f));
        SetPixelSafe(data, w, h, cx + 1, cy, LerpColor(bright, voidC[3], 0.5f));
        SetPixelSafe(data, w, h, cx, cy - 1, LerpColor(bright, voidC[3], 0.5f));
        SetPixelSafe(data, w, h, cx, cy + 1, LerpColor(bright, voidC[3], 0.5f));
    }
    
    // === HELPER: Void corona ===
    private void DrawVoidCorona(Color[] data, int w, int h, int cx, int cy, int radius, Color[] voidC)
    {
        for (int angle = 0; angle < 360; angle += 3)
        {
            float rad = angle * MathF.PI / 180;
            for (int r = radius; r < radius + 8; r++)
            {
                int px = cx + (int)(MathF.Cos(rad) * r);
                int py = cy + (int)(MathF.Sin(rad) * r * 0.7f);
                
                float fade = (float)(r - radius) / 8f;
                if (fade > 0.9f) continue;
                
                Color c = LerpColor(voidC[0], voidC[4], fade);
                SetPixelSafe(data, w, h, px, py, c);
            }
        }
    }
}
