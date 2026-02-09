namespace MechaRogue.Rendering;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MechaRogue.Models;

/// <summary>
/// GBA-style outdoor battlefield renderer.
/// Bright sky, clouds, green hills, grass — side-view with player left, enemy right.
/// Includes charge gauges, animations, damage popups, screen shake.
/// </summary>
public class BattlefieldRenderer
{
    private readonly DrawHelper _draw;
    private readonly PixelFont _font;
    private readonly GraphicsDevice _gd;
    private readonly Random _rng = new();

    // Layout
    private const int SceneH = 320;
    private const float GroundRatio = 0.72f; // 1v1 flat view
    private const float HorizonRatio = 0.36f; // 2.5D horizon line
    private const float SpriteScale = 4.5f;
    private const int SpriteW = (int)(16 * SpriteScale); // 72
    private const int SpriteH = (int)(20 * SpriteScale); // 90

    // Animation
    private readonly List<Particle> _particles = [];
    private readonly List<DamagePopup> _popups = [];
    private float _breatheTimer;
    private float _shakeX, _shakeY;
    private float _shakeTimer;
    private float _readyPulse;

    // Cloud positions (x offset, y, scale)
    private readonly (float x, float y, float w, float h)[] _clouds;

    // Cached textures
    private Texture2D? _playerTex, _enemyTex;
    private Medabot? _lastPlayer, _lastEnemy;

    // Squad texture cache (3v3)
    private readonly Dictionary<string, Texture2D> _squadTexCache = [];
    private readonly HashSet<string> _squadTexKeys = [];

    // Melee dash animation — 3-phase: run-to [0–0.35], strike [0.35–0.50], run-back [0.50–0.85]
    private float _meleeProgress = -1;
    private bool _meleeIsPlayer;
    private Medabot? _dashingBot;
    private const float MeleePhaseRunTo = 0.35f;
    private const float MeleePhaseStrike = 0.50f;
    private const float MeleePhaseRunBack = 0.85f;

    // Track attacker/target bots for animation positioning
    private Medabot? _animAttacker;
    private Medabot? _animTarget;

    // Visible projectile travel (0→1 lerp from attacker to target)
    private float _projectileProgress = -1;
    private Medabot? _projectileFrom;
    private Medabot? _projectileTo;

    // Hit flash overlays
    private readonly List<HitFlash> _hitFlashes = [];

    public BattlefieldRenderer(GraphicsDevice gd, DrawHelper draw, PixelFont font)
    {
        _gd = gd;
        _draw = draw;
        _font = font;

        _clouds = [
            (0.10f, 20, 60, 18),
            (0.35f, 45, 45, 14),
            (0.62f, 15, 55, 16),
            (0.85f, 50, 40, 12),
        ];
    }

    private int GroundY(int sceneWidth) => (int)(SceneH * GroundRatio);
    private int PlayerX(int sceneWidth) => Math.Max(90, (int)(sceneWidth * 0.24f));
    private int EnemyX(int sceneWidth) => Math.Max(200, (int)(sceneWidth * 0.76f));

    /// <summary>
    /// Update animation state. Call every frame.
    /// </summary>
    public void Update(float deltaSeconds)
    {
        _breatheTimer += deltaSeconds * 2.5f;
        _readyPulse += deltaSeconds * 5f;

        // Screen shake decay
        if (_shakeTimer > 0)
        {
            _shakeTimer -= deltaSeconds;
            float intensity = _shakeTimer / 0.3f;
            _shakeX = (float)(_rng.NextDouble() * 8 - 4) * intensity;
            _shakeY = (float)(_rng.NextDouble() * 6 - 3) * intensity;
        }
        else
        {
            _shakeX = _shakeY = 0;
        }

        // Melee dash (multi-phase, snappy like GBA)
        if (_meleeProgress >= 0)
        {
            _meleeProgress += deltaSeconds * 2.8f;
            if (_meleeProgress > 1f) _meleeProgress = -1;
        }

        // Projectile travel
        if (_projectileProgress >= 0)
        {
            _projectileProgress += deltaSeconds * 3.5f;
            if (_projectileProgress > 1f) _projectileProgress = -1;
        }

        // Hit flashes
        for (int i = _hitFlashes.Count - 1; i >= 0; i--)
        {
            _hitFlashes[i].Life -= deltaSeconds;
            if (_hitFlashes[i].Life <= 0) _hitFlashes.RemoveAt(i);
        }

        // Particles (per-particle gravity)
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Life -= deltaSeconds;
            p.X += p.VX * deltaSeconds;
            p.Y += p.VY * deltaSeconds;
            p.VY += p.Gravity * deltaSeconds;
            if (p.Life <= 0) _particles.RemoveAt(i);
        }

