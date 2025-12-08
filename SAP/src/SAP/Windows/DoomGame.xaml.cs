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

namespace SAP.Windows;

public partial class DoomGame : Window
{
    #region Win32 API for Mouse
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

    #region Duke Quotes
    static readonly string[] KillQuotes = {
        "Damn, I'm good!",
        "Rest in pieces!",
        "Suck it down!",
        "Eat this!",
        "Who wants some?!",
        "Come get some!",
        "Your face, your ass... what's the difference?",
        "Blow it out your ass!",
        "Piece of cake!",
        "Let God sort 'em out!"
    };

    static readonly string[] LevelCompleteQuotes = {
        "Hail to the king, baby!",
        "I'm gonna rip off your head and... oh wait, I already did!",
        "Damn, those alien bastards are gonna pay!",
        "Nobody steals our chicks... and lives!",
        "It's time to kick ass and chew bubblegum!"
    };

    static readonly string[] HurtQuotes = {
        "Ooh, that hurt!",
        "Damn!",
        "Son of a..."
    };
    #endregion

    #region Game State
    WriteableBitmap _bmp, _minimapBmp;
    uint[] _px;
    byte[] _minimapPx;
    double[] _zBuffer;
    DispatcherTimer? _timer;

    // Player - Duke style with vertical look, jumping, crouching
    double _px_pos, _py, _pa;          // Position and horizontal angle
    double _pitch = 0;                  // Vertical look angle
    double _pz = 0;                     // Vertical position (for jumping/crouching)
    double _pzVel = 0;                  // Vertical velocity
    bool _isJumping = false;
    bool _isCrouching = false;
    double _eyeHeight = 0;              // Eye height offset
    int _hp = 100, _armor = 0, _score = 0;
    bool[] _keys = new bool[3];         // R, B, Y keys
    int _currentWeapon = 1;
    int[] _ammo = { 999, 48, 12, 200, 10, 5 }; // Boot, Pistol, Shotgun, Ripper, RPG, Pipebomb
    int _currentLevel = 1;
    int _kills = 0, _totalEnemies = 0, _secretsFound = 0, _totalSecrets = 0;

    // Inventory items
    int _medkits = 0, _steroids = 0;
    double _jetpackFuel = 100;
    bool _hasJetpack = false;
    bool _jetpackActive = false;
    double _steroidsTimer = 0;

    // Weapons - Duke style
    readonly string[] _weaponNames = { "MIGHTY BOOT", "PISTOL", "SHOTGUN", "RIPPER", "RPG", "PIPE BOMB" };
    readonly int[] _weaponDamage = { 25, 12, 50, 8, 120, 150 };
    readonly double[] _weaponFireRate = { 0.5, 0.25, 0.9, 0.08, 1.2, 0.3 };
    readonly double[] _weaponSpread = { 0, 0.02, 0.12, 0.06, 0, 0 };
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
    bool _gameOver = false, _levelComplete = false, _victory = false, _paused = false;
    double _messageTimer = 0;
    string _message = "";
    double _quoteTimer = 0;
    Random _rnd = new();
    double _gameTime = 0;

    // Map
    int[] _map = new int[MAP_SIZE * MAP_SIZE];
    #endregion

    #region Entity Classes
    enum EnemyType { Trooper, PigCop, Enforcer, Octabrain, BattleLord }

    class Enemy
    {
        public double X, Y, Hp, MaxHp, Speed, AttackRange, AttackDamage, AttackCooldown;
        public double Timer, HurtTimer, DeathTimer;
        public bool Dead, Alerted;
        public EnemyType Type;
        public int ScoreValue;
    }

