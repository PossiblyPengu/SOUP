using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SAP.Core;
using SAP.Core.Models;

namespace SAP.Windows;

public partial class DoomGame : Window
{
    #region Win32 API for Mouse
    // Minimal fix: add missing field for build
    bool _victory = false;
    [DllImport("user32.dll")]
    static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    struct POINT { public int X, Y; }
    #endregion

    #region Constants
    const int W = 400, H = 300, TEX = 64;
    const int MAP_SIZE = 24;
    const double PI = Math.PI, PI2 = PI * 2;
    #endregion

    #region Textures
    static readonly uint[][] Textures = new uint[8][];
    static readonly uint[] FloorTex = new uint[TEX * TEX];
    static readonly uint[] CeilTex = new uint[TEX * TEX];
    #endregion

    #region Silly Quotes
    static readonly string[] KillQuotes = {
        "Boop!",
        "You've been spreadsheet'd!",
        "Ctrl+Alt+Defeated!",
        "Error 404: Enemy not found!",
        "Have a nice day!",
        "That tickled!",
        "Woopsie daisy!",
        "Back to the recycle bin!",
        "Your data has been corrupted!",
        "Task failed successfully!"
    };

    static readonly string[] LevelCompleteQuotes = {
        "Spreadsheet complete!",
        "All formulas balanced!",
        "Time for a coffee break!",
        "That was actually kinda fun!",
        "Another productive day at the office!"
    };

    static readonly string[] HurtQuotes = {
        "Ow! My spreadsheets!",
        "That's not in my budget!",
        "I need a vacation..."
    };

    static readonly string[] KillStreakQuotes = {
        "Combo meal!",
        "Hat trick!",
        "You're on fire! (figuratively)",
        "Somebody stop them!",
        "IS THIS EVEN LEGAL?!",
        "MOM GET THE CAMERA!",
        "ABSOLUTE LEGEND!"
    };
    #endregion

    #region Game State
    WriteableBitmap _bmp, _minimapBmp;
    uint[] _px;
    byte[] _minimapPx;
    double[] _zBuffer;
    DispatcherTimer? _timer;

    // Per-tile floor heights for verticality
    int[] _floorHeights = new int[MAP_SIZE * MAP_SIZE];
    
    // Hazard tiles (lava/acid)
    bool[] _hazardTiles = new bool[MAP_SIZE * MAP_SIZE];

    // Player - Duke style with vertical look, jumping, crouching
    double _px_pos, _py, _pa;          // Position and horizontal angle
    double _pitch = 0;                  // Vertical look angle
    double _pz = 0;                     // Vertical position (for jumping/crouching)
    double _pzVel = 0;                  // Vertical velocity
#pragma warning disable CS0414 // _isJumping is assigned in code but not read; keep for future logic
    bool _isJumping = false;
#pragma warning restore CS0414
    bool _isCrouching = false;
    double _eyeHeight = 0;              // Eye height offset
    int _hp = 100, _armor = 0, _score = 0;
    bool[] _keys = new bool[3];         // R, B, Y keys
    int _currentWeapon = 1;
    int[] _ammo = { 999, 48, 12, 200, 10, 5, 8 }; // Boot, Pistol, Shotgun, Ripper, RPG, Pipebomb, MiniRocket
    int _currentLevel = 1; // Now used as 'stage' or 'depth' in endless mode
    int _kills = 0, _totalEnemies = 0, _secretsFound = 0, _totalSecrets = 0;
    
    // Kill streak tracking
    int _killStreak = 0;
    double _killStreakTimer = 0;
    const double KILL_STREAK_WINDOW = 3.0; // seconds to chain kills

    // Inventory items
    int _medkits = 0, _steroids = 0;
    double _jetpackFuel = 100;
    bool _hasJetpack = false;
    bool _jetpackActive = false;
    double _steroidsTimer = 0;
    
    // Power-ups
    double _invincibilityTimer = 0;
    double _damageBoostTimer = 0;
    double _damageBoostMultiplier = 2.0;
    
    // Visual effects
    double _screenShakeTimer = 0;
    double _screenShakeIntensity = 0;
    bool _isAiming = false; // Right-click aim down sights
    double _aimZoom = 1.0;
    double _damageVignetteTimer = 0; // Red vignette when hurt
    double _weaponBob = 0; // Weapon sway
#pragma warning disable CS0414 // Assigned but not used - reserved for future head bob feature
    double _headBob = 0; // Head bob when walking
#pragma warning restore CS0414
    double _muzzleFlashTimer = 0; // Extended muzzle flash glow
    
    // Particles for blood/explosion effects
    List<(double x, double y, double dx, double dy, double life, uint color)> _particles = new();
    
    // Wall decals (blood splatters, bullet holes) - reserved for future feature
#pragma warning disable CS0414
    List<(int mapX, int mapY, int side, double wallPos, uint color, double size)> _wallDecals = new();
#pragma warning restore CS0414

    // Weapons - Duke style
    readonly string[] _weaponNames = { "MIGHTY BOOT", "PISTOL", "SHOTGUN", "RIPPER", "RPG", "PIPE BOMB", "MINI ROCKET" };

    // Sound effect helper
    void PlaySound(string filename)
    {
        try
        {
            var soundPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", filename);
            if (System.IO.File.Exists(soundPath))
            {
                var player = new System.Media.SoundPlayer(soundPath);
                player.Play();
            }
        }
        catch (Exception)
        {
            // Sound playback is non-critical, silently ignore
        }
    }

    readonly int[] _weaponDamage = { 25, 12, 50, 8, 120, 150, 80 };
    readonly double[] _weaponFireRate = { 0.5, 0.25, 0.9, 0.08, 1.2, 0.3, 0.9 };
    readonly double[] _weaponSpread = { 0, 0.02, 0.12, 0.06, 0, 0, 0 };
    bool _shooting = false;
    int _shootFrame = 0;
    double _lastShot = 0;
    List<(double x, double y, double timer)> _pipeBombs = new();

    // Entities
    List<Enemy> _enemies = new();
    List<Pickup> _pickups = new();
    List<Projectile> _projectiles = new();
    List<Door> _doors = new();

    // Input
    HashSet<Key> _keysDown = new();
    bool _mouseCaptured = false;
    double _mouseSensitivity = 0.2;
    int _centerX, _centerY;

    // Game flow
    bool _gameOver = false, _levelComplete = false, _paused = false;
    double _messageTimer = 0;
    string _message = "";
    double _quoteTimer = 0;
    Random _rnd = new();
    double _gameTime = 0;
    HighScoreService? _highScoreService;
    SettingsService? _settingsService;
    GameSettings _settings = new();
    double _hitMarkerTimer = 0;

    // Map
    int[] _map = new int[MAP_SIZE * MAP_SIZE];
    #endregion

    #region Entity Classes
    enum EnemyType { Trooper, PigCop, Enforcer, Octabrain, BattleLord }

    // Body part for ragdoll physics
    enum BodyPartType { Head, Torso, ArmL, ArmR, LegL, LegR }
    
    class BodyPart
    {
        public double X, Y, Z;           // 3D position (Z is height)
        public double Vx, Vy, Vz;        // Velocity
        public double Rotation;          // Spin rotation
        public double RotationSpeed;     // Angular velocity
        public double Size;              // Scale
        public BodyPartType Type;
        public uint Color;
        public double Life;              // Time before disappearing
        public double Bounce;            // Bounciness
    }

    class Enemy
    {
        public double X, Y, Hp, MaxHp, Speed, AttackRange, AttackDamage, AttackCooldown;
        public double Timer, HurtTimer, DeathTimer;
        public bool Dead, Alerted;
        public EnemyType Type;
        public int ScoreValue;
        
        // Ragdoll physics while alive (wobble/sway)
        public double WobblePhase;
        public double WobbleSpeed;
        public double LeanAngle;         // Leaning when moving
        public double BobPhase;          // Bounce when walking
    }

    enum PickupType { Health, Armor, Ammo, Shotgun, Ripper, RPG, KeyRed, KeyBlue, KeyYellow, Medkit, Jetpack, Steroids, AtomicHealth, Exit, Invincibility, DamageBoost }

    class Pickup
    {
        public double X, Y, BobOffset;
        public PickupType Type;
        public bool Collected;
    }

    class Projectile
    {
        public double X, Y, Dx, Dy, Damage;
        public bool FromPlayer, Dead;
        public bool IsRocket;
    }

    class Door
    {
        public int X, Y, KeyRequired;
        public double OpenAmount;
        // Initialize flags explicitly to silence CS0649 (field never assigned)
        public bool Opening = false;
        public bool Closing = false;
    }
    
    // Global list of flying body parts (ragdolls)
    List<BodyPart> _bodyParts = new();
    #endregion

    public DoomGame()
    {
        InitializeComponent();
        GenerateTextures();
        _bmp = new WriteableBitmap(W, H, 96, 96, PixelFormats.Bgra32, null);
        _px = new uint[W * H];
        _zBuffer = new double[W];
        _minimapBmp = new WriteableBitmap(120, 120, 96, 96, PixelFormats.Bgra32, null);
        _minimapPx = new byte[120 * 120 * 4];
        Screen.Source = _bmp;
        Minimap.Source = _minimapBmp;
        LoadLevel(_currentLevel);
    }

    #region Texture Generation
    void GenerateTextures()
    {
        for (int t = 0; t < 8; t++)
        {
            Textures[t] = new uint[TEX * TEX];
            uint baseR, baseG, baseB;

            // Duke Nukem style textures - more colorful
            switch (t)
            {
                case 0: baseR = 100; baseG = 90; baseB = 80; break;     // Gray concrete
                case 1: baseR = 140; baseG = 70; baseB = 50; break;     // Red brick
                case 2: baseR = 50; baseG = 80; baseB = 130; break;     // Blue tech
                case 3: baseR = 130; baseG = 120; baseB = 40; break;    // Yellow/gold
                case 4: baseR = 80; baseG = 130; baseB = 80; break;     // Green slime
                case 5: baseR = 110; baseG = 110; baseB = 120; break;   // Metal
                case 6: baseR = 90; baseG = 60; baseB = 40; break;      // Wood
                default: baseR = 40; baseG = 40; baseB = 50; break;     // Dark door
            }

            for (int y = 0; y < TEX; y++)
            {
                for (int x = 0; x < TEX; x++)
                {
                    int noise = _rnd.Next(-20, 20);
                    bool brick = (y % 16 < 1) || (x % 16 < 1 && (y / 16) % 2 == 0) || ((x + 8) % 16 < 1 && (y / 16) % 2 == 1);
                    bool panel = t == 2 && ((x % 32 < 2 || x % 32 > 29) || (y % 32 < 2 || y % 32 > 29));

                    uint r = (uint)Math.Clamp(baseR + noise - (brick ? 35 : 0) + (panel ? 40 : 0), 0, 255);
                    uint g = (uint)Math.Clamp(baseG + noise - (brick ? 35 : 0) + (panel ? 40 : 0), 0, 255);
                    uint b = (uint)Math.Clamp(baseB + noise - (brick ? 35 : 0) + (panel ? 60 : 0), 0, 255);

                    Textures[t][y * TEX + x] = 0xFF000000 | (r << 16) | (g << 8) | b;
                }
            }
        }

        // Floor - industrial tiles
        for (int y = 0; y < TEX; y++)
        {
            for (int x = 0; x < TEX; x++)
            {
                int noise = _rnd.Next(-12, 12);
                bool edge = x % 32 < 2 || y % 32 < 2;
                bool rivet = (x % 16 == 4 || x % 16 == 12) && (y % 16 == 4 || y % 16 == 12);
                uint v = (uint)Math.Clamp(55 + noise - (edge ? 25 : 0) + (rivet ? 30 : 0), 0, 255);
                FloorTex[y * TEX + x] = 0xFF000000 | (v << 16) | ((uint)(v * 0.95) << 8) | (uint)(v * 0.85);
            }
        }

        // Ceiling - tech panels with lights
        for (int y = 0; y < TEX; y++)
        {
            for (int x = 0; x < TEX; x++)
            {
                int noise = _rnd.Next(-10, 10);
                bool panel = (x % 16 > 2 && x % 16 < 14) && (y % 16 > 2 && y % 16 < 14);
                bool light = (x > 28 && x < 36) && (y > 28 && y < 36);
                uint v = (uint)Math.Clamp(35 + noise + (panel ? 20 : 0), 0, 255);
                if (light) { CeilTex[y * TEX + x] = 0xFFFFFFAA; }
                else CeilTex[y * TEX + x] = 0xFF000000 | (v << 16) | (v << 8) | ((uint)(v * 1.3));
            }
        }
    }

    void SaveScoreButton_Click(object? s, RoutedEventArgs e)
    {
        try
        {
            var name = (NameInput?.Text ?? "").Trim();
            if (string.IsNullOrEmpty(name)) name = Environment.UserName.ToUpperInvariant();
            _highScoreService?.AddScore(_score, name);
            UpdateTopScoresUI();
            SaveScoreButton.IsEnabled = false;
            ShowMessage("Score saved!");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save score: {ex.Message}");
            ShowMessage("Failed to save score");
        }
    }

    void TogglePause()
    {
        _paused = !_paused;
        PauseMenu.Visibility = _paused ? Visibility.Visible : Visibility.Collapsed;
        if (_paused)
        {
            CaptureMouse(false);
        }
        else
        {
            CaptureMouse(true);
        }
    }

    void PauseSensitivitySlider_ValueChanged(object? sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _mouseSensitivity = e.NewValue;
        PauseSensitivityValue.Text = _mouseSensitivity.ToString("F2");
        _settings.MouseSensitivity = _mouseSensitivity;
        _settingsService?.Save(_settings);
    }
    #endregion