        // Popups
        for (int i = _popups.Count - 1; i >= 0; i--)
        {
            var d = _popups[i];
            d.Life -= deltaSeconds;
            d.Y -= 35 * deltaSeconds;
            if (d.Life <= 0) _popups.RemoveAt(i);
        }
    }

    /// <summary>
    /// Draw the complete battlefield scene.
    /// </summary>
    public void Draw(SpriteBatch sb, int sceneWidth, Medabot? player, Medabot? enemy,
        double playerCharge, double enemyCharge)
    {
        int groundY = GroundY(sceneWidth);
        int px = PlayerX(sceneWidth);
        int ex = EnemyX(sceneWidth);

        // Apply screen shake offset
        var offset = new Vector2(_shakeX, _shakeY);

        // ── Sky gradient ──
        _draw.FillGradientV(sb, new Rectangle(0, 0, sceneWidth, groundY),
            new Color(0x68, 0xC8, 0xFF), new Color(0xB0, 0xE8, 0xFF));

        // ── Clouds ──
        DrawClouds(sb, sceneWidth);

        // ── Hills ──
        DrawHills(sb, sceneWidth, groundY);

        // ── Ground ──
        _draw.FillGradientV(sb, new Rectangle(0, groundY, sceneWidth, SceneH - groundY),
            new Color(0x4A, 0xB8, 0x3C), new Color(0x38, 0x8E, 0x2A));

        // Grass stripes
        for (int y = groundY + 4; y < SceneH; y += 8)
        {
            _draw.FillRect(sb, new Rectangle(0, y, sceneWidth, 1),
                new Color(0x56, 0xCC, 0x48) * 0.3f);
        }

        // Ground details (flowers, rocks)
        DrawGroundDetails(sb, sceneWidth, groundY);

        // ── Sprites ──
        float breatheOffset = (float)Math.Sin(_breatheTimer) * 2;

        // Refresh textures if bots changed
        if (player != _lastPlayer) { _playerTex?.Dispose(); _playerTex = player != null ? SpriteGenerator.CreateTexture(_gd, player) : null; _lastPlayer = player; }
        if (enemy != _lastEnemy) { _enemyTex?.Dispose(); _enemyTex = enemy != null ? SpriteGenerator.CreateTexture(_gd, enemy) : null; _lastEnemy = enemy; }

        // Player mech
        if (_playerTex != null)
        {
            float drawX = px - SpriteW / 2 + offset.X;
            float drawY = groundY - SpriteH + breatheOffset + offset.Y;

            // Melee dash offset
            if (_meleeProgress >= 0 && _meleeIsPlayer)
            {
                float dashT = _meleeProgress < 0.5f ? _meleeProgress * 2 : (1 - _meleeProgress) * 2;
                drawX += (ex - px) * 0.6f * dashT;
            }

            sb.Draw(_playerTex, new Rectangle((int)drawX, (int)drawY, SpriteW, SpriteH),
                null, Color.White, 0, Vector2.Zero, SpriteEffects.None, 0);
        }

        // Enemy mech (flipped horizontally)
        if (_enemyTex != null)
        {
            float drawX = ex - SpriteW / 2 + offset.X;
            float drawY = groundY - SpriteH + breatheOffset + offset.Y;

            if (_meleeProgress >= 0 && !_meleeIsPlayer)
            {
                float dashT = _meleeProgress < 0.5f ? _meleeProgress * 2 : (1 - _meleeProgress) * 2;
                drawX -= (ex - px) * 0.6f * dashT;
            }

            sb.Draw(_enemyTex, new Rectangle((int)drawX, (int)drawY, SpriteW, SpriteH),
                null, Color.White, 0, Vector2.Zero, SpriteEffects.FlipHorizontally, 0);
        }

        // ── Name plates ──
        if (player != null)
            _font.DrawStringWithShadow(sb, player.Name.ToUpperInvariant(),
                new Vector2(px - _font.MeasureString(player.Name, 2).X / 2, groundY - SpriteH - 40 + offset.Y), Color.White, 2);
        if (enemy != null)
            _font.DrawStringWithShadow(sb, enemy.Name.ToUpperInvariant(),
                new Vector2(ex - _font.MeasureString(enemy.Name, 2).X / 2, groundY - SpriteH - 40 + offset.Y), Color.White, 2);

        // ── Charge gauges ──
        int gaugeW = 80, gaugeH = 10;
        int gaugeY = groundY - SpriteH - 22;

        // Player gauge
        DrawChargeGauge(sb, px - gaugeW / 2, gaugeY + (int)offset.Y, gaugeW, gaugeH,
            (float)(playerCharge / 100.0), new Color(0x40, 0x90, 0xFF), "CHARGE");
        if (playerCharge >= 100)
            DrawReadyFlash(sb, px, gaugeY + (int)offset.Y - 2, gaugeW);

        // Enemy gauge
        DrawChargeGauge(sb, ex - gaugeW / 2, gaugeY + (int)offset.Y, gaugeW, gaugeH,
            (float)(enemyCharge / 100.0), new Color(0xFF, 0x50, 0x50), "CHARGE");
        if (enemyCharge >= 100)
            DrawReadyFlash(sb, ex, gaugeY + (int)offset.Y - 2, gaugeW);

        // ── Particles ──
        foreach (var p in _particles)
        {
            _draw.FillRect(sb, new Rectangle((int)(p.X + offset.X), (int)(p.Y + offset.Y),
                (int)(p.Size), (int)(p.Size)), p.Color * Math.Clamp(p.Life / p.MaxLife, 0, 1));
        }

        // ── Damage popups ──
        foreach (var d in _popups)
        {
            float alpha = Math.Clamp(d.Life / d.MaxLife, 0, 1);
            _font.DrawStringWithShadow(sb, d.Text,
                new Vector2(d.X + offset.X - _font.MeasureString(d.Text, 2).X / 2, d.Y + offset.Y),
                d.Color * alpha, 2);
        }
    }

    private void DrawChargeGauge(SpriteBatch sb, int x, int y, int w, int h, float fill, Color fillColor, string label)
    {
        // Label
        _font.DrawString(sb, label, new Vector2(x, y - 10), Color.White * 0.8f, 1);
        // Bar
        _draw.DrawBar(sb, new Rectangle(x, y, w, h), fill, fillColor,
            new Color(16, 16, 24, 180), Color.White * 0.7f);
    }

    private void DrawReadyFlash(SpriteBatch sb, int centerX, int y, int gaugeW)
    {
        float pulse = (float)(0.5 + 0.5 * Math.Sin(_readyPulse));
        var color = Color.Lerp(new Color(0x40, 0xFF, 0x40), Color.White, pulse);
        _font.DrawStringCentered(sb, "READY!", new Vector2(centerX, y - 14), color, 2);
    }

    private void DrawClouds(SpriteBatch sb, int sceneWidth)
    {
        var cloudColor = Color.White * 0.7f;
        foreach (var (cx, cy, cw, ch) in _clouds)
        {
            int x = (int)(cx * sceneWidth);
            _draw.FillRect(sb, new Rectangle(x, (int)cy, (int)cw, (int)ch), cloudColor);
            _draw.FillRect(sb, new Rectangle(x + 5, (int)cy - 4, (int)(cw * 0.6f), (int)ch), cloudColor);
            _draw.FillRect(sb, new Rectangle(x + (int)(cw * 0.3f), (int)cy - 6, (int)(cw * 0.4f), (int)(ch * 0.6f)), cloudColor);
        }
    }

    private void DrawHills(SpriteBatch sb, int sceneWidth, int groundY)
    {
        // Hill silhouettes behind ground
        var hillColor = new Color(0x38, 0x9E, 0x32);
        var darkHill = new Color(0x2C, 0x7A, 0x28);

        // Far hills
        for (int x = 0; x < sceneWidth; x += 2)
        {
            float h = 30 + 20 * (float)Math.Sin(x * 0.012) + 10 * (float)Math.Sin(x * 0.027);
            _draw.FillRect(sb, new Rectangle(x, groundY - (int)h, 2, (int)h), darkHill);
        }
        // Near hills
        for (int x = 0; x < sceneWidth; x += 2)
        {
            float h = 18 + 15 * (float)Math.Sin(x * 0.02 + 1.5) + 8 * (float)Math.Sin(x * 0.04);
            _draw.FillRect(sb, new Rectangle(x, groundY - (int)h, 2, (int)h), hillColor);
        }

        // Tree silhouettes on far hills
        var treeColor = new Color(0x20, 0x68, 0x1E);
        for (int i = 0; i < 8; i++)
        {
            int tx = sceneWidth * (i + 1) / 9;
            int th = 15 + (i * 7) % 12;
            _draw.FillRect(sb, new Rectangle(tx - 3, groundY - 30 - th, 6, th), treeColor);
            _draw.FillRect(sb, new Rectangle(tx - 8, groundY - 30 - th + 2, 16, th - 5), treeColor);
        }
    }

    private void DrawGroundDetails(SpriteBatch sb, int sceneWidth, int groundY)
    {
        // Deterministic "random" decorations based on position
        var flowerColor = new Color(0xFF, 0xE0, 0x40);
        var rockColor = new Color(0x5A, 0x5A, 0x5A);
        var grassColor = new Color(0x5C, 0xD8, 0x50);

        for (int x = 10; x < sceneWidth - 10; x += 35)
        {
            int seed = x * 31;
            if (seed % 3 == 0)
                _draw.FillRect(sb, new Rectangle(x, groundY + 8 + (seed % 15), 3, 3), flowerColor);
            else if (seed % 3 == 1)
                _draw.FillRect(sb, new Rectangle(x, groundY + 5 + (seed % 20), 5, 3), rockColor);
            else
                _draw.FillRect(sb, new Rectangle(x, groundY + 2 + (seed % 10), 2, 6), grassColor);
        }
    }

    // ═══════════════ ANIMATION TRIGGERS ═══════════════

    public void TriggerScreenShake(float duration = 0.3f) => _shakeTimer = duration;

    public void TriggerMeleeDash(bool isPlayer)
    {
        _meleeIsPlayer = isPlayer;
        _meleeProgress = 0;
        _dashingBot = null;
    }

    /// <summary>Trigger a melee dash for a specific bot (3v3 — multi-phase: run-to → strike → run-back).</summary>
    public void TriggerMeleeDash(Medabot attacker, Medabot target)
    {
        _meleeIsPlayer = attacker.IsPlayerOwned;
        _meleeProgress = 0;
        _dashingBot = attacker;
        _animAttacker = attacker;
        _animTarget = target;
    }

    /// <summary>Fire a visible projectile from attacker to target.</summary>
    public void SpawnProjectileAt(Medabot attacker, Medabot target)
    {
        _animAttacker = attacker;
        _animTarget = target;
        _projectileFrom = attacker;
        _projectileTo = target;
        _projectileProgress = 0;
    }

    public void SpawnImpact(int sceneWidth, bool onPlayer)
    {
        int x = onPlayer ? PlayerX(sceneWidth) : EnemyX(sceneWidth);
        int y = GroundY(sceneWidth) - SpriteH / 2;
        for (int i = 0; i < 12; i++)
        {
            _particles.Add(new Particle
            {
                X = x + _rng.Next(-15, 15),
                Y = y + _rng.Next(-10, 10),
                VX = (_rng.NextSingle() - 0.5f) * 200,
                VY = (_rng.NextSingle() - 0.8f) * 150,
                Size = 3 + _rng.Next(4),
                Color = Color.Lerp(Color.Yellow, Color.OrangeRed, _rng.NextSingle()),
                Life = 0.3f + _rng.NextSingle() * 0.3f,
                MaxLife = 0.6f
            });
        }
    }

    public void SpawnProjectile(int sceneWidth, bool fromPlayer)
    {
        int sx = fromPlayer ? PlayerX(sceneWidth) + 20 : EnemyX(sceneWidth) - 20;
        int tx = fromPlayer ? EnemyX(sceneWidth) : PlayerX(sceneWidth);
        int y = GroundY(sceneWidth) - SpriteH / 2;
        float dir = fromPlayer ? 1 : -1;

        for (int i = 0; i < 4; i++)
        {
            _particles.Add(new Particle
            {
                X = sx,
                Y = y + _rng.Next(-5, 5),
                VX = dir * (350 + i * 50),
                VY = (_rng.NextSingle() - 0.5f) * 30,
                Size = 4,
                Color = Color.Lerp(Color.Cyan, Color.White, _rng.NextSingle()),
                Life = 0.4f + i * 0.05f,
                MaxLife = 0.6f
            });
        }
    }

    public void SpawnExplosion(int sceneWidth, bool onPlayer)
    {
        int x = onPlayer ? PlayerX(sceneWidth) : EnemyX(sceneWidth);
        int y = GroundY(sceneWidth) - SpriteH / 2;
        for (int i = 0; i < 20; i++)
        {
            float angle = _rng.NextSingle() * MathF.Tau;
            float speed = 80 + _rng.NextSingle() * 150;
            _particles.Add(new Particle
            {
                X = x, Y = y,
                VX = MathF.Cos(angle) * speed,
                VY = MathF.Sin(angle) * speed,
                Size = 3 + _rng.Next(5),
                Color = Color.Lerp(Color.Orange, Color.Red, _rng.NextSingle()),
                Life = 0.4f + _rng.NextSingle() * 0.4f,
                MaxLife = 0.8f
            });
        }
        TriggerScreenShake(0.4f);
    }

    public void SpawnMedaforceEffect(int sceneWidth, bool fromPlayer)
    {
        int cx = fromPlayer ? PlayerX(sceneWidth) : EnemyX(sceneWidth);
        int y = GroundY(sceneWidth) - SpriteH / 2;
        for (int i = 0; i < 30; i++)
        {
            float angle = _rng.NextSingle() * MathF.Tau;
            float speed = 50 + _rng.NextSingle() * 200;
            _particles.Add(new Particle
            {
                X = cx, Y = y,
                VX = MathF.Cos(angle) * speed,
                VY = MathF.Sin(angle) * speed - 50,
                Size = 4 + _rng.Next(5),
                Color = Color.Lerp(new Color(0x80, 0x80, 0xFF), Color.White, _rng.NextSingle()),
                Life = 0.6f + _rng.NextSingle() * 0.4f,
                MaxLife = 1.0f
            });
        }
        TriggerScreenShake(0.5f);
    }

    public void SpawnHealEffect(int sceneWidth, bool onPlayer)
    {
        int x = onPlayer ? PlayerX(sceneWidth) : EnemyX(sceneWidth);
        int baseY = GroundY(sceneWidth) - SpriteH;
        for (int i = 0; i < 10; i++)
        {
            _particles.Add(new Particle
            {
                X = x + _rng.Next(-20, 20),
                Y = baseY + SpriteH + _rng.Next(-10, 10),
                VX = (_rng.NextSingle() - 0.5f) * 20,
                VY = -40 - _rng.NextSingle() * 60,
                Size = 3 + _rng.Next(3),
                Color = Color.Lerp(new Color(0x40, 0xFF, 0x40), Color.White, _rng.NextSingle() * 0.5f),
                Life = 0.6f + _rng.NextSingle() * 0.4f,
                MaxLife = 1.0f
            });
        }
    }

    public void SpawnDamagePopup(int sceneWidth, bool onPlayer, int damage, bool crit)
    {
        int x = onPlayer ? PlayerX(sceneWidth) : EnemyX(sceneWidth);
        int y = GroundY(sceneWidth) - SpriteH - 50;
        _popups.Add(new DamagePopup
        {
            X = x + _rng.Next(-10, 10),
            Y = y,
            Text = crit ? $"{damage}!!" : $"{damage}",
            Color = crit ? Color.Yellow : Color.White,
            Life = 1.2f,
            MaxLife = 1.2f
        });
    }

    // ═══════════════ SQUAD-AWARE ANIMATION TRIGGERS ═══════════════

    // Store squad lists so we can resolve positions during Draw
    private List<Medabot>? _playerSquadRef;
    private List<Medabot>? _enemySquadRef;
    private int _lastSceneWidth;
    private int _lastSceneHeight;

    /// <summary>Resolve a bot's center X position given the current squad layout.</summary>
    private int BotCenterX(Medabot bot)
    {
        var pos = GetSquadPositionForBot(bot, _lastSceneWidth, _lastSceneHeight);
        return (int)pos.X;
    }

    private int BotCenterY(Medabot bot)
    {
        var pos = GetSquadPositionForBot(bot, _lastSceneWidth, _lastSceneHeight);
        int horizonY = (int)(_lastSceneHeight * HorizonRatio);
        float ds = GetDepthScale(pos.Y, horizonY, _lastSceneHeight);
        int sprH = (int)(20 * 3.5f * ds);
        return (int)pos.Y - sprH / 2;
    }

    /// <summary>GBA hit sparks — white flash overlay + white/yellow burst at target.</summary>
    public void SpawnImpactAt(Medabot target)
    {
        int x = BotCenterX(target);
        int y = BotCenterY(target);

        // White flash on target sprite
        _hitFlashes.Add(new HitFlash { Bot = target, Life = 0.12f, MaxLife = 0.12f });

        // Spark burst (GBA-style: mostly white/yellow with some orange)
        for (int i = 0; i < 16; i++)
        {
            float angle = _rng.NextSingle() * MathF.Tau;
            float speed = 60 + _rng.NextSingle() * 140;
            _particles.Add(new Particle
            {
                X = x + _rng.Next(-8, 8), Y = y + _rng.Next(-8, 8),
                VX = MathF.Cos(angle) * speed,
                VY = MathF.Sin(angle) * speed,
                Size = 2 + _rng.Next(3),
                Color = _rng.NextSingle() < 0.6f ? Color.White : Color.Lerp(Color.Yellow, Color.OrangeRed, _rng.NextSingle()),
                Life = 0.15f + _rng.NextSingle() * 0.15f,
                MaxLife = 0.3f,
                Gravity = 0
            });
        }
        TriggerScreenShake(0.12f);
    }

    /// <summary>KO explosion — parts fly off, big boom, GBA-style.</summary>
    public void SpawnExplosionAt(Medabot target)
    {
        int x = BotCenterX(target);
        int y = BotCenterY(target);

        // White flash
        _hitFlashes.Add(new HitFlash { Bot = target, Life = 0.2f, MaxLife = 0.2f });

        // Explosion burst (orange/red/yellow)
        for (int i = 0; i < 28; i++)
        {
            float angle = _rng.NextSingle() * MathF.Tau;
            float speed = 60 + _rng.NextSingle() * 180;
            _particles.Add(new Particle
            {
                X = x, Y = y,
                VX = MathF.Cos(angle) * speed,
                VY = MathF.Sin(angle) * speed,
                Size = 3 + _rng.Next(5),
                Color = Color.Lerp(Color.Orange, Color.Red, _rng.NextSingle()),
                Life = 0.3f + _rng.NextSingle() * 0.4f,
                MaxLife = 0.7f,
                Gravity = 80
            });
        }

        // Flying parts (gray/silver rectangles launching upward)
        for (int i = 0; i < 5; i++)
        {
            _particles.Add(new Particle
            {
                X = x + _rng.Next(-12, 12), Y = y + _rng.Next(-8, 8),
                VX = (_rng.NextSingle() - 0.5f) * 250,
                VY = -120 - _rng.NextSingle() * 180,
                Size = 5 + _rng.Next(4),
                Color = Color.Lerp(new Color(0x80, 0x88, 0x90), new Color(0xC0, 0xC0, 0xD0), _rng.NextSingle()),
                Life = 0.5f + _rng.NextSingle() * 0.5f,
                MaxLife = 1.0f,
                Gravity = 300
            });
        }

        TriggerScreenShake(0.4f);
    }

    /// <summary>Medaforce: blue/white energy burst + launches beam projectile at target.</summary>
    public void SpawnMedaforceAt(Medabot attacker)
    {
        int cx = BotCenterX(attacker);
        int y = BotCenterY(attacker);

        // Blue/white energy explosion
        for (int i = 0; i < 35; i++)
        {
            float angle = _rng.NextSingle() * MathF.Tau;
            float speed = 40 + _rng.NextSingle() * 220;
            _particles.Add(new Particle
            {
                X = cx, Y = y,
                VX = MathF.Cos(angle) * speed,
                VY = MathF.Sin(angle) * speed - 40,
                Size = 4 + _rng.Next(5),
                Color = Color.Lerp(new Color(0x60, 0x80, 0xFF), Color.White, _rng.NextSingle()),
                Life = 0.5f + _rng.NextSingle() * 0.5f,
                MaxLife = 1.0f,
                Gravity = -20 // float upward
            });
        }

        // Launch beam projectile toward target if we have one
        if (_animTarget != null)
        {
            _projectileFrom = attacker;
            _projectileTo = _animTarget;
            _projectileProgress = 0;
        }

        TriggerScreenShake(0.5f);
    }

    /// <summary>Green heal particles rising from target.</summary>
    public void SpawnHealAt(Medabot target)
    {
        int x = BotCenterX(target);
        int baseY = BotCenterY(target);
        for (int i = 0; i < 14; i++)
        {
            _particles.Add(new Particle
            {
                X = x + _rng.Next(-18, 18),
                Y = baseY + _rng.Next(0, 20),
                VX = (_rng.NextSingle() - 0.5f) * 15,
                VY = -50 - _rng.NextSingle() * 70,
                Size = 3 + _rng.Next(3),
                Color = Color.Lerp(new Color(0x40, 0xFF, 0x40), Color.White, _rng.NextSingle() * 0.4f),
                Life = 0.5f + _rng.NextSingle() * 0.4f,
                MaxLife = 0.9f,
                Gravity = -30
            });
        }
    }

    /// <summary>Spawn damage popup at a specific bot.</summary>
    public void SpawnDamagePopupAt(Medabot target, int damage, bool crit)
    {
        int x = BotCenterX(target);
        int y = BotCenterY(target) - 40;
        _popups.Add(new DamagePopup
        {
            X = x + _rng.Next(-10, 10),
            Y = y,
            Text = crit ? $"{damage}!!" : $"{damage}",
            Color = crit ? Color.Yellow : Color.White,
            Life = 1.2f,
            MaxLife = 1.2f
        });
    }

    // ═══════════════ 3v3 SQUAD BATTLEFIELD (2.5D) ═══════════════

    /// <summary>
    /// Draw a 2.5D perspective battlefield — 3/4 overhead view of a rectangular arena.
    /// Player bots on the left facing right, enemy bots on the right facing left.
    /// Ground plane has perspective tiles converging toward a horizon.
    /// </summary>
    public void DrawSquadBattle(SpriteBatch sb, int sceneWidth, int sceneHeight,
        List<Medabot> playerSquad, List<Medabot> enemySquad)
    {
        // Store refs for position lookups in animation triggers
        _playerSquadRef = playerSquad;
        _enemySquadRef = enemySquad;
        _lastSceneWidth = sceneWidth;
        _lastSceneHeight = sceneHeight;

        int horizonY = (int)(sceneHeight * HorizonRatio);
        float centerX = sceneWidth / 2f;
        var offset = new Vector2(_shakeX, _shakeY);

        // ── Sky gradient ──
        _draw.FillGradientV(sb, new Rectangle(0, 0, sceneWidth, horizonY + 10),
            new Color(0x50, 0xB0, 0xF0), new Color(0x98, 0xD8, 0xFF));

        // ── Clouds ──
        foreach (var (cx, cy, cw, ch) in _clouds)
        {
            var cloudColor = Color.White * 0.6f;
            int x = (int)(cx * sceneWidth);
            int y = (int)(cy * horizonY / 80f);
            _draw.FillRect(sb, new Rectangle(x, y, (int)cw, (int)ch), cloudColor);
            _draw.FillRect(sb, new Rectangle(x + 5, y - 3, (int)(cw * 0.6f), (int)ch), cloudColor);
        }

        // ── Distant mountains near horizon ──
        var farMtn = new Color(0x50, 0x80, 0x4A);
        var nearMtn = new Color(0x3A, 0x6E, 0x34);
        for (int x = 0; x < sceneWidth; x += 2)
        {
            float h = 12 + 8 * (float)Math.Sin(x * 0.015) + 5 * (float)Math.Sin(x * 0.035);
            _draw.FillRect(sb, new Rectangle(x, horizonY - (int)h, 2, (int)h + 4), farMtn);
        }
        for (int x = 0; x < sceneWidth; x += 2)
        {
            float h = 7 + 6 * (float)Math.Sin(x * 0.025 + 1.2f) + 3 * (float)Math.Sin(x * 0.05);
            _draw.FillRect(sb, new Rectangle(x, horizonY - (int)h, 2, (int)h + 2), nearMtn);
        }

        // ── Perspective ground plane ──
        DrawPerspectiveGround(sb, sceneWidth, sceneHeight, horizonY, centerX, offset);

        // ── Collect all bots and depth-sort (back-to-front = ascending Y) ──
        var allBots = new List<(Medabot bot, bool isPlayer, int index, int total)>();
        for (int i = 0; i < playerSquad.Count; i++)
            allBots.Add((playerSquad[i], true, i, playerSquad.Count));
        for (int i = 0; i < enemySquad.Count; i++)
            allBots.Add((enemySquad[i], false, i, enemySquad.Count));

        allBots.Sort((a, b) =>
        {
            var posA = GetSquadPosition(sceneWidth, sceneHeight, a.isPlayer, a.index, a.total);
            var posB = GetSquadPosition(sceneWidth, sceneHeight, b.isPlayer, b.index, b.total);
            return posA.Y.CompareTo(posB.Y);
        });

        float breatheOffset = (float)Math.Sin(_breatheTimer) * 1.5f;

        // ── Shadow pass ──
        foreach (var (bot, isPlayer, index, total) in allBots)
        {
            if (bot.IsKnockedOut) continue;
            var pos = GetSquadPosition(sceneWidth, sceneHeight, isPlayer, index, total);
            float ds = GetDepthScale(pos.Y, horizonY, sceneHeight);
            DrawBotShadow(sb, pos, ds, offset);
        }

        // ── Sprite pass (back-to-front) ──
        foreach (var (bot, isPlayer, index, total) in allBots)
        {
            var pos = GetSquadPosition(sceneWidth, sceneHeight, isPlayer, index, total);
            float ds = GetDepthScale(pos.Y, horizonY, sceneHeight);
            float scale = 3.5f * ds;
            int sprW = (int)(16 * scale);
            int sprH = (int)(20 * scale);

            float drawX = pos.X - sprW / 2 + offset.X;
            float drawY = pos.Y - sprH + breatheOffset * ds + offset.Y;

            // ── Multi-phase melee dash (run-to → strike-hold → run-back → settle) ──
            if (_meleeProgress >= 0 && _dashingBot == bot && _animTarget != null)
            {
                var targetPos = GetSquadPositionForBot(_animTarget, sceneWidth, sceneHeight);
                // Offset so the attacker stops next to target, not overlapping
                float targetDrawX = targetPos.X - (isPlayer ? sprW : -sprW);
                float targetDrawY = targetPos.Y;

                if (_meleeProgress < MeleePhaseRunTo)
                {
                    // Phase 1: Run TO target (smoothstep ease)
                    float t = _meleeProgress / MeleePhaseRunTo;
                    float ease = t * t * (3 - 2 * t);
                    drawX += (targetDrawX - pos.X) * ease;
                    drawY += (targetDrawY - pos.Y) * 0.3f * ease;
                }
                else if (_meleeProgress < MeleePhaseStrike)
                {
                    // Phase 2: Strike hold (at target)
                    drawX += (targetDrawX - pos.X);
                    drawY += (targetDrawY - pos.Y) * 0.3f;
                }
                else if (_meleeProgress < MeleePhaseRunBack)
                {
                    // Phase 3: Run BACK to home
                    float t = (_meleeProgress - MeleePhaseStrike) / (MeleePhaseRunBack - MeleePhaseStrike);
                    float ease = t * t * (3 - 2 * t);
                    float atTargetDX = targetDrawX - pos.X;
                    float atTargetDY = (targetDrawY - pos.Y) * 0.3f;
                    drawX += atTargetDX * (1 - ease);
                    drawY += atTargetDY * (1 - ease);
                }
                // Phase 4: Settle (already at home, no offset needed)
            }

            var tex = GetOrCreateSquadTexture(bot);
            if (tex != null)
            {
                float alpha = bot.IsKnockedOut ? 0.25f : 1.0f;
                var effects = isPlayer ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

                // Check for hit flash (rapid white/normal toggle)
                bool flashing = false;
                foreach (var hf in _hitFlashes)
                {
                    if (hf.Bot == bot) { flashing = ((int)(hf.Life * 40)) % 2 == 0; break; }
                }

                sb.Draw(tex, new Rectangle((int)drawX, (int)drawY, sprW, sprH),
                    null, Color.White * alpha, 0, Vector2.Zero, effects, 0);

                // White overlay flash (additive-style brightening)
                if (flashing && !bot.IsKnockedOut)
                {
                    sb.Draw(tex, new Rectangle((int)drawX, (int)drawY, sprW, sprH),
                        null, Color.White * 0.6f, 0, Vector2.Zero, effects, 0);
                }
            }

            // ── Name plate + charge gauge ──
            if (!bot.IsKnockedOut)
            {
                var nameColor = bot.IsDefending ? Color.Cyan : Color.White;
                _font.DrawStringCentered(sb, bot.Name.ToUpperInvariant(),
                    new Vector2(pos.X + offset.X, drawY - 14 * ds), nameColor, 1);

                float chargePct = bot.ChargeGauge < 0 ? 0 : (float)(bot.ChargeGauge / 100.0);
                int gw = (int)(50 * ds), gh = Math.Max(3, (int)(5 * ds));
                var gaugeColor = chargePct >= 1.0f ? Color.Yellow
                    : (isPlayer ? new Color(0x40, 0x90, 0xFF) : new Color(0xFF, 0x50, 0x50));
                _draw.DrawBar(sb, new Rectangle((int)(pos.X - gw / 2 + offset.X),
                    (int)(drawY - 24 * ds), gw, gh),
                    chargePct, gaugeColor, new Color(16, 16, 24, 180), Color.White * 0.5f);

                if (chargePct >= 1.0f)
                {
                    float pulse = (float)(0.5 + 0.5 * Math.Sin(_readyPulse));
                    var readyColor = Color.Lerp(isPlayer ? Color.Yellow : Color.Red, Color.White, pulse);
                    _font.DrawStringCentered(sb, "READY!",
                        new Vector2(pos.X + offset.X, drawY - 34 * ds), readyColor, 1);
                }
            }
            else
            {
                _font.DrawStringCentered(sb, "K.O.",
                    new Vector2(pos.X + offset.X, pos.Y - sprH / 2 + offset.Y),
                    Color.Red, ds > 0.7f ? 2 : 1);
            }
        }

        // ── Visible projectile ──
        if (_projectileProgress >= 0 && _projectileFrom != null && _projectileTo != null)
        {
            DrawProjectile(sb, offset);
        }

        // ── Particles ──
        foreach (var p in _particles)
        {
            float a = Math.Clamp(p.Life / p.MaxLife, 0, 1);
            _draw.FillRect(sb, new Rectangle((int)(p.X + offset.X), (int)(p.Y + offset.Y),
                (int)p.Size, (int)p.Size), p.Color * a);
        }

        // ── Damage popups ──
        foreach (var d in _popups)
        {
            float alpha = Math.Clamp(d.Life / d.MaxLife, 0, 1);
            float popScale = d.Life > d.MaxLife * 0.8f ? 2.5f : 2f;
            _font.DrawStringWithShadow(sb, d.Text,
                new Vector2(d.X + offset.X - _font.MeasureString(d.Text, (int)popScale).X / 2, d.Y + offset.Y),
                d.Color * alpha, (int)popScale);
        }
    }

    // ═══════════════ 2.5D RENDERING HELPERS ═══════════════

    /// <summary>
    /// Draw 3/4 overhead perspective ground with converging tile grid.
    /// The arena is a rectangular field viewed from above-and-behind,
    /// wider at the bottom (near camera) and narrower at the horizon.
    /// </summary>
    private void DrawPerspectiveGround(SpriteBatch sb, int sceneWidth, int sceneHeight,
        int horizonY, float centerX, Vector2 offset)
    {
        int groundH = sceneHeight - horizonY;
        if (groundH <= 0) return;

        // Arena tile colors — earthy robattle arena (GBA-style tan/brown)
        var tileA = new Color(0xC0, 0xA8, 0x78); // light tan
        var tileB = new Color(0xA8, 0x90, 0x68); // darker tan
        var gridColor = new Color(0x90, 0x78, 0x58);
        var edgeColor = new Color(0x68, 0x50, 0x38);

        // Horizon haze
        _draw.FillGradientV(sb, new Rectangle(0, horizonY - 2, sceneWidth, 8),
            new Color(0xA0, 0xC8, 0x90) * 0.4f, new Color(0xC0, 0xA8, 0x78) * 0.3f);

        // Fill outside arena with grass
        var grassOutside = new Color(0x48, 0x88, 0x3A);
        _draw.FillRect(sb, new Rectangle(0, horizonY, sceneWidth, groundH), grassOutside);

        int numCols = 12; // vertical divisions across arena
        float prevWorldZ = -1;
        int step = 2;

        for (int y = horizonY; y < sceneHeight; y += step)
        {
            float t = (float)(y - horizonY) / groundH; // 0=horizon, 1=bottom
            if (t < 0.005f) t = 0.005f;

            // Arena half-width: narrow at horizon, wide at bottom
            float halfW = centerX * (0.22f + 0.78f * MathF.Pow(t, 0.70f));

            // World-space depth for tile row pattern
            float worldZ = 1.0f / t;
            int tileRow = (int)(worldZ * 0.10f);

            // Detect row boundary for horizontal grid lines
            bool hGridLine = false;
            if (prevWorldZ >= 0)
            {
                int prevRow = (int)(prevWorldZ * 0.10f);
                if (tileRow != prevRow) hGridLine = true;
            }
            prevWorldZ = worldZ;

            // Atmospheric haze (fades near horizon)
            float atm = 0.50f + 0.50f * t;

            // Fill tile columns
            for (int col = 0; col < numCols; col++)
            {
                float u0 = (float)col / numCols * 2f - 1f;
                float u1 = (float)(col + 1) / numCols * 2f - 1f;
                int x0 = (int)(centerX + u0 * halfW + offset.X);
                int x1 = (int)(centerX + u1 * halfW + offset.X);

                bool dark = ((tileRow + col) % 2) == 0;
                var color = (dark ? tileB : tileA) * atm;
                _draw.FillRect(sb, new Rectangle(x0, (int)(y + offset.Y), x1 - x0, step), color);
            }

            // Horizontal grid line at tile row boundaries
            if (hGridLine)
            {
                int lx = (int)(centerX - halfW + offset.X);
                int w = (int)(halfW * 2);
                _draw.FillRect(sb, new Rectangle(lx, (int)(y + offset.Y), w, 1), gridColor * (atm * 0.35f));
            }

            // Vertical grid lines
            for (int col = 1; col < numCols; col++)
            {
                float u = (float)col / numCols * 2f - 1f;
                int gx = (int)(centerX + u * halfW + offset.X);
                _draw.FillRect(sb, new Rectangle(gx, (int)(y + offset.Y), 1, step), gridColor * (atm * 0.20f));
            }
        }

        // Arena border edges (converging sides)
        for (int y = horizonY; y < sceneHeight; y++)
        {
            float t = (float)(y - horizonY) / groundH;
            if (t < 0.005f) t = 0.005f;
            float halfW = centerX * (0.22f + 0.78f * MathF.Pow(t, 0.70f));
            int lx = (int)(centerX - halfW + offset.X);
            int rx = (int)(centerX + halfW + offset.X);
            _draw.FillRect(sb, new Rectangle(lx - 1, (int)(y + offset.Y), 2, 1), edgeColor);
            _draw.FillRect(sb, new Rectangle(rx - 1, (int)(y + offset.Y), 2, 1), edgeColor);
        }

        // Top edge (narrow, at horizon)
        float topHalfW = centerX * 0.22f;
        _draw.FillRect(sb, new Rectangle((int)(centerX - topHalfW + offset.X),
            (int)(horizonY + offset.Y), (int)(topHalfW * 2), 2), edgeColor);

        // Bottom edge
        _draw.FillRect(sb, new Rectangle((int)offset.X, (int)(sceneHeight - 2 + offset.Y),
            sceneWidth, 2), edgeColor);

        // Center line (halfway between left and right — the robattle arena divider)
        for (int y = horizonY; y < sceneHeight; y += 4)
        {
            float t = (float)(y - horizonY) / groundH;
            if (t < 0.005f) continue;
            float atm = 0.50f + 0.50f * t;
            _draw.FillRect(sb, new Rectangle((int)(centerX + offset.X) - 1, (int)(y + offset.Y), 2, 2),
                Color.White * (0.18f * atm));
        }

        // Arena pylons at four corners
        DrawArenaPylon(sb, centerX - topHalfW, horizonY, 0.4f, offset);
        DrawArenaPylon(sb, centerX + topHalfW, horizonY, 0.4f, offset);
        DrawArenaPylon(sb, 0, sceneHeight, 1.0f, offset);
        DrawArenaPylon(sb, sceneWidth, sceneHeight, 1.0f, offset);
    }

    /// <summary>Draw a small pylon/marker at an arena corner.</summary>
    private void DrawArenaPylon(SpriteBatch sb, float x, float y, float scale, Vector2 offset)
    {
        int pw = (int)(6 * scale);
        int ph = (int)(14 * scale);
        var pylonColor = new Color(0xC0, 0xC0, 0xD0);
        var pylonTop = new Color(0xFF, 0x60, 0x30);
        _draw.FillRect(sb, new Rectangle((int)(x - pw / 2 + offset.X),
            (int)(y - ph + offset.Y), pw, ph), pylonColor);
        _draw.FillRect(sb, new Rectangle((int)(x - pw / 2 + offset.X),
            (int)(y - ph + offset.Y), pw, (int)(4 * scale)), pylonTop);
    }

    /// <summary>
    /// Draw a visible bullet/beam traveling from attacker to target.
    /// Bright core + cyan trail, plus tiny spark particles.
    /// </summary>
    private void DrawProjectile(SpriteBatch sb, Vector2 offset)
    {
        if (_projectileFrom == null || _projectileTo == null) return;

        float fromX = BotCenterX(_projectileFrom);
        float fromY = BotCenterY(_projectileFrom);
        float toX = BotCenterX(_projectileTo);
        float toY = BotCenterY(_projectileTo);

        float t = Math.Clamp(_projectileProgress, 0, 1);

        // Bullet position
        float bx = fromX + (toX - fromX) * t;
        float by = fromY + (toY - fromY) * t;

        // Main bullet (bright core)
        _draw.FillRect(sb, new Rectangle((int)(bx - 3 + offset.X), (int)(by - 2 + offset.Y), 6, 4), Color.White);
        _draw.FillRect(sb, new Rectangle((int)(bx - 4 + offset.X), (int)(by - 1 + offset.Y), 8, 2), Color.Cyan);

        // Trail (fading behind)
        for (int i = 1; i <= 4; i++)
        {
            float tt = Math.Clamp(t - i * 0.03f, 0, 1);
            float tx = fromX + (toX - fromX) * tt;
            float ty = fromY + (toY - fromY) * tt;
            float alpha = 0.6f - i * 0.12f;
            _draw.FillRect(sb, new Rectangle((int)(tx - 2 + offset.X), (int)(ty - 1 + offset.Y), 4, 2),
                Color.Cyan * alpha);
        }

        // Trail sparks
        if (_rng.NextSingle() < 0.4f)
        {
            _particles.Add(new Particle
            {
                X = bx + _rng.Next(-3, 3), Y = by + _rng.Next(-3, 3),
                VX = (_rng.NextSingle() - 0.5f) * 40,
                VY = (_rng.NextSingle() - 0.5f) * 40,
                Size = 2, Color = Color.White * 0.7f,
                Life = 0.08f, MaxLife = 0.08f, Gravity = 0
            });
        }
    }

    /// <summary>Draw an elliptical shadow beneath a bot.</summary>
    private void DrawBotShadow(SpriteBatch sb, Vector2 footPos, float depthScale, Vector2 offset)
    {
        int shadowW = (int)(36 * depthScale);
        int shadowH = Math.Max(2, (int)(8 * depthScale));
        for (int row = -shadowH / 2; row <= shadowH / 2; row++)
        {
            float rowT = (float)Math.Abs(row) / (shadowH / 2f + 0.001f);
            int rowW = (int)(shadowW * MathF.Sqrt(Math.Max(0, 1 - rowT * rowT)));
            if (rowW <= 0) continue;
            _draw.FillRect(sb, new Rectangle(
                (int)(footPos.X - rowW / 2 + offset.X),
                (int)(footPos.Y + row + offset.Y),
                rowW, 1), Color.Black * (0.25f * depthScale));
        }
    }

    /// <summary>
    /// Returns a scale factor (0.55 – 1.0) based on screen Y depth.
    /// Objects near the horizon (small Y) are smaller, objects near bottom are full-size.
    /// </summary>
    private static float GetDepthScale(float screenY, int horizonY, int sceneHeight)
    {
        int groundH = sceneHeight - horizonY;
        if (groundH <= 0) return 1f;
        float t = Math.Clamp((screenY - horizonY) / groundH, 0, 1);
        return 0.55f + 0.45f * t;
    }

    /// <summary>
    /// Get the foot position for a squad member in the 2.5D left-right layout.
    /// Players occupy the LEFT half of the arena, enemies the RIGHT half.
    /// Bots are staggered vertically (in depth) for a 3/4 view.
    /// Index 0 = front row (lower, bigger), higher index = further back (higher, smaller).
    /// </summary>
    private static Vector2 GetSquadPosition(int sceneWidth, int sceneHeight, bool isPlayer, int index, int total)
    {
        int horizonY = (int)(sceneHeight * HorizonRatio);
        int groundH = sceneHeight - horizonY;
        float centerX = sceneWidth / 2f;

        // Vertical depth row: t=0 is bottom (near camera), t=1 is horizon (far)
        // Bots sit in rows at different depths. All bots between 25-65% depth.
        // index 0 = front (t ~ 0.35), index 1 = mid (t ~ 0.50), index 2 = back (t ~ 0.65)
        float baseT;
        if (total <= 1) baseT = 0.45f;
        else if (total == 2) baseT = 0.35f + index * 0.20f;
        else baseT = 0.30f + index * 0.17f; // 0.30, 0.47, 0.64

        // Screen Y from depth t
        float screenY = sceneHeight - baseT * groundH;

        // Arena half-width at this depth (same formula as ground rendering)
        float depthT = (screenY - horizonY) / (float)groundH;
        if (depthT < 0.01f) depthT = 0.01f;
        float halfW = centerX * (0.22f + 0.78f * MathF.Pow(depthT, 0.70f));

        // X position: players in LEFT arena half, enemies in RIGHT arena half
        // Spread within their half of the arena
        float halfArenaX;
        if (isPlayer)
        {
            // Players at ~25% of arena width (left quarter)
            halfArenaX = centerX - halfW * 0.50f;
        }
        else
        {
            // Enemies at ~75% of arena width (right quarter)
            halfArenaX = centerX + halfW * 0.50f;
        }

        return new Vector2(halfArenaX, screenY);
    }

    /// <summary>Helper to find a bot's foot position from squad refs.</summary>
    private Vector2 GetSquadPositionForBot(Medabot bot, int sceneWidth, int sceneHeight)
    {
        if (_playerSquadRef != null)
        {
            int idx = _playerSquadRef.IndexOf(bot);
            if (idx >= 0)
                return GetSquadPosition(sceneWidth, sceneHeight, true, idx, _playerSquadRef.Count);
        }
        if (_enemySquadRef != null)
        {
            int idx = _enemySquadRef.IndexOf(bot);
            if (idx >= 0)
                return GetSquadPosition(sceneWidth, sceneHeight, false, idx, _enemySquadRef.Count);
        }
        return new Vector2(sceneWidth / 2f, sceneHeight * 0.7f);
    }

    private Texture2D? GetOrCreateSquadTexture(Medabot bot)
    {
        string key = $"{bot.Name}_{bot.TotalArmor:F0}_{bot.IsKnockedOut}";
        if (_squadTexCache.TryGetValue(key, out var cached))
            return cached;

        var tex = SpriteGenerator.CreateTexture(_gd, bot);
        _squadTexCache[key] = tex;
        return tex;
    }

    /// <summary>Force refresh of sprite textures (e.g. after part damage).</summary>
    public void InvalidateSprites()
    {
        _lastPlayer = null;
        _lastEnemy = null;
        // Clear squad cache
        foreach (var tex in _squadTexCache.Values) tex.Dispose();
        _squadTexCache.Clear();
        _squadTexKeys.Clear();
    }

    private class Particle
    {
        public float X, Y, VX, VY, Size, Life, MaxLife, Gravity;
        public Color Color;
    }

    private class DamagePopup
    {
        public float X, Y, Life, MaxLife;
        public string Text = "";
        public Color Color;
    }

    private class HitFlash
    {
        public Medabot Bot = null!;
        public float Life, MaxLife;
    }
}