    enum PickupType { Health, Armor, Ammo, Shotgun, Ripper, RPG, KeyRed, KeyBlue, KeyYellow, Medkit, Jetpack, Steroids, AtomicHealth, Exit }

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
        public bool Opening, Closing;
    }
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
    #endregion

    #region Level Loading
    void LoadLevel(int level)
    {
        _enemies.Clear();
        _pickups.Clear();
        _projectiles.Clear();
        _doors.Clear();
        _pipeBombs.Clear();

        for (int i = 0; i < MAP_SIZE * MAP_SIZE; i++) _map[i] = 0;

        for (int i = 0; i < MAP_SIZE; i++)
        {
            _map[i] = 1;
            _map[(MAP_SIZE - 1) * MAP_SIZE + i] = 1;
            _map[i * MAP_SIZE] = 1;
            _map[i * MAP_SIZE + MAP_SIZE - 1] = 1;
        }

        switch (level)
        {
            case 1: GenerateLevel1(); break;
            case 2: GenerateLevel2(); break;
            case 3: GenerateLevel3(); break;
            default: GenerateBossLevel(); break;
        }

        _totalEnemies = _enemies.Count;
        _totalSecrets = _pickups.Count(p => p.Type == PickupType.AtomicHealth);
        _kills = 0;
        _secretsFound = 0;
        _pitch = 0;
        _pz = 0;
        _pzVel = 0;

        Title = $"S.A.P NUKEM - Level {level}";
        SayQuote("It's time to kick ass and chew bubblegum...");
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
        var e = new Enemy { X = x + 0.5, Y = y + 0.5, Type = type };
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

        Render();
        RenderMinimap();
        UpdateHUD();

        if (_messageTimer > 0) _messageTimer -= dt;
        else MessageText.Text = "";

        if (_quoteTimer > 0) _quoteTimer -= dt;
        else DukeQuote.Text = "";

        if (_steroidsTimer > 0) _steroidsTimer -= dt;
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

            double dx = _px_pos - en.X, dy = _py - en.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);

            if (dist < 12) en.Alerted = true;
            if (!en.Alerted) continue;

            if (dist > en.AttackRange * 0.4 && dist < 16)
            {
                double nx = en.X + dx / dist * en.Speed;
                double ny = en.Y + dy / dist * en.Speed;
                if (!IsWall(nx, en.Y)) en.X = nx;
                if (!IsWall(en.X, ny)) en.Y = ny;
            }

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
        if (_armor > 0)
        {
            int absorbed = Math.Min(_armor, damage * 2 / 3);
            _armor -= absorbed;
            damage -= absorbed / 2;
        }
        _hp -= damage;

        if (_rnd.NextDouble() < 0.3)
            SayQuote(HurtQuotes[_rnd.Next(HurtQuotes.Length)]);

        if (_hp <= 0)
        {
            _hp = 0;
            _gameOver = true;
            GameOverScreen.Visibility = Visibility.Visible;
            FinalScoreText.Text = $"Final Score: {_score}";
            CaptureMouse(false);
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
                if (p.IsRocket && p.FromPlayer) ExplosionAt(p.X, p.Y, 100);
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
                        if (p.IsRocket) ExplosionAt(p.X, p.Y, 100);
                        else
                        {
                            en.Hp -= p.Damage;
                            en.HurtTimer = 0.15;
                            en.Alerted = true;
                            if (en.Hp <= 0) KillEnemy(en);
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
        if (_pipeBombs.Count > 0) SayQuote("Blow it out your ass!");
    }

    void ExplosionAt(double x, double y, int damage)
    {
        foreach (var en in _enemies)
        {
            if (en.Dead) continue;
            double d = Math.Sqrt((x - en.X) * (x - en.X) + (y - en.Y) * (y - en.Y));
            if (d < 3)
            {
                int dmg = (int)(damage * (1 - d / 3));
                en.Hp -= dmg;
                en.HurtTimer = 0.2;
                en.Alerted = true;
                if (en.Hp <= 0) KillEnemy(en);
            }
        }

        double pd = Math.Sqrt((x - _px_pos) * (x - _px_pos) + (y - _py) * (y - _py));
        if (pd < 3) TakeDamage((int)(damage * 0.3 * (1 - pd / 3)));
    }

    void KillEnemy(Enemy en)
    {
        en.Dead = true;
        _score += en.ScoreValue;
        _kills++;

        if (_rnd.NextDouble() < 0.4)
            SayQuote(KillQuotes[_rnd.Next(KillQuotes.Length)]);
    }
    #endregion

    #region Door Update
    void UpdateDoors(double dt)
    {
        foreach (var door in _doors)
        {
            if (door.Opening)
            {
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
                        ShowMessage("Ammo");
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
        LevelCompleteScreen.Visibility = Visibility.Visible;
        LevelScoreText.Text = $"Score: {_score}";
        LevelStatsText.Text = $"Kills: {_kills}/{_totalEnemies}  Secrets: {_secretsFound}/{_totalSecrets}";
        LevelQuote.Text = $"\"{LevelCompleteQuotes[_rnd.Next(LevelCompleteQuotes.Length)]}\"";
        CaptureMouse(false);
    }

    void NextLevel()
    {
        _currentLevel++;
        _levelComplete = false;
        LevelCompleteScreen.Visibility = Visibility.Collapsed;

        if (_currentLevel > 4)
        {
            _victory = true;
            VictoryScreen.Visibility = Visibility.Visible;
            TotalScoreText.Text = $"Total Score: {_score}";
        }
        else
        {
            LoadLevel(_currentLevel);
            CaptureMouse(true);
        }
    }
    #endregion

    #region Rendering
    void Render()
    {
        for (int i = 0; i < W; i++) _zBuffer[i] = double.MaxValue;

        RenderFloorCeiling();
        RenderWalls();
        RenderSprites();
        RenderWeapon();

        _bmp.WritePixels(new Int32Rect(0, 0, W, H), _px, W * 4, 0);

        // Update crosshair position based on pitch
        Canvas.SetTop(CrosshairCanvas, 278 - _pitch);
    }

    void RenderFloorCeiling()
    {
        int horizon = H / 2 + (int)_pitch + (int)_eyeHeight;

        for (int y = 0; y < H; y++)
        {
            bool isFloor = y > horizon;
            double rowDist = Math.Abs((H / 2.0) / (y - horizon + 0.01));

            double floorStepX = rowDist * (Math.Cos(_pa + 0.5) - Math.Cos(_pa - 0.5)) / W;
            double floorStepY = rowDist * (Math.Sin(_pa + 0.5) - Math.Sin(_pa - 0.5)) / W;

            double floorX = _px_pos + rowDist * Math.Cos(_pa - 0.5);
            double floorY = _py + rowDist * Math.Sin(_pa - 0.5);

            for (int x = 0; x < W; x++)
            {
                int tx = (int)(floorX * TEX) & (TEX - 1);
                int ty = (int)(floorY * TEX) & (TEX - 1);

                uint color = isFloor ? FloorTex[ty * TEX + tx] : CeilTex[ty * TEX + tx];

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

        for (int x = 0; x < W; x++)
        {
            double camX = 2.0 * x / W - 1;
            double rayDirX = Math.Cos(_pa) + Math.Cos(_pa + PI / 2) * camX * 0.66;
            double rayDirY = Math.Sin(_pa) + Math.Sin(_pa + PI / 2) * camX * 0.66;

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
            int drawStart = Math.Max(0, -lineHeight / 2 + horizon - (int)(_pz / perpWallDist * 20));
            int drawEnd = Math.Min(H - 1, lineHeight / 2 + horizon - (int)(_pz / perpWallDist * 20));

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

            int spriteScreenX = (int)(W / 2 * (1 + transformX / transformY));
            int spriteHeight = (int)Math.Abs(H / transformY);
            int spriteWidth = spriteHeight;

            int vertOffset = (int)(_pz / transformY * 20);

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

            int drawStartY = Math.Max(0, -spriteHeight / 2 + horizon - vertOffset);
            int drawEndY = Math.Min(H - 1, spriteHeight / 2 + horizon - vertOffset);
            int drawStartX = Math.Max(0, -spriteWidth / 2 + spriteScreenX);
            int drawEndX = Math.Min(W - 1, spriteWidth / 2 + spriteScreenX);

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

    uint GetEnemyColor(Enemy en, double x, double y)
    {
        if (en.Dead) return y > 0.5 && Math.Abs(x) < 0.4 ? 0xFF302010u : 0;

        bool hurt = en.HurtTimer > 0;
        uint baseColor = en.Type switch
        {
            EnemyType.Trooper => hurt ? 0xFFFFAAAAu : 0xFF336633u,    // Green
            EnemyType.PigCop => hurt ? 0xFFFFAAAAu : 0xFF4444AAu,     // Blue
            EnemyType.Enforcer => hurt ? 0xFFFFAAAAu : 0xFF666666u,   // Gray
            EnemyType.Octabrain => hurt ? 0xFFFFAAAAu : 0xFFAA4488u,  // Purple
            EnemyType.BattleLord => hurt ? 0xFFFFAAAAu : 0xFF888844u, // Brown
            _ => 0xFF336633u
        };

        if (y < 0.3 && x * x + (y - 0.15) * (y - 0.15) < 0.025)
        {
            if (y > 0.1 && y < 0.2 && (Math.Abs(x - 0.06) < 0.025 || Math.Abs(x + 0.06) < 0.025))
                return en.Type == EnemyType.Octabrain ? 0xFF00FF00u : 0xFFFF0000u;
            return baseColor;
        }

        if (y >= 0.25 && y < 0.9 && Math.Abs(x) < 0.22 - (y - 0.25) * 0.25)
            return baseColor;

        if (y > 0.3 && y < 0.55 && Math.Abs(x) > 0.18 && Math.Abs(x) < 0.38)
            return baseColor;

        return 0;
    }

    uint GetPickupColor(Pickup p, double x, double y)
    {
        if (x * x + y * y > 0.18) return 0;

        return p.Type switch
        {
            PickupType.Health => 0xFF44FF44u,
            PickupType.AtomicHealth => 0xFF00FFFFu,
            PickupType.Armor => 0xFF4488FFu,
            PickupType.Ammo => 0xFFFFDD44u,
            PickupType.Shotgun => 0xFFBB9966u,
            PickupType.Ripper => 0xFF888888u,
            PickupType.RPG => 0xFF66BB66u,
            PickupType.Medkit => 0xFFFF4444u,
            PickupType.Jetpack => 0xFFFF8800u,
            PickupType.Steroids => 0xFFFFFF00u,
            PickupType.KeyRed => 0xFFFF4444u,
            PickupType.KeyBlue => 0xFF4444FFu,
            PickupType.KeyYellow => 0xFFFFFF44u,
            PickupType.Exit => 0xFFFFFFFFu,
            _ => 0xFFFFFFFFu
        };
    }

    double Dist(double x, double y) => (x - _px_pos) * (x - _px_pos) + (y - _py) * (y - _py);

    void RenderWeapon()
    {
        int weaponW = 120, weaponH = 90;
        int baseX = W / 2 - weaponW / 2;
        int baseY = H - weaponH + (_shooting ? -20 : 0);
        int bob = (int)(Math.Sin(_gameTime * 10) * 4);

        uint weaponColor = _currentWeapon switch
        {
            0 => 0xFFDDAAAAu,
            1 => 0xFF666677u,
            2 => 0xFF886644u,
            3 => 0xFF555566u,
            4 => 0xFF446655u,
            5 => 0xFF446644u,
            _ => 0xFF666666u
        };

        for (int y = Math.Max(0, baseY + bob); y < H; y++)
        {
            for (int x = Math.Max(0, baseX); x < Math.Min(W, baseX + weaponW); x++)
            {
                double rx = (x - baseX - weaponW / 2.0) / weaponW;
                double ry = (y - baseY - bob) / (double)weaponH;

                if (_currentWeapon == 0)
                {
                    if (Math.Abs(rx) < 0.35 && ry > 0.25)
                        _px[y * W + x] = weaponColor;
                }
                else
                {
                    if (Math.Abs(rx) < 0.12 && ry > 0.15)
                        _px[y * W + x] = weaponColor;
                    if (Math.Abs(rx) < 0.22 && ry > 0.55)
                        _px[y * W + x] = weaponColor;
                    if (_currentWeapon == 4 && Math.Abs(rx) < 0.18 && ry > 0.1 && ry < 0.35)
                        _px[y * W + x] = 0xFF335533u;
                }
            }
        }

        if (_shooting && _shootFrame < 4 && _currentWeapon > 0 && _currentWeapon != 5)
        {
            int flashY = baseY + bob - 25;
            for (int y = Math.Max(0, flashY); y < Math.Min(H, flashY + 35); y++)
            {
                for (int x = W / 2 - 18; x < W / 2 + 18; x++)
                {
                    double d = Math.Sqrt((x - W / 2) * (x - W / 2) + (y - flashY - 17) * (y - flashY - 17));
                    if (d < 18)
                    {
                        uint intensity = (uint)(255 * (1 - d / 18));
                        _px[y * W + x] = 0xFF000000 | (intensity << 16) | ((intensity * 3 / 4) << 8) | (intensity / 4);
                    }
                }
            }
        }
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

        DukeFace.Text = _hp > 75 ? "😎" : _hp > 50 ? "😤" : _hp > 25 ? "😠" : "🤬";
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

        if (_currentWeapon == 4) // RPG
        {
            _projectiles.Add(new Projectile
            {
                X = _px_pos, Y = _py,
                Dx = Math.Cos(_pa) * 0.25,
                Dy = Math.Sin(_pa) * 0.25,
                Damage = _weaponDamage[_currentWeapon],
                FromPlayer = true,
                IsRocket = true
            });
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
                HitscanShot(_pa + spread, _weaponDamage[_currentWeapon] / pellets);
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

        if (_gameOver || _victory)
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

        if (e.Key == Key.Tab) MinimapBorder.Visibility = MinimapBorder.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
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
        if (e.RightButton == MouseButtonState.Pressed && _currentWeapon == 5)
            DetonatePipeBombs();
    }

    void Restart()
    {
        _currentLevel = 1;
        _hp = 100; _armor = 0; _score = 0;
        _keys = new bool[3];
        _ammo = new[] { 999, 48, 12, 200, 10, 5 };
        _currentWeapon = 1;
        _medkits = 0; _steroids = 0; _hasJetpack = false; _jetpackFuel = 100;
        _gameOver = false; _levelComplete = false; _victory = false;
        GameOverScreen.Visibility = Visibility.Collapsed;
        VictoryScreen.Visibility = Visibility.Collapsed;
        LoadLevel(_currentLevel);
        CaptureMouse(true);
    }
    #endregion
}