    #region Level Loading
    void LoadLevel(int level)
    {
        _enemies.Clear();
        _pickups.Clear();
        _projectiles.Clear();
        _doors.Clear();
        _pipeBombs.Clear();
        _particles.Clear();
        _bodyParts.Clear();

        for (int i = 0; i < MAP_SIZE * MAP_SIZE; i++) { _map[i] = 0; _floorHeights[i] = 0; _hazardTiles[i] = false; }
        
        // Reset kill streak
        _killStreak = 0;
        _killStreakTimer = 0;

        GenerateRandomLevel();

        _totalEnemies = _enemies.Count;
        _totalSecrets = _pickups.Count(p => p.Type == PickupType.AtomicHealth);
        _kills = 0;
        _secretsFound = 0;
        _pitch = 0;
        _pz = 0;
        _pzVel = 0;

        Title = $"S.A.P NUKEM - Stage {_currentLevel}";
        SayQuote("It's time to kick ass and chew bubblegum...");
        // --- Random Level Generator with verticality ---
        void GenerateRandomLevel()
        {
            // Outer walls
            for (int i = 0; i < MAP_SIZE; i++)
            {
                _map[i] = 1;
                _map[(MAP_SIZE - 1) * MAP_SIZE + i] = 1;
                _map[i * MAP_SIZE] = 1;
                _map[i * MAP_SIZE + MAP_SIZE - 1] = 1;
            }

            // Fill with random rooms and corridors
            for (int y = 1; y < MAP_SIZE - 1; y++)
            {
                for (int x = 1; x < MAP_SIZE - 1; x++)
                {
                    // Randomly place walls (sparse)
                    if (_rnd.NextDouble() < 0.13)
                        _map[y * MAP_SIZE + x] = _rnd.Next(1, 6); // random wall type
                    else
                        _map[y * MAP_SIZE + x] = 0;

                    // Random floor height (verticality)
                    if (_map[y * MAP_SIZE + x] == 0)
                    {
                        // Make some areas higher/lower
                        if (_rnd.NextDouble() < 0.10)
                            _floorHeights[y * MAP_SIZE + x] = _rnd.Next(-16, 33); // -16 to +32 px
                        else
                            _floorHeights[y * MAP_SIZE + x] = 0;
                        
                        // Hazard tiles (lava/acid) - more common in later levels
                        double hazardChance = 0.02 + (_currentLevel * 0.005);
                        if (_rnd.NextDouble() < hazardChance)
                            _hazardTiles[y * MAP_SIZE + x] = true;
                    }
                    else
                    {
                        _floorHeights[y * MAP_SIZE + x] = 0;
                    }
                }
            }

            // Place player at a random open spot (not on hazard)
            while (true)
            {
                int px = _rnd.Next(2, MAP_SIZE - 2), py = _rnd.Next(2, MAP_SIZE - 2);
                if (_map[py * MAP_SIZE + px] == 0 && !_hazardTiles[py * MAP_SIZE + px])
                {
                    _px_pos = px + 0.5;
                    _py = py + 0.5;
                    _pa = _rnd.NextDouble() * PI2;
                    break;
                }
            }

            // Place exit
            while (true)
            {
                int ex = _rnd.Next(2, MAP_SIZE - 2), ey = _rnd.Next(2, MAP_SIZE - 2);
                if (_map[ey * MAP_SIZE + ex] == 0 && Math.Abs(ex - (int)_px_pos) + Math.Abs(ey - (int)_py) > 10)
                {
                    AddPickup(PickupType.Exit, ex, ey);
                    break;
                }
            }

            // Place enemies - more in later levels
            int enemyCount = 12 + _rnd.Next(8) + (_currentLevel * 2);
            for (int i = 0; i < enemyCount; i++)
            {
                int ex = _rnd.Next(2, MAP_SIZE - 2), ey = _rnd.Next(2, MAP_SIZE - 2);
                if (_map[ey * MAP_SIZE + ex] == 0 && !_hazardTiles[ey * MAP_SIZE + ex])
                    AddEnemy((EnemyType)_rnd.Next(0, 5), ex, ey);
            }

            // Place pickups
            for (int i = 0; i < 10 + _rnd.Next(8); i++)
            {
                int px = _rnd.Next(2, MAP_SIZE - 2), py = _rnd.Next(2, MAP_SIZE - 2);
                if (_map[py * MAP_SIZE + px] == 0 && !_hazardTiles[py * MAP_SIZE + px])
                    AddPickup((PickupType)_rnd.Next(0, 13), px, py);
            }
            
            // Rare power-up spawns (invincibility and damage boost)
            if (_rnd.NextDouble() < 0.3 + (_currentLevel * 0.05)) // More likely in later levels
            {
                int px = _rnd.Next(2, MAP_SIZE - 2), py = _rnd.Next(2, MAP_SIZE - 2);
                if (_map[py * MAP_SIZE + px] == 0 && !_hazardTiles[py * MAP_SIZE + px])
                    AddPickup(PickupType.Invincibility, px, py);
            }
            if (_rnd.NextDouble() < 0.35 + (_currentLevel * 0.05))
            {
                int px = _rnd.Next(2, MAP_SIZE - 2), py = _rnd.Next(2, MAP_SIZE - 2);
                if (_map[py * MAP_SIZE + px] == 0 && !_hazardTiles[py * MAP_SIZE + px])
                    AddPickup(PickupType.DamageBoost, px, py);
            }

            // Place a few keys
            for (int k = 0; k < 3; k++)
            {
                int kx = _rnd.Next(2, MAP_SIZE - 2), ky = _rnd.Next(2, MAP_SIZE - 2);
                if (_map[ky * MAP_SIZE + kx] == 0)
                    AddPickup((PickupType)(9 + k), kx, ky);
            }

            // Place a few doors
            for (int d = 0; d < 4; d++)
            {
                int dx = _rnd.Next(2, MAP_SIZE - 2), dy = _rnd.Next(2, MAP_SIZE - 2);
                if (_map[dy * MAP_SIZE + dx] == 0)
                    AddDoor(dx, dy, _rnd.Next(1, 4));
            }
        }
    }

    void GenerateLevel1()
    {
        _px_pos = 3; _py = 3; _pa = 0;

        // L.A. Meltdown style
        SetWalls(5, 2, 5, 10, 0);
        SetWalls(2, 5, 10, 5, 1);
        SetWalls(10, 2, 10, 8, 2);
        SetWalls(12, 5, 20, 5, 0);
        SetWalls(15, 8, 15, 15, 2);
        SetWalls(8, 12, 14, 12, 1);
        SetWalls(18, 8, 18, 18, 5);
        SetWalls(10, 18, 18, 18, 0);

        AddEnemy(EnemyType.Trooper, 8, 4);
        AddEnemy(EnemyType.Trooper, 12, 8);
        AddEnemy(EnemyType.PigCop, 16, 12);
        AddEnemy(EnemyType.Trooper, 20, 15);

        AddPickup(PickupType.Health, 6, 3);
        AddPickup(PickupType.Ammo, 9, 6);
        AddPickup(PickupType.Shotgun, 14, 10);
        AddPickup(PickupType.Armor, 17, 7);
        AddPickup(PickupType.Medkit, 4, 8);
        AddPickup(PickupType.KeyRed, 20, 20);
        AddPickup(PickupType.Exit, 21, 3);

        AddDoor(20, 3, 1);
    }

    void GenerateLevel2()
    {
        _px_pos = 2; _py = 2; _pa = PI / 4;

        // Lunar Apocalypse style
        SetWalls(4, 2, 4, 12, 2);
        SetWalls(7, 5, 7, 15, 5);
        SetWalls(2, 8, 6, 8, 2);
        SetWalls(10, 2, 10, 10, 5);
        SetWalls(13, 5, 13, 18, 2);
        SetWalls(8, 12, 12, 12, 5);
        SetWalls(16, 2, 16, 12, 2);
        SetWalls(19, 8, 19, 20, 5);
        SetWalls(10, 16, 18, 16, 2);
        SetWalls(2, 18, 8, 18, 5);

        AddEnemy(EnemyType.Trooper, 5, 4);
        AddEnemy(EnemyType.PigCop, 8, 10);
        AddEnemy(EnemyType.Enforcer, 11, 6);
        AddEnemy(EnemyType.PigCop, 15, 14);
        AddEnemy(EnemyType.Trooper, 18, 10);
        AddEnemy(EnemyType.Octabrain, 20, 18);

        AddPickup(PickupType.Health, 3, 6);
        AddPickup(PickupType.Ripper, 6, 12);
        AddPickup(PickupType.Ammo, 12, 4);
        AddPickup(PickupType.Armor, 14, 8);
        AddPickup(PickupType.Jetpack, 3, 3);
        AddPickup(PickupType.KeyBlue, 8, 20);
        AddPickup(PickupType.AtomicHealth, 20, 4);
        AddPickup(PickupType.Exit, 21, 20);

        AddDoor(20, 20, 2);
    }

    void GenerateLevel3()
    {
        _px_pos = 2; _py = 12; _pa = 0;

        // Shrapnel City style
        SetWalls(5, 2, 5, 22, 4);
        SetWalls(10, 2, 10, 10, 1);
        SetWalls(10, 14, 10, 22, 4);
        SetWalls(15, 5, 15, 19, 1);
        SetWalls(20, 2, 20, 22, 4);
        SetWalls(2, 7, 8, 7, 1);
        SetWalls(2, 17, 8, 17, 4);
        SetWalls(12, 10, 18, 10, 1);

        AddEnemy(EnemyType.PigCop, 4, 4);
        AddEnemy(EnemyType.Enforcer, 4, 20);
        AddEnemy(EnemyType.Trooper, 8, 12);
        AddEnemy(EnemyType.Octabrain, 12, 6);
        AddEnemy(EnemyType.PigCop, 12, 18);
        AddEnemy(EnemyType.Octabrain, 17, 12);
        AddEnemy(EnemyType.Enforcer, 18, 6);
        AddEnemy(EnemyType.Enforcer, 18, 18);

        AddPickup(PickupType.RPG, 7, 4);
        AddPickup(PickupType.Health, 7, 20);
        AddPickup(PickupType.Armor, 13, 12);
        AddPickup(PickupType.Ammo, 17, 4);
        AddPickup(PickupType.Steroids, 3, 10);
        AddPickup(PickupType.KeyYellow, 17, 20);
        AddPickup(PickupType.AtomicHealth, 3, 12);
        AddPickup(PickupType.Exit, 21, 12);

        AddDoor(21, 12, 3);
    }

    void GenerateBossLevel()
    {
        _px_pos = 12; _py = 20; _pa = -PI / 2;

        // Boss arena
        SetWalls(4, 4, 4, 20, 5);
        SetWalls(20, 4, 20, 20, 5);
        SetWalls(4, 4, 20, 4, 5);
        SetWalls(8, 8, 8, 16, 5);
        SetWalls(16, 8, 16, 16, 5);

        AddEnemy(EnemyType.BattleLord, 12, 6);
        AddEnemy(EnemyType.Octabrain, 6, 12);
        AddEnemy(EnemyType.Octabrain, 18, 12);
        AddEnemy(EnemyType.Enforcer, 10, 10);
        AddEnemy(EnemyType.Enforcer, 14, 10);

        AddPickup(PickupType.Health, 6, 18);
        AddPickup(PickupType.Health, 18, 18);
        AddPickup(PickupType.Ammo, 12, 16);
        AddPickup(PickupType.Armor, 10, 18);
        AddPickup(PickupType.Armor, 14, 18);
        AddPickup(PickupType.Medkit, 12, 18);
        AddPickup(PickupType.Exit, 12, 2);

        SayQuote("Die, you son of a bitch!");
    }

    void SetWalls(int x1, int y1, int x2, int y2, int type)
    {
        for (int y = Math.Min(y1, y2); y <= Math.Max(y1, y2); y++)
            for (int x = Math.Min(x1, x2); x <= Math.Max(x1, x2); x++)
                if (x < MAP_SIZE && y < MAP_SIZE)
                    _map[y * MAP_SIZE + x] = type + 1;
    }

    void AddEnemy(EnemyType type, double x, double y)
    {
        // Only place enemy if not in wall
        int ix = (int)x, iy = (int)y;
        if (_map[iy * MAP_SIZE + ix] != 0) return;
        var e = new Enemy { 
            X = x + 0.5, Y = y + 0.5, Type = type,
            WobblePhase = _rnd.NextDouble() * PI2,
            WobbleSpeed = 2.0 + _rnd.NextDouble() * 2.0
        };
        switch (type)
        {
            case EnemyType.Trooper:
                e.Hp = e.MaxHp = 40; e.Speed = 0.035; e.AttackDamage = 8; e.AttackRange = 10; e.AttackCooldown = 1.2; e.ScoreValue = 100;
                break;
            case EnemyType.PigCop:
                e.Hp = e.MaxHp = 100; e.Speed = 0.03; e.AttackDamage = 20; e.AttackRange = 8; e.AttackCooldown = 1.0; e.ScoreValue = 200;
                break;
            case EnemyType.Enforcer:
                e.Hp = e.MaxHp = 150; e.Speed = 0.025; e.AttackDamage = 15; e.AttackRange = 12; e.AttackCooldown = 0.8; e.ScoreValue = 300;
                break;
            case EnemyType.Octabrain:
                e.Hp = e.MaxHp = 175; e.Speed = 0.02; e.AttackDamage = 25; e.AttackRange = 15; e.AttackCooldown = 2.0; e.ScoreValue = 400;
                break;
            case EnemyType.BattleLord:
                e.Hp = e.MaxHp = 800; e.Speed = 0.015; e.AttackDamage = 40; e.AttackRange = 18; e.AttackCooldown = 0.6; e.ScoreValue = 2000;
                break;
        }
        _enemies.Add(e);
    }

    void AddPickup(PickupType type, double x, double y)
    {
        _pickups.Add(new Pickup { X = x + 0.5, Y = y + 0.5, Type = type, BobOffset = _rnd.NextDouble() * PI2 });
    }

    void AddDoor(int x, int y, int keyRequired)
    {
        _map[y * MAP_SIZE + x] = 8;
        _doors.Add(new Door { X = x, Y = y, KeyRequired = keyRequired });
    }
    #endregion

