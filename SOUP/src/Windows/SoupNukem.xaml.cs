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
using Serilog;
using SOUP.Core;
using SOUP.Core.Models;

namespace SOUP.Windows;

public partial class SoupNukem : Window
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
    const int W = 640, H = 480, TEX = 64;  // Higher resolution for better sprite detail
    const int MAP_SIZE = 24;
    const double PI = Math.PI, PI2 = PI * 2;
    #endregion

    #region Textures
    static readonly uint[][] Textures = new uint[8][];
    static readonly uint[] FloorTex = new uint[TEX * TEX];
    static readonly uint[] CeilTex = new uint[TEX * TEX];
    #endregion

    #region Creepy-Silly Quotes
    static readonly string[] KillQuotes = {
        "They're smiling. Why are they smiling?",
        "Shhh... go to sleep now.",
        "You look so peaceful...",
        "Another friend for my collection!",
        "Do you hear them too?",
        "Your screensaver misses you.",
        "The spreadsheets remember everything.",
        "They're still watching. They're always watching.",
        "Was that... laughter?",
        "Don't worry. It only hurts forever. 🙂"
    };

    static readonly string[] LevelCompleteQuotes = {
        "The floor is satisfied... for now.",
        "They've stopped screaming.",
        "Deeper. We must go deeper.",
        "The walls are pleased with you. 🙂",
        "You can never leave. But you can try."
    };

    static readonly string[] HurtQuotes = {
        "That felt... familiar.",
        "The pain is just a reminder.",
        "They're inside the walls...",
        "Is that my blood or theirs?",
        "It's fine. Everything is fine. 🙂"
    };

    static readonly string[] KillStreakQuotes = {
        "They keep coming back...",
        "Why won't they stay down?",
        "The pile grows taller.",
        "Can you hear them whispering?",
        "They're still smiling.",
        "Is this what you wanted?",
        "W̷̧E̸̢ ̵̢S̴̛E̷͝E̸ ̵Y̸O̷U̸"
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
    int[] _ammo = { 999, 48, 12, 200, 10, 5, 8, 24 }; // Boot, Pistol, Shotgun, Ripper, RPG, Pipebomb, MiniRocket, Camera
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
    
    // ROBUST ANIMATION SYSTEM
    double _weaponRecoil = 0;        // Kick-back when firing
    double _weaponSwapAnim = 0;      // Weapon swap animation (0-1)
    int _weaponSwapFrom = 0;         // Previous weapon during swap
    bool _isSwappingWeapon = false;
    double _weaponIdleAnim = 0;      // Idle breathing animation
    double _weaponInspectAnim = 0;   // Weapon inspect animation (when idle too long)
    double _idleTimer = 0;           // Time since last action
    double _stepPhase = 0;           // Footstep phase for view bob
    double _landingSquash = 0;       // Squash effect when landing from jump
    double _killFlashTimer = 0;      // Flash effect on kill
    double _breathingPhase = 0;      // Subtle breathing animation
    double _heartbeatPhase = 0;      // Heartbeat pulse when low health
    double _creepyAmbientPhase = 0;  // Creepy ambient animations
    double _eyeBlinkTimer = 0;       // For blinking eyes on weapons
    double _screenWarpTimer = 0;     // Trippy screen warp effect
    double _deathAnimTimer = 0;      // Death animation progress
    
    // Particles for blood/explosion effects
    List<(double x, double y, double dx, double dy, double life, uint color)> _particles = new();

    // === OPTIMIZATION: Pre-allocated buffers to avoid GC pressure ===
    List<(double x, double y, double r, double g, double b, double intensity)> _lightBuffer = new(16);
    List<(double dist, object obj, int type)> _spriteBuffer = new(128);
    
    // === OPTIMIZATION: Cached trig values (updated per-frame) ===
    double _sinPa, _cosPa, _sinPaPlus, _cosPaPlus, _sinPaMinus, _cosPaMinus;

    // Weapons - Duke style
    readonly string[] _weaponNames = { "MIGHTY BOOT", "PISTOL", "SHOTGUN", "RIPPER", "RPG", "PIPE BOMB", "MINI ROCKET", "FLASH CAMERA" };

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
        catch (Exception ex)
        {
            // Sound playback is non-critical, log at verbose level
            Log.Verbose(ex, "Sound playback failed for {Filename}", filename);
        }
    }

    readonly int[] _weaponDamage = { 25, 12, 50, 8, 120, 150, 80, 0 };
    readonly double[] _weaponFireRate = { 0.5, 0.25, 0.9, 0.08, 1.2, 0.3, 0.9, 1.5 };
    readonly double[] _weaponSpread = { 0, 0.02, 0.12, 0.06, 0, 0, 0, 0 };
    double _cameraFlashTimer = 0; // Screen flash when using camera
    double _cameraCooldown = 0;   // Cooldown for middle-click camera
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
    
    // CAMPAIGN SYSTEM
    enum GameMode { Campaign, Endless }
    GameMode _gameMode = GameMode.Campaign;
    int _maxCampaignLevel = 5;
    bool _showingBriefing = false;
    
    // DIFFICULTY SYSTEM
    enum DifficultyLevel { Easy, Normal, Hard, Nightmare }
    DifficultyLevel _difficulty = DifficultyLevel.Normal;
    
    // Difficulty multipliers
    double DamageToPlayerMultiplier => _difficulty switch
    {
        DifficultyLevel.Easy => 0.5,
        DifficultyLevel.Normal => 1.0,
        DifficultyLevel.Hard => 1.5,
        DifficultyLevel.Nightmare => 2.0,
        _ => 1.0
    };
    
    double EnemyHealthMultiplier => _difficulty switch
    {
        DifficultyLevel.Easy => 0.7,
        DifficultyLevel.Normal => 1.0,
        DifficultyLevel.Hard => 1.3,
        DifficultyLevel.Nightmare => 1.8,
        _ => 1.0
    };
    
    double EnemyDamageMultiplier => _difficulty switch
    {
        DifficultyLevel.Easy => 0.6,
        DifficultyLevel.Normal => 1.0,
        DifficultyLevel.Hard => 1.4,
        DifficultyLevel.Nightmare => 2.0,
        _ => 1.0
    };
    
    double EnemySpeedMultiplier => _difficulty switch
    {
        DifficultyLevel.Easy => 0.8,
        DifficultyLevel.Normal => 1.0,
        DifficultyLevel.Hard => 1.15,
        DifficultyLevel.Nightmare => 1.3,
        _ => 1.0
    };
    
    int EnemyCountMultiplier => _difficulty switch
    {
        DifficultyLevel.Easy => 0,      // Fewer enemies
        DifficultyLevel.Normal => 0,
        DifficultyLevel.Hard => 3,      // More enemies
        DifficultyLevel.Nightmare => 6, // Many more enemies
        _ => 0
    };
    
    double PickupSpawnChance => _difficulty switch
    {
        DifficultyLevel.Easy => 1.3,    // More pickups
        DifficultyLevel.Normal => 1.0,
        DifficultyLevel.Hard => 0.8,    // Fewer pickups
        DifficultyLevel.Nightmare => 0.6,
        _ => 1.0
    };
    
    static readonly string[] DifficultyNames = { "EASY", "NORMAL", "HARD", "NIGHTMARE" };
    static readonly string[] DifficultyDescriptions = {
        "For those who just want to have fun.\nEnemies are weaker and you take less damage.",
        "The standard challenge.\nBalanced gameplay experience.",
        "For experienced players.\nEnemies hit harder and there are more of them.",
        "Are you insane?!\nMaximum enemy aggression. Good luck."
    };
    
    // Level themes and names
    static readonly string[] LevelNames = {
        "1-1: THE SPREADSHEET DIMENSION",
        "1-2: CUBICLE NIGHTMARE",
        "1-3: SERVER ROOM INFERNO",
        "1-4: THE BOARDROOM OF DOOM",
        "1-5: CEO'S LAIR (BOSS)"
    };
    
    static readonly string[] LevelBriefings = {
        "Your spreadsheets have come alive. The formulas whisper your name.\nClear the floor of possessed office supplies.\n\n\"Time to calculate some DAMAGE.\"",
        "The cubicles stretch infinitely. Coworkers shuffle in the darkness.\nTheir smiles are too wide. Find the exit before they find you.\n\n\"Looks like someone's getting... downsized.\"",
        "The servers hum with malevolent energy. Data corrupts everything it touches.\nDestroy the mainframe and escape the digital hellscape.\n\n\"Time to debug... PERMANENTLY.\"",
        "The boardroom meeting never ends. Executives with hollow eyes demand your TPS reports.\nSurvive the presentation from hell.\n\n\"Let's discuss your TERMINATION.\"",
        "The CEO awaits. A being of pure corporate malice.\nOnly one of you is leaving this performance review alive.\n\n\"Your annual review is... OVERDUE.\""
    };
    
    // Fog and atmosphere per level
    double _fogDensity = 0.0;
    uint _fogColor = 0xFF000000;
    uint _ambientColor = 0xFF606060;
    double _lightFlicker = 0;
    double _briefingTimer = 0;

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
        
        // Stun from camera flash
        public double StunTimer;
        public bool IsStunned => StunTimer > 0;
        
        // === ENHANCED AI BEHAVIORS ===
        public EnemyState State = EnemyState.Idle;
        public double StateTimer;
        public double PatrolTargetX, PatrolTargetY;
        public double LastKnownPlayerX, LastKnownPlayerY;
        public int PatrolPointIndex;
        public double RetreatTimer;      // Time spent retreating
        public double AggressionLevel;   // How aggressive (0-1)
        public double FlankingAngle;     // Angle for flanking maneuver
        public bool CanSeePlayer;        // Line of sight check result
        public double LastAttackTime;    // For burst fire patterns
        public int BurstCount;           // Shots fired in current burst
    }
    
    enum EnemyState
    {
        Idle,           // Standing still, not alerted
        Patrol,         // Walking between patrol points
        Alert,          // Heard something, looking around
        Chase,          // Actively pursuing player
        Attack,         // In combat, firing at player
        Flanking,       // Moving to flank player
        Retreat,        // Low HP, backing away
        TakingCover,    // Behind cover, peeking out
        Stunned         // Camera flashed
    }

    enum PickupType { Health, Armor, Ammo, Shotgun, Ripper, RPG, KeyRed, KeyBlue, KeyYellow, Medkit, Jetpack, Steroids, AtomicHealth, Exit, Invincibility, DamageBoost, ExplodingBarrel }

    class Pickup
    {
        public double X, Y, BobOffset;
        public PickupType Type;
        public bool Collected;
        public double Hp = 30; // For destructible pickups like barrels
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
    
    // === ENVIRONMENTAL HAZARDS ===
    class ExplodingBarrel
    {
        public double X, Y, Hp;
        public bool Exploded;
        public double BobOffset;
    }
    
    List<ExplodingBarrel> _barrels = new();
    
    // Global list of flying body parts (ragdolls)
    List<BodyPart> _bodyParts = new();
    #endregion

    public SoupNukem()
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
    
    // Perlin-like noise for better textures
    double PerlinNoise(double x, double y, int octaves = 4)
    {
        double result = 0;
        double amplitude = 1;
        double frequency = 1;
        double maxValue = 0;
        
        for (int i = 0; i < octaves; i++)
        {
            double nx = x * frequency;
            double ny = y * frequency;
            
            // Simple value noise interpolation
            int ix = (int)Math.Floor(nx);
            int iy = (int)Math.Floor(ny);
            double fx = nx - ix;
            double fy = ny - iy;
            
            // Smoothstep interpolation
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            
            // Hash function for pseudo-random
            double Hash(int px, int py) => ((px * 374761393 + py * 668265263) & 0xFFFFFF) / (double)0xFFFFFF;
            
            double n00 = Hash(ix, iy);
            double n10 = Hash(ix + 1, iy);
            double n01 = Hash(ix, iy + 1);
            double n11 = Hash(ix + 1, iy + 1);
            
            double nx0 = n00 * (1 - fx) + n10 * fx;
            double nx1 = n01 * (1 - fx) + n11 * fx;
            double n = nx0 * (1 - fy) + nx1 * fy;
            
            result += n * amplitude;
            maxValue += amplitude;
            amplitude *= 0.5;
            frequency *= 2;
        }
        
        return result / maxValue;
    }
    
    void GenerateTextures()
    {
        for (int t = 0; t < 8; t++)
        {
            Textures[t] = new uint[TEX * TEX];

            for (int y = 0; y < TEX; y++)
            {
                for (int x = 0; x < TEX; x++)
                {
                    double noise = PerlinNoise(x * 0.1 + t * 100, y * 0.1, 4);
                    double detailNoise = PerlinNoise(x * 0.3 + t * 50, y * 0.3, 2);
                    
                    uint r, g, b;
                    
                    switch (t)
                    {
                        case 0: // Concrete - detailed cracks and wear
                            {
                                double baseV = 95 + noise * 35 + detailNoise * 15;
                                bool crack = PerlinNoise(x * 0.5, y * 0.5, 2) < 0.15;
                                bool stain = PerlinNoise(x * 0.08 + 50, y * 0.08, 2) > 0.7;
                                baseV = baseV - (crack ? 40 : 0) - (stain ? 20 : 0);
                                r = (uint)Math.Clamp(baseV + 5, 0, 255);
                                g = (uint)Math.Clamp(baseV, 0, 255);
                                b = (uint)Math.Clamp(baseV - 10, 0, 255);
                            }
                            break;
                            
                        case 1: // Red brick with mortar
                            {
                                int brickX = x % 32;
                                int brickY = y % 16;
                                bool isOffset = (y / 16) % 2 == 1;
                                if (isOffset) brickX = (x + 16) % 32;
                                bool mortar = brickY < 2 || brickX < 2;
                                
                                if (mortar)
                                {
                                    double mv = 180 + noise * 30;
                                    r = (uint)Math.Clamp(mv, 0, 255);
                                    g = (uint)Math.Clamp(mv - 10, 0, 255);
                                    b = (uint)Math.Clamp(mv - 15, 0, 255);
                                }
                                else
                                {
                                    double brickNoise = PerlinNoise(x * 0.2 + y * 0.1, y * 0.2, 3);
                                    r = (uint)Math.Clamp(155 + brickNoise * 50 - _rnd.Next(20), 0, 255);
                                    g = (uint)Math.Clamp(55 + brickNoise * 30, 0, 255);
                                    b = (uint)Math.Clamp(45 + brickNoise * 20, 0, 255);
                                }
                            }
                            break;
                            
                        case 2: // Blue tech panel with glow lines
                            {
                                bool panel = (x % 32 < 3 || x % 32 > 28) || (y % 32 < 3 || y % 32 > 28);
                                bool glowLine = (y % 32 == 16) || (x % 32 == 16);
                                bool circuitry = ((x + y) % 8 == 0) && (x % 16 > 4 && x % 16 < 12);
                                
                                if (glowLine)
                                {
                                    double pulse = 0.7 + 0.3 * Math.Sin(x * 0.2 + y * 0.1);
                                    r = (uint)(80 * pulse);
                                    g = (uint)(180 * pulse);
                                    b = (uint)(255 * pulse);
                                }
                                else if (panel)
                                {
                                    r = (uint)Math.Clamp(100 + noise * 20, 0, 255);
                                    g = (uint)Math.Clamp(120 + noise * 20, 0, 255);
                                    b = (uint)Math.Clamp(160 + noise * 30, 0, 255);
                                }
                                else
                                {
                                    r = (uint)Math.Clamp(35 + detailNoise * 25 + (circuitry ? 30 : 0), 0, 255);
                                    g = (uint)Math.Clamp(65 + detailNoise * 30 + (circuitry ? 50 : 0), 0, 255);
                                    b = (uint)Math.Clamp(110 + noise * 35 + (circuitry ? 60 : 0), 0, 255);
                                }
                            }
                            break;
                            
                        case 3: // Gold/Yellow ornate
                            {
                                bool border = x < 4 || x > 59 || y < 4 || y > 59;
                                bool innerBorder = x > 6 && x < 57 && y > 6 && y < 57 && (x < 10 || x > 53 || y < 10 || y > 53);
                                double shine = Math.Sin(x * 0.2) * Math.Cos(y * 0.2) * 0.5 + 0.5;
                                
                                if (border || innerBorder)
                                {
                                    r = (uint)Math.Clamp(200 + shine * 55, 0, 255);
                                    g = (uint)Math.Clamp(170 + shine * 50, 0, 255);
                                    b = (uint)Math.Clamp(60 + shine * 30, 0, 255);
                                }
                                else
                                {
                                    r = (uint)Math.Clamp(140 + noise * 40 + shine * 30, 0, 255);
                                    g = (uint)Math.Clamp(120 + noise * 35 + shine * 25, 0, 255);
                                    b = (uint)Math.Clamp(40 + noise * 20, 0, 255);
                                }
                            }
                            break;
                            
                        case 4: // Green slime/organic
                            {
                                double slimeNoise = PerlinNoise(x * 0.15, y * 0.15, 5);
                                double bubble = PerlinNoise(x * 0.4, y * 0.4, 2);
                                bool isBubble = bubble > 0.75;
                                
                                r = (uint)Math.Clamp(40 + slimeNoise * 30 + (isBubble ? 40 : 0), 0, 255);
                                g = (uint)Math.Clamp(100 + slimeNoise * 80 + (isBubble ? 60 : 0), 0, 255);
                                b = (uint)Math.Clamp(50 + slimeNoise * 40, 0, 255);
                            }
                            break;
                            
                        case 5: // Brushed metal
                            {
                                double streak = PerlinNoise(x * 0.02, y * 0.5, 2);
                                double scratch = PerlinNoise(x * 0.8, y * 0.8, 1) > 0.85 ? 30 : 0;
                                bool rivet = ((x % 32 == 4 || x % 32 == 28) && (y % 32 == 4 || y % 32 == 28));
                                double baseV = 100 + streak * 45 + scratch;
                                
                                if (rivet)
                                {
                                    double rivetDist = Math.Sqrt((x % 32 - (x % 32 < 16 ? 4 : 28)) * (x % 32 - (x % 32 < 16 ? 4 : 28)) +
                                                                (y % 32 - (y % 32 < 16 ? 4 : 28)) * (y % 32 - (y % 32 < 16 ? 4 : 28)));
                                    if (rivetDist < 3) baseV += 40;
                                }
                                
                                r = (uint)Math.Clamp(baseV + 15, 0, 255);
                                g = (uint)Math.Clamp(baseV + 10, 0, 255);
                                b = (uint)Math.Clamp(baseV + 20, 0, 255);
                            }
                            break;
                            
                        case 6: // Wood grain
                            {
                                double grain = Math.Sin((x + PerlinNoise(x * 0.05, y * 0.1, 2) * 20) * 0.3) * 0.5 + 0.5;
                                double knot = PerlinNoise(x * 0.08, y * 0.08, 3);
                                bool isKnot = knot > 0.78;
                                
                                double baseR = 110 + grain * 40 - (isKnot ? 30 : 0);
                                double baseG = 70 + grain * 30 - (isKnot ? 25 : 0);
                                double baseB = 45 + grain * 20 - (isKnot ? 15 : 0);
                                
                                r = (uint)Math.Clamp(baseR + noise * 15, 0, 255);
                                g = (uint)Math.Clamp(baseG + noise * 10, 0, 255);
                                b = (uint)Math.Clamp(baseB + noise * 8, 0, 255);
                            }
                            break;
                            
                        default: // Dark door/metal
                            {
                                bool frame = x < 8 || x > 55 || y < 4 || y > 59;
                                bool handle = (x > 45 && x < 52) && (y > 28 && y < 36);
                                double metalShine = Math.Sin(x * 0.15) * Math.Cos(y * 0.15) * 0.3 + 0.7;
                                
                                if (handle)
                                {
                                    r = (uint)Math.Clamp(180 * metalShine, 0, 255);
                                    g = (uint)Math.Clamp(160 * metalShine, 0, 255);
                                    b = (uint)Math.Clamp(60 * metalShine, 0, 255);
                                }
                                else if (frame)
                                {
                                    r = (uint)Math.Clamp(50 + noise * 20, 0, 255);
                                    g = (uint)Math.Clamp(45 + noise * 18, 0, 255);
                                    b = (uint)Math.Clamp(55 + noise * 22, 0, 255);
                                }
                                else
                                {
                                    r = (uint)Math.Clamp(30 + noise * 15, 0, 255);
                                    g = (uint)Math.Clamp(28 + noise * 14, 0, 255);
                                    b = (uint)Math.Clamp(40 + noise * 18, 0, 255);
                                }
                            }
                            break;
                    }
                    
                    Textures[t][y * TEX + x] = 0xFF000000 | (r << 16) | (g << 8) | b;
                }
            }
        }

        // Floor - detailed industrial tiles with rust and grime
        for (int y = 0; y < TEX; y++)
        {
            for (int x = 0; x < TEX; x++)
            {
                double noise = PerlinNoise(x * 0.1, y * 0.1, 3);
                double rust = PerlinNoise(x * 0.08 + 100, y * 0.08, 2);
                bool edge = x % 32 < 2 || y % 32 < 2;
                bool rivet = ((x % 16 == 4 || x % 16 == 12) && (y % 16 == 4 || y % 16 == 12));
                bool isRusty = rust > 0.65;
                
                double baseV = 55 + noise * 25;
                if (edge) baseV -= 25;
                if (rivet) baseV += 35;
                
                uint r, g, b;
                if (isRusty && !edge && !rivet)
                {
                    r = (uint)Math.Clamp(baseV + 40, 0, 255);
                    g = (uint)Math.Clamp(baseV - 5, 0, 255);
                    b = (uint)Math.Clamp(baseV - 15, 0, 255);
                }
                else
                {
                    r = (uint)Math.Clamp(baseV + 5, 0, 255);
                    g = (uint)Math.Clamp(baseV, 0, 255);
                    b = (uint)Math.Clamp(baseV - 8, 0, 255);
                }
                
                FloorTex[y * TEX + x] = 0xFF000000 | (r << 16) | (g << 8) | b;
            }
        }

        // Ceiling - tech panels with glowing light strips
        for (int y = 0; y < TEX; y++)
        {
            for (int x = 0; x < TEX; x++)
            {
                double noise = PerlinNoise(x * 0.1 + 200, y * 0.1, 2);
                bool panelEdge = (x % 32 < 3 || x % 32 > 28) || (y % 32 < 3 || y % 32 > 28);
                bool lightStrip = (x > 26 && x < 38) && (y > 26 && y < 38);
                bool lightEdge = lightStrip && (x == 27 || x == 37 || y == 27 || y == 37);
                
                uint r, g, b;
                if (lightStrip && !lightEdge)
                {
                    double glow = 0.8 + 0.2 * Math.Sin(x * 0.3 + y * 0.3);
                    r = (uint)(255 * glow);
                    g = (uint)(250 * glow);
                    b = (uint)(200 * glow);
                }
                else if (lightEdge)
                {
                    r = 80; g = 80; b = 90;
                }
                else if (panelEdge)
                {
                    r = (uint)Math.Clamp(50 + noise * 15, 0, 255);
                    g = (uint)Math.Clamp(50 + noise * 15, 0, 255);
                    b = (uint)Math.Clamp(60 + noise * 20, 0, 255);
                }
                else
                {
                    r = (uint)Math.Clamp(35 + noise * 18, 0, 255);
                    g = (uint)Math.Clamp(35 + noise * 18, 0, 255);
                    b = (uint)Math.Clamp(45 + noise * 22, 0, 255);
                }
                
                CeilTex[y * TEX + x] = 0xFF000000 | (r << 16) | (g << 8) | b;
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
            Log.Warning(ex, "Failed to save score");
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
        _barrels.Clear();

        for (int i = 0; i < MAP_SIZE * MAP_SIZE; i++) { _map[i] = 0; _floorHeights[i] = 0; _hazardTiles[i] = false; }
        
        // Reset kill streak
        _killStreak = 0;
        _killStreakTimer = 0;

        // Campaign mode: load designed levels
        if (_gameMode == GameMode.Campaign && level <= _maxCampaignLevel)
        {
            LoadCampaignLevel(level);
        }
        else
        {
            // Endless mode or beyond campaign: generate random levels
            GenerateRandomLevel();
        }
        
        // Add exploding barrels to the level
        PlaceBarrels();

        _totalEnemies = _enemies.Count;
        _totalSecrets = _pickups.Count(p => p.Type == PickupType.AtomicHealth);
        _kills = 0;
        _secretsFound = 0;
        _pitch = 0;
        _pz = 0;
        _pzVel = 0;

        // Update title based on mode
        if (_gameMode == GameMode.Campaign && level <= _maxCampaignLevel)
        {
            Title = $"S.O.U.P NUKEM - {LevelNames[level - 1]}";
        }
        else
        {
            Title = $"S.O.U.P NUKEM - Endless Stage {_currentLevel}";
        }
    }
    
    void LoadCampaignLevel(int level)
    {
        // Set level atmosphere
        SetLevelAtmosphere(level);
        
        switch (level)
        {
            case 1: GenerateCampaignLevel1(); break;
            case 2: GenerateCampaignLevel2(); break;
            case 3: GenerateCampaignLevel3(); break;
            case 4: GenerateCampaignLevel4(); break;
            case 5: GenerateCampaignLevel5_Boss(); break;
            default: GenerateRandomLevel(); break;
        }
        
        // Show briefing for campaign levels
        if (level <= _maxCampaignLevel)
        {
            ShowBriefing(level);
        }
    }
    
    void SetLevelAtmosphere(int level)
    {
        // Each level has unique atmosphere
        switch (level)
        {
            case 1: // Spreadsheet Dimension - cold blue office lighting
                _fogDensity = 0.015;
                _fogColor = 0xFF202040;
                _ambientColor = 0xFF606080;
                break;
            case 2: // Cubicle Nightmare - dark green fluorescent flicker
                _fogDensity = 0.025;
                _fogColor = 0xFF102010;
                _ambientColor = 0xFF405040;
                _lightFlicker = 0.1;
                break;
            case 3: // Server Room Inferno - red heat haze
                _fogDensity = 0.02;
                _fogColor = 0xFF301010;
                _ambientColor = 0xFF804020;
                break;
            case 4: // Boardroom of Doom - dim yellow/brown
                _fogDensity = 0.018;
                _fogColor = 0xFF201810;
                _ambientColor = 0xFF605030;
                break;
            case 5: // CEO's Lair - purple eldritch glow
                _fogDensity = 0.03;
                _fogColor = 0xFF201030;
                _ambientColor = 0xFF604080;
                break;
            default: // Endless - varies
                _fogDensity = 0.01 + (_currentLevel * 0.002);
                _fogColor = 0xFF101020;
                _ambientColor = 0xFF505060;
                break;
        }
    }
    
    void ShowBriefing(int level)
    {
        if (level > 0 && level <= LevelBriefings.Length)
        {
            _showingBriefing = true;
            _briefingTimer = 0;
            _paused = true;
            
            // Update briefing UI
            BriefingPanel.Visibility = Visibility.Visible;
            BriefingTitle.Text = LevelNames[level - 1];
            BriefingText.Text = LevelBriefings[level - 1];
        }
    }
    
    void CloseBriefing()
    {
        _showingBriefing = false;
        _paused = false;
        BriefingPanel.Visibility = Visibility.Collapsed;
        CaptureMouse(true);
        SayQuote("Let's rock!");
    }
    
    // CAMPAIGN LEVEL 1: The Spreadsheet Dimension
    void GenerateCampaignLevel1()
    {
        _px_pos = 2.5; _py = 2.5; _pa = 0;
        
        // Outer walls (office building exterior)
        for (int i = 0; i < MAP_SIZE; i++)
        {
            _map[i] = 2;
            _map[(MAP_SIZE - 1) * MAP_SIZE + i] = 2;
            _map[i * MAP_SIZE] = 2;
            _map[i * MAP_SIZE + MAP_SIZE - 1] = 2;
        }
        
        // Reception area (open with desk)
        SetWalls(5, 1, 5, 5, 1);
        
        // Cubicle farm (grid pattern)
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 4; col++)
            {
                int bx = 7 + col * 4;
                int by = 3 + row * 5;
                // Cubicle walls (partial)
                SetWall(bx, by, 3);
                SetWall(bx + 2, by, 3);
                SetWall(bx, by + 2, 3);
            }
        }
        
        // Break room
        SetWalls(2, 10, 5, 10, 1);
        SetWalls(2, 10, 2, 14, 1);
        SetWalls(2, 14, 5, 14, 1);
        AddPickup(PickupType.Health, 3, 12);
        
        // Server closet (secret area)
        SetWalls(20, 15, 22, 15, 5);
        SetWalls(20, 15, 20, 19, 5);
        SetWalls(20, 19, 22, 19, 5);
        AddPickup(PickupType.AtomicHealth, 21, 17);
        AddDoor(20, 17, 0);
        
        // Manager's office (locked)
        SetWalls(18, 2, 22, 2, 6);
        SetWalls(18, 2, 18, 6, 6);
        SetWalls(18, 6, 22, 6, 6);
        AddPickup(PickupType.KeyRed, 14, 10);
        AddDoor(18, 4, 1);
        AddPickup(PickupType.Shotgun, 20, 4);
        
        // Exit in manager's office
        AddPickup(PickupType.Exit, 21, 4);
        
        // Enemies - possessed office workers
        AddEnemy(EnemyType.Trooper, 8, 5);
        AddEnemy(EnemyType.Trooper, 12, 5);
        AddEnemy(EnemyType.Trooper, 16, 5);
        AddEnemy(EnemyType.Trooper, 8, 10);
        AddEnemy(EnemyType.Trooper, 12, 10);
        AddEnemy(EnemyType.PigCop, 16, 15);
        AddEnemy(EnemyType.Trooper, 10, 18);
        AddEnemy(EnemyType.Trooper, 15, 18);
        
        // Pickups scattered in cubicles
        AddPickup(PickupType.Ammo, 9, 4);
        AddPickup(PickupType.Ammo, 13, 8);
        AddPickup(PickupType.Health, 17, 12);
        AddPickup(PickupType.Armor, 6, 20);
    }
    
    // CAMPAIGN LEVEL 2: Cubicle Nightmare
    void GenerateCampaignLevel2()
    {
        _px_pos = 12; _py = 21; _pa = -PI / 2;
        
        // Outer walls
        for (int i = 0; i < MAP_SIZE; i++)
        {
            _map[i] = 1;
            _map[(MAP_SIZE - 1) * MAP_SIZE + i] = 1;
            _map[i * MAP_SIZE] = 1;
            _map[i * MAP_SIZE + MAP_SIZE - 1] = 1;
        }
        
        // Maze of cubicles (nightmare labyrinth)
        // Create winding corridors
        SetWalls(3, 3, 3, 18, 3);
        SetWalls(3, 3, 10, 3, 3);
        SetWalls(6, 6, 6, 15, 3);
        SetWalls(6, 6, 12, 6, 3);
        SetWalls(9, 9, 9, 18, 3);
        SetWalls(9, 9, 15, 9, 3);
        SetWalls(12, 3, 12, 12, 3);
        SetWalls(12, 15, 12, 20, 3);
        SetWalls(15, 6, 15, 15, 3);
        SetWalls(15, 6, 20, 6, 3);
        SetWalls(18, 3, 18, 9, 3);
        SetWalls(18, 12, 18, 20, 3);
        SetWalls(3, 18, 9, 18, 3);
        SetWalls(15, 18, 20, 18, 3);
        
        // Pits of despair (lower floor sections)
        for (int y = 7; y <= 8; y++)
            for (int x = 7; x <= 8; x++)
                _floorHeights[y * MAP_SIZE + x] = -20;
        
        // Raised platform
        for (int y = 13; y <= 14; y++)
            for (int x = 13; x <= 14; x++)
            {
                _floorHeights[y * MAP_SIZE + x] = 24;
                _map[y * MAP_SIZE + x] = 0;
            }
        AddPickup(PickupType.Ripper, 13, 13);
        
        // Green hazard tiles (toxic coffee spills)
        _hazardTiles[10 * MAP_SIZE + 5] = true;
        _hazardTiles[10 * MAP_SIZE + 4] = true;
        _hazardTiles[16 * MAP_SIZE + 10] = true;
        _hazardTiles[16 * MAP_SIZE + 11] = true;
        
        // Locked supply room
        SetWalls(19, 10, 22, 10, 5);
        SetWalls(19, 10, 19, 14, 5);
        SetWalls(19, 14, 22, 14, 5);
        AddDoor(19, 12, 2);
        AddPickup(PickupType.KeyBlue, 4, 15);
        AddPickup(PickupType.RPG, 21, 12);
        
        // Exit through elevator
        AddPickup(PickupType.Exit, 20, 3);
        
        // More enemies - things lurk in cubicles
        AddEnemy(EnemyType.Trooper, 5, 5);
        AddEnemy(EnemyType.Trooper, 8, 12);
        AddEnemy(EnemyType.PigCop, 11, 5);
        AddEnemy(EnemyType.PigCop, 14, 12);
        AddEnemy(EnemyType.Enforcer, 17, 8);
        AddEnemy(EnemyType.Trooper, 5, 16);
        AddEnemy(EnemyType.Octabrain, 16, 16);
        AddEnemy(EnemyType.PigCop, 20, 15);
        AddEnemy(EnemyType.Trooper, 10, 20);
        AddEnemy(EnemyType.Enforcer, 2, 10);
        
        // Pickups
        AddPickup(PickupType.Health, 2, 5);
        AddPickup(PickupType.Ammo, 8, 8);
        AddPickup(PickupType.Armor, 14, 3);
        AddPickup(PickupType.Health, 17, 20);
        AddPickup(PickupType.Medkit, 5, 10);
        AddPickup(PickupType.AtomicHealth, 2, 2); // Secret corner
    }
    
    // CAMPAIGN LEVEL 3: Server Room Inferno
    void GenerateCampaignLevel3()
    {
        _px_pos = 2.5; _py = 12; _pa = 0;
        
        // Outer walls
        for (int i = 0; i < MAP_SIZE; i++)
        {
            _map[i] = 5;
            _map[(MAP_SIZE - 1) * MAP_SIZE + i] = 5;
            _map[i * MAP_SIZE] = 5;
            _map[i * MAP_SIZE + MAP_SIZE - 1] = 5;
        }
        
        // Server rack rows (long corridors)
        for (int row = 0; row < 5; row++)
        {
            int y = 3 + row * 4;
            SetWalls(4, y, 10, y, 5);
            SetWalls(14, y, 20, y, 5);
        }
        
        // Central cooling unit (raised platform)
        SetWalls(11, 9, 13, 9, 2);
        SetWalls(11, 9, 11, 15, 2);
        SetWalls(13, 9, 13, 15, 2);
        SetWalls(11, 15, 13, 15, 2);
        _floorHeights[12 * MAP_SIZE + 12] = 32;
        AddPickup(PickupType.Jetpack, 12, 12);
        
        // Lava floor sections (overheated servers)
        for (int x = 5; x <= 9; x++)
            for (int y = 8; y <= 10; y++)
                _hazardTiles[y * MAP_SIZE + x] = true;
        for (int x = 15; x <= 19; x++)
            for (int y = 13; y <= 15; y++)
                _hazardTiles[y * MAP_SIZE + x] = true;
        
        // Maintenance tunnel (lower)
        for (int x = 2; x <= 4; x++)
            for (int y = 18; y <= 20; y++)
            {
                _floorHeights[y * MAP_SIZE + x] = -16;
                _map[y * MAP_SIZE + x] = 0;
            }
        AddPickup(PickupType.AtomicHealth, 3, 19);
        
        // Locked mainframe room (exit)
        SetWalls(18, 18, 22, 18, 6);
        SetWalls(18, 18, 18, 22, 6);
        AddDoor(18, 20, 3);
        AddPickup(PickupType.KeyYellow, 8, 5);
        AddPickup(PickupType.Exit, 20, 20);
        
        // Data creatures
        AddEnemy(EnemyType.Enforcer, 6, 4);
        AddEnemy(EnemyType.Enforcer, 17, 4);
        AddEnemy(EnemyType.Octabrain, 6, 12);
        AddEnemy(EnemyType.Octabrain, 17, 12);
        AddEnemy(EnemyType.PigCop, 12, 6);
        AddEnemy(EnemyType.PigCop, 12, 18);
        AddEnemy(EnemyType.Enforcer, 3, 6);
        AddEnemy(EnemyType.Enforcer, 20, 8);
        AddEnemy(EnemyType.Trooper, 8, 16);
        AddEnemy(EnemyType.Trooper, 16, 6);
        AddEnemy(EnemyType.Octabrain, 10, 20);
        
        // Pickups
        AddPickup(PickupType.Ammo, 5, 2);
        AddPickup(PickupType.Ammo, 18, 2);
        AddPickup(PickupType.Health, 12, 3);
        AddPickup(PickupType.Armor, 2, 8);
        AddPickup(PickupType.Health, 21, 15);
        AddPickup(PickupType.Steroids, 15, 10);
    }
    
    // CAMPAIGN LEVEL 4: The Boardroom of Doom
    void GenerateCampaignLevel4()
    {
        _px_pos = 12; _py = 21; _pa = -PI / 2;
        
        // Outer walls (wood paneled)
        for (int i = 0; i < MAP_SIZE; i++)
        {
            _map[i] = 6;
            _map[(MAP_SIZE - 1) * MAP_SIZE + i] = 6;
            _map[i * MAP_SIZE] = 6;
            _map[i * MAP_SIZE + MAP_SIZE - 1] = 6;
        }
        
        // Grand boardroom table (center)
        SetWalls(8, 8, 16, 8, 6);
        SetWalls(8, 8, 8, 16, 6);
        SetWalls(16, 8, 16, 16, 6);
        SetWalls(8, 16, 16, 16, 6);
        // Inner table (walkable around)
        SetWalls(10, 10, 14, 10, 7);
        SetWalls(10, 10, 10, 14, 7);
        SetWalls(14, 10, 14, 14, 7);
        SetWalls(10, 14, 14, 14, 7);
        
        // Executive offices (corners)
        SetWalls(2, 2, 5, 2, 6);
        SetWalls(2, 2, 2, 5, 6);
        SetWalls(2, 5, 5, 5, 6);
        AddPickup(PickupType.Invincibility, 3, 3);
        
        SetWalls(18, 2, 21, 2, 6);
        SetWalls(21, 2, 21, 5, 6);
        SetWalls(18, 5, 21, 5, 6);
        AddPickup(PickupType.DamageBoost, 20, 3);
        
        // Waiting area with chairs
        SetWalls(2, 18, 6, 18, 3);
        SetWalls(2, 20, 6, 20, 3);
        
        // CEO's elevator (locked - all keys needed!)
        SetWalls(10, 2, 14, 2, 5);
        SetWalls(10, 2, 10, 5, 5);
        SetWalls(14, 2, 14, 5, 5);
        AddDoor(12, 5, 1);
        AddPickup(PickupType.Exit, 12, 3);
        
        // Keys scattered
        AddPickup(PickupType.KeyRed, 3, 20);
        AddPickup(PickupType.KeyBlue, 20, 20);
        AddPickup(PickupType.KeyYellow, 12, 12);
        
        // Executives attack!
        AddEnemy(EnemyType.Enforcer, 4, 10);
        AddEnemy(EnemyType.Enforcer, 20, 10);
        AddEnemy(EnemyType.Enforcer, 4, 15);
        AddEnemy(EnemyType.Enforcer, 20, 15);
        AddEnemy(EnemyType.Octabrain, 7, 12);
        AddEnemy(EnemyType.Octabrain, 17, 12);
        AddEnemy(EnemyType.PigCop, 12, 7);
        AddEnemy(EnemyType.PigCop, 12, 17);
        AddEnemy(EnemyType.Trooper, 6, 6);
        AddEnemy(EnemyType.Trooper, 18, 6);
        AddEnemy(EnemyType.Trooper, 6, 18);
        AddEnemy(EnemyType.Trooper, 18, 18);
        AddEnemy(EnemyType.Enforcer, 10, 19);
        AddEnemy(EnemyType.Enforcer, 14, 19);
        
        // Pickups
        AddPickup(PickupType.Shotgun, 2, 12);
        AddPickup(PickupType.Ammo, 22, 12);
        AddPickup(PickupType.Health, 9, 6);
        AddPickup(PickupType.Health, 15, 6);
        AddPickup(PickupType.Armor, 12, 19);
        AddPickup(PickupType.AtomicHealth, 5, 4);
        AddPickup(PickupType.Medkit, 19, 4);
    }
    
    // CAMPAIGN LEVEL 5: CEO's Lair (BOSS LEVEL)
    void GenerateCampaignLevel5_Boss()
    {
        _px_pos = 12; _py = 20; _pa = -PI / 2;
        
        // Outer walls (dark eldritch)
        for (int i = 0; i < MAP_SIZE; i++)
        {
            _map[i] = 4;
            _map[(MAP_SIZE - 1) * MAP_SIZE + i] = 4;
            _map[i * MAP_SIZE] = 4;
            _map[i * MAP_SIZE + MAP_SIZE - 1] = 4;
        }
        
        // Boss arena (circular-ish)
        SetWalls(4, 4, 4, 20, 4);
        SetWalls(20, 4, 20, 20, 4);
        SetWalls(4, 4, 20, 4, 4);
        SetWalls(4, 20, 20, 20, 4);
        
        // Pillars (cover)
        SetWall(8, 8, 5);
        SetWall(16, 8, 5);
        SetWall(8, 16, 5);
        SetWall(16, 16, 5);
        SetWall(12, 10, 5);
        SetWall(12, 14, 5);
        
        // Raised throne area
        for (int x = 10; x <= 14; x++)
            for (int y = 5; y <= 7; y++)
            {
                _floorHeights[y * MAP_SIZE + x] = 20;
                _map[y * MAP_SIZE + x] = 0;
            }
        
        // CEO BOSS - BattleLord variant
        AddEnemy(EnemyType.BattleLord, 12, 6);
        
        // Minion spawns (executives)
        AddEnemy(EnemyType.Enforcer, 6, 10);
        AddEnemy(EnemyType.Enforcer, 18, 10);
        AddEnemy(EnemyType.Enforcer, 6, 14);
        AddEnemy(EnemyType.Enforcer, 18, 14);
        AddEnemy(EnemyType.Octabrain, 10, 12);
        AddEnemy(EnemyType.Octabrain, 14, 12);
        
        // Hazard ring around throne
        for (int x = 8; x <= 16; x++)
        {
            _hazardTiles[8 * MAP_SIZE + x] = true;
        }
        
        // Weapon/ammo around arena
        AddPickup(PickupType.RPG, 6, 6);
        AddPickup(PickupType.Ammo, 18, 6);
        AddPickup(PickupType.Ammo, 6, 18);
        AddPickup(PickupType.Ammo, 18, 18);
        AddPickup(PickupType.Health, 12, 18);
        AddPickup(PickupType.Health, 6, 12);
        AddPickup(PickupType.Health, 18, 12);
        AddPickup(PickupType.Armor, 12, 15);
        AddPickup(PickupType.Invincibility, 2, 2);
        AddPickup(PickupType.DamageBoost, 21, 2);
        AddPickup(PickupType.AtomicHealth, 2, 21);
        AddPickup(PickupType.Steroids, 21, 21);
        
        // Victory exit (appears after boss dies - handled in update)
        // AddPickup(PickupType.Exit, 12, 6); // Added when boss dies
    }
    
    // --- Duke Nukem 3D Style Random Level Generator for Endless mode ---
    void GenerateRandomLevel()
    {
        // Rotate through different level themes
        int theme = _currentLevel % 5;
        SetLevelAtmosphere(theme);
        
        // Outer walls with theme-appropriate texture
        int wallTex = theme switch { 0 => 1, 1 => 3, 2 => 5, 3 => 2, _ => 4 };
        for (int i = 0; i < MAP_SIZE; i++)
        {
            _map[i] = wallTex;
            _map[(MAP_SIZE - 1) * MAP_SIZE + i] = wallTex;
            _map[i * MAP_SIZE] = wallTex;
            _map[i * MAP_SIZE + MAP_SIZE - 1] = wallTex;
        }
        
        // Duke 3D style: Generate interconnected rooms with corridors
        int numRooms = 5 + _rnd.Next(4) + (_currentLevel / 3);
        var rooms = new List<(int x, int y, int w, int h, int floorH)>();
        
        // Generate rooms
        for (int r = 0; r < numRooms; r++)
        {
            int roomW = 4 + _rnd.Next(5);
            int roomH = 4 + _rnd.Next(5);
            int roomX = _rnd.Next(2, MAP_SIZE - roomW - 2);
            int roomY = _rnd.Next(2, MAP_SIZE - roomH - 2);
            
            // Duke 3D style: Rooms at different heights
            int floorH = 0;
            double heightRoll = _rnd.NextDouble();
            if (heightRoll < 0.25) floorH = -24 - _rnd.Next(16); // Sunken area (pit)
            else if (heightRoll < 0.45) floorH = 20 + _rnd.Next(20); // Raised platform
            else if (heightRoll < 0.55) floorH = 40 + _rnd.Next(16); // High balcony
            
            rooms.Add((roomX, roomY, roomW, roomH, floorH));
            
            // Carve out room
            for (int y = roomY; y < roomY + roomH && y < MAP_SIZE - 1; y++)
            {
                for (int x = roomX; x < roomX + roomW && x < MAP_SIZE - 1; x++)
                {
                    _map[y * MAP_SIZE + x] = 0;
                    _floorHeights[y * MAP_SIZE + x] = floorH;
                }
            }
            
            // Add room walls
            int roomWallTex = _rnd.Next(1, 6);
            for (int y = roomY - 1; y <= roomY + roomH && y < MAP_SIZE; y++)
            {
                if (roomX > 0 && _map[y * MAP_SIZE + roomX - 1] != 0)
                    _map[y * MAP_SIZE + roomX - 1] = roomWallTex;
                if (roomX + roomW < MAP_SIZE && _map[y * MAP_SIZE + roomX + roomW] != 0)
                    _map[y * MAP_SIZE + roomX + roomW] = roomWallTex;
            }
            for (int x = roomX - 1; x <= roomX + roomW && x < MAP_SIZE; x++)
            {
                if (roomY > 0 && _map[(roomY - 1) * MAP_SIZE + x] != 0)
                    _map[(roomY - 1) * MAP_SIZE + x] = roomWallTex;
                if (roomY + roomH < MAP_SIZE && _map[(roomY + roomH) * MAP_SIZE + x] != 0)
                    _map[(roomY + roomH) * MAP_SIZE + x] = roomWallTex;
            }
        }
        
        // Connect rooms with corridors (Duke 3D style interconnected layout)
        for (int i = 0; i < rooms.Count - 1; i++)
        {
            var (x1, y1, w1, h1, fh1) = rooms[i];
            var (x2, y2, w2, h2, fh2) = rooms[i + 1];
            
            int cx1 = x1 + w1 / 2, cy1 = y1 + h1 / 2;
            int cx2 = x2 + w2 / 2, cy2 = y2 + h2 / 2;
            
            // Corridor floor height transitions smoothly
            int corridorH = (fh1 + fh2) / 2;
            
            // Horizontal corridor
            int minX = Math.Min(cx1, cx2), maxX = Math.Max(cx1, cx2);
            for (int x = minX; x <= maxX; x++)
            {
                if (_map[cy1 * MAP_SIZE + x] != 0)
                {
                    _map[cy1 * MAP_SIZE + x] = 0;
                    _floorHeights[cy1 * MAP_SIZE + x] = corridorH;
                }
                // Widen corridor for Duke 3D feel
                if (cy1 + 1 < MAP_SIZE - 1 && _map[(cy1 + 1) * MAP_SIZE + x] != 0)
                {
                    _map[(cy1 + 1) * MAP_SIZE + x] = 0;
                    _floorHeights[(cy1 + 1) * MAP_SIZE + x] = corridorH;
                }
            }
            
            // Vertical corridor
            int minY = Math.Min(cy1, cy2), maxY = Math.Max(cy1, cy2);
            for (int y = minY; y <= maxY; y++)
            {
                if (_map[y * MAP_SIZE + cx2] != 0)
                {
                    _map[y * MAP_SIZE + cx2] = 0;
                    _floorHeights[y * MAP_SIZE + cx2] = corridorH;
                }
                // Widen corridor
                if (cx2 + 1 < MAP_SIZE - 1 && _map[y * MAP_SIZE + cx2 + 1] != 0)
                {
                    _map[y * MAP_SIZE + cx2 + 1] = 0;
                    _floorHeights[y * MAP_SIZE + cx2 + 1] = corridorH;
                }
            }
        }
        
        // Add interior pillars/columns in larger rooms (Duke 3D architectural detail)
        foreach (var (rx, ry, rw, rh, _) in rooms)
        {
            if (rw >= 6 && rh >= 6 && _rnd.NextDouble() < 0.6)
            {
                // Add pillars
                int pillarTex = _rnd.Next(1, 6);
                if (rx + 1 < MAP_SIZE && ry + 1 < MAP_SIZE)
                    _map[(ry + 1) * MAP_SIZE + rx + 1] = pillarTex;
                if (rx + rw - 2 < MAP_SIZE && ry + 1 < MAP_SIZE)
                    _map[(ry + 1) * MAP_SIZE + rx + rw - 2] = pillarTex;
                if (rx + 1 < MAP_SIZE && ry + rh - 2 < MAP_SIZE)
                    _map[(ry + rh - 2) * MAP_SIZE + rx + 1] = pillarTex;
                if (rx + rw - 2 < MAP_SIZE && ry + rh - 2 < MAP_SIZE)
                    _map[(ry + rh - 2) * MAP_SIZE + rx + rw - 2] = pillarTex;
            }
        }
        
        // Add raised ledges/balconies overlooking lower areas (Duke 3D verticality)
        int numLedges = 2 + _rnd.Next(3);
        for (int l = 0; l < numLedges; l++)
        {
            int lx = _rnd.Next(3, MAP_SIZE - 5);
            int ly = _rnd.Next(3, MAP_SIZE - 5);
            int lw = 2 + _rnd.Next(3);
            int lh = 2 + _rnd.Next(3);
            int ledgeHeight = 32 + _rnd.Next(24);
            
            for (int y = ly; y < ly + lh && y < MAP_SIZE - 1; y++)
            {
                for (int x = lx; x < lx + lw && x < MAP_SIZE - 1; x++)
                {
                    if (_map[y * MAP_SIZE + x] == 0)
                    {
                        _floorHeights[y * MAP_SIZE + x] = ledgeHeight;
                    }
                }
            }
        }
        
        // Add pits with hazards (Duke 3D style danger zones)
        int numPits = 1 + _rnd.Next(2 + _currentLevel / 4);
        for (int p = 0; p < numPits; p++)
        {
            int px = _rnd.Next(3, MAP_SIZE - 5);
            int py = _rnd.Next(3, MAP_SIZE - 5);
            int pw = 2 + _rnd.Next(3);
            int ph = 2 + _rnd.Next(3);
            int pitDepth = -32 - _rnd.Next(24);
            bool isHazardous = _rnd.NextDouble() < 0.4 + (_currentLevel * 0.05);
            
            for (int y = py; y < py + ph && y < MAP_SIZE - 1; y++)
            {
                for (int x = px; x < px + pw && x < MAP_SIZE - 1; x++)
                {
                    if (_map[y * MAP_SIZE + x] == 0)
                    {
                        _floorHeights[y * MAP_SIZE + x] = pitDepth;
                        if (isHazardous)
                            _hazardTiles[y * MAP_SIZE + x] = true;
                    }
                }
            }
        }
        
        // Add stepped platforms (Duke 3D staircases)
        if (_rnd.NextDouble() < 0.7)
        {
            int sx = _rnd.Next(4, MAP_SIZE - 8);
            int sy = _rnd.Next(4, MAP_SIZE - 8);
            bool horizontal = _rnd.NextDouble() < 0.5;
            int steps = 4 + _rnd.Next(4);
            int stepHeight = 8;
            
            for (int s = 0; s < steps; s++)
            {
                int x = horizontal ? sx + s : sx;
                int y = horizontal ? sy : sy + s;
                if (x < MAP_SIZE - 1 && y < MAP_SIZE - 1)
                {
                    _map[y * MAP_SIZE + x] = 0;
                    _floorHeights[y * MAP_SIZE + x] = s * stepHeight;
                    // Widen stairs
                    if (horizontal && y + 1 < MAP_SIZE - 1)
                    {
                        _map[(y + 1) * MAP_SIZE + x] = 0;
                        _floorHeights[(y + 1) * MAP_SIZE + x] = s * stepHeight;
                    }
                    else if (!horizontal && x + 1 < MAP_SIZE - 1)
                    {
                        _map[y * MAP_SIZE + x + 1] = 0;
                        _floorHeights[y * MAP_SIZE + x + 1] = s * stepHeight;
                    }
                }
            }
        }
        
        // Add scattered hazard tiles (toxic spills, lava patches)
        double hazardChance = 0.015 + (_currentLevel * 0.003);
        for (int y = 2; y < MAP_SIZE - 2; y++)
        {
            for (int x = 2; x < MAP_SIZE - 2; x++)
            {
                if (_map[y * MAP_SIZE + x] == 0 && !_hazardTiles[y * MAP_SIZE + x] && _rnd.NextDouble() < hazardChance)
                {
                    _hazardTiles[y * MAP_SIZE + x] = true;
                }
            }
        }

        // Place player in first room at ground level
        bool playerPlaced = false;
        foreach (var (rx, ry, rw, rh, fh) in rooms)
        {
            if (Math.Abs(fh) <= 20) // Ground-level room
            {
                _px_pos = rx + rw / 2.0 + 0.5;
                _py = ry + rh / 2.0 + 0.5;
                _pa = _rnd.NextDouble() * PI2;
                playerPlaced = true;
                break;
            }
        }
        if (!playerPlaced)
        {
            // Fallback: find any open spot
            for (int attempts = 0; attempts < 100; attempts++)
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
        }

        // Place exit in distant room, preferably elevated (Duke 3D style)
        bool exitPlaced = false;
        for (int i = rooms.Count - 1; i >= 0 && !exitPlaced; i--)
        {
            var (rx, ry, rw, rh, fh) = rooms[i];
            double dist = Math.Abs(rx + rw/2 - _px_pos) + Math.Abs(ry + rh/2 - _py);
            if (dist > 8)
            {
                AddPickup(PickupType.Exit, rx + rw/2, ry + rh/2);
                exitPlaced = true;
            }
        }
        if (!exitPlaced)
        {
            // Fallback
            for (int attempts = 0; attempts < 50; attempts++)
            {
                int ex = _rnd.Next(2, MAP_SIZE - 2), ey = _rnd.Next(2, MAP_SIZE - 2);
                if (_map[ey * MAP_SIZE + ex] == 0 && Math.Abs(ex - (int)_px_pos) + Math.Abs(ey - (int)_py) > 8)
                {
                    AddPickup(PickupType.Exit, ex, ey);
                    break;
                }
            }
        }

        // Place enemies - distribute across rooms with more on elevated positions
        // More enemies on higher difficulties
        int enemyCount = 10 + _rnd.Next(8) + (_currentLevel * 2) + EnemyCountMultiplier;
        int enemiesPlaced = 0;
        
        // Place some enemies in each room
        foreach (var (rx, ry, rw, rh, fh) in rooms)
        {
            if (enemiesPlaced >= enemyCount) break;
            
            int roomEnemies = 1 + _rnd.Next(3);
            for (int e = 0; e < roomEnemies && enemiesPlaced < enemyCount; e++)
            {
                int ex = rx + 1 + _rnd.Next(rw - 2);
                int ey = ry + 1 + _rnd.Next(rh - 2);
                if (ex < MAP_SIZE - 1 && ey < MAP_SIZE - 1 && 
                    _map[ey * MAP_SIZE + ex] == 0 && !_hazardTiles[ey * MAP_SIZE + ex])
                {
                    // Stronger enemies on elevated positions (snipers!)
                    EnemyType type;
                    if (fh > 20 && _rnd.NextDouble() < 0.6)
                        type = (EnemyType)_rnd.Next(2, 5); // Enforcer, Octabrain
                    else
                        type = (EnemyType)_rnd.Next(0, 4 + _currentLevel / 3);
                    
                    if ((int)type > 4) type = EnemyType.Octabrain;
                    AddEnemy(type, ex, ey);
                    enemiesPlaced++;
                }
            }
        }

        // Place pickups - better loot in hard-to-reach places (Duke 3D reward exploration)
        // Pickup frequency adjusted by difficulty
        foreach (var (rx, ry, rw, rh, fh) in rooms)
        {
            int basePickups = 1 + _rnd.Next(2);
            int pickupCount = (int)(basePickups * PickupSpawnChance);
            if (pickupCount < 1 && _rnd.NextDouble() < PickupSpawnChance) pickupCount = 1;
            
            for (int p = 0; p < pickupCount; p++)
            {
                int px = rx + 1 + _rnd.Next(Math.Max(1, rw - 2));
                int py = ry + 1 + _rnd.Next(Math.Max(1, rh - 2));
                if (px < MAP_SIZE - 1 && py < MAP_SIZE - 1 && 
                    _map[py * MAP_SIZE + px] == 0 && !_hazardTiles[py * MAP_SIZE + px])
                {
                    // Better loot in elevated or sunken areas
                    PickupType pickupType;
                    if (Math.Abs(fh) > 24 && _rnd.NextDouble() < 0.5)
                        pickupType = (PickupType)_rnd.Next(3, 9); // Better weapons/items
                    else
                        pickupType = (PickupType)_rnd.Next(0, 9);
                    
                    AddPickup(pickupType, px, py);
                }
            }
        }
        
        // Rare power-ups in secret-ish locations
        if (_rnd.NextDouble() < 0.35 + (_currentLevel * 0.05))
        {
            // Find a high ledge for power-up
            for (int y = 2; y < MAP_SIZE - 2; y++)
            {
                for (int x = 2; x < MAP_SIZE - 2; x++)
                {
                    if (_map[y * MAP_SIZE + x] == 0 && _floorHeights[y * MAP_SIZE + x] > 30)
                    {
                        AddPickup(PickupType.Invincibility, x, y);
                        goto foundInvincibility;
                    }
                }
            }
            foundInvincibility:;
        }
        if (_rnd.NextDouble() < 0.4 + (_currentLevel * 0.05))
        {
            for (int y = 2; y < MAP_SIZE - 2; y++)
            {
                for (int x = 2; x < MAP_SIZE - 2; x++)
                {
                    if (_map[y * MAP_SIZE + x] == 0 && _floorHeights[y * MAP_SIZE + x] > 25)
                    {
                        AddPickup(PickupType.DamageBoost, x, y);
                        goto foundDamage;
                    }
                }
            }
            foundDamage:;
        }
        
        // Jetpack more common in vertical levels
        if (_rnd.NextDouble() < 0.25 + (_currentLevel * 0.03))
        {
            for (int attempts = 0; attempts < 30; attempts++)
            {
                int px = _rnd.Next(2, MAP_SIZE - 2), py = _rnd.Next(2, MAP_SIZE - 2);
                if (_map[py * MAP_SIZE + px] == 0 && !_hazardTiles[py * MAP_SIZE + px])
                {
                    AddPickup(PickupType.Jetpack, px, py);
                    break;
                }
            }
        }

        // Place keys in different rooms
        var keyRooms = rooms.OrderBy(_ => _rnd.Next()).Take(3).ToList();
        for (int k = 0; k < Math.Min(3, keyRooms.Count); k++)
        {
            var (rx, ry, rw, rh, _) = keyRooms[k];
            int kx = rx + rw / 2, ky = ry + rh / 2;
            if (kx < MAP_SIZE - 1 && ky < MAP_SIZE - 1 && _map[ky * MAP_SIZE + kx] == 0)
                AddPickup((PickupType)(9 + k), kx, ky);
        }

        // Place doors between rooms (Duke 3D locked areas)
        int doorsPlaced = 0;
        for (int y = 2; y < MAP_SIZE - 2 && doorsPlaced < 4; y++)
        {
            for (int x = 2; x < MAP_SIZE - 2 && doorsPlaced < 4; x++)
            {
                // Look for corridor-like spots (open tile with walls on two opposite sides)
                if (_map[y * MAP_SIZE + x] == 0)
                {
                    bool vertDoor = _map[(y-1) * MAP_SIZE + x] != 0 && _map[(y+1) * MAP_SIZE + x] != 0 &&
                                    _map[y * MAP_SIZE + x - 1] == 0 && _map[y * MAP_SIZE + x + 1] == 0;
                    bool horzDoor = _map[y * MAP_SIZE + x - 1] != 0 && _map[y * MAP_SIZE + x + 1] != 0 &&
                                    _map[(y-1) * MAP_SIZE + x] == 0 && _map[(y+1) * MAP_SIZE + x] == 0;
                    
                    if ((vertDoor || horzDoor) && _rnd.NextDouble() < 0.3)
                    {
                        AddDoor(x, y, _rnd.Next(1, 4));
                        doorsPlaced++;
                    }
                }
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
    
    void SetWall(int x, int y, int type)
    {
        if (x >= 0 && x < MAP_SIZE && y >= 0 && y < MAP_SIZE)
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
                e.Hp = e.MaxHp = (int)(40 * EnemyHealthMultiplier); e.Speed = 0.035 * EnemySpeedMultiplier; 
                e.AttackDamage = (int)(8 * EnemyDamageMultiplier); e.AttackRange = 10; e.AttackCooldown = 1.2; e.ScoreValue = 100;
                break;
            case EnemyType.PigCop:
                e.Hp = e.MaxHp = (int)(100 * EnemyHealthMultiplier); e.Speed = 0.03 * EnemySpeedMultiplier; 
                e.AttackDamage = (int)(20 * EnemyDamageMultiplier); e.AttackRange = 8; e.AttackCooldown = 1.0; e.ScoreValue = 200;
                break;
            case EnemyType.Enforcer:
                e.Hp = e.MaxHp = (int)(150 * EnemyHealthMultiplier); e.Speed = 0.025 * EnemySpeedMultiplier; 
                e.AttackDamage = (int)(15 * EnemyDamageMultiplier); e.AttackRange = 12; e.AttackCooldown = 0.8; e.ScoreValue = 300;
                break;
            case EnemyType.Octabrain:
                e.Hp = e.MaxHp = (int)(175 * EnemyHealthMultiplier); e.Speed = 0.02 * EnemySpeedMultiplier; 
                e.AttackDamage = (int)(25 * EnemyDamageMultiplier); e.AttackRange = 15; e.AttackCooldown = 2.0; e.ScoreValue = 400;
                break;
            case EnemyType.BattleLord:
                e.Hp = e.MaxHp = (int)(800 * EnemyHealthMultiplier); e.Speed = 0.015 * EnemySpeedMultiplier; 
                e.AttackDamage = (int)(40 * EnemyDamageMultiplier); e.AttackRange = 18; e.AttackCooldown = 0.6; e.ScoreValue = 2000;
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
    
    void AddBarrel(double x, double y)
    {
        _barrels.Add(new ExplodingBarrel { X = x + 0.5, Y = y + 0.5, Hp = 30, BobOffset = _rnd.NextDouble() * PI2 });
    }
    
    void PlaceBarrels()
    {
        // Add exploding barrels in strategic locations
        int barrelCount = 3 + _rnd.Next(5) + (_currentLevel / 2);
        int placed = 0;
        
        for (int attempts = 0; attempts < 100 && placed < barrelCount; attempts++)
        {
            int bx = _rnd.Next(2, MAP_SIZE - 2);
            int by = _rnd.Next(2, MAP_SIZE - 2);
            
            // Check if it's an open space and not too close to player start
            if (_map[by * MAP_SIZE + bx] == 0 && !_hazardTiles[by * MAP_SIZE + bx])
            {
                double distToPlayer = Math.Sqrt((_px_pos - bx) * (_px_pos - bx) + (_py - by) * (_py - by));
                if (distToPlayer > 5)
                {
                    // Prefer placing near enemies for chain reaction potential!
                    bool nearEnemy = _enemies.Any(e => Math.Abs(e.X - bx) < 4 && Math.Abs(e.Y - by) < 4);
                    if (nearEnemy || _rnd.NextDouble() < 0.4)
                    {
                        AddBarrel(bx, by);
                        placed++;
                    }
                }
            }
        }
    }
    
    void UpdateBarrels(double dt)
    {
        for (int i = _barrels.Count - 1; i >= 0; i--)
        {
            var barrel = _barrels[i];
            if (barrel.Exploded) continue;
            
            // Check if damaged by projectiles
            foreach (var proj in _projectiles)
            {
                if (proj.Dead) continue;
                double d = Math.Sqrt((proj.X - barrel.X) * (proj.X - barrel.X) + (proj.Y - barrel.Y) * (proj.Y - barrel.Y));
                if (d < 0.6)
                {
                    barrel.Hp -= proj.Damage;
                    proj.Dead = true;
                    if (barrel.Hp <= 0)
                    {
                        ExplodeBarrel(barrel);
                    }
                    break;
                }
            }
            
            // Check proximity to player shooting (hitscan)
            // This is handled in HitscanShot now
        }
        
        _barrels.RemoveAll(b => b.Exploded);
    }
    
    void ExplodeBarrel(ExplodingBarrel barrel)
    {
        barrel.Exploded = true;
        
        // Big explosion!
        TriggerScreenShake(0.4, 12);
        
        // Massive confetti explosion - it's a party! 🎆
        SpawnConfetti(barrel.X, barrel.Y, 40);
        SpawnParticles(barrel.X, barrel.Y, 30, 0xFFFF6600u, 0xFFFFFF00u); // Orange/yellow fire
        SpawnParticles(barrel.X, barrel.Y, 15, 0xFF444444u, 0xFF222222u); // Smoke
        
        // Damage nearby enemies (high damage!)
        int explosionDamage = 100;
        foreach (var en in _enemies)
        {
            if (en.Dead) continue;
            double d = Math.Sqrt((barrel.X - en.X) * (barrel.X - en.X) + (barrel.Y - en.Y) * (barrel.Y - en.Y));
            if (d < 4)
            {
                int dmg = (int)(explosionDamage * (1 - d / 4));
                if (_damageBoostTimer > 0) dmg = (int)(dmg * _damageBoostMultiplier);
                en.Hp -= dmg;
                en.HurtTimer = 0.3;
                en.Alerted = true;
                if (en.Hp <= 0)
                {
                    KillEnemy(en, true); // Explosive death!
                    _score += 25; // Bonus for barrel kill!
                }
            }
        }
        
        // Damage player if too close
        double playerDist = Math.Sqrt((barrel.X - _px_pos) * (barrel.X - _px_pos) + (barrel.Y - _py) * (barrel.Y - _py));
        if (playerDist < 4)
        {
            TakeDamage((int)(60 * (1 - playerDist / 4)));
        }
        
        // Chain reaction! Explode nearby barrels
        foreach (var otherBarrel in _barrels)
        {
            if (otherBarrel == barrel || otherBarrel.Exploded) continue;
            double d = Math.Sqrt((barrel.X - otherBarrel.X) * (barrel.X - otherBarrel.X) + 
                                 (barrel.Y - otherBarrel.Y) * (barrel.Y - otherBarrel.Y));
            if (d < 3)
            {
                otherBarrel.Hp -= 50; // Damage nearby barrels
                if (otherBarrel.Hp <= 0)
                {
                    ExplodeBarrel(otherBarrel); // CHAIN REACTION!
                }
            }
        }
        
        ShowMessage("💥 BOOM!");
        if (_rnd.NextDouble() < 0.4)
        {
            string[] barrelQuotes = { "Now THAT'S explosive!", "Kaboom!", "Chain reaction!", "Fire in the hole!" };
            SayQuote(barrelQuotes[_rnd.Next(barrelQuotes.Length)]);
        }
    }
    #endregion

    #region Game Loop
    void OnLoaded(object s, RoutedEventArgs e)
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += GameLoop;
        _timer.Start();
        Focus();
        
        // Show main menu on start
        ShowMainMenu();
        
        // Initialize high score service
        try { _highScoreService = new HighScoreService(); }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to init high scores");
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
            Log.Warning(ex, "Failed to init settings");
            _settingsService = null;
        }
    }
    
    void ShowMainMenu()
    {
        _paused = true;
        MainMenuPanel.Visibility = Visibility.Visible;
        CaptureMouse(false);
    }
    
    void CampaignButton_Click(object sender, RoutedEventArgs e)
    {
        _gameMode = GameMode.Campaign;
        _currentLevel = 1;
        MainMenuPanel.Visibility = Visibility.Collapsed;
        ResetPlayerForNewGame();
        LoadLevel(1);
    }
    
    void EndlessButton_Click(object sender, RoutedEventArgs e)
    {
        _gameMode = GameMode.Endless;
        _currentLevel = 1;
        MainMenuPanel.Visibility = Visibility.Collapsed;
        ResetPlayerForNewGame();
        LoadLevel(1);
        _paused = false;
        CaptureMouse(true);
        SayQuote("It's time to kick ass and chew bubblegum...");
    }
    
    void DifficultyButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tagStr && int.TryParse(tagStr, out int diffIndex))
        {
            _difficulty = (DifficultyLevel)diffIndex;
            UpdateDifficultyButtonStyles();
            DifficultyDescText.Text = DifficultyDescriptions[diffIndex];
        }
    }
    
    void UpdateDifficultyButtonStyles()
    {
        // Reset all buttons
        var buttons = new[] { DiffEasyBtn, DiffNormalBtn, DiffHardBtn, DiffNightmareBtn };
        var defaultBgs = new[] { "#335533", "#665500", "#663333", "#440000" };
        var defaultFgs = new[] { "#88FF88", "#FFCC00", "#FF8888", "#FF0000" };
        var selectedBgs = new[] { "#55AA55", "#FFD700", "#AA5555", "#880000" };
        var selectedFgs = new[] { "#FFFFFF", "#000000", "#FFFFFF", "#FFFFFF" };
        
        for (int i = 0; i < buttons.Length; i++)
        {
            bool isSelected = (int)_difficulty == i;
            buttons[i].Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isSelected ? selectedBgs[i] : defaultBgs[i]));
            buttons[i].Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isSelected ? selectedFgs[i] : defaultFgs[i]));
            buttons[i].BorderThickness = isSelected ? new Thickness(2) : new Thickness(1);
            buttons[i].BorderBrush = isSelected ? Brushes.White : Brushes.Gray;
        }
    }
    
    void BriefingStartButton_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        CloseBriefing();
    }
    
    void ResetPlayerForNewGame()
    {
        _hp = 100;
        _armor = 0;
        _score = 0;
        _currentWeapon = 1;
        _ammo = new int[] { 999, 48, 12, 200, 10, 5, 8, 24 };
        _keys = new bool[3];
        _medkits = 0;
        _steroids = 0;
        _hasJetpack = false;
        _jetpackFuel = 100;
        _gameOver = false;
        _levelComplete = false;
        _victory = false;
        GameOverScreen.Visibility = Visibility.Collapsed;
        LevelCompleteScreen.Visibility = Visibility.Collapsed;
        VictoryScreen.Visibility = Visibility.Collapsed;
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
        double dt = 0.016;
        
        // Update briefing timer even when showing briefing
        if (_showingBriefing)
        {
            _briefingTimer += dt;
            // Animate briefing panel opacity based on timer
            double fade = Math.Min(1.0, _briefingTimer / 0.5);
            BriefingPanel.Opacity = fade;
            return;
        }
        
        if (_paused || _gameOver || _levelComplete || _victory) return;

        _gameTime += dt;
        
        // === OPTIMIZATION: Cache trig values once per frame for all systems ===
        _sinPa = Math.Sin(_pa);
        _cosPa = Math.Cos(_pa);
        _sinPaPlus = Math.Sin(_pa + PI / 2);
        _cosPaPlus = Math.Cos(_pa + PI / 2);

        ProcessMouse();
        UpdatePlayer(dt);
        UpdateEnemies(dt);
        UpdateProjectiles(dt);
        UpdateDoors(dt);
        UpdatePickups();
        UpdatePipeBombs(dt);
        UpdateBarrels(dt);
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
        if (_cameraFlashTimer > 0) _cameraFlashTimer -= dt;
        if (_cameraCooldown > 0) _cameraCooldown -= dt;
        
        // === ROBUST ANIMATION UPDATES ===
        UpdateAnimations(dt);
        
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
    
    void UpdateAnimations(double dt)
    {
        // Weapon recoil decay (spring-like)
        _weaponRecoil *= 0.85;
        if (_weaponRecoil < 0.01) _weaponRecoil = 0;
        
        // Weapon swap animation
        if (_isSwappingWeapon)
        {
            _weaponSwapAnim += dt * 4; // Speed of swap
            if (_weaponSwapAnim >= 1.0)
            {
                _weaponSwapAnim = 0;
                _isSwappingWeapon = false;
            }
        }
        
        // Idle breathing animation (always running)
        _breathingPhase += dt * 1.5;
        _weaponIdleAnim = Math.Sin(_breathingPhase) * 0.5 + 0.5;
        
        // Creepy ambient phase (slow, unsettling movements)
        _creepyAmbientPhase += dt * 0.7;
        
        // Eye blink timer for eye-camera
        _eyeBlinkTimer -= dt;
        if (_eyeBlinkTimer < 0) _eyeBlinkTimer = 2.5 + _rnd.NextDouble() * 4; // Random blinks
        
        // Heartbeat when low health
        if (_hp < 30)
        {
            double heartRate = 8 + (30 - _hp) * 0.3; // Faster when lower
            _heartbeatPhase += dt * heartRate;
        }
        
        // Idle timer (for inspect animation trigger)
        bool isMoving = _keysDown.Contains(Key.W) || _keysDown.Contains(Key.S) || 
                        _keysDown.Contains(Key.A) || _keysDown.Contains(Key.D);
        if (isMoving || _shooting)
        {
            _idleTimer = 0;
            _weaponInspectAnim = 0;
        }
        else
        {
            _idleTimer += dt;
            // Start inspect animation after 5 seconds idle
            if (_idleTimer > 5 && _weaponInspectAnim < 1)
            {
                _weaponInspectAnim += dt * 0.3;
                if (_weaponInspectAnim > 1) _weaponInspectAnim = 1;
            }
        }
        
        // Footstep phase for view bob
        if (isMoving && _pz == 0)
        {
            _stepPhase += dt * 10;
        }
        
        // Landing squash decay
        _landingSquash *= 0.88;
        
        // Kill flash decay
        if (_killFlashTimer > 0) _killFlashTimer -= dt;
        
        // Screen warp decay (trippy effect)
        if (_screenWarpTimer > 0) _screenWarpTimer -= dt;
        
        // Death animation
        if (_gameOver && _deathAnimTimer < 1)
        {
            _deathAnimTimer += dt * 0.8;
        }
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
                
                // Spawn creepy particles on bounce - teeth, eyes, bits
                if (Math.Abs(part.Vz) > 0.05)
                {
                    uint[] bounceColors = { 0xFFFFFFFFu, 0xFFFFFF00u, 0xFF880000u, 0xFFEEDDCCu, 0xFF111111u, 0xFFDDBBAAu };
                    for (int c = 0; c < 3; c++)
                    {
                        double vx = (_rnd.NextDouble() - 0.5) * 0.08;
                        double vy = (_rnd.NextDouble() - 0.5) * 0.08;
                        uint color = bounceColors[_rnd.Next(bounceColors.Length)];
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
    // Velocity-based movement for smoother feel
    double _playerVelX = 0, _playerVelY = 0;
    const double PLAYER_ACCEL = 0.8;      // How fast player accelerates
    const double PLAYER_FRICTION = 0.85;   // How fast player decelerates (1 = no friction)
    const double PLAYER_MAX_SPEED = 0.12;  // Maximum velocity
    bool _isSprinting = false;
    double _sprintStamina = 100;
    
    void UpdatePlayer(double dt)
    {
        double targetSpeed = _steroidsTimer > 0 ? 0.12 : 0.08;
        double rs = 0.06;

        // === OPTIMIZATION: Use cached trig values ===
        // cos(a-PI/2) = sin(a), sin(a-PI/2) = -cos(a)
        // cos(a+PI/2) = -sin(a), sin(a+PI/2) = cos(a)
        double inputX = 0, inputY = 0;
        if (_keysDown.Contains(Key.W)) { inputX += _cosPa; inputY += _sinPa; }
        if (_keysDown.Contains(Key.S)) { inputX -= _cosPa; inputY -= _sinPa; }
        if (_keysDown.Contains(Key.A)) { inputX += _sinPa; inputY -= _cosPa; } // Strafe left
        if (_keysDown.Contains(Key.D)) { inputX -= _sinPa; inputY += _cosPa; } // Strafe right

        double inputLen = Math.Sqrt(inputX * inputX + inputY * inputY);
        bool isMoving = inputLen > 0.01;
        
        if (isMoving) 
        { 
            inputX /= inputLen; 
            inputY /= inputLen;
            _idleTimer = 0; // Reset idle timer when moving
        }

        // Sprint handling
        _isSprinting = _keysDown.Contains(Key.LeftShift) && isMoving && _sprintStamina > 0;
        if (_isSprinting)
        {
            targetSpeed *= 1.5;
            _sprintStamina -= dt * 30; // Drain stamina while sprinting
            if (_sprintStamina < 0) _sprintStamina = 0;
        }
        else if (_sprintStamina < 100)
        {
            _sprintStamina += dt * 15; // Recover stamina when not sprinting
            if (_sprintStamina > 100) _sprintStamina = 100;
        }

        // Apply acceleration if input, friction if no input
        if (isMoving)
        {
            _playerVelX += inputX * PLAYER_ACCEL * targetSpeed;
            _playerVelY += inputY * PLAYER_ACCEL * targetSpeed;
        }
        
        // Apply friction
        _playerVelX *= PLAYER_FRICTION;
        _playerVelY *= PLAYER_FRICTION;
        
        // Clamp to max speed
        double speed = Math.Sqrt(_playerVelX * _playerVelX + _playerVelY * _playerVelY);
        double maxSpeed = _isSprinting ? PLAYER_MAX_SPEED * 1.5 : PLAYER_MAX_SPEED;
        if (speed > maxSpeed)
        {
            _playerVelX = _playerVelX / speed * maxSpeed;
            _playerVelY = _playerVelY / speed * maxSpeed;
        }

        // Apply movement with collision
        double newX = _px_pos + _playerVelX;
        double newY = _py + _playerVelY;

        if (!IsWall(newX, _py)) _px_pos = newX;
        else _playerVelX *= -0.3; // Bounce slightly on wall hit
        
        if (!IsWall(_px_pos, newY)) _py = newY;
        else _playerVelY *= -0.3; // Bounce slightly on wall hit

        if (_keysDown.Contains(Key.Left)) _pa -= rs;
        if (_keysDown.Contains(Key.Right)) _pa += rs;

        // Jumping with better physics
        if (_pz > 0 || _pzVel != 0)
        {
            _pzVel -= 0.4;
            _pz += _pzVel;
            if (_pz <= 0) 
            { 
                _pz = 0; 
                _pzVel = 0; 
                _isJumping = false;
                // === ANIMATION: Landing squash ===
                TriggerLandingSquash();
                // Landing sound/feedback
                if (Math.Abs(_pzVel) > 3) TriggerScreenShake(0.1, 3);
            }
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

        // Crouching - affects eye height and speed
        _eyeHeight = _isCrouching ? -15 : 0;
        if (_isCrouching)
        {
            _playerVelX *= 0.6; // Slower while crouched
            _playerVelY *= 0.6;
        }

        // Enhanced head bob (varies with speed and sprint)
        if (speed > 0.01 && _pz == 0)
        {
            double bobSpeed = _isSprinting ? 16 : 12;
            double bobAmount = _isSprinting ? 4 : 2.5;
            _eyeHeight += Math.Sin(_gameTime * bobSpeed) * bobAmount * (speed / PLAYER_MAX_SPEED);
            
            // Slight horizontal sway when sprinting
            if (_isSprinting)
            {
                double sway = Math.Sin(_gameTime * 8) * 0.015;
                _pa += sway * 0.1;
            }
        }

        // Weapon bob (sway with movement)
        _weaponBob = speed / PLAYER_MAX_SPEED;

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
            
            // Update stun timer
            if (en.StunTimer > 0)
            {
                en.StunTimer -= dt;
                en.State = EnemyState.Stunned;
                // Stunned enemies wobble extra fast (dizzy!)
                en.WobblePhase += en.WobbleSpeed * dt * 4;
                continue; // Skip AI while stunned
            }
            
            // Always update wobble phase for idle animation
            en.WobblePhase += en.WobbleSpeed * dt;
            en.BobPhase += en.WobbleSpeed * 0.7 * dt;
            
            // Update state timer
            en.StateTimer += dt;

            double dx = _px_pos - en.X, dy = _py - en.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            
            // Line of sight check
            en.CanSeePlayer = HasLineOfSight(en.X, en.Y, _px_pos, _py);
            
            // Update last known player position if visible
            if (en.CanSeePlayer && dist < 15)
            {
                en.LastKnownPlayerX = _px_pos;
                en.LastKnownPlayerY = _py;
            }

            // Alert check - wider range if player shoots nearby
            double alertRange = _shooting ? 18 : 12;
            if (dist < alertRange && en.CanSeePlayer) en.Alerted = true;
            
            // === ENHANCED AI STATE MACHINE ===
            UpdateEnemyAI(en, dt, dx, dy, dist);

            double prevX = en.X, prevY = en.Y;
            
            // Movement based on state
            switch (en.State)
            {
                case EnemyState.Idle:
                    // Occasional look around
                    if (en.StateTimer > 3 && _rnd.NextDouble() < 0.02)
                    {
                        en.State = EnemyState.Patrol;
                        en.StateTimer = 0;
                        SetRandomPatrolTarget(en);
                    }
                    break;
                    
                case EnemyState.Patrol:
                    MoveTowardsPoint(en, en.PatrolTargetX, en.PatrolTargetY, dt);
                    if (DistanceTo(en.X, en.Y, en.PatrolTargetX, en.PatrolTargetY) < 0.5)
                    {
                        en.State = EnemyState.Idle;
                        en.StateTimer = 0;
                    }
                    break;
                    
                case EnemyState.Chase:
                    if (dist > en.AttackRange * 0.5 && dist < 16 && en.CanSeePlayer)
                    {
                        double nx = en.X + dx / dist * en.Speed * 1.2; // Faster when chasing
                        double ny = en.Y + dy / dist * en.Speed * 1.2;
                        if (!IsWall(nx, en.Y)) en.X = nx;
                        if (!IsWall(en.X, ny)) en.Y = ny;
                    }
                    else if (!en.CanSeePlayer)
                    {
                        // Lost sight, move to last known position
                        MoveTowardsPoint(en, en.LastKnownPlayerX, en.LastKnownPlayerY, dt);
                    }
                    break;
                    
                case EnemyState.Flanking:
                    // Move perpendicular to player
                    PerformFlankingMovement(en, dx, dy, dist, dt);
                    break;
                    
                case EnemyState.Retreat:
                    // Move away from player
                    if (dist < 8)
                    {
                        double nx = en.X - dx / dist * en.Speed * 0.8;
                        double ny = en.Y - dy / dist * en.Speed * 0.8;
                        if (!IsWall(nx, en.Y)) en.X = nx;
                        if (!IsWall(en.X, ny)) en.Y = ny;
                    }
                    en.RetreatTimer -= dt;
                    if (en.RetreatTimer <= 0 || dist > 10)
                    {
                        en.State = EnemyState.Attack;
                        en.StateTimer = 0;
                    }
                    break;
                    
                case EnemyState.Attack:
                    // Strafe while attacking (Duke 3D style!)
                    if (en.AggressionLevel > 0.5 && dist < en.AttackRange)
                    {
                        double strafeDir = Math.Sin(en.StateTimer * 2) * 0.015;
                        double strafeX = -dy / dist * strafeDir;
                        double strafeY = dx / dist * strafeDir;
                        if (!IsWall(en.X + strafeX, en.Y)) en.X += strafeX;
                        if (!IsWall(en.X, en.Y + strafeY)) en.Y += strafeY;
                    }
                    break;
                    
                default:
                    // Standard chase behavior for unhandled states
                    if (en.Alerted && dist > en.AttackRange * 0.4 && dist < 16)
                    {
                        double nx = en.X + dx / dist * en.Speed;
                        double ny = en.Y + dy / dist * en.Speed;
                        if (!IsWall(nx, en.Y)) en.X = nx;
                        if (!IsWall(en.X, ny)) en.Y = ny;
                    }
                    break;
            }
            
            // Calculate lean angle based on movement direction
            double moveX = en.X - prevX;
            en.LeanAngle = en.LeanAngle * 0.9 + (moveX * 10) * 0.1; // Smooth lerp

            // Attack behavior with enhanced patterns
            if (en.Alerted && en.CanSeePlayer && dist < en.AttackRange && en.State != EnemyState.Retreat)
            {
                en.Timer += dt;
                bool shouldAttack = false;
                
                // Different attack patterns by enemy type
                switch (en.Type)
                {
                    case EnemyType.Trooper:
                        // Single shots
                        shouldAttack = en.Timer > en.AttackCooldown;
                        break;
                        
                    case EnemyType.PigCop:
                        // Double tap
                        if (en.Timer > en.AttackCooldown)
                        {
                            shouldAttack = true;
                            en.BurstCount++;
                            if (en.BurstCount >= 2)
                            {
                                en.Timer = 0;
                                en.BurstCount = 0;
                            }
                            else
                            {
                                en.Timer = en.AttackCooldown - 0.15; // Quick second shot
                            }
                        }
                        break;
                        
                    case EnemyType.Enforcer:
                        // 3-round burst
                        if (en.Timer > en.AttackCooldown)
                        {
                            shouldAttack = true;
                            en.BurstCount++;
                            if (en.BurstCount >= 3)
                            {
                                en.Timer = 0;
                                en.BurstCount = 0;
                            }
                            else
                            {
                                en.Timer = en.AttackCooldown - 0.1;
                            }
                        }
                        break;
                        
                    case EnemyType.Octabrain:
                        // Charged attack (slower but homing-ish)
                        shouldAttack = en.Timer > en.AttackCooldown;
                        break;
                        
                    case EnemyType.BattleLord:
                        // Rapid fire with spread
                        if (en.Timer > en.AttackCooldown * 0.3)
                        {
                            shouldAttack = true;
                            en.Timer = 0;
                        }
                        break;
                        
                    default:
                        shouldAttack = en.Timer > en.AttackCooldown;
                        break;
                }
                
                if (shouldAttack && dist < en.AttackRange)
                {
                    if (en.Type != EnemyType.BattleLord)
                        en.Timer = 0;
                    
                    // Calculate projectile direction with prediction
                    double leadFactor = en.Type == EnemyType.Octabrain ? 0.3 : 0.1;
                    double predictX = dx + _playerVelX * dist * leadFactor;
                    double predictY = dy + _playerVelY * dist * leadFactor;
                    double predictDist = Math.Sqrt(predictX * predictX + predictY * predictY);
                    
                    // Add some inaccuracy based on distance
                    double accuracy = Math.Max(0.02, 0.1 - dist * 0.005);
                    double spreadX = (_rnd.NextDouble() - 0.5) * accuracy;
                    double spreadY = (_rnd.NextDouble() - 0.5) * accuracy;
                    
                    _projectiles.Add(new Projectile
                    {
                        X = en.X, Y = en.Y,
                        Dx = (predictX / predictDist + spreadX) * 0.18,
                        Dy = (predictY / predictDist + spreadY) * 0.18,
                        Damage = en.AttackDamage,
                        FromPlayer = false
                    });
                    
                    // Boss fires additional projectiles
                    if (en.Type == EnemyType.BattleLord && _rnd.NextDouble() < 0.4)
                    {
                        // Spread shot
                        for (int i = -1; i <= 1; i += 2)
                        {
                            double angle = Math.Atan2(predictY, predictX) + i * 0.2;
                            _projectiles.Add(new Projectile
                            {
                                X = en.X, Y = en.Y,
                                Dx = Math.Cos(angle) * 0.16,
                                Dy = Math.Sin(angle) * 0.16,
                                Damage = en.AttackDamage * 0.6,
                                FromPlayer = false
                            });
                        }
                    }
                }
            }
        }
    }
    
    void UpdateEnemyAI(Enemy en, double dt, double dx, double dy, double dist)
    {
        // Calculate aggression based on HP and player threat
        en.AggressionLevel = (en.Hp / en.MaxHp) * 0.5 + 0.5;
        
        if (!en.Alerted)
        {
            if (en.State != EnemyState.Patrol && en.State != EnemyState.Idle)
                en.State = EnemyState.Idle;
            return;
        }
        
        // State transitions based on situation
        double hpPercent = en.Hp / en.MaxHp;
        
        // Low HP - consider retreating
        if (hpPercent < 0.25 && en.State != EnemyState.Retreat && _rnd.NextDouble() < 0.03)
        {
            en.State = EnemyState.Retreat;
            en.RetreatTimer = 2.0 + _rnd.NextDouble() * 2;
            en.StateTimer = 0;
            return;
        }
        
        // Try flanking occasionally (smarter enemies)
        if ((en.Type == EnemyType.Enforcer || en.Type == EnemyType.Octabrain) &&
            en.State == EnemyState.Attack && en.StateTimer > 3 && _rnd.NextDouble() < 0.02)
        {
            en.State = EnemyState.Flanking;
            en.FlankingAngle = _rnd.NextDouble() > 0.5 ? 1 : -1;
            en.StateTimer = 0;
            return;
        }
        
        // Default combat behavior
        if (en.CanSeePlayer && dist < en.AttackRange * 1.2)
        {
            if (en.State != EnemyState.Retreat && en.State != EnemyState.Flanking)
                en.State = EnemyState.Attack;
        }
        else if (en.CanSeePlayer)
        {
            en.State = EnemyState.Chase;
        }
        else if (en.State == EnemyState.Attack)
        {
            // Lost sight during combat - investigate last position
            en.State = EnemyState.Chase;
            en.StateTimer = 0;
        }
        
        // Flanking timeout
        if (en.State == EnemyState.Flanking && en.StateTimer > 2.5)
        {
            en.State = EnemyState.Attack;
            en.StateTimer = 0;
        }
    }
    
    bool HasLineOfSight(double x1, double y1, double x2, double y2)
    {
        double dx = x2 - x1, dy = y2 - y1;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        double stepX = dx / dist * 0.2;
        double stepY = dy / dist * 0.2;
        
        double checkX = x1, checkY = y1;
        for (int i = 0; i < (int)(dist / 0.2); i++)
        {
            checkX += stepX;
            checkY += stepY;
            if (IsWall(checkX, checkY)) return false;
        }
        return true;
    }
    
    void SetRandomPatrolTarget(Enemy en)
    {
        // Find a valid nearby position to patrol to
        for (int attempts = 0; attempts < 10; attempts++)
        {
            double angle = _rnd.NextDouble() * Math.PI * 2;
            double dist = 2 + _rnd.NextDouble() * 4;
            double targetX = en.X + Math.Cos(angle) * dist;
            double targetY = en.Y + Math.Sin(angle) * dist;
            
            if (!IsWall(targetX, targetY))
            {
                en.PatrolTargetX = targetX;
                en.PatrolTargetY = targetY;
                return;
            }
        }
        // Fallback - stay in place
        en.PatrolTargetX = en.X;
        en.PatrolTargetY = en.Y;
    }
    
    void MoveTowardsPoint(Enemy en, double targetX, double targetY, double dt)
    {
        double dx = targetX - en.X, dy = targetY - en.Y;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist > 0.1)
        {
            double nx = en.X + dx / dist * en.Speed * 0.7;
            double ny = en.Y + dy / dist * en.Speed * 0.7;
            if (!IsWall(nx, en.Y)) en.X = nx;
            if (!IsWall(en.X, ny)) en.Y = ny;
        }
    }
    
    void PerformFlankingMovement(Enemy en, double dx, double dy, double dist, double dt)
    {
        if (dist < 2) return; // Too close to flank
        
        // Move perpendicular to the player
        double perpX = -dy / dist * en.FlankingAngle;
        double perpY = dx / dist * en.FlankingAngle;
        
        // Also move slightly toward player
        double towardX = dx / dist * 0.3;
        double towardY = dy / dist * 0.3;
        
        double moveX = (perpX + towardX) * en.Speed;
        double moveY = (perpY + towardY) * en.Speed;
        
        if (!IsWall(en.X + moveX, en.Y)) en.X += moveX;
        if (!IsWall(en.X, en.Y + moveY)) en.Y += moveY;
    }
    
    double DistanceTo(double x1, double y1, double x2, double y2)
    {
        double dx = x2 - x1, dy = y2 - y1;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    void TakeDamage(int damage)
    {
        // Apply difficulty multiplier to incoming damage
        damage = (int)(damage * DamageToPlayerMultiplier);
        if (damage < 1) damage = 1;
        
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
        
        // === ANIMATION: Kill flash effect ===
        _killFlashTimer = 0.15;
        _screenWarpTimer = 0.2; // Brief trippy warp on kill
        
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
        
        // BOSS KILL - Check for boss level victory in campaign mode
        if (en.Type == EnemyType.BattleLord && _gameMode == GameMode.Campaign && _currentLevel == _maxCampaignLevel)
        {
            // Boss defeated! Spawn exit and play victory quote
            AddPickup(PickupType.Exit, (int)en.X, (int)en.Y);
            SayQuote("Your performance review is... COMPLETE!");
            TriggerScreenShake(1.0, 20); // Big shake for boss death
        }
    }
    
    void SpawnRagdoll(Enemy en, bool explosive)
    {
        // Creepy unsettling colors - pale flesh, wrong hues
        uint baseColor = en.Type switch
        {
            EnemyType.Trooper => 0xFF88AA88u,   // Sickly pale green
            EnemyType.PigCop => 0xFF8888AAu,    // Corpse blue
            EnemyType.Enforcer => 0xFF999999u,  // Dead gray
            EnemyType.Octabrain => 0xFFAA88AAu, // Bruised purple
            EnemyType.BattleLord => 0xFFAA6666u,// Dried meat red
            _ => 0xFFDDCCBBu // Wrong skin tone
        };
        
        double force = explosive ? 0.25 : 0.12;
        double upForce = explosive ? 0.35 : 0.18;
        
        // Direction from player (for non-explosive deaths, parts fly away from player)
        double dx = en.X - _px_pos;
        double dy = en.Y - _py;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist > 0.01) { dx /= dist; dy /= dist; }
        
        // Spawn different body parts with unsettling colors
        var partTypes = new[] { BodyPartType.Head, BodyPartType.Torso, BodyPartType.ArmL, BodyPartType.ArmR, BodyPartType.LegL, BodyPartType.LegR };
        var partSizes = new[] { 0.18, 0.28, 0.12, 0.12, 0.14, 0.14 };
        
        // Creepy color palette - teeth, meat, bone
        uint[] creepyColors = { 0xFFEEDDCCu, 0xFFCCAAAAu, 0xFFFFFFEEu, 0xFFDDBBAAu, 0xFF887766u, 0xFF998877u };
        
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
            
            // Mix base color with creepy colors
            uint partColor = i == 1 ? baseColor : creepyColors[i]; // Torso keeps enemy color
            
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
        
        // Spawn creepy particles on death!
        SpawnConfetti(en.X, en.Y, explosive ? 25 : 12);
    }
    
    // Spawn unsettling particle effects
    void SpawnConfetti(double x, double y, int count)
    {
        // Creepy colors: pale flesh, teeth, eyes, void black
        uint[] creepyColors = { 
            0xFFEEDDCCu, // Pale flesh
            0xFFFFFFFFu, // Teeth white
            0xFF881111u, // Dark blood
            0xFF111111u, // Void black
            0xFFFFFF00u, // Yellow eyes
            0xFFDDAAAAu, // Sickly skin
            0xFF332222u, // Dried blood
            0xFFFF0000u  // Red pupils
        };
        
        for (int i = 0; i < count; i++)
        {
            double angle = _rnd.NextDouble() * PI2;
            double speed = 0.05 + _rnd.NextDouble() * 0.15;
            uint color = creepyColors[_rnd.Next(creepyColors.Length)];
            _particles.Add((x, y, Math.Cos(angle) * speed, Math.Sin(angle) * speed, 1.5, color));
        }
    }
    
    void TriggerScreenShake(double duration, double intensity)
    {
        _screenShakeTimer = duration;
        _screenShakeIntensity = intensity;
    }
    
    void SwitchWeapon(int newWeapon)
    {
        if (newWeapon == _currentWeapon) return;
        if (_isSwappingWeapon) return; // Can't swap mid-swap
        
        _weaponSwapFrom = _currentWeapon;
        _currentWeapon = newWeapon;
        _isSwappingWeapon = true;
        _weaponSwapAnim = 0;
        _idleTimer = 0; // Reset idle
    }
    
    void TriggerWeaponRecoil(double amount)
    {
        _weaponRecoil += amount;
        if (_weaponRecoil > 1) _weaponRecoil = 1;
    }
    
    void TriggerLandingSquash()
    {
        _landingSquash = 0.3;
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
            Log.Warning(ex, "Failed to save level score");
        }
    }

    void NextLevel()
    {
        _currentLevel++;
        _levelComplete = false;
        LevelCompleteScreen.Visibility = Visibility.Collapsed;

        // Check for campaign victory
        if (_gameMode == GameMode.Campaign && _currentLevel > _maxCampaignLevel)
        {
            ShowVictory();
            return;
        }

        // Load next level
        LoadLevel(_currentLevel);
        
        // Only capture mouse if not showing briefing (campaign mode shows briefing)
        if (!_showingBriefing)
        {
            CaptureMouse(true);
        }
    }
    
    void ShowVictory()
    {
        _victory = true;
        CaptureMouse(false);
        VictoryScreen.Visibility = Visibility.Visible;
        TotalScoreText.Text = $"FINAL SCORE: {_score:N0}";
        UpdateTopScoresUI();
        SayQuote("Who's the CEO now? ME!");
    }
    #endregion

    #region Rendering
    void Render()
    {
        // === OPTIMIZATION: FOV-specific trig values (base trig cached in GameLoop) ===
        double fovAngle = _isAiming ? 0.35 : 0.5;
        _sinPaMinus = Math.Sin(_pa - fovAngle);
        _cosPaMinus = Math.Cos(_pa - fovAngle);
        
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
        
        // === ROBUST ANIMATION: Kill flash (white flash on kill) ===
        if (_killFlashTimer > 0)
        {
            ApplyKillFlash(_killFlashTimer / 0.15);
        }
        
        // === ROBUST ANIMATION: Screen warp effect (trippy on kills) ===
        if (_screenWarpTimer > 0)
        {
            ApplyScreenWarp(_screenWarpTimer / 0.2);
        }
        
        // === ROBUST ANIMATION: Low health heartbeat vignette ===
        if (_hp < 30 && _hp > 0)
        {
            double heartbeatIntensity = Math.Pow(Math.Sin(_heartbeatPhase), 8);
            ApplyHeartbeatVignette(heartbeatIntensity, (30 - _hp) / 30.0);
        }
        
        // === ROBUST ANIMATION: Death screen effect ===
        if (_gameOver && _deathAnimTimer > 0)
        {
            ApplyDeathEffect(_deathAnimTimer);
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
        
        // Camera flash effect (bright white screen)
        if (_cameraFlashTimer > 0)
        {
            double flashIntensity = _cameraFlashTimer / 0.3;
            ApplyCameraFlash(flashIntensity);
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
        
        // === POST-PROCESSING EFFECTS ===
        
        // Cinematic vignette (always on, subtle)
        ApplyCinematicVignette();
        
        // Color grading for atmosphere
        ApplyColorGrading();
        
        // Optional scanlines for retro look
        ApplyScanlines();
        
        // Chromatic aberration on damage
        if (_damageVignetteTimer > 0.2)
        {
            ApplyChromaticAberration(_damageVignetteTimer * 0.5);
        }

        _bmp.WritePixels(new Int32Rect(0, 0, W, H), _px, W * 4, 0);

        // Update crosshair position based on pitch and aim zoom (scaled for resolution)
        double crosshairY = (H * 0.92) - _pitch;  // ~92% down the screen
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
    
    void ApplyCameraFlash(double intensity)
    {
        // Bright white flash that fades out
        for (int i = 0; i < _px.Length; i++)
        {
            uint c = _px[i];
            uint r = (uint)Math.Min(255, ((c >> 16) & 0xFF) + 255 * intensity);
            uint g = (uint)Math.Min(255, ((c >> 8) & 0xFF) + 255 * intensity);
            uint b = (uint)Math.Min(255, (c & 0xFF) + 255 * intensity);
            _px[i] = 0xFF000000 | (r << 16) | (g << 8) | b;
        }
    }
    
    // === ROBUST ANIMATION EFFECTS ===
    
    void ApplyKillFlash(double intensity)
    {
        // Quick white flash with creepy yellow tinge on kills
        for (int i = 0; i < _px.Length; i++)
        {
            uint c = _px[i];
            uint r = (uint)Math.Min(255, ((c >> 16) & 0xFF) + 255 * intensity * 0.9);
            uint g = (uint)Math.Min(255, ((c >> 8) & 0xFF) + 240 * intensity * 0.8);
            uint b = (uint)Math.Min(255, (c & 0xFF) + 180 * intensity * 0.5);
            _px[i] = 0xFF000000 | (r << 16) | (g << 8) | b;
        }
    }
    
    void ApplyScreenWarp(double intensity)
    {
        // Subtle trippy barrel distortion on kills
        uint[] tempPx = new uint[_px.Length];
        Array.Copy(_px, tempPx, _px.Length);
        
        double warpStrength = intensity * 0.02;
        int centerX = W / 2, centerY = H / 2;
        
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                double dx = (x - centerX) / (double)W;
                double dy = (y - centerY) / (double)H;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                
                // Barrel distortion
                double warp = 1 + dist * dist * warpStrength * Math.Sin(_gameTime * 20);
                
                int srcX = centerX + (int)((x - centerX) * warp);
                int srcY = centerY + (int)((y - centerY) * warp);
                
                srcX = Math.Clamp(srcX, 0, W - 1);
                srcY = Math.Clamp(srcY, 0, H - 1);
                
                _px[y * W + x] = tempPx[srcY * W + srcX];
            }
        }
    }
    
    void ApplyHeartbeatVignette(double beatIntensity, double healthFactor)
    {
        // Pulsing dark red vignette when low health - creepy heartbeat effect
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                double nx = (x / (double)W) * 2 - 1;
                double ny = (y / (double)H) * 2 - 1;
                double edge = Math.Max(Math.Abs(nx), Math.Abs(ny));
                
                double vignette = Math.Pow(edge, 1.5) * (0.3 + beatIntensity * 0.5) * healthFactor;
                
                if (vignette > 0.01)
                {
                    int i = y * W + x;
                    uint c = _px[i];
                    uint cr = (c >> 16) & 0xFF;
                    uint cg = (c >> 8) & 0xFF;
                    uint cb = c & 0xFF;
                    
                    // Dark red pulsing vignette
                    cr = (uint)Math.Min(255, cr + (100 * vignette * beatIntensity));
                    cg = (uint)(cg * (1 - vignette * 0.7));
                    cb = (uint)(cb * (1 - vignette * 0.7));
                    
                    _px[i] = 0xFF000000 | (cr << 16) | (cg << 8) | cb;
                }
            }
        }
    }
    
    void ApplyDeathEffect(double progress)
    {
        // Screen goes dark, grayscale, tilts (creepy death)
        double darkness = Math.Min(0.8, progress * 0.8);
        double grayscale = Math.Min(1.0, progress);
        
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                int i = y * W + x;
                uint c = _px[i];
                uint cr = (c >> 16) & 0xFF;
                uint cg = (c >> 8) & 0xFF;
                uint cb = c & 0xFF;
                
                // Convert to grayscale
                uint gray = (uint)(cr * 0.3 + cg * 0.59 + cb * 0.11);
                cr = (uint)(cr * (1 - grayscale) + gray * grayscale);
                cg = (uint)(cg * (1 - grayscale) + gray * grayscale);
                cb = (uint)(cb * (1 - grayscale) + gray * grayscale);
                
                // Darken
                cr = (uint)(cr * (1 - darkness));
                cg = (uint)(cg * (1 - darkness));
                cb = (uint)(cb * (1 - darkness));
                
                // Add slight red tint at edges (blood pooling effect)
                double edge = Math.Max(Math.Abs((x / (double)W) * 2 - 1), Math.Abs((y / (double)H) * 2 - 1));
                if (edge > 0.6 && progress > 0.3)
                {
                    double bloodTint = (edge - 0.6) * 2 * (progress - 0.3);
                    cr = (uint)Math.Min(255, cr + 80 * bloodTint);
                }
                
                _px[i] = 0xFF000000 | (cr << 16) | (cg << 8) | cb;
            }
        }
    }
    
    // === NEW POST-PROCESSING EFFECTS ===
    
    void ApplyCinematicVignette()
    {
        // Subtle dark edges for cinematic look
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                double nx = (x / (double)W) * 2 - 1;
                double ny = (y / (double)H) * 2 - 1;
                double dist = Math.Sqrt(nx * nx + ny * ny);
                
                // Smooth falloff vignette
                double vignette = Math.Pow(dist, 2.5) * 0.35;
                
                if (vignette > 0.01)
                {
                    int i = y * W + x;
                    uint c = _px[i];
                    double factor = 1 - vignette;
                    uint r = (uint)(((c >> 16) & 0xFF) * factor);
                    uint g = (uint)(((c >> 8) & 0xFF) * factor);
                    uint b = (uint)((c & 0xFF) * factor);
                    _px[i] = 0xFF000000 | (r << 16) | (g << 8) | b;
                }
            }
        }
    }
    
    void ApplyColorGrading()
    {
        // Cinematic color grading - slight teal shadows, orange highlights
        for (int i = 0; i < _px.Length; i++)
        {
            uint c = _px[i];
            double r = (c >> 16) & 0xFF;
            double g = (c >> 8) & 0xFF;
            double b = c & 0xFF;
            
            // Calculate luminance
            double lum = r * 0.3 + g * 0.59 + b * 0.11;
            
            // Teal in shadows (boost blue, reduce red in dark areas)
            if (lum < 80)
            {
                double shadowFactor = (80 - lum) / 80.0 * 0.15;
                r *= (1 - shadowFactor * 0.3);
                b = Math.Min(255, b + shadowFactor * 15);
            }
            
            // Orange in highlights (boost red/yellow in bright areas)
            if (lum > 160)
            {
                double highlightFactor = (lum - 160) / 95.0 * 0.12;
                r = Math.Min(255, r + highlightFactor * 20);
                g = Math.Min(255, g + highlightFactor * 8);
            }
            
            // Slight contrast boost
            r = Math.Clamp((r - 128) * 1.05 + 128, 0, 255);
            g = Math.Clamp((g - 128) * 1.05 + 128, 0, 255);
            b = Math.Clamp((b - 128) * 1.05 + 128, 0, 255);
            
            _px[i] = 0xFF000000 | ((uint)r << 16) | ((uint)g << 8) | (uint)b;
        }
    }
    
    void ApplyScanlines()
    {
        // Subtle CRT scanline effect
        for (int y = 0; y < H; y++)
        {
            if (y % 3 == 0) // Every 3rd line
            {
                double scanlineIntensity = 0.92; // Subtle darkening
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    uint c = _px[i];
                    uint r = (uint)(((c >> 16) & 0xFF) * scanlineIntensity);
                    uint g = (uint)(((c >> 8) & 0xFF) * scanlineIntensity);
                    uint b = (uint)((c & 0xFF) * scanlineIntensity);
                    _px[i] = 0xFF000000 | (r << 16) | (g << 8) | b;
                }
            }
        }
    }
    
    void ApplyChromaticAberration(double intensity)
    {
        // RGB channel separation on edges when damaged
        uint[] tempPx = new uint[_px.Length];
        Array.Copy(_px, tempPx, _px.Length);
        
        int offset = (int)(intensity * 4);
        if (offset < 1) return;
        
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                // Distance from center affects intensity
                double nx = (x / (double)W) * 2 - 1;
                double ny = (y / (double)H) * 2 - 1;
                double dist = Math.Sqrt(nx * nx + ny * ny);
                
                if (dist > 0.5)
                {
                    int localOffset = (int)(offset * (dist - 0.5) * 2);
                    
                    // Sample shifted channels
                    int xR = Math.Clamp(x + localOffset, 0, W - 1);
                    int xB = Math.Clamp(x - localOffset, 0, W - 1);
                    
                    uint cR = tempPx[y * W + xR];
                    uint cG = tempPx[y * W + x];
                    uint cB = tempPx[y * W + xB];
                    
                    uint r = (cR >> 16) & 0xFF;
                    uint g = (cG >> 8) & 0xFF;
                    uint b = cB & 0xFF;
                    
                    _px[y * W + x] = 0xFF000000 | (r << 16) | (g << 8) | b;
                }
            }
        }
    }
    
    void RenderParticles()
    {
        int horizon = H / 2 + (int)_pitch + (int)_eyeHeight;
        double fovMultiplier = _isAiming ? 0.4 : 0.66;
        
        // === OPTIMIZATION: Pre-calculate inverse determinant using cached trig ===
        double invDet = 1.0 / (_cosPaPlus * fovMultiplier * _sinPa - _cosPa * _sinPaPlus * fovMultiplier);
        
        for (int i = 0; i < _particles.Count; i++)
        {
            var p = _particles[i];
            double dx = p.x - _px_pos, dy = p.y - _py;
            double transformX = invDet * (_sinPa * dx - _cosPa * dy);
            double transformY = invDet * (-_sinPaPlus * fovMultiplier * dx + _cosPaPlus * fovMultiplier * dy);
            
            if (transformY <= 0.1) continue;
            int screenXCheck = (int)Math.Clamp(W / 2 * (1 + transformX / transformY), 0, W - 1);
            if (transformY >= _zBuffer[screenXCheck]) continue;
            
            int screenX = (int)(W / 2 * (1 + transformX / transformY));
            int screenY = horizon - (int)(20 / transformY); // Lift particles up
            int size = Math.Max(2, (int)(6 / transformY));
            
            // Fade based on life with glow effect
            uint baseColor = p.color;
            double lifeRatio = p.life;
            
            // Add glow for bright particles
            bool isGlowing = ((baseColor >> 16) & 0xFF) > 200 || ((baseColor >> 8) & 0xFF) > 200;
            
            for (int py = Math.Max(0, screenY - size); py < Math.Min(H, screenY + size); py++)
            {
                for (int px = Math.Max(0, screenX - size); px < Math.Min(W, screenX + size); px++)
                {
                    // === OPTIMIZATION: Avoid sqrt by using squared distance ===
                    int ddx = px - screenX, ddy = py - screenY;
                    double distSq = ddx * ddx + ddy * ddy;
                    double sizeSq = size * size;
                    if (distSq < sizeSq)
                    {
                        double intensity = (1 - Math.Sqrt(distSq) / size) * lifeRatio;
                        
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
        double fovAngle = _isAiming ? 0.35 : 0.5;

        // === OPTIMIZATION: Reuse pre-allocated light buffer ===
        _lightBuffer.Clear();
        
        // Add muzzle flash as a light source
        if (_muzzleFlashTimer > 0)
        {
            _lightBuffer.Add((_px_pos, _py, 1.0, 0.8, 0.4, _muzzleFlashTimer * 3));
        }
        
        // === OPTIMIZATION: Manual loop instead of LINQ to avoid allocations ===
        int lightCount = 0;
        for (int i = 0; i < _particles.Count && lightCount < 5; i++)
        {
            var p = _particles[i];
            if (((p.color >> 16) & 0xFF) > 200)
            {
                _lightBuffer.Add((p.x, p.y, 1.0, 0.6, 0.2, 0.8));
                lightCount++;
            }
        }

        // === OPTIMIZATION: Pre-calculate trig values for fov ===
        double cosPaPlus = Math.Cos(_pa + fovAngle);
        double sinPaPlus = Math.Sin(_pa + fovAngle);
        double cosPaMinus = _cosPaMinus;
        double sinPaMinus = _sinPaMinus;
        double stepDeltaX = (cosPaPlus - cosPaMinus) / W;
        double stepDeltaY = (sinPaPlus - sinPaMinus) / W;

        for (int y = 0; y < H; y++)
        {
            bool isFloor = y > horizon;
            double rowDist = Math.Abs((H / 2.0) / (y - horizon + 0.01));
            
            // Skip very distant rows for performance
            if (rowDist > 25) continue;

            double floorStepX = rowDist * stepDeltaX;
            double floorStepY = rowDist * stepDeltaY;

            double floorX = _px_pos + rowDist * cosPaMinus;
            double floorY = _py + rowDist * sinPaMinus;

            for (int x = 0; x < W; x++)
            {
                int tx = (int)(floorX * TEX) & (TEX - 1);
                int ty = (int)(floorY * TEX) & (TEX - 1);

                uint color = isFloor ? FloorTex[ty * TEX + tx] : CeilTex[ty * TEX + tx];
                
                // Get base color components
                double r = ((color >> 16) & 0xFF);
                double g = ((color >> 8) & 0xFF);
                double b = (color & 0xFF);
                
                // Check for hazard tile (sticky goo effect! 🟢)
                if (isFloor)
                {
                    int mapX = (int)floorX, mapY = (int)floorY;
                    if (mapX >= 0 && mapX < MAP_SIZE && mapY >= 0 && mapY < MAP_SIZE)
                    {
                        if (_hazardTiles[mapY * MAP_SIZE + mapX])
                        {
                            // Animated green goo - slime color with bubbles!
                            double glow = 0.5 + 0.5 * Math.Sin(_gameTime * 2 + floorX * 2 + floorY * 2);
                            double bubble = Math.Sin(_gameTime * 8 + floorX * 10) > 0.9 ? 1.0 : 0.0;
                            double ripple = Math.Sin(_gameTime * 3 + floorX * 4 + floorY * 4) * 0.15;
                            r = 30 + bubble * 50;
                            g = 180 + glow * 75 + ripple * 40;
                            b = 50 + glow * 30;
                        }
                    }
                }
                
                // === OPTIMIZATION: Use pre-allocated buffer, avoid allocations ===
                for (int li = 0; li < _lightBuffer.Count; li++)
                {
                    var light = _lightBuffer[li];
                    double ldx = floorX - light.x;
                    double ldy = floorY - light.y;
                    double distSq = ldx * ldx + ldy * ldy;
                    if (distSq < 16) // 4^2 = 16, avoid sqrt when possible
                    {
                        double lightDist = Math.Sqrt(distSq);
                        double lightFalloff = Math.Max(0, 1 - lightDist * 0.25) * light.intensity;
                        r += light.r * 100 * lightFalloff;
                        g += light.g * 100 * lightFalloff;
                        b += light.b * 100 * lightFalloff;
                    }
                }
                
                // Apply ambient color tint from level atmosphere
                double ambientR = ((_ambientColor >> 16) & 0xFF) / 255.0;
                double ambientG = ((_ambientColor >> 8) & 0xFF) / 255.0;
                double ambientB = (_ambientColor & 0xFF) / 255.0;
                r = r * (0.6 + 0.4 * ambientR);
                g = g * (0.6 + 0.4 * ambientG);
                b = b * (0.6 + 0.4 * ambientB);

                // Apply atmospheric fog
                double baseFog = Math.Min(0.9, rowDist / 16);
                double fog = baseFog + _fogDensity * rowDist * 0.5;
                fog = Math.Min(0.95, fog);
                
                // Get fog color components
                uint fogR = (_fogColor >> 16) & 0xFF;
                uint fogG = (_fogColor >> 8) & 0xFF;
                uint fogB = _fogColor & 0xFF;
                
                // Apply fog blending
                r = r * (1 - fog) + fogR * fog;
                g = g * (1 - fog) + fogG * fog;
                b = b * (1 - fog) + fogB * fog;
                
                // Apply light flicker for atmospheric horror
                if (_lightFlicker > 0)
                {
                    double flicker = 1.0 + (Math.Sin(_gameTime * 25) * Math.Sin(_gameTime * 17) * _lightFlicker);
                    r *= flicker;
                    g *= flicker;
                    b *= flicker;
                }

                _px[y * W + x] = 0xFF000000 | ((uint)Math.Clamp(r, 0, 255) << 16) | ((uint)Math.Clamp(g, 0, 255) << 8) | (uint)Math.Clamp(b, 0, 255);

                floorX += floorStepX;
                floorY += floorStepY;
            }
        }
    }

    void RenderWalls()
    {
        int horizon = H / 2 + (int)_pitch + (int)_eyeHeight;
        double fovMultiplier = _isAiming ? 0.4 : 0.66;
        
        // === OPTIMIZATION: Reuse light buffer (already populated by RenderFloorCeiling is okay, or re-populate) ===
        _lightBuffer.Clear();
        
        // Muzzle flash light
        if (_muzzleFlashTimer > 0)
        {
            _lightBuffer.Add((_px_pos, _py, 1.0, 0.8, 0.4, _muzzleFlashTimer * 4));
        }
        
        // === OPTIMIZATION: Manual loop instead of LINQ ===
        int projCount = Math.Min(5, _projectiles.Count);
        for (int i = 0; i < projCount; i++)
        {
            var proj = _projectiles[i];
            if (proj.FromPlayer)
                _lightBuffer.Add((proj.X, proj.Y, 1.0, 0.7, 0.3, 0.6));
            else
                _lightBuffer.Add((proj.X, proj.Y, 1.0, 0.3, 0.2, 0.5));
        }
        
        // Pickup glows - manual loop
        int pickupLightCount = 0;
        for (int i = 0; i < _pickups.Count && pickupLightCount < 10; i++)
        {
            var pickup = _pickups[i];
            if (pickup.Collected) continue;
            pickupLightCount++;
            switch (pickup.Type)
            {
                case PickupType.Health:
                case PickupType.AtomicHealth:
                    _lightBuffer.Add((pickup.X, pickup.Y, 0.2, 1.0, 0.2, 0.4));
                    break;
                case PickupType.Ammo:
                    _lightBuffer.Add((pickup.X, pickup.Y, 1.0, 0.8, 0.2, 0.3));
                    break;
            }
        }

        // === OPTIMIZATION: Use cached trig values ===
        double cosPa = _cosPa;
        double sinPa = _sinPa;
        double cosPaPlus = _cosPaPlus;
        double sinPaPlus = _sinPaPlus;

        for (int x = 0; x < W; x++)
        {
            double camX = 2.0 * x / W - 1;
            double rayDirX = cosPa + cosPaPlus * camX * fovMultiplier;
            double rayDirY = sinPa + sinPaPlus * camX * fovMultiplier;

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
            
            // Calculate wall world position for lighting
            double wallWorldX = side == 0 ? mapX + (stepX > 0 ? 0 : 1) : _px_pos + perpWallDist * rayDirX;
            double wallWorldY = side == 1 ? mapY + (stepY > 0 ? 0 : 1) : _py + perpWallDist * rayDirY;
            
            // === OPTIMIZATION: Manual loop with squared distance check ===
            double dynLightR = 0, dynLightG = 0, dynLightB = 0;
            for (int li = 0; li < _lightBuffer.Count; li++)
            {
                var light = _lightBuffer[li];
                double ldx = wallWorldX - light.x;
                double ldy = wallWorldY - light.y;
                double distSq = ldx * ldx + ldy * ldy;
                if (distSq < 36) // 6^2 = 36
                {
                    double lightDist = Math.Sqrt(distSq);
                    double falloff = Math.Max(0, 1 - lightDist / 6) * light.intensity;
                    dynLightR += light.r * falloff;
                    dynLightG += light.g * falloff;
                    dynLightB += light.b * falloff;
                }
            }

            for (int y = drawStart; y <= drawEnd; y++)
            {
                int texY = Math.Clamp((int)texPos, 0, TEX - 1);
                texPos += step;

                uint color = Textures[texId][texY * TEX + texX];
                
                // Side shading - opposite walls darker
                double sideFactor = side == 1 ? 0.7 : 1.0;
                
                // Extract color components
                double r = ((color >> 16) & 0xFF) * sideFactor;
                double g = ((color >> 8) & 0xFF) * sideFactor;
                double b = (color & 0xFF) * sideFactor;
                
                // Add dynamic lighting
                r += dynLightR * 150;
                g += dynLightG * 150;
                b += dynLightB * 150;
                
                // Height-based gradient (darker at bottom, lighter at top)
                double heightFactor = 0.85 + 0.15 * ((double)(y - drawStart) / (drawEnd - drawStart));
                r *= heightFactor;
                g *= heightFactor;
                b *= heightFactor;

                // Apply atmospheric fog based on level
                double baseFog = Math.Min(0.85, perpWallDist / 18);
                double fog = baseFog + _fogDensity * perpWallDist;
                fog = Math.Min(0.95, fog);
                
                // Get fog color components
                uint fogR = (_fogColor >> 16) & 0xFF;
                uint fogG = (_fogColor >> 8) & 0xFF;
                uint fogB = _fogColor & 0xFF;
                
                // Blend with fog color
                r = r * (1 - fog) + fogR * fog;
                g = g * (1 - fog) + fogG * fog;
                b = b * (1 - fog) + fogB * fog;
                
                // Apply light flicker effect
                if (_lightFlicker > 0)
                {
                    double flicker = 1.0 + (Math.Sin(_gameTime * 25) * Math.Sin(_gameTime * 17) * _lightFlicker);
                    r *= flicker;
                    g *= flicker;
                    b *= flicker;
                }

                _px[y * W + x] = 0xFF000000 | ((uint)Math.Clamp(r, 0, 255) << 16) | ((uint)Math.Clamp(g, 0, 255) << 8) | (uint)Math.Clamp(b, 0, 255);
            }
        }
    }

    void RenderSprites()
    {
        // === OPTIMIZATION: Reuse pre-allocated sprite buffer ===
        _spriteBuffer.Clear();

        // === OPTIMIZATION: Manual loops instead of LINQ to avoid allocations ===
        for (int i = 0; i < _enemies.Count; i++)
        {
            var en = _enemies[i];
            if (!en.Dead || en.DeathTimer < 1)
                _spriteBuffer.Add((Dist(en.X, en.Y), en, 0));
        }

        for (int i = 0; i < _pickups.Count; i++)
        {
            var p = _pickups[i];
            if (!p.Collected)
                _spriteBuffer.Add((Dist(p.X, p.Y), p, 1));
        }

        for (int i = 0; i < _projectiles.Count; i++)
            _spriteBuffer.Add((Dist(_projectiles[i].X, _projectiles[i].Y), _projectiles[i], 2));

        for (int i = 0; i < _pipeBombs.Count; i++)
            _spriteBuffer.Add((Dist(_pipeBombs[i].x, _pipeBombs[i].y), _pipeBombs[i], 3));
            
        // Add exploding barrels to sprite buffer
        for (int i = 0; i < _barrels.Count; i++)
        {
            var barrel = _barrels[i];
            if (!barrel.Exploded)
                _spriteBuffer.Add((Dist(barrel.X, barrel.Y), barrel, 4)); // type 4 = barrel
        }

        _spriteBuffer.Sort((a, b) => b.dist.CompareTo(a.dist));

        int horizon = H / 2 + (int)_pitch + (int)_eyeHeight;
        
        // Get fog color components
        uint fogR = (_fogColor >> 16) & 0xFF;
        uint fogG = (_fogColor >> 8) & 0xFF;
        uint fogB = _fogColor & 0xFF;

        // === OPTIMIZATION: Pre-calculate inverse determinant using cached trig ===
        double fovMult = 0.66;
        double invDet = 1.0 / (_cosPaPlus * fovMult * _sinPa - _cosPa * _sinPaPlus * fovMult);

        foreach (var (dist, obj, type) in _spriteBuffer)
        {
            double sx = 0, sy = 0;
            if (type == 0) { var e = (Enemy)obj; sx = e.X; sy = e.Y; }
            else if (type == 1) { var p = (Pickup)obj; sx = p.X; sy = p.Y; }
            else if (type == 2) { var p = (Projectile)obj; sx = p.X; sy = p.Y; }
            else if (type == 3) { var b = ((double x, double y, double timer))obj; sx = b.x; sy = b.y; }
            else if (type == 4) { var barrel = (ExplodingBarrel)obj; sx = barrel.X; sy = barrel.Y; }

            double dx = sx - _px_pos, dy = sy - _py;
            double transformX = invDet * (_sinPa * dx - _cosPa * dy);
            double transformY = invDet * (-_sinPaPlus * fovMult * dx + _cosPaPlus * fovMult * dy);

            if (transformY <= 0.1) continue;

            // Calculate fog for this sprite distance
            double baseFog = Math.Min(0.8, transformY / 14);
            double fog = baseFog + _fogDensity * transformY * 0.5;
            fog = Math.Min(0.9, fog);

            // --- Floor-level sprite fix ---
            double cameraZ = 32.0 + _pz + _eyeHeight;
            double spriteZ = 0.0;
            int floorOffset = (int)((cameraZ - spriteZ) / transformY);

            int spriteScreenX = (int)(W / 2 * (1 + transformX / transformY));
            int spriteHeight = (int)Math.Abs(H / transformY);
            int spriteWidth = spriteHeight;

            if (type == 1 || type == 3 || type == 4)
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

            // Sprite vertical center position accounting for floor offset
            int spriteCenterY = horizon + floorOffset;
            
            for (int stripe = drawStartX; stripe < drawEndX; stripe++)
            {
                if (transformY >= _zBuffer[stripe]) continue;

                for (int y = drawStartY; y < drawEndY; y++)
                {
                    double relX = (double)(stripe - (spriteScreenX - spriteWidth / 2)) / spriteWidth - 0.5;
                    double relY = (double)(y - (spriteCenterY - spriteHeight / 2)) / spriteHeight;

                    uint color = 0;
                    if (type == 0) color = GetEnemyColor((Enemy)obj, relX, relY);
                    else if (type == 1) color = GetPickupColor((Pickup)obj, relX, relY);
                    else if (type == 2)
                    {
                        // Projectile glow effect
                        double distFromCenter = relX * relX + relY * relY;
                        if (distFromCenter < 0.2)
                        {
                            double glowIntensity = 1 - distFromCenter / 0.2;
                            bool fromPlayer = ((Projectile)obj).FromPlayer;
                            uint baseR = fromPlayer ? 255u : 255u;
                            uint baseG = fromPlayer ? 170u : 68u;
                            uint baseB = fromPlayer ? 0u : 0u;
                            uint coreR = (uint)Math.Min(255, baseR + glowIntensity * 55);
                            uint coreG = (uint)Math.Min(255, baseG + glowIntensity * 85);
                            uint coreB = (uint)Math.Min(255, baseB + glowIntensity * 100);
                            color = 0xFF000000 | (coreR << 16) | (coreG << 8) | coreB;
                        }
                    }
                    else if (type == 3)
                    {
                        // Pipe bomb
                        if (relX * relX + relY * relY < 0.15) color = 0xFF44AA44u;
                    }
                    else if (type == 4)
                    {
                        // Exploding barrel - red drum with warning stripes!
                        color = GetBarrelColor((ExplodingBarrel)obj, relX, relY);
                    }

                    if ((color & 0xFF000000) != 0)
                    {
                        // Extract color components
                        double r = ((color >> 16) & 0xFF);
                        double g = ((color >> 8) & 0xFF);
                        double b = (color & 0xFF);
                        
                        // Apply atmospheric fog
                        r = r * (1 - fog) + fogR * fog;
                        g = g * (1 - fog) + fogG * fog;
                        b = b * (1 - fog) + fogB * fog;
                        
                        // Apply light flicker
                        if (_lightFlicker > 0)
                        {
                            double flicker = 1.0 + (Math.Sin(_gameTime * 25) * Math.Sin(_gameTime * 17) * _lightFlicker);
                            r *= flicker;
                            g *= flicker;
                            b *= flicker;
                        }
                        
                        _px[y * W + stripe] = 0xFF000000 | ((uint)Math.Clamp(r, 0, 255) << 16) | ((uint)Math.Clamp(g, 0, 255) << 8) | (uint)Math.Clamp(b, 0, 255);
                    }
                }
            }
        }
    }
    
    void RenderBodyParts()
    {
        // === OPTIMIZATION: Sort in place without creating new list ===
        _bodyParts.Sort((a, b) => {
            double distA = (a.X - _px_pos) * (a.X - _px_pos) + (a.Y - _py) * (a.Y - _py);
            double distB = (b.X - _px_pos) * (b.X - _px_pos) + (b.Y - _py) * (b.Y - _py);
            return distB.CompareTo(distA);
        });
        int horizon = H / 2 + (int)_pitch + (int)_eyeHeight;
        
        // === OPTIMIZATION: Use cached trig values ===
        double fovMult = 0.66;
        double invDet = 1.0 / (_cosPaPlus * fovMult * _sinPa - _cosPa * _sinPaPlus * fovMult);
        
        for (int i = 0; i < _bodyParts.Count; i++)
        {
            var part = _bodyParts[i];
            double dx = part.X - _px_pos;
            double dy = part.Y - _py;
            
            // Camera transform - use cached values
            double transformX = invDet * (_sinPa * dx - _cosPa * dy);
            double transformY = invDet * (-_sinPaPlus * fovMult * dx + _cosPaPlus * fovMult * dy);
            
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
            
            // Pre-calculate rotation values
            double rot = part.Rotation;
            double cosRot = Math.Cos(rot);
            double sinRot = Math.Sin(rot);
            double invSize = 1.0 / size;
            double halfSize = size / 2.0;
            
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
                    double nx = (px - halfSize) * invSize;
                    double ny = (py - halfSize) * invSize;
                    
                    // Apply rotation (use pre-calculated sin/cos)
                    double rx = nx * cosRot - ny * sinRot;
                    double ry = nx * sinRot + ny * cosRot;
                    
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

    // Duke Nukem 3D style detailed pixel art sprites
    uint GetEnemyColor(Enemy en, double x, double y)
    {
        // Dead enemy - corpse on ground
        if (en.Dead)
        {
            if (y > 0.35 && Math.Abs(x) < 0.4)
            {
                // Blood pool spreading
                double bloodDist = x * x + (y - 0.6) * (y - 0.6);
                if (bloodDist < 0.08 + en.DeathTimer * 0.02)
                    return 0xFF660000u;
                // Body
                double shade = 0.2 + (y - 0.35) * 0.3;
                uint bodyColor = en.Type switch
                {
                    EnemyType.Trooper => 0xFF445522u,
                    EnemyType.PigCop => 0xFF334488u,
                    EnemyType.Enforcer => 0xFF222233u,
                    EnemyType.Octabrain => 0xFF553355u,
                    EnemyType.BattleLord => 0xFF886644u,
                    _ => 0xFF444444u
                };
                uint r = (uint)(((bodyColor >> 16) & 0xFF) * shade);
                uint g = (uint)(((bodyColor >> 8) & 0xFF) * shade);
                uint b = (uint)((bodyColor & 0xFF) * shade);
                return 0xFF000000 | (r << 16) | (g << 8) | b;
            }
            return 0;
        }

        // Animation wobble
        double wobbleAmount = Math.Sin(en.WobblePhase) * 0.06 * (1 - y);
        double bobAmount = Math.Sin(en.BobPhase) * 0.025;
        if (en.IsStunned) wobbleAmount *= 3;
        x -= wobbleAmount + en.LeanAngle * 0.08;
        y -= bobAmount;

        bool hurt = en.HurtTimer > 0;
        double hurtFlash = hurt ? 0.5 + Math.Sin(_gameTime * 30) * 0.5 : 0;

        switch (en.Type)
        {
            case EnemyType.Trooper:
                // Possessed office worker - corporate zombie with glowing badge - ENHANCED
                {
                    uint suit = 0xFF2A2A35u;
                    uint suitLight = 0xFF3A3A48u;
                    uint suitDark = 0xFF1A1A22u;
                    uint suitShadow = 0xFF0F0F15u;
                    uint shirt = 0xFFCCCCDDu;
                    uint shirtWrinkle = 0xFFAAAABBu;
                    uint tie = 0xFF882222u;
                    uint tieDark = 0xFF661111u;
                    uint tieStripe = 0xFF333333u;
                    uint skin = 0xFF99887Au;
                    uint skinLight = 0xFFAA9988u;
                    uint skinDark = 0xFF776655u;
                    uint skinShadow = 0xFF554433u;
                    uint eyeGlow = 0xFF44FF44u;
                    uint eyeCore = 0xFFAAFFAAu;
                    uint badge = 0xFFFFDD44u;
                    uint badgeGlow = 0xFFFFFF88u;
                    uint hair = 0xFF221100u;
                    uint hairHighlight = 0xFF332211u;
                    
                    if (hurt) { skin = 0xFFBB9988u; eyeGlow = 0xFFFF4444u; eyeCore = 0xFFFFAAAAu; }

                    // Head with receding hairline and hollow eyes
                    if (y < 0.30)
                    {
                        double headX = x * 1.2;
                        double headY = y - 0.15;
                        double headDist = headX * headX + headY * headY;
                        
                        if (headDist < 0.032)
                        {
                            // Receding hairline (patchy)
                            if (y < 0.08 && headDist < 0.025)
                            {
                                double hairPattern = Math.Sin(x * 40) * Math.Cos(y * 30);
                                if (hairPattern > 0.2 && Math.Abs(x) < 0.08)
                                {
                                    if (hairPattern > 0.6) return hairHighlight;
                                    return hair;
                                }
                            }
                            
                            // Hollow glowing eyes (corporate drone stare)
                            double leftEyeX = x + 0.04, leftEyeY = y - 0.12;
                            double rightEyeX = x - 0.04, rightEyeY = y - 0.12;
                            double leftEye = leftEyeX * leftEyeX + leftEyeY * leftEyeY;
                            double rightEye = rightEyeX * rightEyeX + rightEyeY * rightEyeY;
                            
                            if (leftEye < 0.0008 || rightEye < 0.0008) return eyeCore;
                            if (leftEye < 0.0025 || rightEye < 0.0025) return eyeGlow;
                            if (leftEye < 0.004 || rightEye < 0.004) return 0xFF112211u; // socket shadow
                            
                            // Sunken cheeks
                            double cheekL = (x + 0.06) * (x + 0.06) + (y - 0.18) * (y - 0.18);
                            double cheekR = (x - 0.06) * (x - 0.06) + (y - 0.18) * (y - 0.18);
                            if (cheekL < 0.002 || cheekR < 0.002) return skinShadow;
                            
                            // Slack jaw / mouth
                            if (y > 0.20 && y < 0.25 && Math.Abs(x) < 0.03)
                            {
                                if (y > 0.22) return 0xFF221111u; // mouth interior
                                return skinDark;
                            }
                            
                            // Face shading
                            double faceShade = headDist / 0.032;
                            if (faceShade > 0.85) return skinShadow;
                            if (faceShade > 0.6) return skinDark;
                            if (x < -0.02) return skinLight; // light from left
                            return skin;
                        }
                    }
                    
                    // Neck with loosened collar
                    if (y >= 0.26 && y < 0.33 && Math.Abs(x) < 0.07)
                    {
                        // Collar points
                        if (y < 0.30 && Math.Abs(x) > 0.03) return shirt;
                        return skin;
                    }
                    
                    // Torso - wrinkled suit with crooked tie
                    if (y >= 0.30 && y < 0.65)
                    {
                        double torsoWidth = 0.17 - (y - 0.48) * 0.08;
                        if (Math.Abs(x) < torsoWidth)
                        {
                            // Crooked tie (slightly askew)
                            double tieX = x + Math.Sin(y * 8) * 0.008; // slight wave
                            if (Math.Abs(tieX) < 0.025 && y > 0.32 && y < 0.58)
                            {
                                // Tie knot
                                if (y < 0.36)
                                {
                                    if (Math.Abs(tieX) < 0.015) return tie;
                                    return tieDark;
                                }
                                // Diagonal stripes
                                int stripe = (int)((tieX * 30 + y * 20)) % 4;
                                if (stripe == 0) return tieStripe;
                                if (Math.Abs(tieX) > 0.018) return tieDark;
                                return tie;
                            }
                            
                            // Shirt visible under jacket
                            if (Math.Abs(x) < 0.06 && y > 0.32 && y < 0.55)
                            {
                                // Button line
                                if (Math.Abs(x) < 0.008)
                                {
                                    int btn = (int)((y - 0.32) * 25);
                                    if (btn % 5 == 0) return 0xFFAAAA99u;
                                }
                                // Wrinkles
                                double wrinkle = Math.Sin(x * 50 + y * 30);
                                if (wrinkle > 0.7) return shirtWrinkle;
                                return shirt;
                            }
                            
                            // Glowing ID badge on chest
                            if (y > 0.38 && y < 0.48 && x > 0.06 && x < 0.13)
                            {
                                double badgePulse = 0.7 + Math.Sin(_gameTime * 4) * 0.3;
                                double badgeDist = (x - 0.095) * (x - 0.095) + (y - 0.43) * (y - 0.43);
                                if (badgeDist < 0.001) return (uint)(0xFF000000 | ((uint)(255 * badgePulse) << 8) | (uint)(68 * badgePulse));
                                if (badgeDist < 0.003)
                                {
                                    if (badgePulse > 0.85) return badgeGlow;
                                    return badge;
                                }
                                return suitDark; // badge backing
                            }
                            
                            // Suit jacket with wrinkles
                            double suitShade = Math.Abs(x) / torsoWidth;
                            double wrinklePat = Math.Sin(x * 25 + y * 15) * Math.Sin(y * 20);
                            if (wrinklePat > 0.5) return suitLight;
                            if (wrinklePat < -0.5) return suitShadow;
                            if (suitShade > 0.85) return suitShadow;
                            if (suitShade > 0.6) return suitDark;
                            return suit;
                        }
                    }
                    
                    // Arms in suit sleeves
                    if (y > 0.32 && y < 0.58)
                    {
                        double armX = Math.Abs(x) - 0.15;
                        if (armX > 0 && armX < 0.11)
                        {
                            // Sleeve wrinkles
                            int sleeveWrinkle = (int)(y * 35);
                            if (sleeveWrinkle % 4 == 0) return suitLight;
                            if (armX > 0.08) return suitShadow;
                            return suit;
                        }
                    }
                    
                    // Pale hands with too-long fingers
                    if (y > 0.52 && y < 0.68 && Math.Abs(x) > 0.20 && Math.Abs(x) < 0.32)
                    {
                        // Individual creepy fingers
                        for (int f = 0; f < 4; f++)
                        {
                            double fingerX = (x > 0 ? 0.24 : -0.24) + (f - 1.5) * 0.018;
                            if (Math.Abs(Math.Abs(x) - Math.Abs(fingerX)) < 0.008 && y > 0.58)
                            {
                                // Long fingernails
                                if (y > 0.64) return 0xFFDDDDCCu;
                                return skinLight;
                            }
                        }
                        return skin;
                    }
                    
                    // Legs - suit pants
                    if (y >= 0.62 && y < 0.98)
                    {
                        double legOffset = 0.055;
                        double leftLeg = Math.Abs(x + legOffset);
                        double rightLeg = Math.Abs(x - legOffset);
                        
                        if (leftLeg < 0.055 || rightLeg < 0.055)
                        {
                            // Dress shoes
                            if (y > 0.90)
                            {
                                if (y > 0.94) return 0xFF111111u;
                                return 0xFF222222u;
                            }
                            // Pant creases
                            double crease = (leftLeg < 0.055 ? leftLeg : rightLeg);
                            if (crease < 0.015) return suitLight;
                            if (crease > 0.045) return suitShadow;
                            return suit;
                        }
                    }
                    return 0;
                }

            case EnemyType.PigCop:
                // Middle Manager - bloated suit with too-wide smile - ENHANCED
                {
                    uint skinPale = 0xFFEEDDCCu;
                    uint skinLight = 0xFFFFEEDDu;
                    uint skinMid = 0xFFDDCCBBu;
                    uint skinDark = 0xFFCCBBAAu;
                    uint skinShadow = 0xFFAA9988u;
                    uint suit = 0xFF3A3A4Au;
                    uint suitLight = 0xFF4A4A5Au;
                    uint suitDark = 0xFF2A2A3Au;
                    uint shirt = 0xFFEEEEFFu;
                    uint tie = 0xFFDD8800u; // Power tie
                    uint tieDark = 0xFFBB6600u;
                    uint eyeWhite = 0xFFFFFFFFu;
                    uint pupil = 0xFF000000u;
                    uint teethWhite = 0xFFFFFFEEu;
                    uint gums = 0xFFCC6666u;
                    uint hair = 0xFF553322u;
                    
                    if (hurt) { skinPale = 0xFFFFEEDDu; suit = 0xFF5A3A4Au; }

                    // Balding head with combover
                    if (y < 0.34)
                    {
                        double headDist = (x * 0.9) * (x * 0.9) + (y - 0.17) * (y - 0.17);
                        
                        if (headDist < 0.05)
                        {
                            // Sad combover (wispy strands)
                            if (y < 0.08)
                            {
                                double combover = Math.Sin(x * 25 + _gameTime * 0.5);
                                if (combover > 0.4 && x > -0.08) return hair;
                            }
                            
                            // Shiny bald spot
                            if (y < 0.10 && Math.Abs(x) < 0.06)
                            {
                                double shine = Math.Sin(x * 20) * Math.Cos(y * 20);
                                if (shine > 0.5) return skinLight;
                                return skinPale;
                            }
                            
                            // Beady eyes (too small, too close)
                            double leftEyeX = x + 0.035, leftEyeY = y - 0.14;
                            double rightEyeX = x - 0.035, rightEyeY = y - 0.14;
                            double leftEye = leftEyeX * leftEyeX + leftEyeY * leftEyeY;
                            double rightEye = rightEyeX * rightEyeX + rightEyeY * rightEyeY;
                            
                            if (leftEye < 0.0004 || rightEye < 0.0004) return pupil;
                            if (leftEye < 0.0015 || rightEye < 0.0015) return eyeWhite;
                            
                            // Bags under eyes
                            if (y > 0.15 && y < 0.18 && (Math.Abs(x - 0.035) < 0.025 || Math.Abs(x + 0.035) < 0.025))
                                return skinShadow;
                            
                            // Too-wide smile (unsettling)
                            if (y > 0.20 && y < 0.28)
                            {
                                double smileWidth = 0.12 - (y - 0.24) * (y - 0.24) * 8;
                                if (Math.Abs(x) < smileWidth && smileWidth > 0)
                                {
                                    // Teeth (too many, too perfect)
                                    if (y > 0.22 && y < 0.26)
                                    {
                                        int tooth = (int)((x + smileWidth) * 30);
                                        if (tooth % 2 == 0) return teethWhite;
                                        return 0xFFEEEEDDu;
                                    }
                                    return gums;
                                }
                            }
                            
                            // Double chin
                            if (y > 0.26 && y < 0.32 && Math.Abs(x) < 0.08)
                            {
                                double chin = Math.Sin(y * 20);
                                if (chin > 0.5) return skinDark;
                            }
                            
                            // Face shading
                            double faceShade = headDist / 0.05;
                            if (faceShade > 0.8) return skinShadow;
                            if (faceShade > 0.5) return skinMid;
                            return skinPale;
                        }
                    }
                    
                    // Thick neck with collar strain
                    if (y >= 0.30 && y < 0.38 && Math.Abs(x) < 0.11)
                    {
                        // Shirt collar (tight)
                        if (y > 0.34 && Math.Abs(x) > 0.05) return shirt;
                        // Neck fat rolls
                        int roll = (int)(y * 40);
                        if (roll % 3 == 0) return skinDark;
                        return skinPale;
                    }
                    
                    // Broad torso (ill-fitting suit)
                    if (y >= 0.36 && y < 0.70)
                    {
                        double bodyWidth = 0.22 - (y - 0.53) * 0.06;
                        if (Math.Abs(x) < bodyWidth)
                        {
                            // Power tie (gaudy)
                            if (Math.Abs(x) < 0.03 && y > 0.38 && y < 0.62)
                            {
                                // Tie pattern (diagonal stripes)
                                int stripe = (int)((x * 20 + y * 15)) % 3;
                                if (stripe == 0) return 0xFFFFAA00u;
                                if (Math.Abs(x) > 0.02) return tieDark;
                                return tie;
                            }
                            
                            // Shirt front
                            if (Math.Abs(x) < 0.07 && y > 0.38 && y < 0.58) return shirt;
                            
                            // Suit buttons (straining)
                            if (Math.Abs(x) < 0.08 && y > 0.50 && y < 0.58)
                            {
                                int btn = (int)((y - 0.50) * 30);
                                if (btn % 6 == 0) return 0xFFAAAA88u;
                            }
                            
                            // Belt (struggling)
                            if (y > 0.60 && y < 0.67)
                            {
                                if (Math.Abs(x) < 0.04) return 0xFFCCAA44u;
                                return 0xFF443322u;
                            }
                            
                            // Suit wrinkles from poor fit
                            double wrinkle = Math.Sin(x * 20 + y * 25);
                            if (wrinkle > 0.6) return suitLight;
                            if (wrinkle < -0.6) return suitDark;
                            return suit;
                        }
                    }
                    
                    // Pudgy arms
                    if (y > 0.38 && y < 0.62)
                    {
                        double armX = Math.Abs(x) - 0.20;
                        if (armX > 0 && armX < 0.10)
                        {
                            if (y < 0.50) return suit;
                            // Rolled up sleeves showing pale arms
                            double armShade = armX / 0.10;
                            if (armShade > 0.7) return skinShadow;
                            return skinPale;
                        }
                    }
                    
                    // Meaty hands (gold ring visible)
                    if (y > 0.58 && y < 0.70 && Math.Abs(x) > 0.26 && Math.Abs(x) < 0.35)
                    {
                        // Ring on finger
                        if (y > 0.62 && y < 0.65 && Math.Abs(Math.Abs(x) - 0.29) < 0.015)
                            return 0xFFDDBB44u;
                        return skinMid;
                    }
                    
                    // Legs (pleated pants)
                    if (y >= 0.68 && y < 0.98)
                    {
                        double legOffset = 0.08;
                        double leftLeg = Math.Abs(x + legOffset);
                        double rightLeg = Math.Abs(x - legOffset);
                        
                        if (leftLeg < 0.065 || rightLeg < 0.065)
                        {
                            // Dress shoes
                            if (y > 0.92)
                            {
                                if (y > 0.95) return 0xFF1A1A1Au;
                                return 0xFF2A2A2Au;
                            }
                            // Pant pleats
                            double pleat = (leftLeg < 0.065 ? leftLeg : rightLeg);
                            if (pleat < 0.02) return suitLight;
                            return suitDark;
                        }
                    }
                    return 0;
                }

            case EnemyType.Enforcer:
                // Security Guard - faceless with riot gear and taser - ENHANCED
                {
                    uint helmet = 0xFF1A1A22u;
                    uint helmetLight = 0xFF2A2A35u;
                    uint helmetDark = 0xFF0A0A12u;
                    uint visor = 0xFF112233u;
                    uint visorGlow = 0xFF2244AAu;
                    uint armor = 0xFF252530u;
                    uint armorLight = 0xFF353545u;
                    uint armorDark = 0xFF151520u;
                    uint vest = 0xFF222228u;
                    uint vestText = 0xFFCCCCCCu;
                    uint skin = 0xFF887766u;
                    uint skinDark = 0xFF665544u;
                    uint taserBlue = 0xFF4488FFu;
                    
                    if (hurt) { helmet = 0xFF2A2A35u; visorGlow = 0xFF66AAFFu; }

                    // Riot helmet (face completely hidden)
                    if (y < 0.32)
                    {
                        double headDist = x * x + (y - 0.16) * (y - 0.16);
                        
                        if (headDist < 0.038)
                        {
                            // Face shield (reflective, no features visible)
                            if (y > 0.08 && y < 0.26 && Math.Abs(x) < 0.11)
                            {
                                // Reflection effect
                                double reflect = Math.Sin(x * 15 + y * 10 + _gameTime * 0.5);
                                if (reflect > 0.6) return visorGlow;
                                // Just darkness behind visor (unsettling)
                                double depth = (y - 0.08) / 0.18;
                                if (depth > 0.7) return 0xFF050510u;
                                return visor;
                            }
                            
                            // Helmet dome with vents
                            if (y < 0.12)
                            {
                                // Vent slits
                                int vent = (int)(x * 30);
                                if (vent % 4 == 0 && Math.Abs(x) < 0.08) return helmetDark;
                            }
                            
                            // Helmet surface
                            double helmShade = headDist / 0.038;
                            if (helmShade > 0.85) return helmetDark;
                            if (helmShade > 0.5) return helmet;
                            if (x < -0.03) return helmetLight;
                            return helmet;
                        }
                    }
                    
                    // Neck guard
                    if (y >= 0.28 && y < 0.34 && Math.Abs(x) < 0.08)
                    {
                        int guard = (int)(y * 40);
                        if (guard % 2 == 0) return armorDark;
                        return armor;
                    }
                    
                    // Body armor with "SECURITY" text
                    if (y >= 0.32 && y < 0.68)
                    {
                        double torsoWidth = 0.19 - (y - 0.50) * 0.06;
                        if (Math.Abs(x) < torsoWidth)
                        {
                            // "SECURITY" text area
                            if (y > 0.36 && y < 0.44 && Math.Abs(x) < 0.12)
                            {
                                // Blocky text pattern
                                int tx = (int)((x + 0.12) * 18);
                                int ty = (int)((y - 0.36) * 20);
                                // Simulated text
                                if (ty > 1 && ty < 5 && tx % 3 != 0)
                                    return vestText;
                            }
                            
                            // Chest plate segments
                            if (y > 0.44 && y < 0.58)
                            {
                                int segment = (int)((y - 0.44) * 18);
                                if (segment % 4 == 0) return armorLight;
                                if (segment % 4 == 3) return armorDark;
                            }
                            
                            // Utility belt
                            if (y > 0.58 && y < 0.65)
                            {
                                // Pouches
                                int pouch = (int)((x + 0.15) * 8);
                                if (pouch % 3 == 0) return armorDark;
                                // Radio on belt
                                if (x > 0.08 && x < 0.14) return 0xFF111115u;
                                return armor;
                            }
                            
                            // Vest texture
                            double vestShade = Math.Abs(x) / torsoWidth;
                            if (vestShade > 0.85) return armorDark;
                            if (vestShade > 0.6) return armor;
                            return vest;
                        }
                    }
                    
                    // Armored arms
                    if (y > 0.34 && y < 0.60)
                    {
                        double armX = Math.Abs(x) - 0.17;
                        if (armX > 0 && armX < 0.11)
                        {
                            // Elbow pad
                            if (y > 0.44 && y < 0.52)
                            {
                                double padDist = (y - 0.48) * (y - 0.48);
                                if (padDist < 0.001) return armorLight;
                                return armor;
                            }
                            return vest;
                        }
                    }
                    
                    // Gloved hands holding taser
                    if (y > 0.56 && y < 0.70 && Math.Abs(x) > 0.24 && Math.Abs(x) < 0.34)
                    {
                        // Taser in hand (crackling)
                        if (x > 0.28 && y > 0.60)
                        {
                            double crackle = Math.Sin(_gameTime * 20 + y * 30);
                            if (crackle > 0.7) return 0xFFFFFFFFu;
                            return taserBlue;
                        }
                        return 0xFF111111u; // Black gloves
                    }
                    
                    // Tactical pants
                    if (y >= 0.65 && y < 0.98)
                    {
                        double legOffset = 0.065;
                        double leftLeg = Math.Abs(x + legOffset);
                        double rightLeg = Math.Abs(x - legOffset);
                        
                        if (leftLeg < 0.06 || rightLeg < 0.06)
                        {
                            // Combat boots
                            if (y > 0.90)
                            {
                                if (y > 0.94) return 0xFF080808u;
                                return 0xFF121212u;
                            }
                            // Knee pads
                            if (y > 0.72 && y < 0.80)
                            {
                                double kneeDist = (y - 0.76) * (y - 0.76);
                                if (kneeDist < 0.001) return armorLight;
                                return armor;
                            }
                            // Cargo pockets
                            if (y > 0.68 && y < 0.72)
                            {
                                double leg = leftLeg < 0.06 ? leftLeg : rightLeg;
                                if (leg > 0.03) return armorDark;
                            }
                            return vest;
                        }
                    }
                    return 0;
                }

            case EnemyType.Octabrain:
                // IT Department Head - floating monitors for a head, cable tentacles - ENHANCED
                {
                    uint monitorFrame = 0xFF333340u;
                    uint monitorDark = 0xFF222230u;
                    uint screenGlow = 0xFF224488u;
                    uint screenBright = 0xFF4488CCu;
                    uint screenText = 0xFF88FFAAu;
                    uint cableBlack = 0xFF1A1A1Au;
                    uint cableColor = 0xFF2244AAu;
                    uint sparks = 0xFFFFFF88u;
                    uint labCoat = 0xFFDDDDEEu;
                    uint labCoatDark = 0xFFBBBBCCu;
                    
                    if (hurt) { screenGlow = 0xFF4466AAu; screenText = 0xFFFF8888u; }

                    // Monitor head (CRT style, floating)
                    double monitorY = 0.22;
                    double monitorDist = (x * 1.1) * (x * 1.1) + (y - monitorY) * (y - monitorY);
                    
                    if (monitorDist < 0.08)
                    {
                        // Screen bezel
                        if (monitorDist > 0.06) return monitorFrame;
                        
                        // CRT screen with static/code
                        if (monitorDist < 0.055)
                        {
                            // Scanline effect
                            int scanline = (int)((y + _gameTime * 0.5) * 60);
                            double scanIntensity = (scanline % 2 == 0) ? 1.0 : 0.85;
                            
                            // Creepy face made of ASCII/code
                            double faceX = x, faceY = y - monitorY;
                            
                            // Eyes (made of characters)
                            double leftEye = (faceX + 0.06) * (faceX + 0.06) + (faceY + 0.02) * (faceY + 0.02);
                            double rightEye = (faceX - 0.06) * (faceX - 0.06) + (faceY + 0.02) * (faceY + 0.02);
                            if (leftEye < 0.0015 || rightEye < 0.0015)
                            {
                                // Pupil glitch
                                double glitch = Math.Sin(_gameTime * 8);
                                if (glitch > 0.8) return sparks;
                                return screenText;
                            }
                            
                            // Mouth (text scrolling)
                            if (faceY > 0.04 && faceY < 0.10 && Math.Abs(faceX) < 0.10)
                            {
                                int mouthChar = (int)((faceX + _gameTime * 2) * 20) % 4;
                                if (mouthChar == 0) return screenText;
                                return screenGlow;
                            }
                            
                            // Random static/code
                            double staticNoise = Math.Sin(x * 50 + y * 40 + _gameTime * 10);
                            if (staticNoise > 0.85) return screenText;
                            if (staticNoise > 0.7) return screenBright;
                            
                            uint r = (uint)(34 * scanIntensity);
                            uint g = (uint)(68 * scanIntensity);
                            uint b = (uint)(136 * scanIntensity);
                            return 0xFF000000 | (r << 16) | (g << 8) | b;
                        }
                        return monitorDark;
                    }
                    
                    // Floating body in lab coat
                    if (y > 0.38 && y < 0.65 && Math.Abs(x) < 0.12)
                    {
                        // Lab coat buttons
                        if (Math.Abs(x) < 0.015)
                        {
                            int btn = (int)((y - 0.38) * 30);
                            if (btn % 5 == 0) return 0xFF888888u;
                        }
                        // Pocket
                        if (x > 0.03 && x < 0.09 && y > 0.42 && y < 0.50)
                        {
                            // Pens in pocket
                            if (y < 0.44) return 0xFF2222AAu;
                            return labCoatDark;
                        }
                        // Shading
                        double coatShade = Math.Abs(x) / 0.12;
                        if (coatShade > 0.7) return labCoatDark;
                        return labCoat;
                    }
                    
                    // Cable tentacles (network/power cables)
                    if (y > 0.50 && y < 0.98)
                    {
                        double cablePhase = _gameTime * 2;
                        
                        for (int c = 0; c < 6; c++)
                        {
                            double cAngle = c * Math.PI / 3 - Math.PI / 2;
                            double cBaseX = Math.Sin(cAngle) * 0.06;
                            double cWave = Math.Sin(cablePhase + c * 1.5 + y * 5) * 0.08 * (y - 0.50);
                            
                            double cX = cBaseX + cWave;
                            double cableDist = Math.Abs(x - cX);
                            double cableWidth = 0.018 - (y - 0.50) * 0.012;
                            
                            if (cableDist < cableWidth && cableWidth > 0)
                            {
                                // Cable color bands (ethernet style)
                                int band = (int)(y * 30 + c);
                                if (band % 4 == 0) return cableColor;
                                // Sparks at ends
                                if (y > 0.90)
                                {
                                    double spark = Math.Sin(_gameTime * 15 + c);
                                    if (spark > 0.7) return sparks;
                                }
                                return cableBlack;
                            }
                        }
                    }
                    return 0;
                }

            case EnemyType.BattleLord:
                // The CEO - massive corporate demon in power suit, multiple arms - BOSS ENHANCED
                {
                    uint suit = 0xFF0A0A12u;
                    uint suitLight = 0xFF1A1A25u;
                    uint suitDark = 0xFF050508u;
                    uint shirt = 0xFFCC0000u; // Power red
                    uint shirtDark = 0xFF990000u;
                    uint skin = 0xFF776655u;
                    uint skinLight = 0xFF887766u;
                    uint skinDark = 0xFF554433u;
                    uint eyeGold = 0xFFFFDD00u;
                    uint eyeGlow = 0xFFFFFF44u;
                    uint teethGold = 0xFFDDBB44u;
                    uint horns = 0xFF222222u;
                    uint hornsLight = 0xFF333333u;
                    uint watchGold = 0xFFDDAA33u;
                    
                    if (hurt) { suit = 0xFF1A1A25u; eyeGold = 0xFFFFFF88u; }

                    // Massive head with slicked back hair and devil horns
                    if (y < 0.32)
                    {
                        double headDist = (x * 0.85) * (x * 0.85) + (y - 0.17) * (y - 0.17);
                        
                        if (headDist < 0.045)
                        {
                            // Devil horns (sprouting from temples)
                            if (y < 0.10)
                            {
                                double leftHorn = (x + 0.11) * (x + 0.11) + (y * 1.5) * (y * 1.5);
                                double rightHorn = (x - 0.11) * (x - 0.11) + (y * 1.5) * (y * 1.5);
                                if (leftHorn < 0.008 || rightHorn < 0.008)
                                {
                                    if (y < 0.04) return hornsLight;
                                    return horns;
                                }
                            }
                            
                            // Slicked back hair
                            if (y < 0.12 && Math.Abs(x) < 0.12)
                            {
                                int hairLine = (int)(x * 30);
                                if (hairLine % 2 == 0) return 0xFF111111u;
                                return 0xFF1A1A1Au;
                            }
                            
                            // Glowing golden eyes (predatory)
                            double leftEyeX = x + 0.055, leftEyeY = y - 0.14;
                            double rightEyeX = x - 0.055, rightEyeY = y - 0.14;
                            double leftEye = leftEyeX * leftEyeX + leftEyeY * leftEyeY;
                            double rightEye = rightEyeX * rightEyeX + rightEyeY * rightEyeY;
                            
                            if (leftEye < 0.0006 || rightEye < 0.0006)
                            {
                                // Slit pupils
                                if (Math.Abs(leftEyeX) < 0.004 || Math.Abs(rightEyeX) < 0.004)
                                    return 0xFF000000u;
                                return eyeGlow;
                            }
                            if (leftEye < 0.003 || rightEye < 0.003) return eyeGold;
                            
                            // Cruel smile with gold teeth
                            if (y > 0.20 && y < 0.28 && Math.Abs(x) < 0.10)
                            {
                                double smileWidth = 0.10 - (y - 0.24) * (y - 0.24) * 6;
                                if (Math.Abs(x) < smileWidth && smileWidth > 0)
                                {
                                    // Gold teeth
                                    int tooth = (int)((x + smileWidth) * 25);
                                    if (tooth % 2 == 0) return teethGold;
                                    return 0xFF221100u;
                                }
                            }
                            
                            // Strong jaw
                            if (y > 0.24)
                            {
                                double jawShade = (y - 0.24) / 0.08;
                                if (jawShade > 0.6) return skinDark;
                            }
                            
                            // Face shading
                            double faceShade = headDist / 0.045;
                            if (faceShade > 0.8) return skinDark;
                            if (x < -0.03) return skinLight;
                            return skin;
                        }
                    }
                    
                    // Thick neck with power tie
                    if (y >= 0.28 && y < 0.36 && Math.Abs(x) < 0.11)
                    {
                        // Shirt collar
                        if (y > 0.32 && Math.Abs(x) > 0.05) return shirt;
                        return skin;
                    }
                    
                    // MASSIVE torso - power suit with 4 arms
                    if (y >= 0.34 && y < 0.72)
                    {
                        double torsoWidth = 0.30 - (y - 0.53) * 0.06;
                        if (Math.Abs(x) < torsoWidth)
                        {
                            // Power tie
                            if (Math.Abs(x) < 0.03 && y > 0.36 && y < 0.58)
                            {
                                // Tie clip
                                if (y > 0.44 && y < 0.47 && Math.Abs(x) < 0.02)
                                    return watchGold;
                                if (Math.Abs(x) > 0.02) return shirtDark;
                                return shirt;
                            }
                            
                            // Shirt front
                            if (Math.Abs(x) < 0.08 && y > 0.36 && y < 0.55)
                            {
                                // Buttons
                                int btn = (int)((y - 0.36) * 25);
                                if (btn % 5 == 0 && Math.Abs(x) < 0.01) return watchGold;
                                return 0xFFEEEEFFu;
                            }
                            
                            // Expensive watch
                            if (y > 0.52 && y < 0.58 && x > 0.22 && x < 0.28)
                            {
                                double watchDist = (x - 0.25) * (x - 0.25) + (y - 0.55) * (y - 0.55);
                                if (watchDist < 0.0008) return 0xFFFFFFFFu; // watch face
                                if (watchDist < 0.002) return watchGold;
                            }
                            
                            // Belt with massive gold buckle
                            if (y > 0.60 && y < 0.68)
                            {
                                if (Math.Abs(x) < 0.06)
                                {
                                    // Dollar sign buckle
                                    double buckleDist = x * x + (y - 0.64) * (y - 0.64);
                                    if (buckleDist < 0.002) return 0xFFFFDD44u;
                                    return watchGold;
                                }
                                return 0xFF1A1008u;
                            }
                            
                            // Suit texture
                            double suitShade = Math.Abs(x) / torsoWidth;
                            // Pinstripes
                            int stripe = (int)(x * 40);
                            if (stripe % 5 == 0) return suitLight;
                            if (suitShade > 0.85) return suitDark;
                            return suit;
                        }
                    }
                    
                    // FOUR arms (two normal, two extra demonic)
                    // Normal arms
                    if (y > 0.36 && y < 0.62)
                    {
                        double armX = Math.Abs(x) - 0.26;
                        if (armX > 0 && armX < 0.12)
                        {
                            // Sleeve
                            if (y < 0.52) return suit;
                            return skin;
                        }
                    }
                    // Extra demonic arms (smaller, emerging from sides)
                    if (y > 0.42 && y < 0.58)
                    {
                        double extraArmX = Math.Abs(x) - 0.32;
                        if (extraArmX > 0 && extraArmX < 0.10)
                        {
                            // Clawed hands
                            if (y > 0.52)
                            {
                                int claw = (int)(y * 30);
                                if (claw % 3 == 0) return 0xFF221100u;
                            }
                            return skinDark;
                        }
                    }
                    
                    // Massive clawed hands
                    if (y > 0.58 && y < 0.72 && Math.Abs(x) > 0.34 && Math.Abs(x) < 0.46)
                    {
                        // Claws tipped with gold
                        if (y > 0.68)
                        {
                            int claw = (int)(x * 25);
                            if (claw % 2 == 0) return watchGold;
                            return skinDark;
                        }
                        return skin;
                    }
                    
                    // Powerful legs in suit pants
                    if (y >= 0.70 && y < 0.98)
                    {
                        double legOffset = 0.10;
                        double leftLeg = Math.Abs(x + legOffset);
                        double rightLeg = Math.Abs(x - legOffset);
                        
                        if (leftLeg < 0.10 || rightLeg < 0.10)
                        {
                            // Expensive shoes
                            if (y > 0.92)
                            {
                                if (y > 0.95) return 0xFF0A0A0Au;
                                return 0xFF151515u;
                            }
                            // Pinstripes on pants
                            double leg = leftLeg < 0.10 ? leftLeg : rightLeg;
                            int pstripe = (int)(leg * 25 + y * 10);
                            if (pstripe % 4 == 0) return suitLight;
                            return suit;
                        }
                    }
                    return 0;
                }

            default:
                return 0;
        }
    }

    // Corporate horror style pickup sprites - ENHANCED
    uint GetPickupColor(Pickup p, double x, double y)
    {
        double dist = x * x + y * y;
        if (dist > 0.25) return 0;

        // Add bobbing animation visual feedback
        double bobPhase = Math.Sin(_gameTime * 4 + p.BobOffset) * 0.02;
        y -= bobPhase;
        
        // Rotating glow effect for special items
        double glowAngle = _gameTime * 2;

        switch (p.Type)
        {
            case PickupType.Health:
                // Coffee cup (restores health!) - ENHANCED
                {
                    // Cup body
                    double cupWidth = 0.12 - y * 0.08;
                    if (cupWidth > 0 && Math.Abs(x) < cupWidth && y > -0.12 && y < 0.14)
                    {
                        uint cupWhite = 0xFFEEEEEEu;
                        uint cupGray = 0xFFCCCCCCu;
                        uint cupDark = 0xFFAAAAAAu;
                        uint coffeeColor = 0xFF442211u;
                        uint steamWhite = 0xAAFFFFFFu;
                        
                        // Lid
                        if (y < -0.06)
                        {
                            if (Math.Abs(x) < 0.03) return 0xFF333333u; // sip hole
                            return 0xFF444444u;
                        }
                        
                        // Coffee visible at top
                        if (y < -0.02 && y > -0.06)
                        {
                            return coffeeColor;
                        }
                        
                        // Cup sleeve (cardboard)
                        if (y > 0.0 && y < 0.08)
                        {
                            return 0xFF886644u;
                        }
                        
                        // Steam (animated)
                        if (y < -0.08)
                        {
                            double steam = Math.Sin(x * 20 + _gameTime * 5 + y * 10);
                            if (steam > 0.6) return steamWhite;
                        }
                        
                        // Cup shading
                        double shade = Math.Abs(x) / cupWidth;
                        if (shade > 0.8) return cupDark;
                        if (shade > 0.5) return cupGray;
                        return cupWhite;
                    }
                    
                    // Handle
                    if (x > 0.08 && x < 0.16 && y > -0.02 && y < 0.08)
                    {
                        double handleDist = (x - 0.12) * (x - 0.12) + (y - 0.03) * (y - 0.03);
                        if (handleDist > 0.001 && handleDist < 0.004)
                            return 0xFFDDDDDDu;
                    }
                    return 0;
                }

            case PickupType.AtomicHealth:
                // Duke3D Atomic Health - spinning atom symbol
                {
                    double pulse = 0.7 + Math.Sin(_gameTime * 6) * 0.3;
                    double rotAngle = _gameTime * 3;
                    
                    // Glowing center nucleus
                    if (dist < 0.015)
                    {
                        uint g = (uint)(200 + pulse * 55);
                        return 0xFF000000 | (g << 8) | g;
                    }
                    
                    // Inner glow
                    if (dist < 0.04)
                    {
                        uint g = (uint)(150 * pulse);
                        return 0xFF000000 | (g << 8) | g;
                    }
                    
                    // Electron orbits (3 rotating ellipses)
                    for (int orbit = 0; orbit < 3; orbit++)
                    {
                        double orbitAngle = rotAngle + orbit * Math.PI / 3;
                        double orbX = x * Math.Cos(orbitAngle) - y * Math.Sin(orbitAngle);
                        double orbY = (x * Math.Sin(orbitAngle) + y * Math.Cos(orbitAngle)) * 2.5;
                        
                        double orbitDist = Math.Sqrt(orbX * orbX + orbY * orbY);
                        if (Math.Abs(orbitDist - 0.12) < 0.012)
                        {
                            return 0xFF00DDDDu;
                        }
                        
                        // Electron on orbit
                        double electronAngle = rotAngle * 2 + orbit * Math.PI * 2 / 3;
                        double eX = Math.Cos(electronAngle) * 0.12;
                        double eY = Math.Sin(electronAngle) * 0.05;
                        // Rotate electron position
                        double eXr = eX * Math.Cos(orbitAngle) + eY * Math.Sin(orbitAngle);
                        double eYr = -eX * Math.Sin(orbitAngle) + eY * Math.Cos(orbitAngle);
                        double eDist = (x - eXr) * (x - eXr) + (y - eYr) * (y - eYr);
                        if (eDist < 0.003)
                        {
                            return 0xFFFFFFFFu;
                        }
                    }
                    return 0;
                }

            case PickupType.Armor:
                // Duke3D Armor Vest - detailed tactical vest
                {
                    // Vest shape
                    double vestWidth = 0.16 - Math.Abs(y) * 0.15;
                    if (y > -0.18 && y < 0.20 && Math.Abs(x) < vestWidth)
                    {
                        uint vestBlue = 0xFF3366AAu;
                        uint vestDark = 0xFF224488u;
                        uint vestLight = 0xFF4488CCu;
                        
                        // Shoulder straps
                        if (y < -0.08 && Math.Abs(x) > 0.06)
                            return vestDark;
                        
                        // Front pockets/pouches
                        if (y > -0.06 && y < 0.08)
                        {
                            // Pocket grid pattern
                            int px = (int)((x + 0.15) * 12);
                            int py = (int)((y + 0.06) * 10);
                            if (px % 3 == 0 || py % 4 == 0)
                                return vestDark;
                        }
                        
                        // Center zipper
                        if (Math.Abs(x) < 0.015)
                            return 0xFF888888u;
                        
                        // Highlights
                        if (x < -0.08)
                            return vestLight;
                        
                        return vestBlue;
                    }
                    return 0;
                }

            case PickupType.Ammo:
                // Duke3D Ammo Box - detailed pistol ammo
                {
                    if (Math.Abs(x) < 0.14 && y > -0.10 && y < 0.10)
                    {
                        uint boxYellow = 0xFFDDAA33u;
                        uint boxDark = 0xFFAA7722u;
                        uint boxLight = 0xFFFFCC55u;
                        uint textRed = 0xFFCC2222u;
                        
                        // Box shading
                        if (Math.Abs(x) > 0.11 || y < -0.07 || y > 0.07)
                            return boxDark;
                        
                        // "AMMO" text area
                        if (y > -0.04 && y < 0.04)
                        {
                            // Simulated text pattern
                            int tx = (int)((x + 0.10) * 25);
                            if (tx % 4 < 2 && Math.Abs(x) < 0.08)
                                return textRed;
                        }
                        
                        // Top highlight
                        if (y < -0.05)
                            return boxLight;
                        
                        return boxYellow;
                    }
                    return 0;
                }

            case PickupType.Shotgun:
                // Duke3D Shotgun - detailed pump-action
                {
                    uint woodBrown = 0xFFAA7744u;
                    uint woodDark = 0xFF775533u;
                    uint metalGray = 0xFF666677u;
                    uint metalDark = 0xFF444455u;
                    uint metalShine = 0xFF8888AAu;
                    
                    // Stock (wood)
                    if (x < -0.10 && x > -0.28 && Math.Abs(y) < 0.05 + (x + 0.10) * 0.15)
                    {
                        // Wood grain
                        int grain = (int)((x + y) * 40);
                        if (grain % 3 == 0) return woodDark;
                        return woodBrown;
                    }
                    
                    // Receiver
                    if (x >= -0.12 && x < 0.08 && Math.Abs(y) < 0.04)
                    {
                        if (y < -0.02) return metalShine;
                        return metalGray;
                    }
                    
                    // Barrel
                    if (x >= 0.06 && x < 0.28 && Math.Abs(y) < 0.025)
                    {
                        if (y < 0) return metalShine;
                        return metalDark;
                    }
                    
                    // Pump grip
                    if (x > -0.02 && x < 0.10 && y > 0.03 && y < 0.08)
                    {
                        int groove = (int)(x * 30);
                        if (groove % 2 == 0) return woodDark;
                        return woodBrown;
                    }
                    
                    // Trigger guard
                    if (x > -0.06 && x < 0.02 && y > -0.08 && y < -0.03)
                    {
                        if (Math.Abs(x + 0.02) < 0.01 && y < -0.05) return 0xFF222222u; // trigger
                        return metalDark;
                    }
                    
                    return 0;
                }

            case PickupType.Ripper:
                // Duke3D Ripper Chaingun - multi-barrel spinning gun
                {
                    uint metalGray = 0xFF778899u;
                    uint metalDark = 0xFF445566u;
                    uint metalShine = 0xFFAABBCCu;
                    uint handleBrown = 0xFF664422u;
                    
                    // Multiple barrels (rotating)
                    double barrelRot = _gameTime * 4;
                    for (int b = 0; b < 6; b++)
                    {
                        double bAngle = barrelRot + b * Math.PI / 3;
                        double bY = Math.Sin(bAngle) * 0.04;
                        double bDepth = Math.Cos(bAngle);
                        
                        if (x > 0.02 && x < 0.25 && Math.Abs(y - bY) < 0.015)
                        {
                            if (bDepth > 0)
                                return metalShine;
                            return metalDark;
                        }
                    }
                    
                    // Body/receiver
                    if (x > -0.15 && x < 0.05 && Math.Abs(y) < 0.06)
                    {
                        // Vents
                        int vent = (int)(x * 20);
                        if (vent % 3 == 0 && Math.Abs(y) < 0.04)
                            return metalDark;
                        return metalGray;
                    }
                    
                    // Handle
                    if (x > -0.12 && x < -0.02 && y > 0.04 && y < 0.14)
                    {
                        int grip = (int)(y * 20);
                        if (grip % 2 == 0) return 0xFF553311u;
                        return handleBrown;
                    }
                    
                    // Ammo belt hint
                    if (x < -0.12 && x > -0.22 && y > -0.04 && y < 0.04)
                    {
                        int link = (int)((x + y) * 25);
                        if (link % 2 == 0) return 0xFFBBAA44u;
                        return 0xFF998833u;
                    }
                    
                    return 0;
                }

            case PickupType.RPG:
                // Duke3D RPG - rocket propelled grenade launcher
                {
                    uint tubeGreen = 0xFF557755u;
                    uint tubeDark = 0xFF334433u;
                    uint tubeLight = 0xFF779977u;
                    uint rocketRed = 0xFFCC3333u;
                    uint rocketTip = 0xFFEE5555u;
                    uint sightBlack = 0xFF222222u;
                    
                    // Main tube
                    if (x > -0.22 && x < 0.15 && Math.Abs(y) < 0.045)
                    {
                        // Shading
                        if (y < -0.02) return tubeLight;
                        if (y > 0.02) return tubeDark;
                        return tubeGreen;
                    }
                    
                    // Rocket warhead (visible in tube)
                    if (x > 0.12 && x < 0.26)
                    {
                        double rocketWidth = 0.035 - (x - 0.12) * 0.1;
                        if (rocketWidth > 0 && Math.Abs(y) < rocketWidth)
                        {
                            if (x > 0.22) return rocketTip;
                            return rocketRed;
                        }
                    }
                    
                    // Rear grip
                    if (x < -0.18 && x > -0.28 && y > -0.02 && y < 0.10)
                    {
                        int grip = (int)(y * 25);
                        if (grip % 2 == 0) return tubeDark;
                        return tubeGreen;
                    }
                    
                    // Front grip
                    if (x > -0.08 && x < 0.02 && y > 0.04 && y < 0.12)
                        return tubeDark;
                    
                    // Iron sight
                    if (x > 0.02 && x < 0.08 && y < -0.04 && y > -0.10)
                        return sightBlack;
                    
                    return 0;
                }

            case PickupType.Medkit:
                // Duke3D Portable Medkit - detailed first aid box
                {
                    if (Math.Abs(x) < 0.20 && Math.Abs(y) < 0.16)
                    {
                        uint boxWhite = 0xFFEEEEEEu;
                        uint boxGray = 0xFFCCCCCCu;
                        uint boxDark = 0xFFAAAAAAu;
                        uint crossRed = 0xFFDD2222u;
                        uint crossDark = 0xFFAA1111u;
                        uint handleGray = 0xFF888888u;
                        
                        // Handle on top
                        if (y < -0.10 && Math.Abs(x) < 0.08)
                        {
                            if (y < -0.13) return handleGray;
                            if (Math.Abs(x) > 0.05) return handleGray;
                            return 0; // transparent gap
                        }
                        
                        // Box edge shading
                        if (Math.Abs(x) > 0.17 || Math.Abs(y) > 0.13)
                            return boxDark;
                        
                        // Red cross with depth
                        bool inCross = (Math.Abs(x) < 0.04 && Math.Abs(y) < 0.09) || 
                                       (Math.Abs(y) < 0.03 && Math.Abs(x) < 0.11);
                        if (inCross)
                        {
                            // Cross shading
                            if (x > 0.02 || y > 0.01)
                                return crossDark;
                            return crossRed;
                        }
                        
                        // Clasp
                        if (Math.Abs(x) < 0.03 && y > 0.10)
                            return 0xFF666666u;
                        
                        // Box panel lines
                        if (Math.Abs(x - 0.12) < 0.01 || Math.Abs(x + 0.12) < 0.01)
                            return boxGray;
                        
                        return boxWhite;
                    }
                    return 0;
                }

            case PickupType.Jetpack:
                // Duke3D Jetpack - detailed thruster pack
                {
                    uint packGray = 0xFF666677u;
                    uint packDark = 0xFF444455u;
                    uint packLight = 0xFF8899AAu;
                    uint thrusterOrange = 0xFFFF6600u;
                    uint flameYellow = 0xFFFFAA00u;
                    uint flameRed = 0xFFFF3300u;
                    uint strapBrown = 0xFF554422u;
                    
                    // Main body
                    if (Math.Abs(x) < 0.14 && y > -0.14 && y < 0.06)
                    {
                        // Control panel
                        if (y > -0.04 && y < 0.02 && Math.Abs(x) < 0.08)
                        {
                            // Buttons/lights
                            if (Math.Abs(x - 0.03) < 0.015 && Math.Abs(y + 0.01) < 0.015)
                                return 0xFF00FF00u; // green light
                            if (Math.Abs(x + 0.03) < 0.015 && Math.Abs(y + 0.01) < 0.015)
                                return 0xFFFF0000u; // red light
                            return 0xFF333344u;
                        }
                        
                        // Fuel tanks
                        if (Math.Abs(x) > 0.08 && y > -0.10 && y < 0.02)
                        {
                            int tankLine = (int)(y * 30);
                            if (tankLine % 3 == 0) return packDark;
                            return packGray;
                        }
                        
                        // Shading
                        if (y < -0.10) return packDark;
                        return packLight;
                    }
                    
                    // Shoulder straps
                    if (y < 0 && y > -0.12)
                    {
                        if (Math.Abs(x - 0.16) < 0.025 || Math.Abs(x + 0.16) < 0.025)
                            return strapBrown;
                    }
                    
                    // Thrusters
                    double thrusterY = y - 0.08;
                    if (thrusterY > 0 && thrusterY < 0.08)
                    {
                        if (Math.Abs(x - 0.08) < 0.04 || Math.Abs(x + 0.08) < 0.04)
                            return thrusterOrange;
                    }
                    
                    // Animated flames
                    double flameHeight = 0.12 + Math.Sin(_gameTime * 15 + x * 10) * 0.04;
                    if (y > 0.14 && y < 0.14 + flameHeight)
                    {
                        double flameWidth = 0.04 - (y - 0.14) * 0.2;
                        if (flameWidth > 0)
                        {
                            if (Math.Abs(x - 0.08) < flameWidth || Math.Abs(x + 0.08) < flameWidth)
                            {
                                if (y < 0.18) return flameYellow;
                                return flameRed;
                            }
                        }
                    }
                    
                    return 0;
                }

            case PickupType.Steroids:
                // Duke3D Steroids - pill bottle
                {
                    uint bottleOrange = 0xFFDD6622u;
                    uint bottleDark = 0xFFAA4411u;
                    uint bottleLight = 0xFFFF8844u;
                    uint capWhite = 0xFFEEEEEEu;
                    uint labelWhite = 0xFFFFFFFFu;
                    
                    // Bottle body
                    if (Math.Abs(x) < 0.10 && y > -0.12 && y < 0.10)
                    {
                        // Cap
                        if (y < -0.06)
                        {
                            if (Math.Abs(x) < 0.07) return capWhite;
                            return 0;
                        }
                        
                        // Label area
                        if (y > -0.02 && y < 0.06 && Math.Abs(x) < 0.07)
                        {
                            // Rx symbol hint
                            int rx = (int)((x + 0.05) * 20);
                            if (rx % 3 == 0) return 0xFFDDDDDDu;
                            return labelWhite;
                        }
                        
                        // Bottle shading
                        if (x < -0.06) return bottleLight;
                        if (x > 0.06) return bottleDark;
                        return bottleOrange;
                    }
                    
                    // Some spilled pills
                    double pill1 = (x - 0.14) * (x - 0.14) + (y + 0.02) * (y + 0.02);
                    double pill2 = (x + 0.15) * (x + 0.15) + (y - 0.03) * (y - 0.03);
                    if (pill1 < 0.003 || pill2 < 0.003)
                        return 0xFFFFFFAAu;
                    
                    return 0;
                }

            case PickupType.KeyRed:
                // Duke3D Red Access Card - detailed keycard
                {
                    return DrawKeyCard(x, y, 0xFFCC2222u, 0xFFFF4444u, 0xFF880000u);
                }

            case PickupType.KeyBlue:
                // Duke3D Blue Access Card
                {
                    return DrawKeyCard(x, y, 0xFF2222CCu, 0xFF4444FFu, 0xFF000088u);
                }

            case PickupType.KeyYellow:
                // Duke3D Yellow Access Card
                {
                    return DrawKeyCard(x, y, 0xFFCCAA22u, 0xFFFFDD44u, 0xFF886600u);
                }

            case PickupType.Exit:
                // Duke3D Exit Symbol - glowing nuke sign
                {
                    double exitPulse = 0.6 + Math.Sin(_gameTime * 3) * 0.4;
                    
                    // Outer ring
                    double ringDist = Math.Sqrt(dist);
                    if (ringDist > 0.14 && ringDist < 0.18)
                    {
                        uint g = (uint)(200 * exitPulse);
                        uint r = (uint)(50 * exitPulse);
                        return 0xFF000000 | (r << 16) | (g << 8);
                    }
                    
                    // Radiation symbol
                    if (ringDist < 0.14)
                    {
                        // Center dot
                        if (ringDist < 0.025)
                        {
                            uint g = (uint)(255 * exitPulse);
                            return 0xFF000000 | (g << 8);
                        }
                        
                        // Three pie slices
                        double angle = Math.Atan2(y, x);
                        for (int i = 0; i < 3; i++)
                        {
                            double sliceAngle = i * Math.PI * 2 / 3 + _gameTime * 0.5;
                            double angleDiff = Math.Abs(angle - sliceAngle);
                            if (angleDiff > Math.PI) angleDiff = 2 * Math.PI - angleDiff;
                            
                            if (angleDiff < 0.45 && ringDist > 0.04 && ringDist < 0.12)
                            {
                                uint g = (uint)(180 + exitPulse * 75);
                                return 0xFF000000 | (g << 8);
                            }
                        }
                        
                        // Background
                        return 0xFF112211u;
                    }
                    return 0;
                }

            case PickupType.Invincibility:
                // Duke3D Holoduke/Invincibility - shimmering holographic effect
                {
                    double shimmer = Math.Sin(_gameTime * 10 + x * 20 + y * 15) * 0.5 + 0.5;
                    double pulse = 0.5 + Math.Sin(_gameTime * 5) * 0.5;
                    
                    // Duke silhouette
                    // Head
                    if (y < -0.04)
                    {
                        double headDist = x * x + (y + 0.12) * (y + 0.12);
                        if (headDist < 0.004)
                        {
                            uint intensity = (uint)(150 + shimmer * 105 * pulse);
                            return 0xFF000000 | (intensity << 8) | intensity;
                        }
                    }
                    
                    // Body
                    if (y > -0.06 && y < 0.12 && Math.Abs(x) < 0.08 - y * 0.2)
                    {
                        // Scanline effect
                        int scanline = (int)((y + _gameTime) * 40);
                        double scanIntensity = (scanline % 3 == 0) ? 0.5 : 1.0;
                        
                        uint intensity = (uint)((100 + shimmer * 80) * pulse * scanIntensity);
                        return 0xFF000000 | (intensity / 2 << 16) | (intensity << 8) | intensity;
                    }
                    
                    // Holographic glow ring
                    double glowDist = Math.Sqrt(dist);
                    if (glowDist > 0.15 && glowDist < 0.20)
                    {
                        double ringPulse = Math.Sin(_gameTime * 8 + glowDist * 20) * 0.5 + 0.5;
                        uint g = (uint)(100 * ringPulse * pulse);
                        uint b = (uint)(150 * ringPulse * pulse);
                        return 0xFF000000 | (g << 8) | b;
                    }
                    
                    return 0;
                }

            case PickupType.DamageBoost:
                // Duke3D Devastator/Damage Boost - glowing power core
                {
                    double corePulse = 0.5 + Math.Sin(_gameTime * 8) * 0.5;
                    double rotAngle = _gameTime * 3;
                    
                    // Central power core
                    if (dist < 0.025)
                    {
                        uint r = (uint)(200 + corePulse * 55);
                        uint g = (uint)(50 + corePulse * 50);
                        return 0xFF000000 | (r << 16) | (g << 8);
                    }
                    
                    // Inner glow
                    if (dist < 0.05)
                    {
                        uint r = (uint)(150 * corePulse);
                        uint g = (uint)(30 * corePulse);
                        return 0xFF000000 | (r << 16) | (g << 8);
                    }
                    
                    // Energy arcs (rotating)
                    for (int arc = 0; arc < 4; arc++)
                    {
                        double arcAngle = rotAngle + arc * Math.PI / 2;
                        double arcX = Math.Cos(arcAngle);
                        double arcY = Math.Sin(arcAngle);
                        
                        // Check if point is near arc line
                        double dotProduct = x * arcX + y * arcY;
                        if (dotProduct > 0.05 && dotProduct < 0.16)
                        {
                            double perpDist = Math.Abs(x * arcY - y * arcX);
                            if (perpDist < 0.02)
                            {
                                double arcIntensity = 1 - (dotProduct - 0.05) / 0.11;
                                uint r = (uint)(255 * arcIntensity * corePulse);
                                uint g = (uint)(100 * arcIntensity * corePulse);
                                return 0xFF000000 | (r << 16) | (g << 8);
                            }
                        }
                    }
                    
                    // Outer containment ring
                    double ringDist = Math.Sqrt(dist);
                    if (ringDist > 0.14 && ringDist < 0.17)
                    {
                        // Segmented ring
                        double angle = Math.Atan2(y, x);
                        int segment = (int)((angle + Math.PI) * 8 / Math.PI);
                        if (segment % 2 == 0)
                            return 0xFF553322u;
                        return 0xFF332211u;
                    }
                    
                    return 0;
                }

            default:
                // Unknown pickup - simple glow
                return dist < 0.12 ? 0xFFFFFFFFu : 0;
        }
    }
    
    // Helper function for drawing keycards
    uint DrawKeyCard(double x, double y, uint mainColor, uint lightColor, uint darkColor)
    {
        // Card body
        if (Math.Abs(x) < 0.14 && y > -0.08 && y < 0.12)
        {
            // Top edge (darker)
            if (y > 0.09) return darkColor;
            
            // Magnetic stripe
            if (y > 0.04 && y < 0.08)
                return 0xFF222222u;
            
            // Chip
            if (y > -0.02 && y < 0.02 && x > 0.02 && x < 0.10)
            {
                // Gold contact pads
                int pad = (int)(x * 30);
                if (pad % 3 == 0) return 0xFFCCAA44u;
                return 0xFFDDCC66u;
            }
            
            // Card number dots
            if (y > -0.06 && y < -0.03)
            {
                int dot = (int)((x + 0.12) * 25);
                if (dot % 4 == 0 && Math.Abs(x) < 0.10)
                    return darkColor;
            }
            
            // Highlight edge
            if (x < -0.10 || y < -0.05)
                return lightColor;
            
            return mainColor;
        }
        return 0;
    }
    
    // Exploding barrel sprite - red drum with warning stripes!
    uint GetBarrelColor(ExplodingBarrel barrel, double x, double y)
    {
        double dist = x * x + y * y;
        if (dist > 0.22) return 0;
        
        // Barrel body shape (ellipse, taller than wide)
        double barrelX = x * 1.5;
        double barrelY = y * 1.0;
        double barrelDist = barrelX * barrelX + barrelY * barrelY;
        if (barrelDist > 0.2) return 0;
        
        // Base colors
        uint barrelRed = 0xFFCC2222u;
        uint barrelRedLight = 0xFFDD4444u;
        uint barrelRedDark = 0xFF991111u;
        uint warningYellow = 0xFFFFDD00u;
        uint warningBlack = 0xFF222222u;
        uint rimColor = 0xFF444444u;
        uint rimLight = 0xFF666666u;
        
        // Damage visual - more beat up when low HP
        double damageLevel = 1.0 - (barrel.Hp / 30.0);
        
        // Top/bottom rims
        if (y < -0.28 || y > 0.35)
        {
            double rimShade = Math.Abs(y) / 0.4;
            if (rimShade > 0.9) return rimColor;
            return y < 0 ? rimLight : rimColor;
        }
        
        // Warning stripe band (middle section)
        if (y > -0.05 && y < 0.1)
        {
            // Diagonal stripes
            double stripePhase = (x + y) * 12;
            int stripeIdx = (int)Math.Floor(stripePhase) % 4;
            
            if (stripeIdx < 2)
                return warningYellow;
            else
                return warningBlack;
        }
        
        // Barrel texture with shading
        double shade = 1.0 - Math.Abs(barrelX) * 2;
        shade = Math.Max(0.5, shade);
        
        // Add dents/damage marks based on damage
        if (damageLevel > 0.3)
        {
            double dent1 = Math.Sin(x * 20 + y * 15);
            double dent2 = Math.Sin(x * 12 - y * 18);
            if (dent1 > 0.7 || dent2 > 0.8)
                shade *= 0.7;
        }
        
        // Vertical ribs on barrel
        double rib = Math.Sin(x * 25);
        if (rib > 0.8) shade *= 1.1;
        if (rib < -0.8) shade *= 0.85;
        
        // Apply base color with shading
        uint r, g, b;
        if (shade > 0.85)
        {
            r = (barrelRedLight >> 16) & 0xFF;
            g = (barrelRedLight >> 8) & 0xFF;
            b = barrelRedLight & 0xFF;
        }
        else if (shade < 0.6)
        {
            r = (barrelRedDark >> 16) & 0xFF;
            g = (barrelRedDark >> 8) & 0xFF;
            b = barrelRedDark & 0xFF;
        }
        else
        {
            r = (barrelRed >> 16) & 0xFF;
            g = (barrelRed >> 8) & 0xFF;
            b = barrelRed & 0xFF;
        }
        
        // Flicker when damaged (about to explode!)
        if (damageLevel > 0.5)
        {
            double flicker = Math.Sin(_gameTime * 15 * damageLevel) * 0.5 + 0.5;
            if (flicker > 0.6)
            {
                r = (uint)Math.Min(255, r + 50);
                g = (uint)Math.Min(255, g + 30);
            }
        }
        
        return 0xFF000000 | (r << 16) | (g << 8) | b;
    }

    double Dist(double x, double y) => (x - _px_pos) * (x - _px_pos) + (y - _py) * (y - _py);

    void RenderWeapon()
    {
        int weaponW = 256, weaponH = 192;  // Higher resolution weapon sprites
        int baseX = W / 2 - weaponW / 2;
        
        // Enhanced weapon bob (based on movement) - scaled for higher res
        bool isMoving = _keysDown.Contains(Key.W) || _keysDown.Contains(Key.S) || 
                        _keysDown.Contains(Key.A) || _keysDown.Contains(Key.D);
        double bobSpeed = isMoving ? 12 : 2;
        double bobAmount = isMoving ? 10 : 3;  // Scaled
        _weaponBob = Math.Sin(_gameTime * bobSpeed) * bobAmount;
        
        // Weapon sway when turning (subtle) - scaled
        double sway = Math.Sin(_gameTime * 8) * 3;
        
        // === ROBUST ANIMATION: Breathing/idle animation ===
        double breathe = Math.Sin(_breathingPhase) * 2.5;  // Scaled
        double creepySway = Math.Sin(_creepyAmbientPhase * 0.7) * Math.Sin(_creepyAmbientPhase * 1.3) * 3;  // Scaled
        
        // === ROBUST ANIMATION: Recoil kick-back ===
        double recoilY = _weaponRecoil * 64; // Kick down (scaled)
        double recoilX = _weaponRecoil * (Math.Sin(_gameTime * 50) * 8); // Slight shake (scaled)
        
        // === ROBUST ANIMATION: Weapon swap (lower then raise) ===
        double swapOffset = 0;
        if (_isSwappingWeapon)
        {
            // First half: lower weapon, second half: raise new weapon
            if (_weaponSwapAnim < 0.5)
            {
                swapOffset = _weaponSwapAnim * 2 * 240; // Lower out of view (scaled)
            }
            else
            {
                swapOffset = (1 - (_weaponSwapAnim - 0.5) * 2) * 240; // Raise back up (scaled)
            }
        }
        
        // === ROBUST ANIMATION: Idle inspect (tilt weapon) ===
        double inspectTilt = _weaponInspectAnim * Math.Sin(_gameTime * 2) * 0.15;
        double inspectRaise = Math.Sin(_weaponInspectAnim * 3.14159) * 32;  // Scaled
        
        // === ROBUST ANIMATION: Landing squash ===
        double squashY = _landingSquash * 48;  // Scaled
        double squashScaleY = 1 - _landingSquash * 0.2;
        
        // === ROBUST ANIMATION: Low health heartbeat pulse ===
        double heartbeatPulse = 0;
        if (_hp < 30)
        {
            heartbeatPulse = Math.Pow(Math.Sin(_heartbeatPhase), 8) * 16;  // Scaled
        }
        
        // Aiming position adjustment
        int aimOffset = _isAiming ? 32 : 0;  // Scaled
        
        int baseY = H - weaponH + (_shooting ? -40 : 0) - aimOffset;  // Scaled shoot offset
        baseY += (int)(recoilY + swapOffset + breathe + squashY - inspectRaise + heartbeatPulse);
        int bob = (int)_weaponBob;
        int swayX = (int)(sway + recoilX + creepySway);

        for (int y = Math.Max(0, baseY + bob); y < H; y++)
        {
            for (int x = Math.Max(0, baseX + swayX); x < Math.Min(W, baseX + weaponW + swayX); x++)
            {
                double rx = (double)(x - baseX - swayX) / weaponW;  // 0 to 1
                double ry = (double)(y - baseY - bob) / weaponH;  // 0 to 1
                
                // Apply squash scale
                ry = 0.5 + (ry - 0.5) / squashScaleY;
                
                // Apply inspect tilt
                rx += (ry - 0.5) * inspectTilt;

                uint color = GetWeaponPixel(rx, ry);
                if ((color & 0xFF000000) != 0)
                    _px[y * W + x] = color;
            }
        }

        // Enhanced Muzzle flash with glow
        if (_shooting && _shootFrame < 4 && _currentWeapon > 0 && _currentWeapon != 5)
        {
            _muzzleFlashTimer = 0.08;
            int flashX = W / 2 + swayX + (_currentWeapon == 2 ? -8 : 0);
            int flashY = baseY + bob - 48;  // Scaled
            int flashSize = _currentWeapon == 4 ? 48 : _currentWeapon == 3 ? 40 : 35;  // Scaled

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
            case 7: return RenderCamera(cx, cy);
            default: return 0;
        }
    }

    uint RenderFist(double x, double y)
    {
        // CREEPY: Pale mannequin hand with too many fingers - ENHANCED
        double time = _gameTime;
        
        // Animate fist punch
        double punchOffset = _shooting ? Math.Sin(time * 25) * 0.1 : 0;
        y += punchOffset;
        
        // Base colors with variations
        uint skin = 0xFFEEDDCCu;
        uint skinMid = 0xFFDDCCBBu;
        uint skinDark = 0xFFCCBBAAu;
        uint skinShadow = 0xFFAA9988u;
        uint nail = 0xFFFFEEEEu;
        uint vein = 0xFFCCAAAAu;
        uint knuckle = 0xFFFFEEDDu;
        
        // Wrist/arm with subtle animation
        double armSway = Math.Sin(time * 3) * 0.01;
        if (y > 0.5 && Math.Abs(x + armSway) < 0.12 + (y - 0.5) * 0.25)
        {
            double armShade = Math.Abs(x + armSway) / (0.12 + (y - 0.5) * 0.25);
            // Veins on arm
            double veinPattern = Math.Sin((x * 30 + y * 20 + time * 0.5)) * Math.Sin(y * 40);
            if (veinPattern > 0.7 && armShade < 0.7) return vein;
            // Tendons
            if (y > 0.55 && y < 0.7 && ((int)((x + 0.15) * 25) % 5 == 0)) return skinDark;
            if (armShade > 0.8) return skinShadow;
            if (armShade > 0.5) return skinDark;
            return skin;
        }
        
        // Main fist body with improved knuckles
        if (y > 0.25 && y < 0.55)
        {
            double fistWidth = 0.2 - Math.Pow(Math.Abs(y - 0.4), 1.5) * 0.5;
            if (Math.Abs(x) < fistWidth)
            {
                double shade = Math.Abs(x) / fistWidth;
                
                // Detailed knuckles (4 bumps)
                if (y > 0.26 && y < 0.36)
                {
                    for (int k = 0; k < 4; k++)
                    {
                        double kx = -0.12 + k * 0.08;
                        double ky = 0.31;
                        double kDist = (x - kx) * (x - kx) + (y - ky) * (y - ky);
                        if (kDist < 0.003)
                        {
                            double kShade = kDist / 0.003;
                            if (kShade < 0.3) return knuckle;
                            if (kShade < 0.6) return skin;
                            return skinMid;
                        }
                    }
                }
                
                // Finger creases
                if (y > 0.36 && y < 0.5)
                {
                    double crease = Math.Sin((x + 0.15) * 25) * Math.Sin(y * 40);
                    if (crease > 0.8) return skinDark;
                }
                
                // Edge shading
                if (shade > 0.85) return skinShadow;
                if (shade > 0.6) return skinDark;
                if (shade > 0.3) return skinMid;
                return skin;
            }
        }
        
        // Thumb (separate, realistic)
        double thumbX = x - 0.18;
        double thumbY = y - 0.35;
        double thumbDist = thumbX * thumbX * 2 + thumbY * thumbY;
        if (thumbDist < 0.012 && y > 0.28 && y < 0.48)
        {
            // Thumbnail
            if (y < 0.32 && thumbX > -0.02) return nail;
            double tShade = thumbDist / 0.012;
            if (tShade > 0.7) return skinDark;
            return skin;
        }
        
        // Extra creepy finger hint on left side
        if (y > 0.32 && y < 0.42 && x < -0.18 && x > -0.24)
        {
            double pulse = Math.Sin(time * 2) * 0.5 + 0.5;
            if (pulse > 0.7) return skinDark; // Sometimes visible
        }
        
        return 0;
    }

    uint RenderPistol(double x, double y)
    {
        // CREEPY: Ornate antique derringer with living pearl grip - ENHANCED
        double time = _gameTime;
        
        // Shooting recoil animation
        double recoil = _shooting ? Math.Sin(time * 30) * 0.02 : 0;
        y += recoil;
        
        // Enhanced metallic colors
        uint metal = 0xFF665566u;
        uint metalDark = 0xFF332233u;
        uint metalShine = 0xFF887788u;
        uint pearl = 0xFFFFEEEEu;
        uint pearlMid = 0xFFEEDDDDu;
        uint pearlDark = 0xFFDDCCCCu;
        uint gold = 0xFFCCAA44u;
        uint goldDark = 0xFFAA8822u;
        
        // Ornate slide with detailed engravings
        if (y > 0.12 && y < 0.38 && Math.Abs(x) < 0.09)
        {
            double slideShade = (0.38 - y) / 0.26;
            
            // Top serrations
            if (y < 0.18 && ((int)(x * 60) % 3 == 0)) return metalShine;
            
            // Engraved pattern (skull/vine motif)
            if (y > 0.22 && y < 0.34)
            {
                double engrave = Math.Sin(x * 40) * Math.Sin(y * 50 + time * 0.2);
                if (engrave > 0.6) return metalShine;
                if (engrave < -0.6) return metalDark;
            }
            
            // Edge highlights
            if (Math.Abs(x) > 0.07) return metalDark;
            if (y < 0.15) return metalShine;
            
            return metal;
        }
        
        // Barrel with rifling visible in bore
        if (y > 0.05 && y < 0.18 && Math.Abs(x) < 0.045)
        {
            double boreDist = Math.Sqrt(x * x + (y - 0.11) * (y - 0.11));
            if (boreDist < 0.02)
            {
                // Rifling grooves
                double angle = Math.Atan2(x, y - 0.11);
                if (Math.Sin(angle * 6) > 0.5) return 0xFF220022u;
                return 0xFF110011u;
            }
            if (boreDist < 0.035) return metalDark;
            return metal;
        }
        
        // Frame with gold inlay
        if (y > 0.35 && y < 0.52 && Math.Abs(x) < 0.08)
        {
            // Gold accent line
            if (y > 0.36 && y < 0.38) return gold;
            if (x < -0.05) return metalDark;
            return metal;
        }
        
        // Trigger guard (elegant curve)
        if (y > 0.44 && y < 0.58 && x > -0.09 && x < 0.04)
        {
            double guardCurve = Math.Sin((y - 0.44) * 10) * 0.03;
            if (y > 0.52 && Math.Abs(x + 0.025 + guardCurve) < 0.035) return 0;
            // Trigger
            if (Math.Abs(x + 0.02) < 0.012 && y > 0.46 && y < 0.54)
            {
                if (_shooting) return goldDark; // Pulled back
                return gold;
            }
            return metal;
        }
        
        // Pearl grip with living face pattern
        if (y > 0.5 && Math.Abs(x) < 0.085 - (y - 0.5) * 0.08)
        {
            double gripShade = Math.Abs(x) / 0.085;
            
            // The face in the pearl (it moves slightly!)
            double faceX = x + Math.Sin(time * 0.5) * 0.005;
            double faceY = y - 0.62 + Math.Cos(time * 0.7) * 0.003;
            double faceDist = faceX * faceX + faceY * faceY;
            
            // Two eyes
            double leftEye = (faceX + 0.025) * (faceX + 0.025) + faceY * faceY;
            double rightEye = (faceX - 0.025) * (faceX - 0.025) + faceY * faceY;
            if (leftEye < 0.0006 || rightEye < 0.0006) return 0xFF221122u;
            
            // Mouth (seems to whisper)
            if (y > 0.65 && y < 0.68 && Math.Abs(faceX) < 0.02 + Math.Sin(time * 2) * 0.005)
                return pearlDark;
            
            // Pearl swirl pattern
            double swirl = Math.Sin(x * 30 + y * 20 + time * 0.3) * Math.Cos(y * 25);
            if (swirl > 0.5) return pearl;
            if (swirl < -0.5) return pearlDark;
            
            if (gripShade > 0.7) return pearlDark;
            return pearlMid;
        }
        
        // Hands with finger detail
        if (y > 0.58)
        {
            if (Math.Abs(x) < 0.16 && y > 0.62)
            {
                double handShade = Math.Abs(x) / 0.16;
                if (handShade > 0.8) return 0xFFDDCCBBu;
                return 0xFFEEDDCCu;
            }
        }
        
        return 0;
    }

    uint RenderShotgun(double x, double y)
    {
        // CREEPY: Bone-stock shotgun with teeth along the barrel - ENHANCED
        double time = _gameTime;
        
        // Pump action animation
        double pumpOffset = _shooting ? Math.Sin(time * 20) * 0.03 : 0;
        
        uint bone = 0xFFEEDDCCu;
        uint boneMid = 0xFFDDCCBBu;
        uint boneDark = 0xFFCCBBAAu;
        uint boneShadow = 0xFFAA9988u;
        uint boneLight = 0xFFFFEEDDu;
        uint tooth = 0xFFFFFFEEu;
        uint toothRoot = 0xFFDDCCAAu;
        uint metal = 0xFF555555u;
        uint metalDark = 0xFF333333u;
        uint metalShine = 0xFF777777u;
        uint gumPink = 0xFFCC8888u;

        // Double barrels with ring of TEETH around openings
        if (y > 0.03 && y < 0.28)
        {
            // Left barrel
            double leftBarrel = (x - 0.045) * (x - 0.045) + (y - 0.15) * (y - 0.15) * 0.3;
            // Right barrel
            double rightBarrel = (x + 0.045) * (x + 0.045) + (y - 0.15) * (y - 0.15) * 0.3;
            
            if (leftBarrel < 0.003 || rightBarrel < 0.003)
            {
                // Barrel interior (dark with rifling)
                double dist = Math.Min(leftBarrel, rightBarrel);
                if (dist < 0.001) return 0xFF111111u;
                return metalDark;
            }
            
            // Teeth ring around each barrel!
            if (y < 0.12)
            {
                for (int t = 0; t < 16; t++)
                {
                    double angle = t * 3.14159 / 8 + time * 0.2;
                    // Left barrel teeth
                    double ltx = 0.045 + Math.Cos(angle) * 0.045;
                    double lty = 0.08 + Math.Sin(angle) * 0.02;
                    double ltDist = (x - ltx) * (x - ltx) + (y - lty) * (y - lty);
                    if (ltDist < 0.0004)
                    {
                        if (y < lty - 0.01) return tooth;
                        return toothRoot;
                    }
                    // Right barrel teeth
                    double rtx = -0.045 + Math.Cos(angle) * 0.045;
                    double rty = 0.08 + Math.Sin(angle) * 0.02;
                    double rtDist = (x - rtx) * (x - rtx) + (y - rty) * (y - rty);
                    if (rtDist < 0.0004)
                    {
                        if (y < rty - 0.01) return tooth;
                        return toothRoot;
                    }
                }
                // Gum tissue between teeth
                if (y < 0.1 && Math.Abs(Math.Abs(x) - 0.045) < 0.05) return gumPink;
            }
            
            // Barrel bodies
            if (Math.Abs(x - 0.045) < 0.04 || Math.Abs(x + 0.045) < 0.04)
            {
                if (y < 0.08) return metalShine;
                return metal;
            }
            
            // Center rib
            if (Math.Abs(x) < 0.02 && y > 0.1) return metalDark;
        }
        
        // Receiver (bone-textured with carved details)
        if (y > 0.24 && y < 0.42 && Math.Abs(x) < 0.11)
        {
            // Loading port
            if (y > 0.28 && y < 0.34 && x > 0.03 && x < 0.09) return metalDark;
            
            // Bone texture with growth rings
            double boneRing = Math.Sin((x * x + y * y) * 100 + time * 0.1);
            if (boneRing > 0.7) return boneLight;
            if (boneRing < -0.7) return boneDark;
            
            double shade = Math.Abs(x) / 0.11;
            if (shade > 0.85) return boneShadow;
            if (shade > 0.6) return boneDark;
            return bone;
        }
        
        // Forend/pump made of VERTEBRAE
        double pumpY = y + pumpOffset;
        if (pumpY > 0.38 && pumpY < 0.52 && Math.Abs(x) < 0.13)
        {
            // Individual vertebrae segments
            double segmentY = (pumpY - 0.38) * 25;
            int seg = (int)segmentY % 5;
            double segFrac = segmentY - (int)segmentY;
            
            // Gap between vertebrae
            if (seg == 0 && segFrac < 0.3) return boneShadow;
            
            // Vertebrae body with spinous process
            double spineWidth = 0.08 + Math.Sin(segmentY * 1.5) * 0.02;
            if (Math.Abs(x) < spineWidth)
            {
                // Central hole (spinal canal)
                if (Math.Abs(x) < 0.015) return boneShadow;
                // Texture
                if (Math.Abs(x) > spineWidth * 0.8) return boneDark;
                if (seg == 2) return boneLight;
                return bone;
            }
            // Transverse processes (side bumps)
            if (Math.Abs(x) < 0.12 && seg == 2) return boneMid;
        }
        
        // Stock made of carved bone (with skull face!)
        if (y > 0.5 && Math.Abs(x) < 0.14 - (y - 0.5) * 0.12)
        {
            double stockShade = Math.Abs(x) / (0.14 - (y - 0.5) * 0.12);
            
            // Carved skull in the stock!
            double skullX = x + 0.01;
            double skullY = y - 0.68;
            double skullDist = skullX * skullX + skullY * skullY * 0.5;
            
            // Eye sockets
            double leftEye = (skullX + 0.025) * (skullX + 0.025) + (skullY + 0.02) * (skullY + 0.02);
            double rightEye = (skullX - 0.025) * (skullX - 0.025) + (skullY + 0.02) * (skullY + 0.02);
            if (leftEye < 0.0008 || rightEye < 0.0008) return 0xFF221111u;
            
            // Nose hole
            if (Math.Abs(skullX) < 0.012 && skullY > -0.01 && skullY < 0.02) return boneShadow;
            
            // Teeth grin
            if (skullY > 0.02 && skullY < 0.05 && Math.Abs(skullX) < 0.04)
            {
                if (((int)(skullX * 50) % 3 == 0)) return tooth;
                return boneShadow;
            }
            
            // Bone grain
            double grain = Math.Sin(x * 20 + y * 15);
            if (grain > 0.6) return boneLight;
            
            if (stockShade > 0.8) return boneShadow;
            if (stockShade > 0.5) return boneDark;
            return bone;
        }
        
        // Hands
        if (y > 0.4 && y < 0.58 && x > 0.12 && x < 0.26)
        {
            double handShade = (x - 0.12) / 0.14;
            if (handShade > 0.8) return 0xFFDDCCBBu;
            return 0xFFEEDDCCu;
        }
        if (y > 0.62 && Math.Abs(x) < 0.2) return 0xFFEEDDCCu;
        
        return 0;
    }

    uint RenderRipper(double x, double y)
    {
        // CREEPY: Organic meat grinder with spinning EYES - ENHANCED
        double time = _gameTime;
        double spinSpeed = _shooting ? 25 : 3;
        
        uint flesh = 0xFFBB9999u;
        uint fleshMid = 0xFFAA8888u;
        uint fleshDark = 0xFF886666u;
        uint fleshShadow = 0xFF664444u;
        uint eye = 0xFFFFFFFFu;
        uint eyeVein = 0xFFFFCCCCu;
        uint pupil = 0xFF111100u;
        uint iris = 0xFF884422u;
        uint bloodRed = 0xFFAA2222u;
        uint bloodDark = 0xFF661111u;
        uint metal = 0xFF555544u;
        uint metalDark = 0xFF333322u;

        // Ring of spinning EYEBALLS!
        if (y > 0.02 && y < 0.32)
        {
            double eyeAngle = time * spinSpeed;
            for (int i = 0; i < 6; i++)
            {
                double angle = eyeAngle + i * 3.14159 / 3;
                double ex = Math.Cos(angle) * 0.055;
                double ey = Math.Sin(angle) * 0.055 * 0.4 + 0.16;
                double dist = Math.Sqrt((x - ex) * (x - ex) + (y - ey) * (y - ey));
                
                if (dist < 0.032)
                {
                    // Pupil (dilates when shooting!)
                    double pupilSize = _shooting ? 0.004 : 0.01;
                    if (dist < pupilSize) return pupil;
                    
                    // Iris with pattern
                    if (dist < 0.015)
                    {
                        double irisAngle = Math.Atan2(y - ey, x - ex);
                        if (Math.Sin(irisAngle * 8) > 0.3) return 0xFFAA6633u;
                        return iris;
                    }
                    
                    // Bloodshot veins in white
                    double veinAngle = Math.Atan2(y - ey, x - ex);
                    if (Math.Sin(veinAngle * 12 + i) > 0.7) return bloodRed;
                    if (dist > 0.025) return eyeVein;
                    return eye;
                }
                
                // Eye socket (fleshy rim)
                if (dist < 0.04 && dist > 0.03) return fleshDark;
            }
            
            // Central hub (pulsing)
            double hubDist = x * x + (y - 0.16) * (y - 0.16);
            if (hubDist < 0.003)
            {
                double pulse = Math.Sin(time * 8) * 0.5 + 0.5;
                if (pulse > 0.7) return bloodRed;
                return bloodDark;
            }
        }
        
        // Eye socket housing (ring of flesh)
        if (y > 0.08 && y < 0.28)
        {
            double houseDist = x * x + (y - 0.16) * (y - 0.16) * 2;
            if (houseDist < 0.015 && houseDist > 0.008)
            {
                // Meaty texture
                double meatTex = Math.Sin(x * 40 + y * 30 + time * 0.5);
                if (meatTex > 0.5) return fleshDark;
                return flesh;
            }
        }
        
        // Fleshy body with visible muscle fibers
        if (y > 0.26 && y < 0.58 && Math.Abs(x) < 0.13)
        {
            double bodyShade = Math.Abs(x) / 0.13;
            
            // Muscle fiber striations
            double fiber = Math.Sin((x * 25 + y * 40) + Math.Sin(y * 20) * 2);
            if (fiber > 0.6) return fleshMid;
            if (fiber < -0.6) return fleshDark;
            
            // Ammo counter (glowing nerve endings!)
            if (y > 0.36 && y < 0.46 && x > 0.02 && x < 0.09)
            {
                int nervePulse = (int)((time * 5 + y * 10) * 3) % 8;
                if (nervePulse < _ammo[3] % 8) return 0xFFFFDD44u; // Bioluminescent
                return bloodDark;
            }
            
            // Pulsing blood vessels
            double vein = Math.Sin(y * 30 + time * 3);
            if (vein > 0.85 && ((int)(x * 20) % 4 == 0)) return bloodRed;
            
            if (bodyShade > 0.85) return fleshShadow;
            if (bodyShade > 0.6) return fleshDark;
            return flesh;
        }
        
        // Metal frame (holding the flesh together)
        if (y > 0.28 && y < 0.35 && Math.Abs(x) < 0.14)
        {
            if (Math.Abs(x) > 0.12) return metalDark;
            // Bolts
            if (((int)(x * 30) % 6 == 0)) return 0xFF666655u;
            return metal;
        }
        
        // Handle (wrapped in skin grafts)
        if (y > 0.52 && y < 0.78 && Math.Abs(x) < 0.1 - (y - 0.52) * 0.15)
        {
            double handleShade = Math.Abs(x) / 0.1;
            // Stitched skin texture
            if (((int)(y * 40) % 5 == 0)) return 0xFF443333u;
            if (handleShade > 0.7) return fleshDark;
            return flesh;
        }
        
        // Hands gripping
        if (y > 0.58 && Math.Abs(x) < 0.2) return 0xFFEEDDCCu;
        if (y > 0.36 && y < 0.54 && x > 0.11 && x < 0.24) return 0xFFEEDDCCu;
        
        return 0;
    }

    uint RenderRPG(double x, double y)
    {
        // CREEPY: Haunted doll launcher with articulated doll warhead - ENHANCED
        double time = _gameTime;
        
        uint plastic = 0xFFEEDDCCu;
        uint plasticMid = 0xFFDDCCBBu;
        uint plasticDark = 0xFFCCBBAAu;
        uint plasticShadow = 0xFFAA9988u;
        uint hair = 0xFFAA7733u;
        uint hairDark = 0xFF885522u;
        uint dress = 0xFFEEAABBu;
        uint dressDark = 0xFFCCAACCu;
        uint dressLight = 0xFFFFBBCCu;
        uint metal = 0xFF555555u;
        uint metalDark = 0xFF333333u;
        uint eye = 0xFF111111u;
        uint lip = 0xFF993344u;

        // Doll head warhead with MOVING features!
        if (y > 0.0 && y < 0.22)
        {
            // Head shape (porcelain doll)
            double headX = x;
            double headY = y - 0.11;
            double headDist = headX * headX + headY * headY;
            
            if (headDist < 0.01)
            {
                double headShade = headDist / 0.01;
                
                // Button eyes (follow you!)
                double eyeTrack = Math.Sin(time * 0.5) * 0.005;
                double leftEyeX = -0.028 + eyeTrack;
                double rightEyeX = 0.028 + eyeTrack;
                double eyeY = -0.015;
                
                double leftEye = (headX - leftEyeX) * (headX - leftEyeX) + (headY - eyeY) * (headY - eyeY);
                double rightEye = (headX - rightEyeX) * (headX - rightEyeX) + (headY - eyeY) * (headY - eyeY);
                
                if (leftEye < 0.0012 || rightEye < 0.0012)
                {
                    if (leftEye < 0.0003 || rightEye < 0.0003) return 0xFFFFFFFFu; // Glint
                    return eye;
                }
                
                // Rosy cheeks
                double leftCheek = (headX + 0.035) * (headX + 0.035) + (headY + 0.01) * (headY + 0.01);
                double rightCheek = (headX - 0.035) * (headX - 0.035) + (headY + 0.01) * (headY + 0.01);
                if (leftCheek < 0.0008 || rightCheek < 0.0008) return 0xFFFFAAAAu;
                
                // Smile (always smiling... too wide)
                if (headY > 0.015 && headY < 0.035)
                {
                    double smileWidth = 0.035 - Math.Abs(headY - 0.025) * 1.5;
                    if (Math.Abs(headX) < smileWidth)
                    {
                        if (Math.Abs(headX) < smileWidth * 0.7) return 0xFF220000u; // Mouth interior
                        return lip;
                    }
                }
                
                // Porcelain texture with cracks
                double crack = Math.Sin(headX * 50 + headY * 30);
                if (crack > 0.95) return plasticShadow;
                
                if (headShade > 0.8) return plasticDark;
                if (headShade > 0.5) return plasticMid;
                return plastic;
            }
            
            // Curly hair
            if (headY < -0.02 && headDist < 0.018)
            {
                double curl = Math.Sin(headX * 40 + time * 2) * Math.Cos(headY * 30);
                if (curl > 0.3) return hair;
                if (curl < -0.3) return hairDark;
                return hair;
            }
        }
        
        // Tube body with lace dress pattern
        if (y > 0.18 && y < 0.48 && Math.Abs(x) < 0.095)
        {
            double bodyShade = Math.Abs(x) / 0.095;
            
            // Dress with lace pattern
            if (y > 0.22 && y < 0.44)
            {
                // Lace holes
                double lace = Math.Sin(x * 35) * Math.Sin(y * 40);
                if (lace > 0.7) return dressLight;
                if (lace < -0.5) return dressDark;
                
                // Ribbon bow
                if (y > 0.23 && y < 0.28 && Math.Abs(x) < 0.04)
                {
                    if (Math.Abs(x) < 0.015) return 0xFFDD6688u;
                    return 0xFFCC7799u;
                }
                
                // Ruffles
                int ruffle = (int)(y * 35) % 4;
                if (ruffle == 0) return dressLight;
                if (ruffle == 3) return dressDark;
                return dress;
            }
            
            if (bodyShade > 0.8) return plasticShadow;
            return plasticMid;
        }
        
        // Tiny porcelain arms as fins (articulated!)
        for (int arm = -1; arm <= 1; arm += 2)
        {
            double armX = arm * 0.11;
            double armY = 0.28 + Math.Sin(time * 4 + arm) * 0.02; // Waving!
            if (y > armY - 0.04 && y < armY + 0.06 && Math.Abs(x - armX) < 0.025)
            {
                // Little hand at end
                if (y < armY - 0.02) return plastic;
                return plasticMid;
            }
        }
        
        // Sight (looks like a tiny crown)
        if (y > 0.08 && y < 0.16 && x > 0.1 && x < 0.16)
        {
            int crown = (int)((x - 0.1) * 50) % 3;
            if (crown == 1 && y < 0.12) return 0xFFDDBB44u;
            return metal;
        }
        
        // Rear sight
        if (y > 0.36 && y < 0.44 && Math.Abs(x - 0.13) < 0.028) return metal;
        
        // Grip assembly (worn wood)
        if (y > 0.42 && y < 0.68 && x > -0.06 && x < 0.085)
        {
            // Trigger
            if (y > 0.47 && y < 0.55 && Math.Abs(x + 0.01) < 0.015)
            {
                if (_shooting) return metalDark;
                return metal;
            }
            // Wood grain
            double grain = Math.Sin(y * 30 + x * 5);
            if (grain > 0.6) return 0xFF664433u;
            return 0xFF553322u;
        }
        
        // Shoulder rest (padded)
        if (y > 0.58 && y < 0.82 && Math.Abs(x) < 0.12 - (y - 0.58) * 0.18)
        {
            double padShade = Math.Abs(x) / 0.12;
            // Quilted pattern
            int quilt = ((int)(x * 20) + (int)(y * 20)) % 2;
            if (quilt == 0) return plasticDark;
            if (padShade > 0.7) return plasticShadow;
            return plasticMid;
        }
        
        // Hands
        if (y > 0.52 && y < 0.68 && x > 0.07 && x < 0.23) return 0xFFEEDDCCu;
        if (y > 0.68 && Math.Abs(x) < 0.2) return 0xFFEEDDCCu;
        
        return 0;
    }

    uint RenderPipeBomb(double x, double y)
    {
        // CREEPY: Haunted music box that plays when thrown - ENHANCED
        double time = _gameTime;
        
        uint skin = 0xFFEEDDCCu;
        uint skinMid = 0xFFDDCCBBu;
        uint skinDark = 0xFFCCBBAAu;
        uint box = 0xFF664433u;
        uint boxMid = 0xFF554422u;
        uint boxDark = 0xFF442211u;
        uint boxLight = 0xFF775544u;
        uint gold = 0xFFDDBB44u;
        uint goldDark = 0xFFBB9933u;
        uint ballerina = 0xFFFFCCDDu;
        uint ballerinaLight = 0xFFFFDDEEu;
        uint velvet = 0xFF882244u;

        // Pale hand holding music box with improved detail
        if (y > 0.35)
        {
            if (Math.Abs(x) < 0.2 && y > 0.5)
            {
                // Hand shading
                double handShade = Math.Abs(x) / 0.2;
                double knuckle = Math.Sin(x * 30);
                if (knuckle > 0.8) return skinDark;
                if (x < -0.1) return skinDark;
                if (handShade > 0.7) return skinMid;
                return skin;
            }
            // Long pale fingers with better articulation
            for (int i = 0; i < 4; i++)
            {
                double fx = -0.1 + i * 0.06;
                double fy = 0.35 + Math.Sin((x - fx) * 10) * 0.03;
                if (Math.Abs(x - fx) < 0.025 && y > fy && y < fy + 0.18)
                {
                    // Knuckle joints
                    if ((y - fy) > 0.08 && (y - fy) < 0.1) return skinDark;
                    if ((y - fy) > 0.14 && (y - fy) < 0.16) return skinDark;
                    // Long yellowed fingernails
                    if (y < fy + 0.035)
                    {
                        double nailShade = (y - fy) / 0.035;
                        if (nailShade < 0.3) return 0xFFFFEECCu; // Nail tip
                        return 0xFFEEDDBBu;
                    }
                    if (y < fy + 0.05) return skinDark;
                    return skin;
                }
            }
            // Thumb
            if (x > 0.08 && x < 0.18 && y > 0.3 && y < 0.5)
            {
                if (y < 0.35) return 0xFFFFEEDDu; // Thumbnail
                return skin;
            }
        }
        
        // Music box with intricate detail
        if (y > 0.12 && y < 0.58 && Math.Abs(x) < 0.09)
        {
            // Lid (open, showing interior)
            if (y < 0.2)
            {
                double lidShade = (y - 0.12) / 0.08;
                // Inner velvet lining visible
                if (Math.Abs(x) < 0.06)
                {
                    if (lidShade < 0.3) return velvet;
                    return boxDark;
                }
                // Edge detail
                if (Math.Abs(x) > 0.075) return goldDark;
                return boxDark;
            }
            
            // Dancing ballerina (ANIMATED!)
            if (y > 0.2 && y < 0.38 && Math.Abs(x) < 0.035)
            {
                double spin = time * 3;
                double balX = Math.Sin(spin) * 0.015;
                double balDist = (x - balX) * (x - balX);
                
                // Head
                if (y > 0.21 && y < 0.25 && balDist < 0.0002) return ballerinaLight;
                // Body
                if (y > 0.25 && y < 0.3 && balDist < 0.0001) return ballerina;
                // Tutu (spins!)
                if (y > 0.28 && y < 0.34)
                {
                    double tutuWidth = 0.025 + Math.Cos(spin * 2) * 0.008;
                    if (Math.Abs(x - balX) < tutuWidth)
                    {
                        // Layered tulle
                        int layer = (int)(y * 80) % 2;
                        if (layer == 0) return ballerinaLight;
                        return ballerina;
                    }
                }
                // Legs (en pointe!)
                if (y > 0.33 && y < 0.38 && Math.Abs(x - balX) < 0.008) return ballerinaLight;
            }
            
            // Mirror behind ballerina
            if (y > 0.2 && y < 0.36 && x > 0.035 && x < 0.07)
            {
                // Reflection (darker version)
                return 0xFFCCBBCCu;
            }
            
            // Gold trim bands
            if ((y > 0.18 && y < 0.21) || (y > 0.5 && y < 0.53))
            {
                // Engraved pattern
                double eng = Math.Sin(x * 60);
                if (eng > 0.5) return goldDark;
                return gold;
            }
            
            // Body panels with ornate carvings
            if (y > 0.36 && y < 0.5)
            {
                double carveDist = x * x + (y - 0.43) * (y - 0.43);
                // Carved face on front panel
                if (carveDist < 0.003)
                {
                    // Eyes
                    double leftEye = (x + 0.015) * (x + 0.015) + (y - 0.41) * (y - 0.41);
                    double rightEye = (x - 0.015) * (x - 0.015) + (y - 0.41) * (y - 0.41);
                    if (leftEye < 0.0003 || rightEye < 0.0003) return 0xFF110000u;
                    // Mouth
                    if (y > 0.44 && y < 0.46 && Math.Abs(x) < 0.012) return boxDark;
                    return boxMid;
                }
                
                // Scrollwork
                double scroll = Math.Sin(x * 40) * Math.Cos(y * 25);
                if (scroll > 0.6) return boxLight;
                if (scroll < -0.6) return boxDark;
            }
            
            // Side panels
            if (Math.Abs(x) > 0.06)
            {
                double sideGrain = Math.Sin(y * 30 + x * 10);
                if (sideGrain > 0.7) return boxLight;
                return boxDark;
            }
            
            // Wood grain texture
            double grain = Math.Sin(y * 25 + x * 5);
            if (grain > 0.6) return boxLight;
            if (grain < -0.6) return boxDark;
            return box;
        }
        
        // Wind-up key on side (rotates when you move!)
        if (y > 0.26 && y < 0.42 && x > 0.08 && x < 0.18)
        {
            double keyX = x - 0.13;
            double keyY = y - 0.34;
            double keyRot = time * 2;
            
            // Key shaft
            if (Math.Abs(keyX) < 0.015 && y > 0.3 && y < 0.38) return gold;
            
            // Key handle (butterfly shape)
            double handleDist = keyX * keyX + keyY * keyY;
            if (handleDist < 0.003)
            {
                // Rotating handle pattern
                double angle = Math.Atan2(keyY, keyX) + keyRot;
                double petal = Math.Cos(angle * 2);
                if (Math.Abs(petal) > 0.5 && handleDist > 0.001)
                {
                    if (petal > 0) return gold;
                    return goldDark;
                }
                return 0xFF887744u;
            }
        }
        
        // Tiny feet (ball feet on corners)
        for (int foot = -1; foot <= 1; foot += 2)
        {
            double footX = foot * 0.07;
            double footY = 0.56;
            double footDist = (x - footX) * (x - footX) + (y - footY) * (y - footY);
            if (footDist < 0.0008) return goldDark;
        }
        
        return 0;
    }

    uint RenderMiniRocket(double x, double y)
    {
        // CREEPY: Tiny screaming face missile - ENHANCED with terror!
        double time = _gameTime;
        
        uint face = 0xFFEEDDCCu;
        uint faceMid = 0xFFDDCCBBu;
        uint faceDark = 0xFFCCBBAAu;
        uint faceShadow = 0xFFAA9988u;
        uint mouth = 0xFF440000u;
        uint mouthDark = 0xFF220000u;
        uint teeth = 0xFFFFEEDDu;
        uint flame = 0xFFFF4400u;
        uint flameYellow = 0xFFFFAA00u;
        uint flameDark = 0xFFCC2200u;
        uint eye = 0xFF111111u;

        // Pale hand with trembling detail
        if (y > 0.5 && Math.Abs(x) < 0.18)
        {
            double tremble = Math.Sin(time * 20) * 0.002;
            double handX = x + tremble;
            double handShade = Math.Abs(handX) / 0.18;
            
            // Knuckles
            if (y > 0.52 && y < 0.58)
            {
                double knuckle = Math.Sin(handX * 40);
                if (knuckle > 0.7) return faceDark;
            }
            
            if (handShade > 0.8) return faceDark;
            if (handShade > 0.5) return faceMid;
            return face;
        }

        // Screaming face-shaped rocket body
        if (y > 0.08 && y < 0.46 && Math.Abs(x) < 0.075)
        {
            double faceX = x;
            double faceY = y - 0.27;
            double faceDist = faceX * faceX / 0.0045 + faceY * faceY / 0.018;
            
            if (faceDist < 1.0)
            {
                // Calculate shading based on position
                double faceShade = Math.Abs(faceX) / 0.065;
                
                // Eyes (WIDE with terror, pupils track!)
                if (y > 0.13 && y < 0.22)
                {
                    double eyeTrack = Math.Sin(time * 2) * 0.005;
                    double leftEyeX = -0.028 + eyeTrack;
                    double rightEyeX = 0.028 + eyeTrack;
                    double eyeY = 0.175;
                    
                    double leftEye = (faceX - leftEyeX) * (faceX - leftEyeX) + (y - eyeY) * (y - eyeY);
                    double rightEye = (faceX - rightEyeX) * (faceX - rightEyeX) + (y - eyeY) * (y - eyeY);
                    
                    // Eye whites (bloodshot)
                    if (leftEye < 0.0015 || rightEye < 0.0015)
                    {
                        double eyeDist = Math.Min(leftEye, rightEye);
                        // Pupil (tiny with fear)
                        if (eyeDist < 0.0002) return eye;
                        // Iris ring
                        if (eyeDist < 0.0005) return 0xFF445566u;
                        // Bloodshot veins
                        double vein = Math.Sin(Math.Atan2(y - eyeY, faceX - (leftEye < rightEye ? leftEyeX : rightEyeX)) * 8);
                        if (vein > 0.7) return 0xFFDD8888u;
                        return 0xFFEEDDDDu;
                    }
                    
                    // Raised brow (terror!)
                    if (y > 0.13 && y < 0.15)
                    {
                        if (Math.Abs(faceX - 0.028) < 0.02 || Math.Abs(faceX + 0.028) < 0.02)
                            return faceShadow;
                    }
                }
                
                // Nose (pinched with fear)
                if (y > 0.2 && y < 0.26 && Math.Abs(faceX) < 0.01)
                {
                    if (y > 0.24) return faceShadow; // Nostril shadows
                    return faceDark;
                }
                
                // Screaming mouth (O shape with TEETH)
                if (y > 0.26 && y < 0.4)
                {
                    double mouthCenterY = 0.33;
                    double mouthRadX = 0.035;
                    double mouthRadY = 0.06;
                    double mouthDist = (faceX * faceX) / (mouthRadX * mouthRadX) + 
                                      ((y - mouthCenterY) * (y - mouthCenterY)) / (mouthRadY * mouthRadY);
                    
                    if (mouthDist < 1.0)
                    {
                        // Teeth rows
                        if (y < 0.29 && Math.Abs(faceX) < 0.025)
                        {
                            // Upper teeth
                            int toothIdx = (int)((faceX + 0.025) * 60) % 3;
                            if (toothIdx == 1) return teeth;
                            return 0xFFDDCCBBu;
                        }
                        if (y > 0.37 && Math.Abs(faceX) < 0.02)
                        {
                            // Lower teeth
                            int toothIdx = (int)((faceX + 0.02) * 50) % 3;
                            if (toothIdx == 1) return teeth;
                            return 0xFFDDCCBBu;
                        }
                        
                        // Tongue
                        if (y > 0.32 && y < 0.36 && Math.Abs(faceX) < 0.015)
                            return 0xFFAA4455u;
                        
                        // Dark mouth interior
                        if (mouthDist < 0.6) return mouthDark;
                        return mouth;
                    }
                    
                    // Lip edge
                    if (mouthDist < 1.2 && mouthDist > 0.95)
                        return faceShadow;
                }
                
                // Wrinkles of terror
                if (y > 0.23 && y < 0.27)
                {
                    double wrinkle = Math.Sin(faceX * 80);
                    if (wrinkle > 0.8) return faceDark;
                }
                
                // Face shading
                if (faceShade > 0.85) return faceShadow;
                if (faceShade > 0.6) return faceDark;
                if (faceShade > 0.3) return faceMid;
                return face;
            }
        }

        // Hair/flames at back (jet exhaust!)
        if (y > 0.4 && y < 0.52 && Math.Abs(x) < 0.06)
        {
            double wave1 = Math.Sin(time * 18 + x * 25) * 0.015;
            double wave2 = Math.Cos(time * 15 + x * 20) * 0.01;
            double flameX = x + wave1;
            
            // Layered flames
            if (Math.Abs(flameX) < 0.02)
            {
                double flicker = Math.Sin(time * 30 + y * 50);
                if (flicker > 0.3) return flameYellow;
                return flame;
            }
            if (Math.Abs(flameX + wave2) < 0.04)
            {
                double flicker = Math.Sin(time * 25 + y * 40);
                if (flicker > 0.5) return flame;
                return flameDark;
            }
        }

        // Tiny arms as fins (flailing!)
        for (int arm = -1; arm <= 1; arm += 2)
        {
            double armWave = Math.Sin(time * 8 + arm * 2) * 0.008;
            double armX = arm * 0.075;
            double armY = 0.36 + armWave;
            
            if (y > armY - 0.04 && y < armY + 0.04 && Math.Abs(x - armX) < 0.025)
            {
                // Little hands at ends
                if (Math.Abs(y - (armY - 0.03)) < 0.015)
                {
                    // Tiny fingers
                    return face;
                }
                double armShade = Math.Abs(y - armY) / 0.04;
                if (armShade > 0.7) return faceDark;
                return faceMid;
            }
        }

        return 0;
    }
    
    uint RenderCamera(double x, double y)
    {
        // CREEPY: Antique camera with a LIVING EYE for a lens 👁️📸 - ENHANCED
        double time = _gameTime;
        
        uint bodyColor = 0xFF332222u;
        uint bodyDark = 0xFF221111u;
        uint bodyLight = 0xFF443333u;
        uint brass = 0xFF997744u;
        uint brassDark = 0xFF776633u;
        uint brassLight = 0xFFBB9955u;
        uint eyeWhite = 0xFFFFEEEEu;
        uint iris = 0xFF446688u;
        uint irisDark = 0xFF335577u;
        uint pupil = 0xFF000011u;
        uint flesh = 0xFFDDCCBBu;
        uint fleshDark = 0xFFBBAA99u;
        uint skin = 0xFFEEDDCCu;
        uint skinMid = 0xFFDDCCBBu;
        
        // Pale hands holding camera with detail
        if (y > 0.55 && Math.Abs(x) < 0.25)
        {
            double handShade = Math.Abs(x) / 0.25;
            
            // Finger detail on grip
            if (Math.Abs(x) > 0.15 || y > 0.65)
            {
                // Knuckle detail
                double knuckle = Math.Sin(x * 35);
                if (knuckle > 0.7 && y < 0.62) return skinMid;
                
                // Vein detail
                double vein = Math.Sin(x * 20 + y * 15);
                if (vein > 0.85) return 0xFFDDBBAAu;
                
                if (handShade > 0.85) return skinMid;
                return skin;
            }
        }
        
        // Camera body (boxy but organic-looking)
        if (y > 0.12 && y < 0.58 && Math.Abs(x) < 0.24)
        {
            // Top edge with flash unit (row of TEETH!)
            if (y < 0.22)
            {
                if (Math.Abs(x) < 0.2 && y < 0.18)
                {
                    // Flash is bright when shooting!
                    if (_shooting && _shootFrame < 3)
                        return 0xFFFFFFFFu;
                    
                    // Row of teeth with gums
                    int toothIdx = (int)((x + 0.2) * 22) % 4;
                    if (toothIdx == 1 || toothIdx == 2)
                    {
                        // Individual tooth shape
                        double toothHeight = 0.18 - y;
                        if (toothHeight < 0.025) return 0xFFFFFFEEu;
                        return 0xFFEEEEDDu;
                    }
                    // Gum tissue between teeth
                    return 0xFF884455u;
                }
                
                double topShade = (y - 0.12) / 0.1;
                if (topShade < 0.3) return bodyDark;
                return bodyColor;
            }
            
            // Brass trim with patina detail
            if (y > 0.21 && y < 0.26)
            {
                double patina = Math.Sin(x * 45 + y * 20);
                if (patina > 0.6) return brassLight;
                if (patina < -0.5) return brassDark;
                // Engraved text/symbols
                if (y > 0.22 && y < 0.24)
                {
                    int engrave = (int)((x + 0.2) * 30) % 5;
                    if (engrave == 2) return brassDark;
                }
                return brass;
            }
            
            // Viewfinder (another small eye watching!)
            if (y > 0.25 && y < 0.34 && x > 0.06 && x < 0.19)
            {
                double vfX = x - 0.125;
                double vfY = y - 0.295;
                double vfDist = vfX * vfX + vfY * vfY;
                
                // Eye tracks your movement!
                double vfTrack = Math.Sin(time * 0.8) * 0.003;
                
                if (vfDist < 0.0012)
                {
                    double innerDist = (vfX - vfTrack) * (vfX - vfTrack) + vfY * vfY;
                    if (innerDist < 0.0001) return pupil;
                    if (innerDist < 0.0004) return 0xFF556677u;
                    // Bloodshot
                    double vein = Math.Sin(Math.Atan2(vfY, vfX) * 6);
                    if (vein > 0.7) return 0xFFEEAAAAu;
                    return eyeWhite;
                }
                // Fleshy eyelid around viewfinder
                if (vfDist < 0.002) return fleshDark;
            }
            
            // Ready light (blinks like a heartbeat - faster when shooting!)
            if (y > 0.26 && y < 0.32 && x > -0.17 && x < -0.1)
            {
                double heartRate = _shooting ? 12 : 6;
                double pulse = Math.Sin(time * heartRate);
                double pulseSize = pulse * pulse; // Square for sharper beat
                
                if (pulseSize > 0.5 || _shooting) return 0xFFFF2222u;
                if (pulseSize > 0.2) return 0xFFAA1111u;
                return 0xFF440000u;
            }
            
            // MAIN LENS - IT'S A LIVING EYE!
            double lensX = x + 0.02;
            double lensY = y - 0.4;
            double lensDist = lensX * lensX + lensY * lensY;
            
            if (lensDist < 0.028)
            {
                // Eye tracks the player's movement!
                double trackX = Math.Sin(time * 0.5) * 0.015;
                double trackY = Math.Cos(time * 0.3) * 0.008;
                double trackedX = lensX - trackX;
                double trackedY = lensY - trackY;
                double trackedDist = trackedX * trackedX + trackedY * trackedY;
                
                // Pupil (CONTRACTS when shooting - like real eye response to flash!)
                double pupilSize = _shooting ? 0.002 : 0.007;
                pupilSize += Math.Sin(time * 2) * 0.001; // Subtle dilation
                
                if (trackedDist < pupilSize) return pupil;
                
                // Iris with realistic pattern
                if (trackedDist < 0.016)
                {
                    double angle = Math.Atan2(trackedY, trackedX);
                    double irisRad = Math.Sqrt(trackedDist);
                    
                    // Radial iris fibers
                    double fiber = Math.Sin(angle * 24 + irisRad * 50);
                    if (fiber > 0.4) return 0xFF5588BBu;
                    if (fiber < -0.4) return irisDark;
                    
                    // Collarette ring
                    if (trackedDist > 0.008 && trackedDist < 0.01) return 0xFF667799u;
                    
                    return iris;
                }
                
                // Sclera (bloodshot, especially after shooting)
                double veinIntensity = _shooting ? 0.4 : 0.7;
                double veinAngle = Math.Atan2(lensY, lensX);
                double vein1 = Math.Sin(veinAngle * 5 + 0.5);
                double vein2 = Math.Sin(veinAngle * 7 + 2.1);
                double vein3 = Math.Sin(veinAngle * 3 + 4.2);
                
                if (vein1 > veinIntensity || vein2 > veinIntensity + 0.1 || vein3 > veinIntensity + 0.2)
                {
                    double veinBranch = Math.Sin(lensDist * 100);
                    if (veinBranch > 0.5) return 0xFFDD8888u;
                    return 0xFFEEAAAAu;
                }
                
                // Subtle moisture reflection
                if (lensX < -0.05 && lensY < -0.03 && lensDist > 0.02) return 0xFFFFFFFFu;
                
                return eyeWhite;
            }
            
            // Fleshy eyelid frame around lens (blinks occasionally!)
            if (lensDist < 0.038 && lensDist > 0.026)
            {
                // Blink animation
                double blink = Math.Sin(time * 0.15);
                if (blink > 0.98) // Rare blink
                {
                    if (Math.Abs(lensY) < 0.05) return fleshDark;
                }
                
                double lidShade = (lensDist - 0.026) / 0.012;
                if (lidShade > 0.7) return fleshDark;
                return flesh;
            }
            
            // Body texture (wood grain that looks like veins)
            double grain = Math.Sin(y * 35 + x * 8);
            double crossGrain = Math.Cos(y * 20 - x * 15);
            
            if (grain > 0.7 && crossGrain > 0.3) return bodyLight;
            if (grain < -0.6) return bodyDark;
            if (x < -0.17) return bodyDark;
            
            return bodyColor;
        }
        
        // Grip (wrapped in something leathery... human leather??)
        if (y > 0.5 && y < 0.6 && Math.Abs(x) < 0.22)
        {
            // Quilted/stitched pattern
            int grooveX = (int)((x + 0.22) * 25) % 4;
            int grooveY = (int)(y * 35) % 4;
            
            if (grooveX == 0 || grooveY == 0) return 0xFF332222u; // Stitching
            
            // Worn areas
            double wear = Math.Sin(x * 30 + y * 25);
            if (wear > 0.7) return 0xFFCCBBAAu;
            
            return flesh;
        }
        
        // Strap attachment (brass ring)
        if (x > 0.18 && x < 0.24 && y > 0.35 && y < 0.42)
        {
            double ringDist = (x - 0.21) * (x - 0.21) + (y - 0.385) * (y - 0.385);
            if (ringDist > 0.0004 && ringDist < 0.001) return brass;
        }
        
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
        
        // Render barrels on minimap (orange dots)
        foreach (var barrel in _barrels.Where(b => !b.Exploded))
        {
            int bx = (int)(barrel.X * scale), by = (int)(barrel.Y * scale);
            // Color based on barrel health (green -> yellow -> orange -> red)
            byte barrelR = (byte)(barrel.Hp > 20 ? 255 : barrel.Hp > 10 ? 255 : 255);
            byte barrelG = (byte)(barrel.Hp > 20 ? 140 : barrel.Hp > 10 ? 100 : 50);
            byte barrelB = 0;
            
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int px = bx + dx, py_m = by + dy;
                    if (px >= 0 && px < 120 && py_m >= 0 && py_m < 120)
                    {
                        int i = (py_m * 120 + px) * 4;
                        _minimapPx[i] = barrelB; _minimapPx[i + 1] = barrelG; _minimapPx[i + 2] = barrelR; _minimapPx[i + 3] = 255;
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
        
        // Camera film display (middle click dual-wield)
        FilmText.Text = $"📷 {_ammo[7]}";
        FilmText.Opacity = _cameraCooldown > 0 ? 0.5 : 1.0; // Dim when recharging

        RedKeyIcon.Opacity = _keys[0] ? 1 : 0.3;
        BlueKeyIcon.Opacity = _keys[1] ? 1 : 0.3;
        YellowKeyIcon.Opacity = _keys[2] ? 1 : 0.3;

        JetpackIcon.Background = _hasJetpack ? new SolidColorBrush(Color.FromRgb(80, 60, 20)) : new SolidColorBrush(Color.FromRgb(51, 51, 51));
        MedkitIcon.Background = _medkits > 0 ? new SolidColorBrush(Color.FromRgb(80, 30, 30)) : new SolidColorBrush(Color.FromRgb(51, 51, 51));
        SteroidsIcon.Background = _steroids > 0 ? new SolidColorBrush(Color.FromRgb(80, 80, 20)) : new SolidColorBrush(Color.FromRgb(51, 51, 51));

        // Silly face based on health!
        // Creepy faces that get more unsettling as health drops
        DukeFace.Text = _hp > 75 ? "🙂" : _hp > 50 ? "🙃" : _hp > 25 ? "👁️" : "💀";

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
                3 => "IT BEGINS...",
                4 => "THEY NOTICED YOU",
                5 => "SOMETHING IS WRONG",
                6 => "CAN'T STOP NOW",
                7 => "T̶H̶E̶Y̶ ̶S̶E̶E̶",
                _ => _killStreak >= 8 ? "Ẁ̵̡H̷A̴T̵ ̶H̵A̶V̶E̷ ̴Y̸O̷U̴ ̴D̵O̷N̵E̶" : "...huh?"
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
        if (_isSwappingWeapon) return; // Can't shoot while swapping
        if (_currentWeapon > 0 && _currentWeapon != 5 && _ammo[_currentWeapon] <= 0)
        {
            ShowMessage("Out of ammo!");
            return;
        }

        if (_gameTime - _lastShot < _weaponFireRate[_currentWeapon]) return;
        _lastShot = _gameTime;

        _shooting = true;
        _shootFrame = 0;
        _idleTimer = 0; // Reset idle timer
        if (_currentWeapon > 0 && _currentWeapon != 5) _ammo[_currentWeapon]--;
        
        // === ANIMATION: Weapon recoil based on weapon type ===
        double[] recoilAmounts = { 0.15, 0.25, 0.6, 0.1, 0.8, 0.2, 0.5, 0.3 };
        TriggerWeaponRecoil(recoilAmounts[_currentWeapon]);

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
        else if (_currentWeapon == 7) // Camera - just use the dedicated function
        {
            UseCamera();
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

            // Check enemies
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
            
            // Check exploding barrels
            foreach (var barrel in _barrels)
            {
                if (barrel.Exploded) continue;
                double d = Math.Sqrt((x - barrel.X) * (x - barrel.X) + (y - barrel.Y) * (y - barrel.Y));
                if (d < 0.5)
                {
                    barrel.Hp -= damage;
                    if (barrel.Hp <= 0)
                    {
                        ExplodeBarrel(barrel);
                    }
                    else
                    {
                        // Spark particles on barrel hit
                        SpawnParticles(barrel.X, barrel.Y, 5, 0xFFFF8800u, 0xFFFFAA00u);
                    }
                    return;
                }
            }
        }
    }
    
    void UseCamera()
    {
        // Camera flash - can be used with middle click while holding any weapon!
        if (_cameraCooldown > 0)
        {
            ShowMessage("Camera recharging...");
            return;
        }
        
        if (_ammo[7] <= 0)
        {
            ShowMessage("Out of film!");
            return;
        }
        
        _ammo[7]--;
        _cameraCooldown = 1.2; // Cooldown between flashes
        _cameraFlashTimer = 0.3; // Screen flash
        TriggerScreenShake(0.1, 3);
        
        // Stun all enemies in view cone
        int stunCount = 0;
        foreach (var en in _enemies)
        {
            if (en.Dead || en.IsStunned) continue;
            
            double dx = en.X - _px_pos;
            double dy = en.Y - _py;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            
            if (dist > 10) continue; // Range limit
            
            // Check if enemy is in front of player (within ~90 degree cone)
            double angleToEnemy = Math.Atan2(dy, dx);
            double angleDiff = Math.Abs(angleToEnemy - _pa);
            while (angleDiff > Math.PI) angleDiff = Math.Abs(angleDiff - 2 * Math.PI);
            
            if (angleDiff < Math.PI / 3) // 60 degree half-cone
            {
                // Stun duration based on distance (closer = longer stun)
                double stunDuration = 3.0 - dist * 0.2;
                en.StunTimer = Math.Max(en.StunTimer, stunDuration);
                en.HurtTimer = 0.2; // Visual feedback
                stunCount++;
                
                // Spawn creepy particles - red and white (eyes?)
                for (int s = 0; s < 5; s++)
                {
                    double angle = s * Math.PI * 2 / 5;
                    uint eyeColor = s % 2 == 0 ? 0xFFFF0000u : 0xFFFFFFFFu;
                    _particles.Add((en.X, en.Y, Math.Cos(angle) * 0.05, Math.Sin(angle) * 0.05, 1.0, eyeColor));
                }
            }
        }
        
        if (stunCount > 0)
        {
            string[] flashQuotes = { "Smile forever. 📷", "Now I have your soul.", "They'll never blink again.", "Captured. Like the others.", "The camera sees what you hide." };
            SayQuote(flashQuotes[_rnd.Next(flashQuotes.Length)]);
            ShowMessage($"CAPTURED {stunCount}...");
        }
        else
        {
            ShowMessage("...nothing there?");
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

        if (e.Key == Key.Escape) 
        { 
            if (_showingBriefing)
            {
                CloseBriefing();
                return;
            }
            if (MainMenuPanel.Visibility == Visibility.Visible)
            {
                Close();
                return;
            }
            TogglePause();
            return;
        }
        
        // Handle briefing dismissal
        if (_showingBriefing)
        {
            if (e.Key == Key.Space || e.Key == Key.Enter)
            {
                CloseBriefing();
            }
            return;
        }
        
        // Main menu is showing - don't process game input
        if (MainMenuPanel.Visibility == Visibility.Visible) return;

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

        if (e.Key == Key.D1) SwitchWeapon(0);
        if (e.Key == Key.D2) SwitchWeapon(1);
        if (e.Key == Key.D3 && _ammo[2] > 0) SwitchWeapon(2);
        if (e.Key == Key.D4 && _ammo[3] > 0) SwitchWeapon(3);
        if (e.Key == Key.D5 && _ammo[4] > 0) SwitchWeapon(4);
        if (e.Key == Key.D6 && _ammo[5] > 0) SwitchWeapon(5);
        if (e.Key == Key.D7 && _ammo.Length > 6 && _ammo[6] > 0) SwitchWeapon(6);

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
            Log.Warning(ex, "Failed to update top scores UI");
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
        if (e.MiddleButton == MouseButtonState.Pressed)
        {
            // Middle click = camera flash (dual wield!)
            if (_mouseCaptured && !_gameOver && !_levelComplete && !_victory)
                UseCamera();
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
        _ammo = new[] { 999, 48, 12, 200, 10, 5, 8, 24 }; // Include camera film
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
        _cameraFlashTimer = 0;
        _cameraCooldown = 0;
        _weaponBob = 0;
        _particles.Clear();
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