    #region Game Loop
    void OnLoaded(object s, RoutedEventArgs e)
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += GameLoop;
        _timer.Start();
        Focus();
        CaptureMouse(true);
        // Initialize high score service
        try { _highScoreService = new HighScoreService(); }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to init high scores: {ex.Message}");
            _highScoreService = null;
        }
        try
        {
            _settingsService = new SettingsService();
            _settings = _settingsService.Load();
            _mouseSensitivity = _settings.MouseSensitivity;
            // wire pause menu controls
            PauseSensitivitySlider.Value = _mouseSensitivity;
            PauseSensitivityValue.Text = _mouseSensitivity.ToString("F2");
            PauseSensitivitySlider.ValueChanged += PauseSensitivitySlider_ValueChanged;
            ResumeButton.Click += (ss, ee) => TogglePause();
            QuitButton.Click += (ss, ee) => Close();
            // wire save score button (if present)
            if (SaveScoreButton != null) SaveScoreButton.Click += SaveScoreButton_Click;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to init settings: {ex.Message}");
            _settingsService = null;
        }
    }

    void CaptureMouse(bool capture)
    {
        _mouseCaptured = capture;
        if (capture)
        {
            Cursor = Cursors.None;
            Mouse.Capture(this);
            CenterMouse();
        }
        else
        {
            Cursor = Cursors.Arrow;
            Mouse.Capture(null);
        }
    }

    void CenterMouse()
    {
        var screenPos = PointToScreen(new Point(ActualWidth / 2, ActualHeight / 2));
        _centerX = (int)screenPos.X;
        _centerY = (int)screenPos.Y;
        SetCursorPos(_centerX, _centerY);
    }

    void OnClosing(object s, System.ComponentModel.CancelEventArgs e) => _timer?.Stop();

    void GameLoop(object? s, EventArgs e)
    {
        if (_paused || _gameOver || _levelComplete || _victory) return;

        double dt = 0.016;
        _gameTime += dt;

        ProcessMouse();
        UpdatePlayer(dt);
        UpdateEnemies(dt);
        UpdateProjectiles(dt);
        UpdateDoors(dt);
        UpdatePickups();
        UpdatePipeBombs(dt);
        UpdateParticles(dt);
        UpdateHazards(dt);
        UpdateBodyParts(dt);

        Render();
        RenderMinimap();
        UpdateHUD();

        if (_messageTimer > 0) _messageTimer -= dt;
        else MessageText.Text = "";

        if (_quoteTimer > 0) _quoteTimer -= dt;
        else DukeQuote.Text = "";

        // update hit marker
        if (_hitMarkerTimer > 0)
        {
            _hitMarkerTimer -= dt;
            double t = Math.Max(0, _hitMarkerTimer / 0.25);
            HitMarkerEllipse.Opacity = t;
        }
        else
        {
            HitMarkerEllipse.Opacity = 0;
        }

        if (_steroidsTimer > 0) _steroidsTimer -= dt;
        if (_invincibilityTimer > 0) _invincibilityTimer -= dt;
        if (_damageBoostTimer > 0) _damageBoostTimer -= dt;
        if (_screenShakeTimer > 0) _screenShakeTimer -= dt;
        if (_damageVignetteTimer > 0) _damageVignetteTimer -= dt;
        if (_muzzleFlashTimer > 0) _muzzleFlashTimer -= dt;
        
        // Kill streak decay
        if (_killStreakTimer > 0)
        {
            _killStreakTimer -= dt;
            if (_killStreakTimer <= 0) _killStreak = 0;
        }
        
        // Aim zoom interpolation
        double targetZoom = _isAiming ? 1.5 : 1.0;
        _aimZoom += (targetZoom - _aimZoom) * 0.15;
    }
    
    void UpdateBodyParts(double dt)
    {
        const double gravity = 0.8;
        const double friction = 0.98;
        
        for (int i = _bodyParts.Count - 1; i >= 0; i--)
        {
            var part = _bodyParts[i];
            
            // Update lifetime
            part.Life -= dt;
            if (part.Life <= 0)
            {
                _bodyParts.RemoveAt(i);
                continue;
            }
            
            // Apply gravity
            part.Vz -= gravity * dt;
            
            // Update position
            part.X += part.Vx;
            part.Y += part.Vy;
            part.Z += part.Vz;
            
            // Update rotation (spinning!)
            part.Rotation += part.RotationSpeed * dt;
            
            // Bounce off floor
            if (part.Z <= 0)
            {
                part.Z = 0;
                part.Vz = -part.Vz * part.Bounce;
                part.Vx *= friction;
                part.Vy *= friction;
                part.RotationSpeed *= 0.8; // Slow down spin on bounce
                
                // Spawn confetti on bounce!
                if (Math.Abs(part.Vz) > 0.05)
                {
                    uint[] confettiColors = { 0xFFFF69B4u, 0xFF00FF00u, 0xFFFFD700u, 0xFF00FFFFu, 0xFFFF6347u, 0xFF9370DBu };
                    for (int c = 0; c < 3; c++)
                    {
                        double vx = (_rnd.NextDouble() - 0.5) * 0.08;
                        double vy = (_rnd.NextDouble() - 0.5) * 0.08;
                        uint color = confettiColors[_rnd.Next(confettiColors.Length)];
                        _particles.Add((part.X, part.Y, vx, vy, 0.5, color));
                    }
                }
                
                // Stop bouncing if velocity is tiny
                if (Math.Abs(part.Vz) < 0.02) part.Vz = 0;
            }
            
            // Wall collision
            int mapX = (int)part.X, mapY = (int)part.Y;
            if (mapX >= 0 && mapX < MAP_SIZE && mapY >= 0 && mapY < MAP_SIZE)
            {
                if (_map[mapY * MAP_SIZE + mapX] > 0)
                {
                    // Bounce off wall
                    part.X -= part.Vx * 2;
                    part.Y -= part.Vy * 2;
                    part.Vx = -part.Vx * 0.5;
                    part.Vy = -part.Vy * 0.5;
                }
            }
        }
    }

    void ProcessMouse()
    {
        if (!_mouseCaptured) return;

        GetCursorPos(out POINT p);
        int dx = p.X - _centerX;
        int dy = p.Y - _centerY;

        if (dx != 0 || dy != 0)
        {
            _pa += dx * _mouseSensitivity * 0.01;
            _pitch = Math.Clamp(_pitch - dy * _mouseSensitivity * 0.5, -60, 60);
            SetCursorPos(_centerX, _centerY);
        }
    }
    #endregion

    #region Player Update
    void UpdatePlayer(double dt)
    {
        double ms = _steroidsTimer > 0 ? 0.12 : 0.08;
        double rs = 0.06;

        double dx = 0, dy = 0;
        if (_keysDown.Contains(Key.W)) { dx += Math.Cos(_pa); dy += Math.Sin(_pa); }
        if (_keysDown.Contains(Key.S)) { dx -= Math.Cos(_pa); dy -= Math.Sin(_pa); }
        if (_keysDown.Contains(Key.A)) { dx += Math.Cos(_pa - PI / 2); dy += Math.Sin(_pa - PI / 2); }
        if (_keysDown.Contains(Key.D)) { dx += Math.Cos(_pa + PI / 2); dy += Math.Sin(_pa + PI / 2); }

        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len > 0) { dx /= len; dy /= len; }

        if (_keysDown.Contains(Key.LeftShift)) ms *= 1.4;

        double newX = _px_pos + dx * ms;
        double newY = _py + dy * ms;

        if (!IsWall(newX, _py)) _px_pos = newX;
        if (!IsWall(_px_pos, newY)) _py = newY;

        if (_keysDown.Contains(Key.Left)) _pa -= rs;
        if (_keysDown.Contains(Key.Right)) _pa += rs;

        // Jumping
        if (_pz > 0 || _pzVel != 0)
        {
            _pzVel -= 0.4;
            _pz += _pzVel;
            if (_pz <= 0) { _pz = 0; _pzVel = 0; _isJumping = false; }
        }

        // Jetpack
        if (_jetpackActive && _jetpackFuel > 0)
        {
            _pzVel = 0.3;
            _pz = Math.Min(_pz + 0.3, 30);
            _jetpackFuel -= 0.3;
            JetpackBar.Visibility = Visibility.Visible;
            JetpackFuel.Width = _jetpackFuel;
            if (_jetpackFuel <= 0) { _jetpackActive = false; SayQuote("Damn, out of fuel!"); }
        }
        else
        {
            JetpackBar.Visibility = _hasJetpack ? Visibility.Visible : Visibility.Collapsed;
            JetpackFuel.Width = _jetpackFuel;
        }

        // Crouching
        _eyeHeight = _isCrouching ? -15 : 0;

        // Head bob
        if (len > 0 && _pz == 0) _eyeHeight += Math.Sin(_gameTime * 12) * 3;

        // Shooting animation
        if (_shooting)
        {
            _shootFrame++;
            if (_shootFrame > 5) { _shooting = false; _shootFrame = 0; }
        }
    }

    bool IsWall(double x, double y)
    {
        int mx = (int)x, my = (int)y;
        if (mx < 0 || mx >= MAP_SIZE || my < 0 || my >= MAP_SIZE) return true;
        int tile = _map[my * MAP_SIZE + mx];
        if (tile == 0) return false;

        var door = _doors.FirstOrDefault(d => d.X == mx && d.Y == my);
        if (door != null && door.OpenAmount > 0.8) return false;

        return true;
    }
    #endregion

    #region Enemy Update
    void UpdateEnemies(double dt)
    {
        foreach (var en in _enemies)
        {
            if (en.Dead) { en.DeathTimer += dt; continue; }
            if (en.HurtTimer > 0) en.HurtTimer -= dt;
            
            // Always update wobble phase for idle animation
            en.WobblePhase += en.WobbleSpeed * dt;
            en.BobPhase += en.WobbleSpeed * 0.7 * dt;

            double dx = _px_pos - en.X, dy = _py - en.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);

            if (dist < 12) en.Alerted = true;
            if (!en.Alerted) continue;

            double prevX = en.X, prevY = en.Y;
            if (dist > en.AttackRange * 0.4 && dist < 16)
            {
                double nx = en.X + dx / dist * en.Speed;
                double ny = en.Y + dy / dist * en.Speed;
                if (!IsWall(nx, en.Y)) en.X = nx;
                if (!IsWall(en.X, ny)) en.Y = ny;
            }
            
            // Calculate lean angle based on movement direction
            double moveX = en.X - prevX;
            en.LeanAngle = en.LeanAngle * 0.9 + (moveX * 10) * 0.1; // Smooth lerp

            en.Timer += dt;
            if (en.Timer > en.AttackCooldown && dist < en.AttackRange)
            {
                en.Timer = 0;
                _projectiles.Add(new Projectile
                {
                    X = en.X, Y = en.Y,
                    Dx = dx / dist * 0.18,
                    Dy = dy / dist * 0.18,
                    Damage = en.AttackDamage,
                    FromPlayer = false
                });
            }
        }
    }

    void TakeDamage(int damage)
    {
        // Invincibility check
        if (_invincibilityTimer > 0) return;
        
        if (_armor > 0)
        {
            int absorbed = Math.Min(_armor, damage * 2 / 3);
            _armor -= absorbed;
            damage -= absorbed / 2;
        }
        _hp -= damage;
        
        // Visual feedback
        TriggerScreenShake(0.15, 4);
        _damageVignetteTimer = 0.5; // Red vignette flash
        
        PlaySound("duke_hurt.wav");
        if (_rnd.NextDouble() < 0.3)
            SayQuote(HurtQuotes[_rnd.Next(HurtQuotes.Length)]);

            if (_hp <= 0)
            {
                _hp = 0;
                _gameOver = true;
                PlaySound("duke_gameover.wav");
                GameOverScreen.Visibility = Visibility.Visible;
                FinalScoreText.Text = $"Final Score: {_score}";
                // populate name input with default username
                NameInput.Text = Environment.UserName.ToUpperInvariant();
                // show top scores too
                UpdateTopScoresUI();
                CaptureMouse(false);
            }
        else
        {
            // show hit marker when player takes damage
            _hitMarkerTimer = 0.25;
            HitMarkerEllipse.Opacity = 1.0;
        }
    }
    #endregion

    #region Projectile & Pipe Bomb Update
    void UpdateProjectiles(double dt)
    {
        foreach (var p in _projectiles)
        {
            if (p.Dead) continue;

            p.X += p.Dx;
            p.Y += p.Dy;

            if (IsWall(p.X, p.Y))
            {
                if (p.IsRocket && p.FromPlayer) ExplosionAt(p.X, p.Y, (int)p.Damage);
                p.Dead = true;
                continue;
            }

            if (p.FromPlayer)
            {
                foreach (var en in _enemies)
                {
                    if (en.Dead) continue;
                    double d = Math.Sqrt((p.X - en.X) * (p.X - en.X) + (p.Y - en.Y) * (p.Y - en.Y));
                    if (d < 0.5)
                    {
                        if (p.IsRocket) ExplosionAt(p.X, p.Y, (int)p.Damage);
                        else
                        {
                            en.Hp -= p.Damage;
                            en.HurtTimer = 0.15;
                            en.Alerted = true;
                            if (en.Hp <= 0) KillEnemy(en);
                            // show hit marker on enemy hit
                            _hitMarkerTimer = 0.12;
                            HitMarkerEllipse.Opacity = 1.0;
                        }
                        p.Dead = true;
                        break;
                    }
                }
            }
            else
            {
                double d = Math.Sqrt((p.X - _px_pos) * (p.X - _px_pos) + (p.Y - _py) * (p.Y - _py));
                if (d < 0.35)
                {
                    TakeDamage((int)p.Damage);
                    p.Dead = true;
                }
            }
        }
        _projectiles.RemoveAll(p => p.Dead);
    }

    void UpdatePipeBombs(double dt)
    {
        for (int i = _pipeBombs.Count - 1; i >= 0; i--)
        {
            var bomb = _pipeBombs[i];
            _pipeBombs[i] = (bomb.x, bomb.y, bomb.timer + dt);
        }
    }

    void DetonatePipeBombs()
    {
        foreach (var bomb in _pipeBombs)
            ExplosionAt(bomb.x, bomb.y, 150);
        _pipeBombs.Clear();
        if (_pipeBombs.Count > 0) SayQuote("Party popper activated!");
    }

    void ExplosionAt(double x, double y, int damage)
    {
        // Screen shake on explosions (bigger shake for bigger damage)
        double shakeIntensity = Math.Min(15, 5 + damage / 20.0);
        TriggerScreenShake(0.5, shakeIntensity);
        
        // PARTY EXPLOSION! 🎆 Fireworks instead of violence
        SpawnConfetti(x, y, 25);
        SpawnParticles(x, y, 15, 0xFFFFFF00u, 0xFFFFFFFFu); // Sparkles
        SpawnParticles(x, y, 10, 0xFFFF69B4u, 0xFF00FF00u); // Pink & green
        
        foreach (var en in _enemies)
        {
            if (en.Dead) continue;
            double d = Math.Sqrt((x - en.X) * (x - en.X) + (y - en.Y) * (y - en.Y));
            if (d < 3)
            {
                int dmg = (int)(damage * (1 - d / 3));
                if (_damageBoostTimer > 0) dmg = (int)(dmg * _damageBoostMultiplier);
                en.Hp -= dmg;
                en.HurtTimer = 0.2;
                en.Alerted = true;
                if (en.Hp <= 0) KillEnemy(en, true); // Explosion death = extra ragdoll force
            }
        }

        double pd = Math.Sqrt((x - _px_pos) * (x - _px_pos) + (y - _py) * (y - _py));
        if (pd < 3) TakeDamage((int)(damage * 0.3 * (1 - pd / 3)));
    }

    void KillEnemy(Enemy en, bool explosive = false)
    {
        en.Dead = true;
        
        // RAGDOLL TIME! 🎉 Spawn body parts flying everywhere
        SpawnRagdoll(en, explosive);
        
        // Spawn confetti particles
        SpawnConfetti(en.X, en.Y, 15);
        
        // Kill streak system
        _killStreak++;
        _killStreakTimer = KILL_STREAK_WINDOW;
        
        // Bonus points for kill streaks
        int streakBonus = 0;
        if (_killStreak >= 2)
        {
            streakBonus = _killStreak * 50;
            if (_killStreak <= KillStreakQuotes.Length)
            {
                SayQuote(KillStreakQuotes[Math.Min(_killStreak - 2, KillStreakQuotes.Length - 1)]);
            }
        }
        
        _score += en.ScoreValue + streakBonus;
        _kills++;
        PlaySound("duke_kill.wav");
        
        if (_killStreak < 2 && _rnd.NextDouble() < 0.4)
            SayQuote(KillQuotes[_rnd.Next(KillQuotes.Length)]);
    }
    
    void SpawnRagdoll(Enemy en, bool explosive)
    {
        // Get silly cartoon colors for this enemy type (like stuffed toys!)
        uint baseColor = en.Type switch
        {
            EnemyType.Trooper => 0xFF66FF66u,   // Bright green plush
            EnemyType.PigCop => 0xFF6699FFu,    // Bright blue plush
            EnemyType.Enforcer => 0xFFBBBBBBu,  // Silver plush
            EnemyType.Octabrain => 0xFFFF88FFu, // Pink plush
            EnemyType.BattleLord => 0xFFFF6666u,// Bright red plush
            _ => 0xFFFFAAAAu
        };
        
        double force = explosive ? 0.25 : 0.12;
        double upForce = explosive ? 0.35 : 0.18;
        
        // Direction from player (for non-explosive deaths, parts fly away from player)
        double dx = en.X - _px_pos;
        double dy = en.Y - _py;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist > 0.01) { dx /= dist; dy /= dist; }
        
        // Spawn different body parts with silly colors
        var partTypes = new[] { BodyPartType.Head, BodyPartType.Torso, BodyPartType.ArmL, BodyPartType.ArmR, BodyPartType.LegL, BodyPartType.LegR };
        var partSizes = new[] { 0.18, 0.28, 0.12, 0.12, 0.14, 0.14 }; // Slightly bigger for visibility
        
        // Rainbow-ish cartoon colors!
        uint[] funColors = { 0xFFFFB6C1u, 0xFFFFA07Au, 0xFFFFD700u, 0xFF98FB98u, 0xFF87CEEBu, 0xFFDDA0DDu };
        
        for (int i = 0; i < partTypes.Length; i++)
        {
            double angle = _rnd.NextDouble() * PI2;
            double speed = force * (0.5 + _rnd.NextDouble());
            
            // Explosive deaths: random directions. Normal deaths: away from player
            double vx, vy;
            if (explosive)
            {
                vx = Math.Cos(angle) * speed;
                vy = Math.Sin(angle) * speed;
            }
            else
            {
                vx = dx * speed + (_rnd.NextDouble() - 0.5) * force;
                vy = dy * speed + (_rnd.NextDouble() - 0.5) * force;
            }
            
            // Mix base color with fun pastel colors
            uint partColor = i == 1 ? baseColor : funColors[i]; // Torso keeps enemy color
            
            _bodyParts.Add(new BodyPart
            {
                X = en.X,
                Y = en.Y,
                Z = 0.6 - i * 0.1, // Start at different heights (head high, legs low)
                Vx = vx,
                Vy = vy,
                Vz = upForce * (0.6 + _rnd.NextDouble()), // Upward velocity
                Rotation = _rnd.NextDouble() * PI2,
                RotationSpeed = (_rnd.NextDouble() - 0.5) * 20, // More spinning!
                Size = partSizes[i],
                Type = partTypes[i],
                Color = partColor,
                Life = 4.0 + _rnd.NextDouble() * 2.0, // Disappear after a few seconds
                Bounce = 0.5 + _rnd.NextDouble() * 0.3 // Extra bouncy!
            });
        }
        
        // Spawn extra confetti on death!
        SpawnConfetti(en.X, en.Y, explosive ? 25 : 12);
    }
    
    // Spawn colorful confetti particles
    void SpawnConfetti(double x, double y, int count)
    {
        uint[] confettiColors = { 
            0xFFFF69B4u, // Hot pink
            0xFF00FF00u, // Lime green
            0xFFFFFF00u, // Yellow
            0xFF00FFFFu, // Cyan
            0xFFFF00FFu, // Magenta
            0xFFFF8C00u, // Orange
            0xFF7B68EEu, // Purple
            0xFF00CED1u  // Turquoise
        };
        
        for (int i = 0; i < count; i++)
        {
            double angle = _rnd.NextDouble() * PI2;
            double speed = 0.05 + _rnd.NextDouble() * 0.15;
            uint color = confettiColors[_rnd.Next(confettiColors.Length)];
            _particles.Add((x, y, Math.Cos(angle) * speed, Math.Sin(angle) * speed, 1.5, color));
        }
    }
    
    void TriggerScreenShake(double duration, double intensity)
    {
        _screenShakeTimer = duration;
        _screenShakeIntensity = intensity;
    }
    
    void SpawnParticles(double x, double y, int count, uint color1, uint color2)
    {
        for (int i = 0; i < count; i++)
        {
            double angle = _rnd.NextDouble() * Math.PI * 2;
            double speed = 0.05 + _rnd.NextDouble() * 0.1;
            uint color = _rnd.NextDouble() > 0.5 ? color1 : color2;
            _particles.Add((x, y, Math.Cos(angle) * speed, Math.Sin(angle) * speed, 0.5 + _rnd.NextDouble() * 0.5, color));
        }
    }
    
    void UpdateParticles(double dt)
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.x += p.dx;
            p.y += p.dy;
            p.life -= dt;
            _particles[i] = p;
            if (p.life <= 0) _particles.RemoveAt(i);
        }
    }
    
    void UpdateHazards(double dt)
    {
        // Check if player is standing on a hazard tile
        int mx = (int)_px_pos, my = (int)_py;
        if (mx >= 0 && mx < MAP_SIZE && my >= 0 && my < MAP_SIZE)
        {
            if (_hazardTiles[my * MAP_SIZE + mx] && _pz == 0) // Only damage if on ground
            {
                // Sticky goo damage - it's gross AND hurts! 🟢
                if (_invincibilityTimer <= 0)
                {
                    TakeDamage(1); // Small constant damage
                }
            }
        }
    }
    #endregion

    #region Door Update
    void UpdateDoors(double dt)
    {
        foreach (var door in _doors)
        {
            if (door.Opening)
            {
                PlaySound("duke_door.wav");
                door.OpenAmount = Math.Min(1, door.OpenAmount + dt * 2.5);
                if (door.OpenAmount >= 1) door.Opening = false;
            }
        }
    }
    #endregion

    #region Pickup Update
    void UpdatePickups()
    {
        foreach (var p in _pickups)
        {
            if (p.Collected) continue;

            double d = Math.Sqrt((p.X - _px_pos) * (p.X - _px_pos) + (p.Y - _py) * (p.Y - _py));
            if (d < 0.6)
            {
                bool collected = true;
                switch (p.Type)
                {
                    case PickupType.Health:
                    case PickupType.Armor:
                    case PickupType.Ammo:
                    case PickupType.Shotgun:
                    case PickupType.Ripper:
                    case PickupType.RPG:
                    case PickupType.Medkit:
                    case PickupType.Jetpack:
                    case PickupType.Steroids:
                    case PickupType.KeyRed:
                    case PickupType.KeyBlue:
                    case PickupType.KeyYellow:
                        PlaySound("duke_pickup.wav");
                        break;
                    case PickupType.AtomicHealth:
                        PlaySound("duke_secret.wav");
                        break;
                    case PickupType.Exit:
                        // No sound, handled by CompleteLevel
                        break;
                }

                switch (p.Type)
                {
                    case PickupType.Health:
                        if (_hp < 100) { _hp = Math.Min(100, _hp + 30); ShowMessage("Health +30"); }
                        else collected = false;
                        break;
                    case PickupType.AtomicHealth:
                        _hp = Math.Min(200, _hp + 50);
                        _secretsFound++;
                        SayQuote("Groovy!");
                        ShowMessage("Atomic Health! Secret Found!");
                        break;
                    case PickupType.Armor:
                        if (_armor < 100) { _armor = Math.Min(100, _armor + 50); ShowMessage("Armor +50"); }
                        else collected = false;
                        break;
                    case PickupType.Ammo:
                        _ammo[1] += 12; _ammo[2] += 4; _ammo[3] += 25;
                        // Small chance to also find Mini Rocket ammo when picking up generic ammo
                        bool gotMini = false;
                        if (_ammo.Length > 6 && _rnd.NextDouble() < 0.35)
                        {
                            _ammo[6] += 2;
                            gotMini = true;
                        }
                        ShowMessage(gotMini ? "Ammo + Mini Rockets!" : "Ammo");
                        break;
                    case PickupType.Shotgun:
                        _currentWeapon = 2; _ammo[2] += 10;
                        SayQuote("Groovy!");
                        ShowMessage("Shotgun!");
                        break;
                    case PickupType.Ripper:
                        _currentWeapon = 3; _ammo[3] += 50;
                        SayQuote("Cool!");
                        ShowMessage("Ripper Chaingun!");
                        break;
                    case PickupType.RPG:
                        _currentWeapon = 4; _ammo[4] += 5;
                        SayQuote("I've got balls of steel!");
                        ShowMessage("RPG!");
                        break;
                    case PickupType.Medkit:
                        _medkits++;
                        ShowMessage("Portable Medkit");
                        break;
                    case PickupType.Jetpack:
                        _hasJetpack = true;
                        _jetpackFuel = 100;
                        SayQuote("Cool!");
                        ShowMessage("Jetpack!");
                        break;
                    case PickupType.Steroids:
                        _steroids++;
                        ShowMessage("Steroids");
                        break;
                    case PickupType.KeyRed:
                        _keys[0] = true;
                        ShowMessage("Red Access Card");
                        break;
                    case PickupType.KeyBlue:
                        _keys[1] = true;
                        ShowMessage("Blue Access Card");
                        break;
                    case PickupType.KeyYellow:
                        _keys[2] = true;
                        ShowMessage("Yellow Access Card");
                        break;
                    case PickupType.Invincibility:
                        _invincibilityTimer = 15.0; // 15 seconds of invincibility
                        SayQuote("I am invincible!");
                        ShowMessage("⭐ INVINCIBILITY! 15 seconds!");
                        break;
                    case PickupType.DamageBoost:
                        _damageBoostTimer = 20.0; // 20 seconds of double damage
                        SayQuote("Time to get medieval!");
                        ShowMessage("💀 DAMAGE BOOST! 2x damage for 20 seconds!");
                        break;
                    case PickupType.Exit:
                        CompleteLevel();
                        break;
                }
                if (collected) { p.Collected = true; _score += 10; }
            }
        }
    }
    #endregion

    #region Level Complete
    void CompleteLevel()
    {
        _levelComplete = true;
        PlaySound("duke_levelup.wav");
        LevelCompleteScreen.Visibility = Visibility.Visible;
        LevelScoreText.Text = $"Score: {_score} | Stage: {_currentLevel}";
        LevelStatsText.Text = $"Kills: {_kills}/{_totalEnemies}  Secrets: {_secretsFound}/{_totalSecrets}";
        LevelQuote.Text = $"\"{LevelCompleteQuotes[_rnd.Next(LevelCompleteQuotes.Length)]}\"";
        CaptureMouse(false);
        // Consider this as an end-of-run save as well
        try
        {
            _highScoreService?.AddScore(_score);
            UpdateTopScoresUI();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save level score: {ex.Message}");
        }
    }

    void NextLevel()
    {
        _currentLevel++;
        _levelComplete = false;
        LevelCompleteScreen.Visibility = Visibility.Collapsed;

        // Increase difficulty: more enemies, more pickups, etc.
        // This can be done by passing _currentLevel to LoadLevel or adjusting random generation
        LoadLevel(_currentLevel);
        CaptureMouse(true);
    }
    #endregion

    #region Rendering
    void Render()
    {
        for (int i = 0; i < W; i++) _zBuffer[i] = double.MaxValue;

        RenderSkyGradient();
        RenderFloorCeiling();
        RenderWalls();
        RenderSprites();
        RenderBodyParts();
        RenderParticles();
        RenderWeapon();
        
        // Apply damage vignette (red edges when hurt)
        if (_damageVignetteTimer > 0)
        {
            ApplyDamageVignette(_damageVignetteTimer / 0.5);
        }
        
        // Apply visual effects for power-ups
        if (_invincibilityTimer > 0)
        {
            // Cyan pulse when invincible
            double pulse = 0.5 + 0.5 * Math.Sin(_gameTime * 8);
            ApplyScreenTint((uint)(0x10 + pulse * 0x15) << 24 | 0x00FFFF);
        }
        if (_damageBoostTimer > 0)
        {
            // Red pulse when damage boosted
            double pulse = 0.5 + 0.5 * Math.Sin(_gameTime * 6);
            ApplyScreenTint((uint)(0x10 + pulse * 0x10) << 24 | 0xFF4400);
        }
        
        // Apply screen shake effect
        if (_screenShakeTimer > 0)
        {
            double shakeX = (_rnd.NextDouble() - 0.5) * _screenShakeIntensity * (_screenShakeTimer / 0.4);
            double shakeY = (_rnd.NextDouble() - 0.5) * _screenShakeIntensity * (_screenShakeTimer / 0.4);
            Screen.RenderTransform = new TranslateTransform(shakeX, shakeY);
        }
        else
        {
            Screen.RenderTransform = null;
        }

        _bmp.WritePixels(new Int32Rect(0, 0, W, H), _px, W * 4, 0);

        // Update crosshair position based on pitch and aim zoom
        double crosshairY = 278 - _pitch;
        Canvas.SetTop(CrosshairCanvas, crosshairY);
        
        // Scale crosshair when aiming
        if (_isAiming)
        {
            CrosshairCanvas.RenderTransform = new ScaleTransform(0.5, 0.5, 0, 0);
        }
        else
        {
            CrosshairCanvas.RenderTransform = null;
        }
    }
    
    void RenderSkyGradient()
    {
        int horizon = H / 2 + (int)_pitch + (int)_eyeHeight;
        
        // Render gradient sky above horizon
        for (int y = 0; y < Math.Min(horizon, H); y++)
        {
            double skyT = (double)y / Math.Max(1, horizon);
            // Dark blue at top to lighter blue/orange at horizon (sunset feel)
            uint r = (uint)(20 + skyT * 80);
            uint g = (uint)(30 + skyT * 60);
            uint b = (uint)(80 + skyT * 40);
            
            // Add some stars at the top
            for (int x = 0; x < W; x++)
            {
                uint color = 0xFF000000 | (r << 16) | (g << 8) | b;
                
                // Sparse stars near top
                if (y < horizon / 3)
                {
                    int starSeed = x * 7919 + y * 104729;
                    if ((starSeed % 997) < 3)
                    {
                        double twinkle = 0.5 + 0.5 * Math.Sin(_gameTime * 3 + starSeed);
                        color = 0xFF000000 | ((uint)(200 + twinkle * 55) << 16) | ((uint)(200 + twinkle * 55) << 8) | (uint)(200 + twinkle * 55);
                    }
                }
                
                _px[y * W + x] = color;
            }
        }
    }
    
    void ApplyDamageVignette(double intensity)
    {
        // Silly cartoon "ouch" effect - purple/pink vignette with stars!
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                // Calculate distance from edge
                double edgeX = Math.Min(x, W - 1 - x) / (double)(W / 2);
                double edgeY = Math.Min(y, H - 1 - y) / (double)(H / 2);
                double edge = Math.Min(edgeX, edgeY);
                
                // Vignette strength (stronger at edges)
                double vignette = 1.0 - Math.Pow(edge, 0.5);
                vignette *= intensity * 0.6;
                
                if (vignette > 0.01)
                {
                    uint c = _px[y * W + x];
                    uint cr = (c >> 16) & 0xFF;
                    uint cg = (c >> 8) & 0xFF;
                    uint cb = c & 0xFF;
                    
                    // Blend towards purple/pink (silly cartoon damage)
                    cr = (uint)Math.Min(255, cr + (200 - cr) * vignette);
                    cg = (uint)(cg * (1 - vignette * 0.5));
                    cb = (uint)Math.Min(255, cb + (255 - cb) * vignette);
                    
                    _px[y * W + x] = 0xFF000000 | (cr << 16) | (cg << 8) | cb;
                }
                
                // Add cartoon stars when hurt
                if (intensity > 0.3 && y < 60)
                {
                    int starSeed = x * 31 + y * 17 + (int)(_gameTime * 10);
                    if ((starSeed % 200) < 2)
                    {
                        double twinkle = 0.5 + 0.5 * Math.Sin(_gameTime * 15 + starSeed);
                        _px[y * W + x] = 0xFF000000 | ((uint)(255 * twinkle) << 16) | ((uint)(255 * twinkle) << 8) | (uint)(50 * twinkle);
                    }
                }
            }
        }
    }
    
    void ApplyScreenTint(uint tintColor)
    {
        uint tintA = (tintColor >> 24) & 0xFF;
        uint tintR = (tintColor >> 16) & 0xFF;
        uint tintG = (tintColor >> 8) & 0xFF;
        uint tintB = tintColor & 0xFF;
        double alpha = tintA / 255.0;
        
        for (int i = 0; i < _px.Length; i++)
        {
            uint c = _px[i];
            uint r = (uint)(((c >> 16) & 0xFF) * (1 - alpha) + tintR * alpha);
            uint g = (uint)(((c >> 8) & 0xFF) * (1 - alpha) + tintG * alpha);
            uint b = (uint)((c & 0xFF) * (1 - alpha) + tintB * alpha);
            _px[i] = 0xFF000000 | (r << 16) | (g << 8) | b;
        }
    }
    
    void RenderParticles()
    {
        int horizon = H / 2 + (int)_pitch + (int)_eyeHeight;
        double fovMultiplier = _isAiming ? 0.4 : 0.66;
        
        foreach (var p in _particles)
        {
            double dx = p.x - _px_pos, dy = p.y - _py;
            double invDet = 1.0 / (Math.Cos(_pa + PI / 2) * fovMultiplier * Math.Sin(_pa) - Math.Cos(_pa) * Math.Sin(_pa + PI / 2) * fovMultiplier);
            double transformX = invDet * (Math.Sin(_pa) * dx - Math.Cos(_pa) * dy);
            double transformY = invDet * (-Math.Sin(_pa + PI / 2) * fovMultiplier * dx + Math.Cos(_pa + PI / 2) * fovMultiplier * dy);
            
            if (transformY <= 0.1) continue;
            int screenXCheck = (int)Math.Clamp(W / 2 * (1 + transformX / transformY), 0, W - 1);
            if (transformY >= _zBuffer[screenXCheck]) continue;
            
            int screenX = (int)(W / 2 * (1 + transformX / transformY));
            int screenY = horizon - (int)(20 / transformY); // Lift particles up
            int size = Math.Max(2, (int)(6 / transformY));
            
            // Fade based on life with glow effect
            uint baseColor = p.color;
            double lifeRatio = p.life / 1.0;
            uint alpha = (uint)(255 * lifeRatio);
            
            // Add glow for bright particles
            bool isGlowing = ((baseColor >> 16) & 0xFF) > 200 || ((baseColor >> 8) & 0xFF) > 200;
            
            for (int py = Math.Max(0, screenY - size); py < Math.Min(H, screenY + size); py++)
            {
                for (int px = Math.Max(0, screenX - size); px < Math.Min(W, screenX + size); px++)
                {
                    double dist = Math.Sqrt((px - screenX) * (px - screenX) + (py - screenY) * (py - screenY));
                    if (dist < size)
                    {
                        double intensity = (1 - dist / size) * lifeRatio;
                        
                        if (isGlowing && intensity > 0.3)
                        {
                            // Additive blending for glowing particles
                            uint existing = _px[py * W + px];
                            uint er = (existing >> 16) & 0xFF;
                            uint eg = (existing >> 8) & 0xFF;
                            uint eb = existing & 0xFF;
                            
                            uint pr = (baseColor >> 16) & 0xFF;
                            uint pg = (baseColor >> 8) & 0xFF;
                            uint pb = baseColor & 0xFF;
                            
                            uint r = (uint)Math.Min(255, er + pr * intensity);
                            uint g = (uint)Math.Min(255, eg + pg * intensity);
                            uint b = (uint)Math.Min(255, eb + pb * intensity);
                            
                            _px[py * W + px] = 0xFF000000 | (r << 16) | (g << 8) | b;
                        }
                        else if (intensity > 0.5)
                        {
                            _px[py * W + px] = baseColor | 0xFF000000;
                        }
                    }
                }
            }
        }
    }

    void RenderFloorCeiling()
    {
        int horizon = H / 2 + (int)_pitch + (int)_eyeHeight;
        double fovAngle = _isAiming ? 0.35 : 0.5; // Narrower angle when aiming

        for (int y = 0; y < H; y++)
        {
            bool isFloor = y > horizon;
            double rowDist = Math.Abs((H / 2.0) / (y - horizon + 0.01));

            double floorStepX = rowDist * (Math.Cos(_pa + fovAngle) - Math.Cos(_pa - fovAngle)) / W;
            double floorStepY = rowDist * (Math.Sin(_pa + fovAngle) - Math.Sin(_pa - fovAngle)) / W;

            double floorX = _px_pos + rowDist * Math.Cos(_pa - fovAngle);
            double floorY = _py + rowDist * Math.Sin(_pa - fovAngle);

            for (int x = 0; x < W; x++)
            {
                int tx = (int)(floorX * TEX) & (TEX - 1);
                int ty = (int)(floorY * TEX) & (TEX - 1);

                uint color = isFloor ? FloorTex[ty * TEX + tx] : CeilTex[ty * TEX + tx];
                
                // Check for hazard tile (sticky goo effect! 🟢)
                if (isFloor)
                {
                    int mapX = (int)floorX, mapY = (int)floorY;
                    if (mapX >= 0 && mapX < MAP_SIZE && mapY >= 0 && mapY < MAP_SIZE)
                    {
                        if (_hazardTiles[mapY * MAP_SIZE + mapX])
                        {
                            // Animated green goo - slime color!
                            double glow = 0.5 + 0.5 * Math.Sin(_gameTime * 2 + floorX * 2 + floorY * 2);
                            double bubble = Math.Sin(_gameTime * 8 + floorX * 10) > 0.9 ? 1.0 : 0.0;
                            color = 0xFF000000 | 
                                ((uint)(30 + bubble * 50)) |        // Red (low)
                                ((uint)(180 + glow * 75) << 8) |    // Green (bright!)
                                ((uint)(50 + glow * 30) << 16);     // Blue (teal tint)
                        }
                    }
                }

                double fog = Math.Min(1, rowDist / 18);
                uint r = (uint)(((color >> 16) & 0xFF) * (1 - fog));
                uint g = (uint)(((color >> 8) & 0xFF) * (1 - fog));
                uint b = (uint)((color & 0xFF) * (1 - fog));

                _px[y * W + x] = 0xFF000000 | (r << 16) | (g << 8) | b;

                floorX += floorStepX;
                floorY += floorStepY;
            }
        }
    }

    void RenderWalls()
    {
        int horizon = H / 2 + (int)_pitch + (int)_eyeHeight;
        double fovMultiplier = _isAiming ? 0.4 : 0.66; // Narrower FOV when aiming (zoom effect)

        for (int x = 0; x < W; x++)
        {
            double camX = 2.0 * x / W - 1;
            double rayDirX = Math.Cos(_pa) + Math.Cos(_pa + PI / 2) * camX * fovMultiplier;
            double rayDirY = Math.Sin(_pa) + Math.Sin(_pa + PI / 2) * camX * fovMultiplier;

            int mapX = (int)_px_pos, mapY = (int)_py;
            double deltaDistX = Math.Abs(1 / rayDirX), deltaDistY = Math.Abs(1 / rayDirY);
            double sideDistX, sideDistY;
            int stepX, stepY, side = 0;

            if (rayDirX < 0) { stepX = -1; sideDistX = (_px_pos - mapX) * deltaDistX; }
            else { stepX = 1; sideDistX = (mapX + 1.0 - _px_pos) * deltaDistX; }
            if (rayDirY < 0) { stepY = -1; sideDistY = (_py - mapY) * deltaDistY; }
            else { stepY = 1; sideDistY = (mapY + 1.0 - _py) * deltaDistY; }

            int hit = 0;
            while (hit == 0)
            {
                if (sideDistX < sideDistY) { sideDistX += deltaDistX; mapX += stepX; side = 0; }
                else { sideDistY += deltaDistY; mapY += stepY; side = 1; }

                if (mapX < 0 || mapX >= MAP_SIZE || mapY < 0 || mapY >= MAP_SIZE) break;
                if (_map[mapY * MAP_SIZE + mapX] > 0)
                {
                    var door = _doors.FirstOrDefault(d => d.X == mapX && d.Y == mapY);
                    if (door == null || door.OpenAmount < 0.9)
                        hit = _map[mapY * MAP_SIZE + mapX];
                }
            }

            double perpWallDist = side == 0 ? sideDistX - deltaDistX : sideDistY - deltaDistY;
            _zBuffer[x] = perpWallDist;

            int lineHeight = (int)(H / perpWallDist);
            int drawStart = Math.Max(0, -lineHeight / 2 + horizon);
            int drawEnd = Math.Min(H - 1, lineHeight / 2 + horizon);

            double wallX = side == 0 ? _py + perpWallDist * rayDirY : _px_pos + perpWallDist * rayDirX;
            wallX -= Math.Floor(wallX);

            int texX = (int)(wallX * TEX);
            if ((side == 0 && rayDirX > 0) || (side == 1 && rayDirY < 0)) texX = TEX - texX - 1;

            int texId = Math.Clamp(hit - 1, 0, 7);
            double step = TEX / (double)lineHeight;
            double texPos = (drawStart - horizon + lineHeight / 2 + (int)(_pz / perpWallDist * 20)) * step;

            for (int y = drawStart; y <= drawEnd; y++)
            {
                int texY = Math.Clamp((int)texPos, 0, TEX - 1);
                texPos += step;

                uint color = Textures[texId][texY * TEX + texX];
                if (side == 1) color = ((color >> 1) & 0x7F7F7F) | 0xFF000000;

                double fog = Math.Min(0.85, perpWallDist / 22);
                uint r = (uint)(((color >> 16) & 0xFF) * (1 - fog));
                uint g = (uint)(((color >> 8) & 0xFF) * (1 - fog));
                uint b = (uint)((color & 0xFF) * (1 - fog));

                _px[y * W + x] = 0xFF000000 | (r << 16) | (g << 8) | b;
            }
        }
    }

    void RenderSprites()
    {
        var sprites = new List<(double dist, object obj, int type)>();

        foreach (var en in _enemies.Where(e => !e.Dead || e.DeathTimer < 1))
            sprites.Add((Dist(en.X, en.Y), en, 0));

        foreach (var p in _pickups.Where(p => !p.Collected))
            sprites.Add((Dist(p.X, p.Y), p, 1));

        foreach (var proj in _projectiles)
            sprites.Add((Dist(proj.X, proj.Y), proj, 2));

        foreach (var bomb in _pipeBombs)
            sprites.Add((Dist(bomb.x, bomb.y), bomb, 3));

        sprites.Sort((a, b) => b.dist.CompareTo(a.dist));

        int horizon = H / 2 + (int)_pitch + (int)_eyeHeight;

        foreach (var (dist, obj, type) in sprites)
        {
            double sx = 0, sy = 0;
            if (type == 0) { var e = (Enemy)obj; sx = e.X; sy = e.Y; }
            else if (type == 1) { var p = (Pickup)obj; sx = p.X; sy = p.Y; }
            else if (type == 2) { var p = (Projectile)obj; sx = p.X; sy = p.Y; }
            else { var b = ((double x, double y, double timer))obj; sx = b.x; sy = b.y; }

            double dx = sx - _px_pos, dy = sy - _py;
            double invDet = 1.0 / (Math.Cos(_pa + PI / 2) * 0.66 * Math.Sin(_pa) - Math.Cos(_pa) * Math.Sin(_pa + PI / 2) * 0.66);
            double transformX = invDet * (Math.Sin(_pa) * dx - Math.Cos(_pa) * dy);
            double transformY = invDet * (-Math.Sin(_pa + PI / 2) * 0.66 * dx + Math.Cos(_pa + PI / 2) * 0.66 * dy);

            if (transformY <= 0.1) continue;

            // --- Floor-level sprite fix ---
            // Project sprite to floor (not camera center):
            // Find the floor height at (sx, sy) in screen space
            // Assume floor is always at y = 0 (no slopes)
            // Calculate vertical offset from camera to floor
            double cameraZ = 32.0 + _pz + _eyeHeight; // camera height (player eye level)
            double spriteZ = 0.0; // floor level
            int floorOffset = (int)((cameraZ - spriteZ) / transformY);

            int spriteScreenX = (int)(W / 2 * (1 + transformX / transformY));
            int spriteHeight = (int)Math.Abs(H / transformY);
            int spriteWidth = spriteHeight;

            if (type == 1 || type == 3)
            {
                spriteHeight = spriteHeight * 2 / 3;
                spriteWidth = spriteHeight;
            }
            else if (type == 2)
            {
                spriteHeight /= 4;
                spriteWidth = spriteHeight;
            }

            int drawStartY = Math.Max(0, -spriteHeight / 2 + horizon + floorOffset);
            int drawEndY = Math.Min(H - 1, spriteHeight / 2 + horizon + floorOffset);
            int drawStartX = Math.Max(0, -spriteWidth / 2 + spriteScreenX);
            int drawEndX = Math.Min(W - 1, spriteWidth / 2 + spriteScreenX);


            // Minimal fix: declare vertOffset if missing
            double vertOffset = 0.0;
            for (int stripe = drawStartX; stripe < drawEndX; stripe++)
            {
                if (transformY >= _zBuffer[stripe]) continue;

                for (int y = drawStartY; y < drawEndY; y++)
                {
                    double relX = (double)(stripe - (spriteScreenX - spriteWidth / 2)) / spriteWidth - 0.5;
                    double relY = (double)(y - (horizon - vertOffset - spriteHeight / 2)) / spriteHeight;

                    uint color = 0;
                    if (type == 0) color = GetEnemyColor((Enemy)obj, relX, relY);
                    else if (type == 1) color = GetPickupColor((Pickup)obj, relX, relY);
                    else if (type == 2)
                    {
                        if (relX * relX + relY * relY < 0.2)
                            color = ((Projectile)obj).FromPlayer ? 0xFFFFAA00u : 0xFFFF4400u;
                    }
                    else
                    {
                        if (relX * relX + relY * relY < 0.15) color = 0xFF44AA44u;
                    }

                    if ((color & 0xFF000000) != 0)
                    {
                        double fog = Math.Min(0.75, transformY / 16);
                        uint r = (uint)(((color >> 16) & 0xFF) * (1 - fog));
                        uint g = (uint)(((color >> 8) & 0xFF) * (1 - fog));
                        uint b = (uint)((color & 0xFF) * (1 - fog));
                        _px[y * W + stripe] = 0xFF000000 | (r << 16) | (g << 8) | b;
                    }
                }
            }
        }
    }
    
    void RenderBodyParts()
    {
        // Sort body parts by distance (furthest first)
        var sortedParts = _bodyParts.OrderByDescending(p => (p.X - _px_pos) * (p.X - _px_pos) + (p.Y - _py) * (p.Y - _py)).ToList();
        int horizon = H / 2 + (int)_pitch + (int)_eyeHeight;
        
        foreach (var part in sortedParts)
        {
            double dx = part.X - _px_pos;
            double dy = part.Y - _py;
            
            // Camera transform
            double invDet = 1.0 / (Math.Cos(_pa + PI / 2) * 0.66 * Math.Sin(_pa) - Math.Cos(_pa) * Math.Sin(_pa + PI / 2) * 0.66);
            double transformX = invDet * (Math.Sin(_pa) * dx - Math.Cos(_pa) * dy);
            double transformY = invDet * (-Math.Sin(_pa + PI / 2) * 0.66 * dx + Math.Cos(_pa + PI / 2) * 0.66 * dy);
            
            if (transformY <= 0.1) continue;
            
            // Screen position
            int screenX = (int)(W / 2 * (1 + transformX / transformY));
            
            // Height offset for Z position (flying parts!)
            double cameraZ = 32.0 + _pz + _eyeHeight;
            double partZ = part.Z * 64; // Scale Z to world units
            int heightOffset = (int)((cameraZ - partZ) / transformY);
            
            // Size based on distance and part size
            int size = (int)(part.Size * H / transformY * 0.3);
            if (size < 2) size = 2;
            if (size > H / 2) size = H / 2;
            
            // Fade out as life decreases
            double fadeAlpha = Math.Min(1.0, part.Life / 0.5);
            
            int startX = screenX - size / 2;
            int startY = horizon + heightOffset - size / 2;
            
            // Render the body part as a spinning shape
            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    int scrX = startX + px;
                    int scrY = startY + py;
                    
                    if (scrX < 0 || scrX >= W || scrY < 0 || scrY >= H) continue;
                    if (transformY >= _zBuffer[scrX]) continue;
                    
                    // Normalized coordinates (-0.5 to 0.5)
                    double nx = (px - size / 2.0) / size;
                    double ny = (py - size / 2.0) / size;
                    
                    // Apply rotation
                    double rot = part.Rotation;
                    double rx = nx * Math.Cos(rot) - ny * Math.Sin(rot);
                    double ry = nx * Math.Sin(rot) + ny * Math.Cos(rot);
                    
                    // Different shapes for different parts
                    bool draw = false;
                    switch (part.Type)
                    {
                        case BodyPartType.Head:
                            // Circle
                            draw = rx * rx + ry * ry < 0.2;
                            break;
                        case BodyPartType.Torso:
                            // Rectangle
                            draw = Math.Abs(rx) < 0.35 && Math.Abs(ry) < 0.4;
                            break;
                        case BodyPartType.ArmL:
                        case BodyPartType.ArmR:
                            // Thin rectangle
                            draw = Math.Abs(rx) < 0.15 && Math.Abs(ry) < 0.4;
                            break;
                        case BodyPartType.LegL:
                        case BodyPartType.LegR:
                            // Leg shape
                            draw = Math.Abs(rx) < 0.2 && Math.Abs(ry) < 0.45;
                            break;
                    }
                    
                    if (draw)
                    {
                        // Apply fog and fade
                        double fog = Math.Min(0.75, transformY / 16);
                        double brightness = (1 - fog) * fadeAlpha;
                        
                        uint r = (uint)(((part.Color >> 16) & 0xFF) * brightness);
                        uint g = (uint)(((part.Color >> 8) & 0xFF) * brightness);
                        uint b = (uint)((part.Color & 0xFF) * brightness);
                        
                        _px[scrY * W + scrX] = 0xFF000000 | (r << 16) | (g << 8) | b;
                    }
                }
            }
        }
    }

    uint GetEnemyColor(Enemy en, double x, double y)
    {
        // Dead enemy - flat on ground
        if (en.Dead)
        {
            if (y > 0.4 && Math.Abs(x) < 0.35)
            {
                double shade = 0.3 + (y - 0.4) * 0.5;
                return 0xFF000000 | ((uint)(48 * shade) << 16) | ((uint)(32 * shade) << 8) | (uint)(16 * shade);
            }
            return 0;
        }

        // Apply wobble/sway animation for living enemies
        // More wobble at the top (head), less at bottom (feet)
        double wobbleAmount = Math.Sin(en.WobblePhase) * 0.08 * (1 - y);
        double bobAmount = Math.Sin(en.BobPhase) * 0.03;
        
        // Apply the wobble to x coordinate
        x -= wobbleAmount + en.LeanAngle * 0.1;
        // Apply bob to y coordinate (bouncing while moving)
        y -= bobAmount;

        bool hurt = en.HurtTimer > 0;

        // Each enemy type has unique appearance
        switch (en.Type)
        {
            case EnemyType.Trooper:
                // Green alien trooper with helmet
                {
                    uint bodyColor = hurt ? 0xFFFFAAAAu : 0xFF336633u;
                    uint helmetColor = 0xFF225522u;
                    uint eyeColor = 0xFFFF0000u;
                    uint visorColor = 0xFF88FF88u;

                    // Helmet/head (top)
                    if (y < 0.25)
                    {
                        double headDist = x * x + (y - 0.12) * (y - 0.12);
                        if (headDist < 0.025)
                        {
                            // Visor
                            if (y > 0.08 && y < 0.18 && Math.Abs(x) < 0.1)
                                return visorColor;
                            return helmetColor;
                        }
                        // Red eyes behind visor
                        if (y > 0.1 && y < 0.16 && (Math.Abs(x - 0.04) < 0.02 || Math.Abs(x + 0.04) < 0.02))
                            return eyeColor;
                    }
                    // Body
                    if (y >= 0.2 && y < 0.75 && Math.Abs(x) < 0.18 - (y - 0.2) * 0.15)
                        return bodyColor;
                    // Arms (holding weapon)
                    if (y > 0.25 && y < 0.5 && Math.Abs(x) > 0.12 && Math.Abs(x) < 0.3)
                        return bodyColor;
                    // Legs
                    if (y >= 0.7 && y < 0.95)
                    {
                        if (Math.Abs(x - 0.08) < 0.06 || Math.Abs(x + 0.08) < 0.06)
                            return bodyColor;
                    }
                    return 0;
                }

            case EnemyType.PigCop:
                // Blue uniformed pig cop
                {
                    uint bodyColor = hurt ? 0xFFFFAAAAu : 0xFF4444AAu;
                    uint skinColor = 0xFFFFBB99u;
                    uint badgeColor = 0xFFFFDD00u;

                    // Pig head (pink, round)
                    if (y < 0.28)
                    {
                        double headDist = x * x + (y - 0.14) * (y - 0.14);
                        if (headDist < 0.03)
                        {
                            // Snout
                            if (y > 0.15 && y < 0.22 && Math.Abs(x) < 0.05)
                                return 0xFFFFAAAAu;
                            // Eyes
                            if (y > 0.08 && y < 0.14 && (Math.Abs(x - 0.06) < 0.025 || Math.Abs(x + 0.06) < 0.025))
                                return 0xFF000000u;
                            return skinColor;
                        }
                    }
                    // Blue uniform body
                    if (y >= 0.25 && y < 0.8 && Math.Abs(x) < 0.2 - (y - 0.25) * 0.12)
                    {
                        // Badge on chest
                        if (y > 0.3 && y < 0.4 && x > 0.02 && x < 0.1)
                            return badgeColor;
                        return bodyColor;
                    }
                    // Arms
                    if (y > 0.3 && y < 0.55 && Math.Abs(x) > 0.15 && Math.Abs(x) < 0.32)
                        return bodyColor;
                    // Legs
                    if (y >= 0.75 && y < 0.98)
                    {
                        if (Math.Abs(x - 0.07) < 0.07 || Math.Abs(x + 0.07) < 0.07)
                            return 0xFF333388u;
                    }
                    return 0;
                }

            case EnemyType.Enforcer:
                // Gray armored enforcer
                {
                    uint armorColor = hurt ? 0xFFFFAAAAu : 0xFF666666u;
                    uint darkArmor = 0xFF444444u;
                    uint visorColor = 0xFFFF4400u;

                    // Helmet with visor
                    if (y < 0.26)
                    {
                        double headDist = x * x + (y - 0.13) * (y - 0.13);
                        if (headDist < 0.028)
                        {
                            // Orange visor slit
                            if (y > 0.1 && y < 0.16 && Math.Abs(x) < 0.12)
                                return visorColor;
                            return darkArmor;
                        }
                    }
                    // Bulky armored body
                    if (y >= 0.22 && y < 0.78 && Math.Abs(x) < 0.22 - (y - 0.22) * 0.1)
                    {
                        // Chest plate detail
                        if (y > 0.28 && y < 0.5 && Math.Abs(x) < 0.15)
                            return armorColor;
                        return darkArmor;
                    }
                    // Big arms
                    if (y > 0.28 && y < 0.6 && Math.Abs(x) > 0.15 && Math.Abs(x) < 0.35)
                        return armorColor;
                    // Legs
                    if (y >= 0.72 && y < 0.98)
                    {
                        if (Math.Abs(x - 0.1) < 0.08 || Math.Abs(x + 0.1) < 0.08)
                            return darkArmor;
                    }
                    return 0;
                }

            case EnemyType.Octabrain:
                // Purple floating brain with tentacles
                {
                    uint brainColor = hurt ? 0xFFFFAAAAu : 0xFFAA4488u;
                    uint eyeColor = 0xFF00FF00u;
                    uint tentacleColor = 0xFF884466u;

                    // Large brain dome
                    double brainY = 0.25;
                    double brainDist = x * x + (y - brainY) * (y - brainY);
                    if (brainDist < 0.08)
                    {
                        // Green glowing eyes
                        if (y > 0.28 && y < 0.38)
                        {
                            if ((Math.Abs(x - 0.08) < 0.04 || Math.Abs(x + 0.08) < 0.04))
                                return eyeColor;
                        }
                        // Brain wrinkles
                        if (y < 0.2 && ((int)((x + 0.3) * 20) % 3 == 0))
                            return 0xFF993377u;
                        return brainColor;
                    }
                    // Tentacles below (wavy)
                    if (y > 0.45 && y < 0.9)
                    {
                        double wave = Math.Sin(_gameTime * 5 + x * 10) * 0.03;
                        if (Math.Abs(x - 0.12 + wave) < 0.04 ||
                            Math.Abs(x + 0.12 + wave) < 0.04 ||
                            Math.Abs(x + wave) < 0.03)
                            return tentacleColor;
                    }
                    return 0;
                }

            case EnemyType.BattleLord:
                // Large brown boss with horns
                {
                    uint bodyColor = hurt ? 0xFFFFAAAAu : 0xFF888844u;
                    uint darkBody = 0xFF665533u;
                    uint hornColor = 0xFF444422u;
                    uint eyeColor = 0xFFFF0000u;

                    // Horns at top
                    if (y < 0.15)
                    {
                        if ((x > 0.1 && x < 0.25 && y < 0.1 - (x - 0.15) * 0.3) ||
                            (x < -0.1 && x > -0.25 && y < 0.1 - (-x - 0.15) * 0.3))
                            return hornColor;
                    }
                    // Large head
                    if (y < 0.3)
                    {
                        double headDist = x * x + (y - 0.18) * (y - 0.18);
                        if (headDist < 0.04)
                        {
                            // Red glowing eyes
                            if (y > 0.14 && y < 0.22)
                            {
                                if (Math.Abs(x - 0.08) < 0.035 || Math.Abs(x + 0.08) < 0.035)
                                    return eyeColor;
                            }
                            return bodyColor;
                        }
                    }
                    // Massive body
                    if (y >= 0.26 && y < 0.85 && Math.Abs(x) < 0.28 - (y - 0.26) * 0.08)
                    {
                        // Chest armor plates
                        if (Math.Abs(x) > 0.1 && y < 0.55)
                            return darkBody;
                        return bodyColor;
                    }
                    // Thick arms
                    if (y > 0.3 && y < 0.65 && Math.Abs(x) > 0.2 && Math.Abs(x) < 0.42)
                        return bodyColor;
                    // Legs
                    if (y >= 0.8 && y < 1.0)
                    {
                        if (Math.Abs(x - 0.12) < 0.1 || Math.Abs(x + 0.12) < 0.1)
                            return darkBody;
                    }
                    return 0;
                }

            default:
                return 0;
        }
    }

    uint GetPickupColor(Pickup p, double x, double y)
    {
        double dist = x * x + y * y;
        if (dist > 0.20) return 0;

        // Add bobbing animation visual feedback
        double bobPhase = Math.Sin(_gameTime * 4 + p.BobOffset) * 0.02;
        y -= bobPhase;

        switch (p.Type)
        {
            case PickupType.Health:
                // Green cross shape
                if ((Math.Abs(x) < 0.08 && Math.Abs(y) < 0.25) || (Math.Abs(y) < 0.08 && Math.Abs(x) < 0.25))
                    return 0xFF44FF44u;
                if (dist < 0.16) return 0xFF228822u;
                return 0;

            case PickupType.AtomicHealth:
                // Glowing cyan atom with pulsing
                double pulse = 0.8 + Math.Sin(_gameTime * 6) * 0.2;
                if (dist < 0.04) return 0xFFFFFFFFu;
                if (dist < 0.12)
                {
                    uint intensity = (uint)(255 * pulse);
                    return 0xFF000000 | (intensity << 8) | intensity;
                }
                if (Math.Abs(x * x + y * y - 0.14) < 0.03) return 0xFF00CCCCu;
                return 0;

            case PickupType.Armor:
                // Blue vest/shield shape
                if (y > -0.2 && y < 0.15 && Math.Abs(x) < 0.18 - y * 0.4)
                    return y < 0 ? 0xFF4488FFu : 0xFF2266CCu;
                if (y >= 0.15 && y < 0.25 && Math.Abs(x) < 0.08)
                    return 0xFF2266CCu;
                return 0;

            case PickupType.Ammo:
                // Yellow ammo box
                if (Math.Abs(x) < 0.15 && y > -0.12 && y < 0.12)
                {
                    if (Math.Abs(x) < 0.02 || Math.Abs(y) < 0.02) return 0xFFCC9900u;
                    return 0xFFFFDD44u;
                }
                return 0;

            case PickupType.Shotgun:
                // Brown/tan shotgun shape
                if (Math.Abs(y) < 0.05 && x > -0.25 && x < 0.25)
                    return x > 0.15 ? 0xFF664422u : 0xFFBB9966u;
                if (y < -0.05 && y > -0.15 && x > 0.05 && x < 0.12)
                    return 0xFF664422u;
                return 0;

            case PickupType.Ripper:
                // Gray chaingun shape
                if (Math.Abs(y) < 0.04 && x > -0.2 && x < 0.2)
                    return 0xFFAAAABBu;
                if (Math.Abs(y - 0.06) < 0.02 && x > 0 && x < 0.15)
                    return 0xFF666677u;
                if (Math.Abs(y + 0.06) < 0.02 && x > 0 && x < 0.15)
                    return 0xFF666677u;
                return 0;

            case PickupType.RPG:
                // Green RPG with red tip
                if (Math.Abs(y) < 0.05 && x > -0.2 && x < 0.2)
                    return x > 0.12 ? 0xFFFF4444u : 0xFF66BB66u;
                if (y > 0.05 && y < 0.12 && x > -0.15 && x < -0.05)
                    return 0xFF448844u;
                return 0;

            case PickupType.Medkit:
                // White box with red cross
                if (Math.Abs(x) < 0.18 && Math.Abs(y) < 0.15)
                {
                    if ((Math.Abs(x) < 0.04 && Math.Abs(y) < 0.10) || (Math.Abs(y) < 0.04 && Math.Abs(x) < 0.10))
                        return 0xFFFF2222u;
                    return 0xFFFFEEEEu;
                }
                return 0;

            case PickupType.Jetpack:
                // Orange jetpack with flames
                if (Math.Abs(x) < 0.12 && y > -0.15 && y < 0.1)
                    return 0xFFFF8800u;
                if (Math.Abs(x - 0.08) < 0.04 && y > 0.1 && y < 0.2 + Math.Sin(_gameTime * 12) * 0.05)
                    return 0xFFFF4400u;
                if (Math.Abs(x + 0.08) < 0.04 && y > 0.1 && y < 0.2 + Math.Sin(_gameTime * 12 + 1) * 0.05)
                    return 0xFFFFAA00u;
                return 0;

            case PickupType.Steroids:
                // Yellow syringe
                if (Math.Abs(y) < 0.03 && x > -0.2 && x < 0.15)
                    return 0xFFFFFF00u;
                if (x > 0.15 && x < 0.22 && Math.Abs(y) < 0.015)
                    return 0xFFCCCCCCu;
                if (x < -0.15 && x > -0.22 && Math.Abs(y) < 0.05)
                    return 0xFFDDDD00u;
                return 0;

            case PickupType.KeyRed:
                // Red key shape
                if (Math.Abs(x) < 0.08 && y > -0.2 && y < 0.05)
                    return 0xFFFF4444u;
                if (y > 0.05 && y < 0.15 && Math.Abs(x) < 0.12)
                    return dist < 0.015 ? 0xFFCC0000u : 0xFFFF4444u;
                return 0;

            case PickupType.KeyBlue:
                // Blue key shape
                if (Math.Abs(x) < 0.08 && y > -0.2 && y < 0.05)
                    return 0xFF4444FFu;
                if (y > 0.05 && y < 0.15 && Math.Abs(x) < 0.12)
                    return dist < 0.015 ? 0xFF0000CCu : 0xFF4444FFu;
                return 0;

            case PickupType.KeyYellow:
                // Yellow key shape
                if (Math.Abs(x) < 0.08 && y > -0.2 && y < 0.05)
                    return 0xFFFFFF44u;
                if (y > 0.05 && y < 0.15 && Math.Abs(x) < 0.12)
                    return dist < 0.015 ? 0xFFCCCC00u : 0xFFFFFF44u;
                return 0;

            case PickupType.Exit:
                // Glowing white/green exit sign
                double exitPulse = 0.7 + Math.Sin(_gameTime * 3) * 0.3;
                if (Math.Abs(x) < 0.2 && Math.Abs(y) < 0.12)
                {
                    if (Math.Abs(x) > 0.16 || Math.Abs(y) > 0.08) return 0xFF44AA44u;
                    uint g = (uint)(255 * exitPulse);
                    return 0xFF000000 | (g << 8) | (g / 2);
                }
                return 0;

            case PickupType.Invincibility:
                // Glowing cyan star with rotation effect
                double starPulse = 0.6 + Math.Sin(_gameTime * 8) * 0.4;
                double starAngle = _gameTime * 2;
                double rotX = x * Math.Cos(starAngle) - y * Math.Sin(starAngle);
                double rotY = x * Math.Sin(starAngle) + y * Math.Cos(starAngle);
                // 5-point star shape
                double starR = Math.Sqrt(rotX * rotX + rotY * rotY);
                double starA = Math.Atan2(rotY, rotX);
                double starShape = 0.12 + 0.06 * Math.Cos(5 * starA);
                if (starR < starShape * 0.6)
                {
                    uint cyan = (uint)(255 * starPulse);
                    return 0xFF000000 | (cyan << 8) | cyan;
                }
                if (starR < starShape)
                    return 0xFF00AAFFu;
                return 0;

            case PickupType.DamageBoost:
                // Red/orange skull with flame effect
                double flamePulse = 0.5 + Math.Sin(_gameTime * 6) * 0.5;
                // Skull shape
                if (Math.Abs(x) < 0.12 && y > -0.1 && y < 0.15)
                {
                    // Eye sockets
                    if ((Math.Abs(x - 0.05) < 0.025 || Math.Abs(x + 0.05) < 0.025) && y > 0.02 && y < 0.08)
                        return 0xFF000000u;
                    // Nose
                    if (Math.Abs(x) < 0.015 && y > -0.02 && y < 0.02)
                        return 0xFF000000u;
                    // Teeth
                    if (y < -0.05 && y > -0.1 && ((int)((x + 0.1) * 30) % 2 == 0))
                        return 0xFF000000u;
                    // Skull body
                    uint red = (uint)(200 + flamePulse * 55);
                    uint orange = (uint)(100 + flamePulse * 50);
                    return 0xFF000000 | (red << 16) | (orange << 8);
                }
                // Flames above
                if (y > 0.12 && y < 0.25 + Math.Sin(_gameTime * 10 + x * 5) * 0.05)
                {
                    if (Math.Abs(x) < 0.08 - (y - 0.12) * 0.3)
                    {
                        uint r = (uint)(255 * flamePulse);
                        uint g = (uint)(80 + flamePulse * 80);
                        return 0xFF000000 | (r << 16) | (g << 8);
                    }
                }
                return 0;

            default:
                return dist < 0.16 ? 0xFFFFFFFFu : 0;
        }
    }

    double Dist(double x, double y) => (x - _px_pos) * (x - _px_pos) + (y - _py) * (y - _py);

    void RenderWeapon()
    {
        int weaponW = 160, weaponH = 120;
        int baseX = W / 2 - weaponW / 2;
        
        // Enhanced weapon bob (based on movement)
        bool isMoving = _keysDown.Contains(Key.W) || _keysDown.Contains(Key.S) || 
                        _keysDown.Contains(Key.A) || _keysDown.Contains(Key.D);
        double bobSpeed = isMoving ? 12 : 2;
        double bobAmount = isMoving ? 6 : 2;
        _weaponBob = Math.Sin(_gameTime * bobSpeed) * bobAmount;
        
        // Weapon sway when turning (subtle)
        double sway = Math.Sin(_gameTime * 8) * 2;
        
        // Aiming position adjustment
        int aimOffset = _isAiming ? 20 : 0;
        
        int baseY = H - weaponH + (_shooting ? -25 : 0) - aimOffset;
        int bob = (int)_weaponBob;
        int swayX = (int)sway;

        for (int y = Math.Max(0, baseY + bob); y < H; y++)
        {
            for (int x = Math.Max(0, baseX + swayX); x < Math.Min(W, baseX + weaponW + swayX); x++)
            {
                double rx = (double)(x - baseX - swayX) / weaponW;  // 0 to 1
                double ry = (double)(y - baseY - bob) / weaponH;  // 0 to 1

                uint color = GetWeaponPixel(rx, ry);
                if ((color & 0xFF000000) != 0)
                    _px[y * W + x] = color;
            }
        }

        // Enhanced Muzzle flash with glow
        if (_shooting && _shootFrame < 4 && _currentWeapon > 0 && _currentWeapon != 5)
        {
            _muzzleFlashTimer = 0.08;
            int flashX = W / 2 + swayX + (_currentWeapon == 2 ? -5 : 0);
            int flashY = baseY + bob - 30;
            int flashSize = _currentWeapon == 4 ? 30 : _currentWeapon == 3 ? 25 : 22;

            // Outer glow
            for (int y = Math.Max(0, flashY - flashSize); y < Math.Min(H, flashY + flashSize * 2); y++)
            {
                for (int x = flashX - flashSize * 2; x < flashX + flashSize * 2; x++)
                {
                    if (x < 0 || x >= W) continue;
                    double d = Math.Sqrt((x - flashX) * (x - flashX) + (y - flashY) * (y - flashY));
                    if (d < flashSize * 1.5)
                    {
                        double intensity = Math.Pow(1 - d / (flashSize * 1.5), 2);
                        uint existing = _px[y * W + x];
                        uint er = (existing >> 16) & 0xFF;
                        uint eg = (existing >> 8) & 0xFF;
                        uint eb = existing & 0xFF;
                        
                        uint r = (uint)Math.Min(255, er + 255 * intensity);
                        uint g = (uint)Math.Min(255, eg + 180 * intensity);
                        uint b = (uint)Math.Min(255, eb + 50 * intensity);
                        _px[y * W + x] = 0xFF000000 | (r << 16) | (g << 8) | b;
                    }
                }
            }
            
            // Core flash (brighter)
            for (int y = Math.Max(0, flashY); y < Math.Min(H, flashY + flashSize); y++)
            {
                for (int x = flashX - flashSize / 2; x < flashX + flashSize / 2; x++)
                {
                    if (x < 0 || x >= W) continue;
                    double d = Math.Sqrt((x - flashX) * (x - flashX) + (y - flashY - flashSize / 2) * (y - flashY - flashSize / 2));
                    if (d < flashSize / 2)
                    {
                        double intensity = 1 - d / (flashSize / 2);
                        _px[y * W + x] = 0xFFFFFFFF; // Pure white core
                    }
                }
            }
        }
    }

    uint GetWeaponPixel(double x, double y)
    {
        // Center coordinates (-0.5 to 0.5)
        double cx = x - 0.5;
        double cy = y;

        switch (_currentWeapon)
        {
            case 0: return RenderFist(cx, cy);
            case 1: return RenderPistol(cx, cy);
            case 2: return RenderShotgun(cx, cy);
            case 3: return RenderRipper(cx, cy);
            case 4: return RenderRPG(cx, cy);
            case 5: return RenderPipeBomb(cx, cy);
            case 6: return RenderMiniRocket(cx, cy);
            default: return 0;
        }
    }

    uint RenderFist(double x, double y)
    {
        // Arm
        if (y > 0.4 && Math.Abs(x) < 0.15 + (y - 0.4) * 0.3)
        {
            uint skin = 0xFFDDBB99u;
            if (Math.Abs(x) > 0.12 + (y - 0.4) * 0.25) skin = 0xFFCCAA88u; // Shading
            return skin;
        }
        // Fist
        if (y > 0.2 && y < 0.5)
        {
            double fistWidth = 0.22 - Math.Abs(y - 0.35) * 0.3;
            if (Math.Abs(x) < fistWidth)
            {
                // Knuckles
                if (y > 0.22 && y < 0.32)
                {
                    double knuckleX = (x + 0.15) * 4;
                    if (Math.Sin(knuckleX * 3.14159) > 0.3) return 0xFFEECCAAu;
                    return 0xFFCCAA88u;
                }
                return 0xFFDDBB99u;
            }
        }
        // Thumb
        if (y > 0.25 && y < 0.4 && x > 0.1 && x < 0.25)
            return 0xFFCCAA88u;
        return 0;
    }

    uint RenderPistol(double x, double y)
    {
        uint metal = 0xFF555566u;
        uint metalDark = 0xFF333344u;
        uint metalLight = 0xFF777788u;
        uint grip = 0xFF443322u;
        uint gripLight = 0xFF554433u;

        // Slide (top part)
        if (y > 0.15 && y < 0.38 && Math.Abs(x) < 0.08)
        {
            if (y < 0.2) return metalLight; // Top edge
            if (Math.Abs(x) > 0.06) return metalDark; // Side shading
            // Ejection port
            if (y > 0.22 && y < 0.28 && x > 0.02 && x < 0.055) return metalDark;
            return metal;
        }
        // Barrel
        if (y > 0.08 && y < 0.2 && Math.Abs(x) < 0.04)
        {
            if (Math.Abs(x) < 0.02) return metalDark; // Bore
            return metal;
        }
        // Frame
        if (y > 0.35 && y < 0.5 && Math.Abs(x) < 0.07)
        {
            if (x < -0.04) return metalDark;
            return metal;
        }
        // Trigger guard
        if (y > 0.42 && y < 0.55 && x > -0.08 && x < 0.03)
        {
            if (y > 0.5 && Math.Abs(x + 0.025) < 0.04) return 0; // Guard hole
            if (Math.Abs(x + 0.02) < 0.015 && y > 0.44 && y < 0.52) return metalDark; // Trigger
            return metal;
        }
        // Grip
        if (y > 0.48 && Math.Abs(x) < 0.09 - (y - 0.48) * 0.1)
        {
            // Grip texture
            int texY = (int)(y * 60) % 3;
            if (texY == 0) return gripLight;
            if (x < 0) return grip;
            return gripLight;
        }
        // Hand
        if (y > 0.55 && Math.Abs(x) < 0.18)
        {
            if (Math.Abs(x) < 0.08) return 0xFFDDBB99u;
            if (y > 0.6) return 0xFFDDBB99u;
        }
        return 0;
    }

    uint RenderShotgun(double x, double y)
    {
        uint wood = 0xFF664422u;
        uint woodLight = 0xFF885533u;
        uint woodDark = 0xFF442211u;
        uint metal = 0xFF555555u;
        uint metalDark = 0xFF333333u;

        // Double barrels
        if (y > 0.05 && y < 0.25)
        {
            if (Math.Abs(x - 0.04) < 0.035 || Math.Abs(x + 0.04) < 0.035)
            {
                if (y < 0.1) return metalDark; // Bore openings
                return metal;
            }
            // Barrel rib
            if (Math.Abs(x) < 0.015 && y > 0.1) return metalDark;
        }
        // Receiver
        if (y > 0.22 && y < 0.4 && Math.Abs(x) < 0.1)
        {
            if (y < 0.26) return metal;
            if (Math.Abs(x) > 0.08) return metalDark;
            return metal;
        }
        // Forend (pump grip)
        if (y > 0.35 && y < 0.5 && Math.Abs(x) < 0.12)
        {
            int groove = (int)((x + 0.12) * 40) % 4;
            if (groove == 0) return woodDark;
            return wood;
        }
        // Stock
        if (y > 0.48 && Math.Abs(x) < 0.14 - (y - 0.48) * 0.15)
        {
            if (x < -0.05) return woodDark;
            if (x > 0.08) return woodLight;
            return wood;
        }
        // Hands
        if (y > 0.38 && y < 0.55)
        {
            if (x > 0.12 && x < 0.28) return 0xFFDDBB99u; // Front hand
        }
        if (y > 0.6 && Math.Abs(x) < 0.2) return 0xFFDDBB99u; // Rear hand
        return 0;
    }

    uint RenderRipper(double x, double y)
    {
        uint metal = 0xFF444455u;
        uint metalDark = 0xFF222233u;
        uint metalLight = 0xFF666677u;

        // Multiple barrels (rotary)
        if (y > 0.02 && y < 0.28)
        {
            double barrelAngle = _gameTime * (_shooting ? 30 : 0);
            for (int i = 0; i < 6; i++)
            {
                double angle = barrelAngle + i * 3.14159 / 3;
                double bx = Math.Cos(angle) * 0.05;
                double by = Math.Sin(angle) * 0.05 * 0.3 + 0.15;
                double dist = Math.Sqrt((x - bx) * (x - bx) + (y - by) * (y - by));
                if (dist < 0.025)
                {
                    if (dist < 0.012) return metalDark; // Bore
                    return metal;
                }
            }
            // Center hub
            if (Math.Abs(x) < 0.03 && y > 0.1 && y < 0.2) return metalLight;
        }
        // Barrel housing
        if (y > 0.1 && y < 0.3 && Math.Abs(x) < 0.1)
        {
            double dist = Math.Sqrt(x * x + (y - 0.15) * (y - 0.15) * 4);
            if (dist < 0.12 && dist > 0.08) return metalDark;
        }
        // Body
        if (y > 0.28 && y < 0.55 && Math.Abs(x) < 0.12)
        {
            if (Math.Abs(x) > 0.1) return metalDark;
            // Ammo counter window
            if (y > 0.35 && y < 0.42 && x > 0.02 && x < 0.08) return 0xFF00AA00u;
            return metal;
        }
        // Handle
        if (y > 0.5 && y < 0.75 && Math.Abs(x) < 0.1 - (y - 0.5) * 0.2)
        {
            if (x < 0) return metalDark;
            return metal;
        }
        // Hand
        if (y > 0.6 && Math.Abs(x) < 0.2) return 0xFFDDBB99u;
        // Forward grip
        if (y > 0.35 && y < 0.5 && x > 0.1 && x < 0.22) return 0xFFDDBB99u;
        return 0;
    }

    uint RenderRPG(double x, double y)
    {
        uint tube = 0xFF445544u;
        uint tubeDark = 0xFF223322u;
        uint tubeLight = 0xFF557755u;
        uint metal = 0xFF555555u;

        // Main tube
        if (y > 0.05 && y < 0.45 && Math.Abs(x) < 0.1)
        {
            double dist = Math.Abs(x);
            if (dist > 0.08) return tubeDark;
            if (dist < 0.03) return tubeLight;
            // Warhead tip
            if (y < 0.12)
            {
                if (y < 0.08) return 0xFF666655u; // Cone
                return 0xFFAA4444u; // Red band
            }
            // Tube segments
            int seg = (int)(y * 20) % 5;
            if (seg == 0) return tubeDark;
            return tube;
        }
        // Front sight
        if (y > 0.1 && y < 0.18 && Math.Abs(x - 0.12) < 0.02) return metal;
        // Rear sight
        if (y > 0.35 && y < 0.42 && Math.Abs(x - 0.12) < 0.025) return metal;
        // Grip assembly
        if (y > 0.4 && y < 0.65 && x > -0.05 && x < 0.08)
        {
            // Trigger
            if (y > 0.45 && y < 0.52 && Math.Abs(x + 0.01) < 0.015) return metal;
            return metal;
        }
        // Shoulder rest
        if (y > 0.55 && y < 0.8 && Math.Abs(x) < 0.12 - (y - 0.55) * 0.2)
        {
            if (x < 0) return tubeDark;
            return tube;
        }
        // Hands
        if (y > 0.5 && y < 0.65 && x > 0.06 && x < 0.22) return 0xFFDDBB99u;
        if (y > 0.65 && Math.Abs(x) < 0.18) return 0xFFDDBB99u;
        return 0;
    }

    uint RenderPipeBomb(double x, double y)
    {
        uint skin = 0xFFDDBB99u;
        uint skinDark = 0xFFCCAA88u;
        uint bomb = 0xFF446644u;
        uint bombDark = 0xFF334433u;
        uint fuse = 0xFFAAAA55u;

        // Hand holding bomb
        if (y > 0.35)
        {
            // Palm
            if (Math.Abs(x) < 0.2 && y > 0.5)
            {
                if (x < -0.1) return skinDark;
                return skin;
            }
            // Fingers wrapped around
            for (int i = 0; i < 4; i++)
            {
                double fx = -0.1 + i * 0.06;
                double fy = 0.35 + Math.Sin((x - fx) * 10) * 0.03;
                if (Math.Abs(x - fx) < 0.025 && y > fy && y < fy + 0.18)
                {
                    if (y < fy + 0.05) return skinDark; // Knuckle
                    return skin;
                }
            }
            // Thumb
            if (x > 0.08 && x < 0.18 && y > 0.3 && y < 0.5) return skin;
        }
        // Pipe bomb
        if (y > 0.15 && y < 0.55 && Math.Abs(x) < 0.07)
        {
            // End caps
            if (y < 0.2 || y > 0.5)
            {
                if (Math.Abs(x) < 0.05) return 0xFF555555u;
            }
            // Fuse
            if (y < 0.2 && Math.Abs(x) < 0.015) return fuse;
            // Body
            if (Math.Abs(x) > 0.055) return bombDark;
            // Label/marking
            if (y > 0.3 && y < 0.4 && Math.Abs(x) < 0.04) return 0xFFAA2222u;
            return bomb;
        }
        // Fuse wire going up
        if (y < 0.18 && y > 0.02 && Math.Abs(x) < 0.01) return fuse;
        // Spark at tip
        if (y < 0.06 && Math.Abs(x) < 0.02)
        {
            double spark = Math.Sin(_gameTime * 20) * 0.5 + 0.5;
            if (spark > 0.3) return 0xFFFFAA00u;
        }
        return 0;
    }

    uint RenderMiniRocket(double x, double y)
    {
        // Small rocket held in hands — compact visual distinct from RPG
        uint body = 0xFF666633u;
        uint tip = 0xFFFF6644u;
        uint fin = 0xFF333322u;

        // Hand
        if (y > 0.5 && Math.Abs(x) < 0.18) return 0xFFDDBB99u;

        // Rocket body (slim)
        if (y > 0.12 && y < 0.42 && Math.Abs(x) < 0.06)
        {
            if (Math.Abs(x) < 0.03) return body;
            return 0xFF444433u;
        }

        // Tip
        if (y > 0.06 && y < 0.14 && Math.Abs(x) < 0.05) return tip;

        // Small fins
        if (y > 0.38 && y < 0.46 && (Math.Abs(x - 0.07) < 0.02 || Math.Abs(x + 0.07) < 0.02)) return fin;

        return 0;
    }

    void RenderMinimap()
    {
        Array.Clear(_minimapPx, 0, _minimapPx.Length);

        double scale = 120.0 / MAP_SIZE;
        for (int y = 0; y < MAP_SIZE; y++)
        {
            for (int x = 0; x < MAP_SIZE; x++)
            {
                int px = (int)(x * scale), py = (int)(y * scale);
                int tile = _map[y * MAP_SIZE + x];

                byte r = 15, g = 15, b = 20;
                if (tile > 0) { r = 70; g = 70; b = 80; }

                for (int dy = 0; dy < (int)scale && py + dy < 120; dy++)
                    for (int dx = 0; dx < (int)scale && px + dx < 120; dx++)
                    {
                        int i = ((py + dy) * 120 + (px + dx)) * 4;
                        _minimapPx[i] = b; _minimapPx[i + 1] = g; _minimapPx[i + 2] = r; _minimapPx[i + 3] = 200;
                    }
            }
        }

        int playerPx = (int)(_px_pos * scale), playerPy = (int)(_py * scale);
        for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
            {
                int px = playerPx + dx, py_m = playerPy + dy;
                if (px >= 0 && px < 120 && py_m >= 0 && py_m < 120)
                {
                    int i = (py_m * 120 + px) * 4;
                    _minimapPx[i] = 0; _minimapPx[i + 1] = 200; _minimapPx[i + 2] = 255; _minimapPx[i + 3] = 255;
                }
            }

        foreach (var en in _enemies.Where(e => !e.Dead))
        {
            int ex = (int)(en.X * scale), ey = (int)(en.Y * scale);
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int px = ex + dx, py_m = ey + dy;
                    if (px >= 0 && px < 120 && py_m >= 0 && py_m < 120)
                    {
                        int i = (py_m * 120 + px) * 4;
                        _minimapPx[i] = 0; _minimapPx[i + 1] = 0; _minimapPx[i + 2] = 255; _minimapPx[i + 3] = 255;
                    }
                }
        }

        _minimapBmp.WritePixels(new Int32Rect(0, 0, 120, 120), _minimapPx, 120 * 4, 0);
    }
    #endregion

    #region HUD
    void UpdateHUD()
    {
        HealthText.Text = _hp.ToString();
        HealthBar.Width = Math.Max(0, _hp * 1.78);

        ArmorText.Text = _armor.ToString();
        WeaponName.Text = _weaponNames[_currentWeapon];
        AmmoText.Text = _ammo[_currentWeapon].ToString();
        ScoreText.Text = _score.ToString();
        KillsText.Text = $"{_kills}/{_totalEnemies}";

        RedKeyIcon.Opacity = _keys[0] ? 1 : 0.3;
        BlueKeyIcon.Opacity = _keys[1] ? 1 : 0.3;
        YellowKeyIcon.Opacity = _keys[2] ? 1 : 0.3;

        JetpackIcon.Background = _hasJetpack ? new SolidColorBrush(Color.FromRgb(80, 60, 20)) : new SolidColorBrush(Color.FromRgb(51, 51, 51));
        MedkitIcon.Background = _medkits > 0 ? new SolidColorBrush(Color.FromRgb(80, 30, 30)) : new SolidColorBrush(Color.FromRgb(51, 51, 51));
        SteroidsIcon.Background = _steroids > 0 ? new SolidColorBrush(Color.FromRgb(80, 80, 20)) : new SolidColorBrush(Color.FromRgb(51, 51, 51));

        // Silly face based on health!
        DukeFace.Text = _hp > 75 ? "😊" : _hp > 50 ? "😅" : _hp > 25 ? "😰" : "🥴";

        // Power-up bars
        bool hasPowerUps = _invincibilityTimer > 0 || _damageBoostTimer > 0;
        PowerUpPanel.Visibility = hasPowerUps ? Visibility.Visible : Visibility.Collapsed;
        
        if (_invincibilityTimer > 0)
        {
            InvincibilityBar.Visibility = Visibility.Visible;
            InvincibilityFill.Width = Math.Max(0, (_invincibilityTimer / 10.0) * 100);
        }
        else
        {
            InvincibilityBar.Visibility = Visibility.Collapsed;
        }

        if (_damageBoostTimer > 0)
        {
            DamageBoostBar.Visibility = Visibility.Visible;
            DamageBoostFill.Width = Math.Max(0, (_damageBoostTimer / 15.0) * 100);
        }
        else
        {
            DamageBoostBar.Visibility = Visibility.Collapsed;
        }

        // Kill streak display - silly names!
        if (_killStreak >= 3 && _killStreakTimer > 0)
        {
            KillStreakPanel.Visibility = Visibility.Visible;
            KillStreakCount.Text = $"x{_killStreak}";
            string streakName = _killStreak switch
            {
                3 => "NICE COMBO!",
                4 => "ON A ROLL!",
                5 => "UNSTOPPABLE-ISH!",
                6 => "WOW SUCH SKILL!",
                7 => "KEYBOARD WARRIOR!",
                _ => _killStreak >= 8 ? "ACTUAL LEGEND!" : "ZOINKS!"
            };
            KillStreakText.Text = streakName;
        }
        else
        {
            KillStreakPanel.Visibility = Visibility.Collapsed;
        }

        // Crosshair when aiming
        AimCrosshairCanvas.Visibility = _isAiming ? Visibility.Visible : Visibility.Collapsed;
    }

    void ShowMessage(string msg)
    {
        _message = msg;
        _messageTimer = 3;
        MessageText.Text = msg;
    }

    void SayQuote(string quote)
    {
        DukeQuote.Text = $"\"{quote}\"";
        _quoteTimer = 2.5;
    }
    #endregion

    #region Shooting
    void Shoot()
    {
        if (_shooting) return;
        if (_currentWeapon > 0 && _currentWeapon != 5 && _ammo[_currentWeapon] <= 0)
        {
            ShowMessage("Out of ammo!");
            return;
        }

        if (_gameTime - _lastShot < _weaponFireRate[_currentWeapon]) return;
        _lastShot = _gameTime;

        _shooting = true;
        _shootFrame = 0;
        if (_currentWeapon > 0 && _currentWeapon != 5) _ammo[_currentWeapon]--;

        foreach (var en in _enemies)
            if (Dist(en.X, en.Y) < 120) en.Alerted = true;

        // Damage multiplier from power-up
        int damageMultiplier = _damageBoostTimer > 0 ? 2 : 1;

        if (_currentWeapon == 4) // RPG
        {
            _projectiles.Add(new Projectile
            {
                X = _px_pos, Y = _py,
                Dx = Math.Cos(_pa) * 0.25,
                Dy = Math.Sin(_pa) * 0.25,
                Damage = _weaponDamage[_currentWeapon] * damageMultiplier,
                FromPlayer = true,
                IsRocket = true
            });
        }
        else if (_currentWeapon == 6) // Mini Rocket
        {
            if (_ammo[6] > 0)
            {
                _ammo[6]--;
                _projectiles.Add(new Projectile
                {
                    X = _px_pos, Y = _py,
                    Dx = Math.Cos(_pa) * 0.35,
                    Dy = Math.Sin(_pa) * 0.35,
                    Damage = _weaponDamage[_currentWeapon] * damageMultiplier,
                    FromPlayer = true,
                    IsRocket = true
                });
            }
            else
            {
                ShowMessage("Out of mini rockets!");
            }
        }
        else if (_currentWeapon == 5) // Pipe bomb
        {
            if (_ammo[5] > 0)
            {
                _pipeBombs.Add((_px_pos + Math.Cos(_pa) * 1.5, _py + Math.Sin(_pa) * 1.5, 0));
                _ammo[5]--;
            }
        }
        else
        {
            int pellets = _currentWeapon == 2 ? 8 : 1;
            for (int i = 0; i < pellets; i++)
            {
                double spread = (_rnd.NextDouble() - 0.5) * _weaponSpread[_currentWeapon];
                HitscanShot(_pa + spread, _weaponDamage[_currentWeapon] * damageMultiplier / pellets);
            }
        }
    }

    void HitscanShot(double angle, int damage)
    {
        double rx = Math.Cos(angle), ry = Math.Sin(angle);
        double x = _px_pos, y = _py;

        for (int i = 0; i < 250; i++)
        {
            x += rx * 0.1;
            y += ry * 0.1;

            if (IsWall(x, y)) break;

            foreach (var en in _enemies)
            {
                if (en.Dead) continue;
                double d = Math.Sqrt((x - en.X) * (x - en.X) + (y - en.Y) * (y - en.Y));
                if (d < 0.45)
                {
                    en.Hp -= damage;
                    en.HurtTimer = 0.15;
                    en.Alerted = true;
                    if (en.Hp <= 0) KillEnemy(en);
                    return;
                }
            }
        }
    }
    #endregion

    #region Door & Item Use
    void TryOpenDoor()
    {
        double checkX = _px_pos + Math.Cos(_pa) * 1.5;
        double checkY = _py + Math.Sin(_pa) * 1.5;

        foreach (var door in _doors)
        {
            if (Math.Abs(door.X + 0.5 - checkX) < 1 && Math.Abs(door.Y + 0.5 - checkY) < 1)
            {
                if (door.KeyRequired > 0 && !_keys[door.KeyRequired - 1])
                {
                    string keyName = door.KeyRequired == 1 ? "Red" : door.KeyRequired == 2 ? "Blue" : "Yellow";
                    ShowMessage($"Need {keyName} Access Card!");
                    return;
                }
                door.Opening = true;
                ShowMessage("Access Granted");
            }
        }
    }

    void UseMedkit()
    {
        if (_medkits > 0 && _hp < 100)
        {
            _medkits--;
            _hp = Math.Min(100, _hp + 50);
            SayQuote("Ahhh, much better!");
        }
    }

    void UseSteroids()
    {
        if (_steroids > 0)
        {
            _steroids--;
            _steroidsTimer = 15;
            SayQuote("Get some!");
        }
    }

    void ToggleJetpack()
    {
        if (_hasJetpack && _jetpackFuel > 0)
        {
            _jetpackActive = !_jetpackActive;
            if (_jetpackActive) SayQuote("Woohooo!");
        }
    }
    #endregion

    #region Input
    void OnKeyDown(object s, KeyEventArgs e)
    {
        _keysDown.Add(e.Key);

        if (e.Key == Key.Escape) { CaptureMouse(false); Close(); }


        if (_gameOver)
        {
            if (e.Key == Key.R) Restart();
            return;
        }

        if (_levelComplete)
        {
            if (e.Key == Key.Space) NextLevel();
            return;
        }

        if (e.Key == Key.Space && _pz == 0 && !_jetpackActive) { _pzVel = 6; _isJumping = true; }
        if (e.Key == Key.LeftCtrl || e.Key == Key.C) _isCrouching = true;
        if (e.Key == Key.E) TryOpenDoor();
        if (e.Key == Key.F) Shoot();
        if (e.Key == Key.G && _currentWeapon == 5) DetonatePipeBombs();
        if (e.Key == Key.M) UseMedkit();
        if (e.Key == Key.J) ToggleJetpack();
        if (e.Key == Key.R && !_gameOver) UseSteroids();

        if (e.Key == Key.D1) _currentWeapon = 0;
        if (e.Key == Key.D2) _currentWeapon = 1;
        if (e.Key == Key.D3 && _ammo[2] > 0) _currentWeapon = 2;
        if (e.Key == Key.D4 && _ammo[3] > 0) _currentWeapon = 3;
        if (e.Key == Key.D5 && _ammo[4] > 0) _currentWeapon = 4;
        if (e.Key == Key.D6 && _ammo[5] > 0) _currentWeapon = 5;
        if (e.Key == Key.D7 && _ammo.Length > 6 && _ammo[6] > 0) _currentWeapon = 6;

        if (e.Key == Key.Tab) MinimapBorder.Visibility = MinimapBorder.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        if (e.Key == Key.P) TogglePause();
        // Adjust mouse sensitivity with + / - keys and save
        if (e.Key == Key.OemPlus || e.Key == Key.Add)
        {
            _mouseSensitivity = Math.Min(5.0, _mouseSensitivity + 0.02);
            _settings.MouseSensitivity = _mouseSensitivity;
            _settingsService?.Save(_settings);
            ShowMessage($"Sensitivity: {_mouseSensitivity:F2}");
        }
        if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
        {
            _mouseSensitivity = Math.Max(0.02, _mouseSensitivity - 0.02);
            _settings.MouseSensitivity = _mouseSensitivity;
            _settingsService?.Save(_settings);
            ShowMessage($"Sensitivity: {_mouseSensitivity:F2}");
        }
    }

    void UpdateTopScoresUI()
    {
        try
        {
            if (_highScoreService == null) return;
            var top = _highScoreService.LoadTop(5);
            if (top == null || top.Count == 0)
            {
                TopScoresText.Text = "Top Scores: (none)";
                TopScoresTextVictory.Text = "Top Scores: (none)";
                return;
            }
            string fmt = "Top Scores:\n" + string.Join('\n', top.Select((t, i) => $"{i+1}. {t.Name} - {t.Score}"));
            TopScoresText.Text = fmt;
            TopScoresTextVictory.Text = fmt;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to update top scores UI: {ex.Message}");
        }
    }

    void OnKeyUp(object s, KeyEventArgs e)
    {
        _keysDown.Remove(e.Key);
        if (e.Key == Key.LeftCtrl || e.Key == Key.C) _isCrouching = false;
    }

    void OnMouseMove(object s, MouseEventArgs e) { }

    void OnMouseDown(object s, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            if (!_mouseCaptured) CaptureMouse(true);
            else if (!_gameOver && !_levelComplete && !_victory) Shoot();
        }
        if (e.RightButton == MouseButtonState.Pressed)
        {
            if (_currentWeapon == 5)
                DetonatePipeBombs();
            else if (_mouseCaptured && !_gameOver && !_levelComplete && !_victory)
                _isAiming = !_isAiming; // Toggle aim-down-sights
        }
    }

    void OnMouseUp(object s, MouseButtonEventArgs e)
    {
        // Could hold to aim instead of toggle - uncomment below:
        // if (e.ChangedButton == MouseButton.Right) _isAiming = false;
    }

    void Restart()
    {
        _currentLevel = 1;
        _hp = 100; _armor = 0; _score = 0;
        _keys = new bool[3];
        _ammo = new[] { 999, 48, 12, 200, 10, 5, 8 };
        _currentWeapon = 1;
        _medkits = 0; _steroids = 0; _hasJetpack = false; _jetpackFuel = 100;
        _gameOver = false; _levelComplete = false;
        // Reset new game state
        _isAiming = false;
        _killStreak = 0; _killStreakTimer = 0;
        _invincibilityTimer = 0; _damageBoostTimer = 0;
        _screenShakeTimer = 0;
        _damageVignetteTimer = 0;
        _muzzleFlashTimer = 0;
        _weaponBob = 0;
        _particles.Clear();
        _wallDecals.Clear();
        _bodyParts.Clear();
        GameOverScreen.Visibility = Visibility.Collapsed;
        VictoryScreen.Visibility = Visibility.Collapsed;
        LoadLevel(_currentLevel);
        CaptureMouse(true);
        // refresh top scores UI after restart
        UpdateTopScoresUI();
    }
    #endregion
}
