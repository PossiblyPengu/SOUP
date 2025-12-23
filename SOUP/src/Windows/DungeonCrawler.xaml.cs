using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SOUP.Windows;

/// <summary>
/// Roguelike dungeon crawler with procedural generation, turn-based combat, and fog of war.
/// </summary>
public partial class DungeonCrawler : Window
{
    #region Constants
    private const int MapWidth = 50;
    private const int MapHeight = 35;
    private const int TileSize = 16;
    private const int MaxFloors = 10;
    private const int VisionRadius = 6;
    private const int MinRoomSize = 4;
    private const int MaxRoomSize = 10;
    private const int MaxRooms = 12;
    #endregion

    #region Game State
    private Tile[,] _map = new Tile[MapWidth, MapHeight];
    private bool[,] _visible = new bool[MapWidth, MapHeight];
    private bool[,] _explored = new bool[MapWidth, MapHeight];
    private Player _player = new();
    private List<Enemy> _enemies = new();
    private List<Item> _items = new();
    private List<Trap> _traps = new();
    private (int X, int Y) _stairsPosition;
    private (int X, int Y)? _elevatorPosition;
    private List<(int X, int Y)> _teleporters = new();
    private (int X, int Y)? _lockedDoorPosition;
    private (int X, int Y)? _keyPosition;
    private (int X, int Y)? _safeRoomCenter;
    private bool _hasKey;
    private int _currentFloor = 1;
    private bool _gameOver;
    private bool _victory;
    private Random _rng = new();
    private ObservableCollection<string> _messages = new();
    private ObservableCollection<InventoryItem> _inventory = new();
    private List<UIElement> _entityLabels = new();
    private List<(int X, int Y, SolidColorBrush Color, DateTime ExpireTime)> _weaponEffects = new();
    private List<ParticleEffect> _particles = new();
    private PlayerSkills _playerSkills = new();
    private int _comboCounter = 0;
    private DateTime _lastAttackTime = DateTime.MinValue;
    private Dictionary<int, Enemy> _bossEnemies = new(); // Floor -> Boss
    private System.Windows.Media.MediaPlayer? _musicPlayer;
    private Dictionary<string, System.Windows.Media.MediaPlayer> _soundEffects = new();
    #endregion

    #region Brushes
    private static readonly SolidColorBrush WallBrush = new(Color.FromRgb(255, 179, 217));  // Pink walls
    private static readonly SolidColorBrush FloorBrush = new(Color.FromRgb(255, 228, 242));  // Light pink floor
    private static readonly SolidColorBrush PlayerBrush = new(Color.FromRgb(34, 197, 94));
    private static readonly SolidColorBrush EnemyBrush = new(Color.FromRgb(255, 105, 180));  // Hot pink enemies
    private static readonly SolidColorBrush TreasureBrush = new(Color.FromRgb(255, 215, 0));
    private static readonly SolidColorBrush StairsBrush = new(Color.FromRgb(218, 112, 214));  // Orchid stairs
    private static readonly SolidColorBrush TrapBrush = new(Color.FromRgb(255, 20, 147));  // Deep pink traps
    private static readonly SolidColorBrush FogBrush = new(Color.FromRgb(255, 228, 248));  // Very light pink fog
    private static readonly SolidColorBrush ExploredFogBrush = new(Color.FromRgb(255, 213, 240));  // Light pink explored fog
    private static readonly SolidColorBrush HealthPotionBrush = new(Color.FromRgb(239, 68, 68));
    private static readonly SolidColorBrush GoldBrush = new(Color.FromRgb(255, 215, 0));  // Gold color
    
    // Sprite colors
    private static readonly SolidColorBrush SkinBrush = new(Color.FromRgb(255, 206, 180));
    private static readonly SolidColorBrush HairBrush = new(Color.FromRgb(139, 90, 43));
    private static readonly SolidColorBrush ClothBrush = new(Color.FromRgb(65, 105, 225));
    private static readonly SolidColorBrush EyeBrush = new(Color.FromRgb(20, 20, 20));
    private static readonly SolidColorBrush WhiteBrush = new(Color.FromRgb(255, 255, 255));
    private static readonly SolidColorBrush RedBrush = new(Color.FromRgb(220, 50, 50));
    private static readonly SolidColorBrush PinkBrush = new(Color.FromRgb(255, 182, 193));
    private static readonly SolidColorBrush BrownBrush = new(Color.FromRgb(139, 90, 43));
    private static readonly SolidColorBrush GrayBrush = new(Color.FromRgb(128, 128, 128));
    private static readonly SolidColorBrush DarkGrayBrush = new(Color.FromRgb(64, 64, 64));
    private static readonly SolidColorBrush YellowBrush = new(Color.FromRgb(255, 215, 0));
    private static readonly SolidColorBrush OrangeBrush = new(Color.FromRgb(255, 140, 0));
    private static readonly SolidColorBrush PurpleBrush = new(Color.FromRgb(148, 0, 211));
    private static readonly SolidColorBrush CyanBrush = new(Color.FromRgb(0, 255, 255));
    private static readonly SolidColorBrush GreenBrush = new(Color.FromRgb(50, 205, 50));
    private static readonly SolidColorBrush DarkBrownBrush = new(Color.FromRgb(80, 50, 20));
    private static readonly SolidColorBrush LightBlueBrush = new(Color.FromRgb(135, 206, 250));
    private static readonly SolidColorBrush MagentaBrush = new(Color.FromRgb(255, 0, 255));
    private static readonly SolidColorBrush TransparentBrush = new(Colors.Transparent);
    #endregion

    public DungeonCrawler()
    {
        InitializeComponent();
        MessageLog.ItemsSource = _messages;
        InventoryList.ItemsSource = _inventory;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        InitializeGame();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (_gameOver || _victory)
        {
            if (e.Key == Key.R)
            {
                RestartGame();
            }
            return;
        }

        int dx = 0, dy = 0;
        bool acted = false;
        bool isTurn = false;

        switch (e.Key)
        {
            case Key.W:
            case Key.Up:
                // Move forward in facing direction
                (dx, dy) = GetDirectionDelta(_player.Facing);
                acted = true;
                break;
            case Key.S:
            case Key.Down:
                // Move backward (opposite of facing direction)
                (dx, dy) = GetDirectionDelta(_player.Facing);
                dx = -dx;
                dy = -dy;
                acted = true;
                break;
            case Key.A:
            case Key.Left:
                // Turn left
                _player.Facing = TurnLeft(_player.Facing);
                acted = true;
                isTurn = true;
                break;
            case Key.D:
            case Key.Right:
                // Turn right
                _player.Facing = TurnRight(_player.Facing);
                acted = true;
                isTurn = true;
                break;
            case Key.Space:
                // Wait/Rest - heal more in safe room
                int healAmount = IsInSafeRoom() ? 5 : 1;
                if (_player.Health < _player.MaxHealth)
                {
                    _player.Health = Math.Min(_player.MaxHealth, _player.Health + healAmount);
                    if (IsInSafeRoom())
                        AddMessage($"☺ The HAPPY ROOM embraces you! (It knows your shape) +{healAmount} HP! ☺");
                    else
                        AddMessage("🌈 You rest! Something is healing you! (Don't ask what) +1 HP! 🌈");
                }
                else
                {
                    AddMessage(IsInSafeRoom() ? "💖 You're SAFE here! (The voices say so) Full health! 💖" : "✨ You wait... Time passes strangely here... ✨");
                }
                acted = true;
                break;
            case Key.E:
                // Interact with special tiles
                HandleInteraction();
                return;
            case Key.F:
                // Use weapon special ability
                UseWeaponSpecial();
                acted = true;
                break;
            case Key.Q:
                // Use elevator (if on elevator tile)
                if (_elevatorPosition.HasValue && 
                    _player.X == _elevatorPosition.Value.X && 
                    _player.Y == _elevatorPosition.Value.Y)
                {
                    UseElevator();
                    return;
                }
                else
                {
                    AddMessage("☺ No Party Lift here! But that's OKAY! ☺");
                }
                break;
            case Key.T:
                // Use teleporter
                if (_teleporters.Count >= 2)
                {
                    var currentTeleporter = _teleporters.FirstOrDefault(t => t.X == _player.X && t.Y == _player.Y);
                    if (currentTeleporter != default)
                    {
                        UseTeleporter(currentTeleporter);
                        acted = true;
                    }
                    else
                    {
                        AddMessage("🌈 No Happy Jump here! Try somewhere else! FUN! 🌈");
                    }
                }
                break;
            case Key.D1:
            case Key.D2:
            case Key.D3:
            case Key.D4:
            case Key.D5:
                int slot = e.Key - Key.D1;
                UseItem(slot);
                acted = true;
                break;
            case Key.R:
                RestartGame();
                return;
            case Key.K:
                // Open skill menu
                ShowSkillMenu();
                return;
        }

        if (acted && !isTurn && (dx != 0 || dy != 0))
        {
            TryMove(dx, dy);
        }

        if (acted)
        {
            if (!isTurn) // Enemies only act if you moved, not if you just turned
            {
                ProcessEnemyTurns();
            }
            UpdateVisibility();
            Render();
            UpdateUI();
            CheckGameState();
        }
    }

    #region Game Initialization
    private void InitializeGame()
    {
        _currentFloor = 1;
        _gameOver = false;
        _victory = false;
        _hasKey = false;
        _player = new Player
        {
            Health = 100,
            MaxHealth = 100,
            Attack = 10,
            Defense = 5,
            Level = 1,
            XP = 0,
            Gold = 0
        };
        _inventory.Clear();
        _messages.Clear();
        
        // Add starting items
        _inventory.Add(new InventoryItem { Icon = "�", Name = "Forbidden Juice", Type = ItemType.HealthPotion, Quantity = 2 });
        
        GameOverOverlay.Visibility = Visibility.Collapsed;
        VictoryOverlay.Visibility = Visibility.Collapsed;

        GenerateFloor();
        AddMessage(GetCreepyEntranceMessage());
        AddMessage("🎈 The stairs (🟣) have been waiting! They knew you'd come! They always know! 🎈");
        UpdateUI();
    }

    private void RestartGame()
    {
        InitializeGame();
    }

    private void GenerateFloor()
    {
        // Reset map
        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                _map[x, y] = Tile.Wall;
                _visible[x, y] = false;
                _explored[x, y] = false;
            }
        }

        _enemies.Clear();
        _items.Clear();
        _traps.Clear();
        _teleporters.Clear();
        _elevatorPosition = null;
        _lockedDoorPosition = null;
        _keyPosition = null;
        _safeRoomCenter = null;

        // Generate rooms using BSP-like approach
        var rooms = GenerateRooms();

        // Connect rooms with corridors
        for (int i = 1; i < rooms.Count; i++)
        {
            var prev = rooms[i - 1];
            var curr = rooms[i];
            CreateCorridor(prev.CenterX, prev.CenterY, curr.CenterX, curr.CenterY);
        }

        // Place player in first room
        var startRoom = rooms[0];
        _player.X = startRoom.CenterX;
        _player.Y = startRoom.CenterY;

        // Place stairs in last room
        var endRoom = rooms[^1];
        _stairsPosition = (endRoom.CenterX, endRoom.CenterY);
        _map[_stairsPosition.X, _stairsPosition.Y] = Tile.Stairs;

        // Spawn enemies (more on deeper floors)
        int enemyCount = 3 + _currentFloor * 2;
        for (int i = 0; i < enemyCount; i++)
        {
            SpawnEnemy(rooms);
        }

        // Spawn items
        int itemCount = 2 + _rng.Next(3);
        for (int i = 0; i < itemCount; i++)
        {
            SpawnItem(rooms);
        }

        // Spawn traps (more on deeper floors)
        int trapCount = _currentFloor;
        for (int i = 0; i < trapCount; i++)
        {
            SpawnTrap(rooms);
        }

        // Generate traversal features
        GenerateTraversalFeatures(rooms);

        // Spawn boss on boss floors
        SpawnBoss(rooms, _currentFloor);

        UpdateVisibility();
        Render();
    }

    private List<Room> GenerateRooms()
    {
        var rooms = new List<Room>();

        for (int i = 0; i < MaxRooms; i++)
        {
            int w = _rng.Next(MinRoomSize, MaxRoomSize + 1);
            int h = _rng.Next(MinRoomSize, MaxRoomSize + 1);
            int x = _rng.Next(1, MapWidth - w - 1);
            int y = _rng.Next(1, MapHeight - h - 1);

            var room = new Room(x, y, w, h);

            // Check for overlaps
            bool overlaps = rooms.Any(r => room.Intersects(r));
            if (!overlaps)
            {
                CarveRoom(room);
                rooms.Add(room);
            }
        }

        return rooms;
    }

    private void CarveRoom(Room room)
    {
        for (int x = room.X; x < room.X + room.Width; x++)
        {
            for (int y = room.Y; y < room.Y + room.Height; y++)
            {
                _map[x, y] = Tile.Floor;
            }
        }
    }

    private void CreateCorridor(int x1, int y1, int x2, int y2)
    {
        // L-shaped corridor
        if (_rng.Next(2) == 0)
        {
            CarveHorizontalTunnel(x1, x2, y1);
            CarveVerticalTunnel(y1, y2, x2);
        }
        else
        {
            CarveVerticalTunnel(y1, y2, x1);
            CarveHorizontalTunnel(x1, x2, y2);
        }
    }

    private void CarveHorizontalTunnel(int x1, int x2, int y)
    {
        for (int x = Math.Min(x1, x2); x <= Math.Max(x1, x2); x++)
        {
            if (x >= 0 && x < MapWidth && y >= 0 && y < MapHeight)
                _map[x, y] = Tile.Floor;
        }
    }

    private void CarveVerticalTunnel(int y1, int y2, int x)
    {
        for (int y = Math.Min(y1, y2); y <= Math.Max(y1, y2); y++)
        {
            if (x >= 0 && x < MapWidth && y >= 0 && y < MapHeight)
                _map[x, y] = Tile.Floor;
        }
    }

    private void SpawnEnemy(List<Room> rooms)
    {
        for (int attempts = 0; attempts < 50; attempts++)
        {
            var room = rooms[_rng.Next(rooms.Count)];
            int x = _rng.Next(room.X, room.X + room.Width);
            int y = _rng.Next(room.Y, room.Y + room.Height);

            if (_map[x, y] == Tile.Floor && 
                !(x == _player.X && y == _player.Y) &&
                !_enemies.Any(e => e.X == x && e.Y == y))
            {
                var enemyTypes = GetEnemyTypesForFloor();
                var type = enemyTypes[_rng.Next(enemyTypes.Length)];
                _enemies.Add(CreateEnemy(type, x, y));
                return;
            }
        }
    }

    private EnemyType[] GetEnemyTypesForFloor()
    {
        return _currentFloor switch
        {
            <= 2 => new[] { EnemyType.LostTeddy, EnemyType.CribSpider, EnemyType.NightLight },
            <= 4 => new[] { EnemyType.SwingChild, EnemyType.SandboxThing, EnemyType.CarouselHorse },
            <= 6 => new[] { EnemyType.SubstituteTeacher, EnemyType.HallMonitor, EnemyType.LunchLady },
            <= 8 => new[] { EnemyType.WrongMom, EnemyType.AtticDweller, EnemyType.BasementFriend },
            _ => new[] { EnemyType.MirrorYou, EnemyType.TheHost, EnemyType.YourBestFriend }
        };
    }

    private Enemy CreateEnemy(EnemyType type, int x, int y)
    {
        return type switch
        {
            // Floor 1-2: The Nursery
            EnemyType.LostTeddy => new Enemy { X = x, Y = y, Type = type, Name = "Lost Teddy", Icon = "🧸", Health = 8, MaxHealth = 8, Attack = 3, Defense = 0, XPValue = 5,
                AttackMessages = new[] { "hugs you until something pops", "whispers your birth weight", "cries stuffing onto you" } },
            EnemyType.CribSpider => new Enemy { X = x, Y = y, Type = type, Name = "Crib Spider", Icon = "🕷️", Health = 6, MaxHealth = 6, Attack = 4, Defense = 0, XPValue = 5,
                AttackMessages = new[] { "tucks you in too tight", "sings in frequencies you forgot", "counts your fingers (there are more now)" } },
            EnemyType.NightLight => new Enemy { X = x, Y = y, Type = type, Name = "Night Light", Icon = "🌙", Health = 10, MaxHealth = 10, Attack = 2, Defense = 1, XPValue = 8,
                AttackMessages = new[] { "shows you what's really in the corner", "flickers your childhood away", "illuminates things that weren't there" } },
            
            // Floor 3-4: The Playground
            EnemyType.SwingChild => new Enemy { X = x, Y = y, Type = type, Name = "Swing Child", Icon = "🧒", Health = 15, MaxHealth = 15, Attack = 6, Defense = 2, XPValue = 15,
                AttackMessages = new[] { "swings through you", "asks if you remember them (you do)", "shares a secret you told no one" } },
            EnemyType.SandboxThing => new Enemy { X = x, Y = y, Type = type, Name = "Sandbox Thing", Icon = "🏖️", Health = 12, MaxHealth = 12, Attack = 7, Defense = 1, XPValue = 12,
                AttackMessages = new[] { "buries your memories", "builds a castle with your teeth", "finds what you hid here" } },
            EnemyType.CarouselHorse => new Enemy { X = x, Y = y, Type = type, Name = "Carousel Horse", Icon = "🎠", Health = 18, MaxHealth = 18, Attack = 5, Defense = 3, XPValue = 18,
                AttackMessages = new[] { "gallops in circles around your sanity", "plays music you danced to once", "never stops smiling" } },
            
            // Floor 5-6: The School
            EnemyType.SubstituteTeacher => new Enemy { X = x, Y = y, Type = type, Name = "Substitute Teacher", Icon = "👩‍🏫", Health = 20, MaxHealth = 20, Attack = 8, Defense = 3, XPValue = 25,
                AttackMessages = new[] { "knows your real name", "marks you absent from existence", "assigns homework due yesterday" } },
            EnemyType.HallMonitor => new Enemy { X = x, Y = y, Type = type, Name = "Hall Monitor", Icon = "📋", Health = 22, MaxHealth = 22, Attack = 7, Defense = 4, XPValue = 22,
                AttackMessages = new[] { "writes you up for being", "asks for a pass you never had", "escorts you somewhere else" } },
            EnemyType.LunchLady => new Enemy { X = x, Y = y, Type = type, Name = "Lunch Lady", Icon = "🥄", Health = 25, MaxHealth = 25, Attack = 9, Defense = 2, XPValue = 28,
                AttackMessages = new[] { "serves you your own memories", "knows exactly what you want (wrong)", "ladles something warm onto you" } },
            
            // Floor 7-8: The Home
            EnemyType.WrongMom => new Enemy { X = x, Y = y, Type = type, Name = "Wrong Mom", Icon = "👩", Health = 35, MaxHealth = 35, Attack = 12, Defense = 5, XPValue = 40,
                AttackMessages = new[] { "calls you by a name you almost remember", "made your favorite (you hate it)", "says dinner is ready (it's not food)" } },
            EnemyType.AtticDweller => new Enemy { X = x, Y = y, Type = type, Name = "Attic Dweller", Icon = "🏚️", Health = 30, MaxHealth = 30, Attack = 14, Defense = 4, XPValue = 35,
                AttackMessages = new[] { "drops photo albums on you", "shows you the room you forgot", "wears your baby clothes" } },
            EnemyType.BasementFriend => new Enemy { X = x, Y = y, Type = type, Name = "Basement Friend", Icon = "🚪", Health = 40, MaxHealth = 40, Attack = 11, Defense = 6, XPValue = 45,
                AttackMessages = new[] { "waited so long for you", "still has your toys", "never left" } },
            
            // Floor 9-10: The End
            EnemyType.MirrorYou => new Enemy { X = x, Y = y, Type = type, Name = "Mirror You", Icon = "🪞", Health = 60, MaxHealth = 60, Attack = 16, Defense = 8, XPValue = 70,
                AttackMessages = new[] { "does what you were going to do", "apologizes for what you'll become", "has your face but not your eyes" } },
            EnemyType.TheHost => new Enemy { X = x, Y = y, Type = type, Name = "The Host", Icon = "🎭", Health = 70, MaxHealth = 70, Attack = 18, Defense = 7, XPValue = 85,
                AttackMessages = new[] { "welcomes you home", "says the party never ended", "offers you a seat (permanent)" } },
            EnemyType.YourBestFriend => new Enemy { X = x, Y = y, Type = type, Name = "Your Best Friend", Icon = "💝", Health = 100, MaxHealth = 100, Attack = 22, Defense = 10, XPValue = 120,
                AttackMessages = new[] { "missed you so much it hurts", "wants to be together forever now", "promises to never let go" } },
            
            _ => new Enemy { X = x, Y = y, Type = type, Name = "Lost Teddy", Icon = "🧸", Health = 8, MaxHealth = 8, Attack = 3, Defense = 0, XPValue = 5,
                AttackMessages = new[] { "hugs you too tight" } }
        };
    }

    private void SpawnItem(List<Room> rooms)
    {
        for (int attempts = 0; attempts < 50; attempts++)
        {
            var room = rooms[_rng.Next(rooms.Count)];
            int x = _rng.Next(room.X, room.X + room.Width);
            int y = _rng.Next(room.Y, room.Y + room.Height);

            if (_map[x, y] == Tile.Floor &&
                !(x == _player.X && y == _player.Y) &&
                !_items.Any(i => i.X == x && i.Y == y))
            {
                var itemType = (ItemType)_rng.Next(1, 5); // Random item type
                _items.Add(new Item { X = x, Y = y, Type = itemType });
                return;
            }
        }
    }

    private void SpawnTrap(List<Room> rooms)
    {
        for (int attempts = 0; attempts < 50; attempts++)
        {
            var room = rooms[_rng.Next(rooms.Count)];
            int x = _rng.Next(room.X, room.X + room.Width);
            int y = _rng.Next(room.Y, room.Y + room.Height);

            if (_map[x, y] == Tile.Floor &&
                !(x == _player.X && y == _player.Y) &&
                !_traps.Any(t => t.X == x && t.Y == y) &&
                !(x == _stairsPosition.X && y == _stairsPosition.Y))
            {
                var trapType = (TrapType)_rng.Next(0, 5); // 5 trap types now
                int damage = 5 + _currentFloor * 3;
                _traps.Add(new Trap { X = x, Y = y, Type = trapType, Damage = damage, Triggered = false });
                return;
            }
        }
    }

    private void GenerateTraversalFeatures(List<Room> rooms)
    {
        // Elevator: 50% chance on floors 2+, can skip floors
        if (_currentFloor >= 2 && _rng.Next(100) < 50)
        {
            GenerateElevator(rooms);
        }

        // Teleporter pair: 40% chance
        if (_rng.Next(100) < 40)
        {
            GenerateTeleporters(rooms);
        }

        // Locked door with key: 60% chance on floors 2+
        if (_currentFloor >= 2 && _rng.Next(100) < 60)
        {
            GenerateLockedDoorAndKey(rooms);
        }

        // Safe room: 30% chance, more likely on harder floors
        if (_rng.Next(100) < 30 + _currentFloor * 5)
        {
            GenerateSafeRoom(rooms);
        }

        // Secret passage: 25% chance
        if (_rng.Next(100) < 25)
        {
            GenerateSecretPassage(rooms);
        }
    }

    private void GenerateElevator(List<Room> rooms)
    {
        for (int attempts = 0; attempts < 50; attempts++)
        {
            var room = rooms[_rng.Next(rooms.Count)];
            int x = _rng.Next(room.X + 1, room.X + room.Width - 1);
            int y = _rng.Next(room.Y + 1, room.Y + room.Height - 1);

            if (_map[x, y] == Tile.Floor && 
                !(x == _stairsPosition.X && y == _stairsPosition.Y) &&
                !(x == _player.X && y == _player.Y))
            {
                _elevatorPosition = (x, y);
                _map[x, y] = Tile.Elevator;
                return;
            }
        }
    }

    private void GenerateTeleporters(List<Room> rooms)
    {
        if (rooms.Count < 2) return;

        // Place two teleporters in different rooms
        var room1 = rooms[_rng.Next(rooms.Count)];
        var room2 = rooms.FirstOrDefault(r => r != room1) ?? room1;

        (int x, int y)? tele1 = null;
        (int x, int y)? tele2 = null;

        for (int attempts = 0; attempts < 50; attempts++)
        {
            int x = _rng.Next(room1.X, room1.X + room1.Width);
            int y = _rng.Next(room1.Y, room1.Y + room1.Height);
            if (_map[x, y] == Tile.Floor && !(x == _player.X && y == _player.Y))
            {
                tele1 = (x, y);
                break;
            }
        }

        for (int attempts = 0; attempts < 50; attempts++)
        {
            int x = _rng.Next(room2.X, room2.X + room2.Width);
            int y = _rng.Next(room2.Y, room2.Y + room2.Height);
            if (_map[x, y] == Tile.Floor && !(x == _player.X && y == _player.Y) && tele1 != (x, y))
            {
                tele2 = (x, y);
                break;
            }
        }

        if (tele1.HasValue && tele2.HasValue)
        {
            _teleporters.Add(tele1.Value);
            _teleporters.Add(tele2.Value);
            _map[tele1.Value.x, tele1.Value.y] = Tile.Teleporter;
            _map[tele2.Value.x, tele2.Value.y] = Tile.Teleporter;
        }
    }

    private void GenerateLockedDoorAndKey(List<Room> rooms)
    {
        if (rooms.Count < 2) return;

        // Put door blocking path to a treasure room, key in another room
        var doorRoom = rooms[_rng.Next(rooms.Count / 2, rooms.Count)]; // Later rooms
        var keyRoom = rooms[_rng.Next(0, rooms.Count / 2)]; // Earlier rooms

        // Find a doorway position (edge of room)
        for (int attempts = 0; attempts < 50; attempts++)
        {
            int edge = _rng.Next(4);
            int x, y;
            switch (edge)
            {
                case 0: x = doorRoom.X; y = _rng.Next(doorRoom.Y, doorRoom.Y + doorRoom.Height); break;
                case 1: x = doorRoom.X + doorRoom.Width - 1; y = _rng.Next(doorRoom.Y, doorRoom.Y + doorRoom.Height); break;
                case 2: x = _rng.Next(doorRoom.X, doorRoom.X + doorRoom.Width); y = doorRoom.Y; break;
                default: x = _rng.Next(doorRoom.X, doorRoom.X + doorRoom.Width); y = doorRoom.Y + doorRoom.Height - 1; break;
            }

            if (_map[x, y] == Tile.Floor && !(x == _stairsPosition.X && y == _stairsPosition.Y))
            {
                _lockedDoorPosition = (x, y);
                _map[x, y] = Tile.LockedDoor;
                break;
            }
        }

        // Place key in earlier room
        if (_lockedDoorPosition.HasValue)
        {
            for (int attempts = 0; attempts < 50; attempts++)
            {
                int x = _rng.Next(keyRoom.X, keyRoom.X + keyRoom.Width);
                int y = _rng.Next(keyRoom.Y, keyRoom.Y + keyRoom.Height);
                if (_map[x, y] == Tile.Floor && !(x == _player.X && y == _player.Y))
                {
                    _keyPosition = (x, y);
                    _items.Add(new Item { X = x, Y = y, Type = ItemType.Key });
                    return;
                }
            }
        }
    }

    private void GenerateSafeRoom(List<Room> rooms)
    {
        // Find a smaller room to convert to safe room
        var candidates = rooms.Where(r => r.Width <= 5 && r.Height <= 5 && r != rooms[0] && r != rooms[^1]).ToList();
        if (candidates.Count == 0) return;

        var safeRoom = candidates[_rng.Next(candidates.Count)];
        _safeRoomCenter = (safeRoom.CenterX, safeRoom.CenterY);

        // Mark the safe room tiles
        for (int x = safeRoom.X; x < safeRoom.X + safeRoom.Width; x++)
        {
            for (int y = safeRoom.Y; y < safeRoom.Y + safeRoom.Height; y++)
            {
                if (_map[x, y] == Tile.Floor)
                    _map[x, y] = Tile.SafeRoom;
            }
        }

        // Remove any enemies in this room
        _enemies.RemoveAll(e => e.X >= safeRoom.X && e.X < safeRoom.X + safeRoom.Width &&
                                e.Y >= safeRoom.Y && e.Y < safeRoom.Y + safeRoom.Height);
    }

    private void GenerateSecretPassage(List<Room> rooms)
    {
        // Create a hidden wall that can be walked through
        for (int attempts = 0; attempts < 100; attempts++)
        {
            int x = _rng.Next(1, MapWidth - 1);
            int y = _rng.Next(1, MapHeight - 1);

            // Check if this wall has floor on opposite sides (horizontal or vertical)
            bool horizontalPassage = _map[x - 1, y] == Tile.Floor && _map[x + 1, y] == Tile.Floor && _map[x, y] == Tile.Wall;
            bool verticalPassage = _map[x, y - 1] == Tile.Floor && _map[x, y + 1] == Tile.Floor && _map[x, y] == Tile.Wall;

            if (horizontalPassage || verticalPassage)
            {
                _map[x, y] = Tile.SecretWall;
                return;
            }
        }
    }
    #endregion

    #region Game Logic
    private void TryMove(int dx, int dy)
    {
        int newX = _player.X + dx;
        int newY = _player.Y + dy;

        if (newX < 0 || newX >= MapWidth || newY < 0 || newY >= MapHeight)
            return;

        var targetTile = _map[newX, newY];

        if (targetTile == Tile.Wall)
            return;

        // Locked door blocks movement unless you have key
        if (targetTile == Tile.LockedDoor)
        {
            if (_hasKey)
            {
                // Auto-unlock when walking through
                _map[newX, newY] = Tile.Floor;
                _lockedDoorPosition = null;
                _hasKey = false;
                var key = _inventory.FirstOrDefault(i => i.Type == ItemType.Key);
                if (key != null) _inventory.Remove(key);
                AddMessage("🗝️ The key DISSOLVES with JOY! The door is SO HAPPY NOW! 🗝️");
                UpdateInventoryUI();
            }
            else
            {
                AddMessage("🚪 A FRIENDLY door wants to play! Find the HAPPY key first! 🚪");
                return;
            }
        }

        // Secret wall - reveal it and pass through
        if (targetTile == Tile.SecretWall)
        {
            _map[newX, newY] = Tile.Floor;
            AddMessage("✨ The wall was just PRETENDING! How FUN! You pass through! ✨");
        }

        // Check for enemy at target
        var enemy = _enemies.FirstOrDefault(e => e.X == newX && e.Y == newY);
        if (enemy != null)
        {
            AttackEnemy(enemy);
            return;
        }

        // Move player
        _player.X = newX;
        _player.Y = newY;

        // Check for items
        var item = _items.FirstOrDefault(i => i.X == newX && i.Y == newY);
        if (item != null)
        {
            PickupItem(item);
        }

        // Check for traps
        var trap = _traps.FirstOrDefault(t => t.X == newX && t.Y == newY && !t.Triggered);
        if (trap != null)
        {
            TriggerTrap(trap);
        }

        // Check for special tiles
        CheckSpecialTileMessages();
    }

    private void CheckSpecialTileMessages()
    {
        var tile = _map[_player.X, _player.Y];
        
        switch (tile)
        {
            case Tile.Stairs:
                AddMessage("🎈 The stairs WHISPER your name! They miss you! Press E to go DEEPER! 🎈");
                break;
            case Tile.Elevator:
                AddMessage("✨ A Party Lift! It knows where you NEED to go! (Not where you WANT) Press Q or E! ✨");
                break;
            case Tile.Teleporter:
                AddMessage("🌈 A Happy Jump! Your atoms will come back! (Probably all of them) Press T or E! 🌈");
                break;
            case Tile.SafeRoom:
                if (_rng.Next(100) < 30)
                    AddMessage("💖 This room is SAFE! The walls are watching over you! ALL of them! 💖");
                break;
        }
    }

    private void AttackEnemy(Enemy enemy)
    {
        // Base damage calculation
        int baseDamage = _player.Attack + _playerSkills.GetAttackBonus();
        baseDamage += _playerSkills.GetBerserkerBonus(_player.Health, _player.MaxHealth);
        int damage = Math.Max(1, baseDamage - enemy.Defense + _rng.Next(-2, 3));

        // Check for critical hit
        bool isCrit = _rng.Next(100) < _playerSkills.GetCritChance();
        if (isCrit)
        {
            damage = (int)(damage * 2.0);
            AddMessage($"💥 CRITICAL HIT! The {enemy.Name} FEELS ALL YOUR JOY at once! 💥");
            CreateParticles(enemy.X, enemy.Y, Brushes.Yellow, 10);
        }
        else
        {
            CreateParticles(enemy.X, enemy.Y, Brushes.Orange, 5);
        }

        // Update combo counter
        var timeSinceLastAttack = DateTime.Now - _lastAttackTime;
        if (timeSinceLastAttack.TotalSeconds < 3)
        {
            _comboCounter++;
            if (_comboCounter >= 3)
            {
                damage = (int)(damage * (1.0 + _comboCounter * 0.1));
                AddMessage($"🎯 {_comboCounter}x COMBO! Your FRIENDSHIP is OVERWHELMING! 🎯");
                CreateParticles(enemy.X, enemy.Y, Brushes.Purple, 15);
            }
        }
        else
        {
            _comboCounter = 1;
        }
        _lastAttackTime = DateTime.Now;

        // Apply damage
        enemy.Health -= damage;
        AddMessage(GetCreepyAttackMessage(enemy, damage));

        // Lifesteal from vampiric skill
        int lifestealPercent = _playerSkills.GetLifestealPercent();
        if (lifestealPercent > 0)
        {
            int heal = (int)(damage * lifestealPercent / 100.0);
            if (heal > 0 && _player.Health < _player.MaxHealth)
            {
                _player.Health = Math.Min(_player.MaxHealth, _player.Health + heal);
                AddMessage($"💉 You ABSORB their essence! +{heal} HP! They're SHARING! 💉");
                CreateParticles(_player.X, _player.Y, Brushes.Pink, 8);
            }
        }

        if (enemy.Health <= 0)
        {
            _enemies.Remove(enemy);
            int xpGain = enemy.XPValue;
            _player.XP += xpGain;
            int goldDrop = (int)(_rng.Next(1, 10) * _currentFloor * _playerSkills.GetGoldMultiplier());
            _player.Gold += goldDrop;
            AddMessage(GetCreepyDeathMessage(enemy, xpGain, goldDrop));
            CreateParticles(enemy.X, enemy.Y, Brushes.Gold, 20);

            // Check for level up
            CheckLevelUp();
        }
    }

    private void UseWeaponSpecial()
    {
        // Check if player has a weapon
        if (_player.EquippedWeapon == null)
        {
            AddMessage("💫 You have no weapon! Your hands flail uselessly! SO FUN! 💫");
            return;
        }

        var weapon = _player.EquippedWeapon;

        // Check cooldown
        if (weapon.SpecialCooldown > 0)
        {
            AddMessage($"⏳ {weapon.Name} needs {weapon.SpecialCooldown} more turns to recharge! BE PATIENT! ⏳");
            return;
        }

        // Find enemy in front of player
        var (dx, dy) = GetDirectionDelta(_player.Facing);
        int targetX = _player.X + dx;
        int targetY = _player.Y + dy;

        // For area damage and special abilities, get all visible enemies
        var visibleEnemies = _enemies.Where(e => _visible[e.X, e.Y]).ToList();

        // Try to find enemy directly in front first
        var targetEnemy = _enemies.FirstOrDefault(e => e.X == targetX && e.Y == targetY);

        // Apply ability based on type
        switch (weapon.Ability)
        {
            case WeaponAbility.DoubleDamage:
                if (targetEnemy != null)
                {
                    int damage = (_player.Attack * weapon.AbilityPower) - targetEnemy.Defense;
                    damage = Math.Max(damage, weapon.AbilityPower);
                    targetEnemy.Health -= damage;
                    AddMessage($"💥 {weapon.Icon} SPECIAL! {weapon.Name} unleashes FURY! {damage} damage! WOW! 💥");
                    ShowWeaponEffect(targetX, targetY, Brushes.Red);
                    CheckEnemyDeath(targetEnemy);
                }
                else
                {
                    AddMessage("✨ You swing dramatically at nothing! The air fears you! ✨");
                }
                break;

            case WeaponAbility.Lifesteal:
                if (targetEnemy != null)
                {
                    int damage = _player.Attack - targetEnemy.Defense + weapon.AbilityPower;
                    damage = Math.Max(damage, weapon.AbilityPower);
                    targetEnemy.Health -= damage;
                    int heal = weapon.AbilityPower;
                    _player.Health = Math.Min(_player.MaxHealth, _player.Health + heal);
                    AddMessage($"💖 {weapon.Icon} You DRAIN their life force! {damage} damage, +{heal} HP! They're sharing! 💖");
                    ShowWeaponEffect(targetX, targetY, Brushes.Pink);
                    CheckEnemyDeath(targetEnemy);
                }
                else
                {
                    AddMessage("✨ You try to steal life from the emptiness! It has none to give! ✨");
                }
                break;

            case WeaponAbility.Stun:
                if (targetEnemy != null)
                {
                    targetEnemy.StunTurnsRemaining = weapon.AbilityPower;
                    int damage = _player.Attack - targetEnemy.Defense;
                    damage = Math.Max(damage, 1);
                    targetEnemy.Health -= damage;
                    AddMessage($"⭐ {weapon.Icon} {targetEnemy.Name} is STUNNED for {weapon.AbilityPower} turns! They're frozen! Just like you! ⭐");
                    ShowWeaponEffect(targetX, targetY, Brushes.Yellow);
                    CheckEnemyDeath(targetEnemy);
                }
                else
                {
                    AddMessage("✨ You stun the empty space! It's very still now! ✨");
                }
                break;

            case WeaponAbility.Bleed:
                if (targetEnemy != null)
                {
                    targetEnemy.BleedDamage = weapon.AbilityPower;
                    targetEnemy.BleedTurnsRemaining = weapon.AbilityPower;
                    int damage = _player.Attack - targetEnemy.Defense;
                    damage = Math.Max(damage, 1);
                    targetEnemy.Health -= damage;
                    AddMessage($"🩸 {weapon.Icon} {targetEnemy.Name} is BLEEDING! {weapon.AbilityPower} damage per turn! They're leaking happiness! 🩸");
                    ShowWeaponEffect(targetX, targetY, Brushes.DarkRed);
                    CheckEnemyDeath(targetEnemy);
                }
                else
                {
                    AddMessage("✨ You try to make nothing bleed! Nothing happens! PERFECT! ✨");
                }
                break;

            case WeaponAbility.AreaDamage:
                {
                    int hitCount = 0;
                    foreach (var enemy in visibleEnemies)
                    {
                        // Hit all enemies within range
                        int distX = Math.Abs(enemy.X - _player.X);
                        int distY = Math.Abs(enemy.Y - _player.Y);
                        if (distX <= 2 && distY <= 2)
                        {
                            int damage = weapon.AbilityPower;
                            enemy.Health -= damage;
                            ShowWeaponEffect(enemy.X, enemy.Y, Brushes.Orange);
                            hitCount++;
                            CheckEnemyDeath(enemy);
                        }
                    }
                    if (hitCount > 0)
                    {
                        AddMessage($"💥 {weapon.Icon} AREA ATTACK! Hit {hitCount} enemies for {weapon.AbilityPower} damage each! EVERYONE SHARES! 💥");
                    }
                    else
                    {
                        AddMessage("✨ Your power explodes outward! Nothing is there to receive it! ✨");
                    }
                }
                break;

            case WeaponAbility.Knockback:
                if (targetEnemy != null)
                {
                    int damage = _player.Attack - targetEnemy.Defense;
                    damage = Math.Max(damage, 1);
                    targetEnemy.Health -= damage;

                    // Push enemy back
                    int pushX = targetEnemy.X + dx * weapon.AbilityPower;
                    int pushY = targetEnemy.Y + dy * weapon.AbilityPower;

                    // Keep pushing until we hit a wall or go far enough
                    for (int i = 1; i <= weapon.AbilityPower; i++)
                    {
                        int newX = targetEnemy.X + dx * i;
                        int newY = targetEnemy.Y + dy * i;
                        if (CanMoveTo(newX, newY) && !_enemies.Any(e => e != targetEnemy && e.X == newX && e.Y == newY))
                        {
                            targetEnemy.X = newX;
                            targetEnemy.Y = newY;
                        }
                        else
                        {
                            break;
                        }
                    }

                    AddMessage($"🌪️ {weapon.Icon} {targetEnemy.Name} is LAUNCHED backward! They're flying! So graceful! 🌪️");
                    ShowWeaponEffect(targetEnemy.X, targetEnemy.Y, Brushes.Cyan);
                    CheckEnemyDeath(targetEnemy);
                }
                else
                {
                    AddMessage("✨ You push against emptiness! It yields! ✨");
                }
                break;
        }

        // Set cooldown
        weapon.SpecialCooldown = weapon.SpecialMaxCooldown;

        // Process enemy turns
        ProcessEnemyTurns();
        UpdateVisibility();
        Render();
        CheckGameState();
    }

    private void CheckEnemyDeath(Enemy enemy)
    {
        if (enemy.Health <= 0)
        {
            _enemies.Remove(enemy);
            int xpGain = enemy.XPValue;
            _player.XP += xpGain;
            int goldDrop = _rng.Next(1, 10) * _currentFloor;
            _player.Gold += goldDrop;
            AddMessage(GetCreepyDeathMessage(enemy, xpGain, goldDrop));
            CheckLevelUp();
        }
    }

    private void ShowWeaponEffect(int x, int y, SolidColorBrush color)
    {
        // Visual effect will be drawn on next render
        // For now we'll just add a temporary visual element
        var effect = new System.Windows.Shapes.Ellipse
        {
            Width = 50,
            Height = 50,
            Fill = color,
            Opacity = 0.7
        };

        // This is a simplified effect - in render we'll show it properly at the 3D position
        _weaponEffects.Add((x, y, color, DateTime.Now.AddMilliseconds(500)));
    }

    private void ProcessEnemyTurns()
    {
        // Reduce weapon cooldown
        if (_player.EquippedWeapon != null && _player.EquippedWeapon.SpecialCooldown > 0)
        {
            _player.EquippedWeapon.SpecialCooldown--;
            if (_player.EquippedWeapon.SpecialCooldown == 0)
            {
                AddMessage($"✨ {_player.EquippedWeapon.Name} is READY! Press F to use special! SO EXCITING! ✨");
            }
        }

        foreach (var enemy in _enemies.ToList())
        {
            // Process status effects
            if (enemy.BleedTurnsRemaining > 0)
            {
                enemy.Health -= enemy.BleedDamage;
                enemy.BleedTurnsRemaining--;
                AddMessage($"🩸 {enemy.Name} bleeds for {enemy.BleedDamage} damage! The floor is HAPPY! 🩸");
                if (enemy.Health <= 0)
                {
                    CheckEnemyDeath(enemy);
                    continue;
                }
            }

            if (enemy.StunTurnsRemaining > 0)
            {
                enemy.StunTurnsRemaining--;
                AddMessage($"⭐ {enemy.Name} is stunned! They stand SO still! Like a statue! ⭐");
                continue; // Skip turn
            }

            // Enhanced AI: Use A* pathfinding if visible, otherwise wander
            if (!_visible[enemy.X, enemy.Y])
            {
                // Random wander when not visible
                if (_rng.Next(100) < 30) // 30% chance to move
                {
                    int wanderDir = _rng.Next(4);
                    int wanderX = enemy.X + (wanderDir == 0 ? 1 : wanderDir == 1 ? -1 : 0);
                    int wanderY = enemy.Y + (wanderDir == 2 ? 1 : wanderDir == 3 ? -1 : 0);
                    if (CanMoveTo(wanderX, wanderY) && !_enemies.Any(e => e != enemy && e.X == wanderX && e.Y == wanderY))
                    {
                        enemy.X = wanderX;
                        enemy.Y = wanderY;
                    }
                }
                continue;
            }

            // Check if adjacent to player - attack!
            if (Math.Abs(_player.X - enemy.X) <= 1 && Math.Abs(_player.Y - enemy.Y) <= 1 &&
                (_player.X != enemy.X || _player.Y != enemy.Y))
            {
                int damage = Math.Max(1, enemy.Attack - _player.Defense + _rng.Next(-2, 3));
                _player.Health -= damage;
                AddMessage(GetCreepyEnemyAttackMessage(enemy, damage));
                CreateParticles(enemy.X, enemy.Y, Brushes.Red, 5);
            }
            else
            {
                // Use A* pathfinding for smarter movement
                var path = FindPathAStar(enemy.X, enemy.Y, _player.X, _player.Y);
                if (path != null && path.Count > 1)
                {
                    var nextStep = path[1]; // path[0] is current position
                    if (CanMoveTo(nextStep.X, nextStep.Y) && !_enemies.Any(e => e != enemy && e.X == nextStep.X && e.Y == nextStep.Y))
                    {
                        enemy.X = nextStep.X;
                        enemy.Y = nextStep.Y;
                    }
                }
                else
                {
                    // Fallback to simple movement if pathfinding fails
                    int dx = Math.Sign(_player.X - enemy.X);
                    int dy = Math.Sign(_player.Y - enemy.Y);

                    if (dx != 0 && CanMoveTo(enemy.X + dx, enemy.Y) && !_enemies.Any(e => e != enemy && e.X == enemy.X + dx && e.Y == enemy.Y))
                    {
                        enemy.X += dx;
                    }
                    else if (dy != 0 && CanMoveTo(enemy.X, enemy.Y + dy) && !_enemies.Any(e => e != enemy && e.X == enemy.X && e.Y == enemy.Y + dy))
                    {
                        enemy.Y += dy;
                    }
                }
            }
        }
    }

    private bool CanMoveTo(int x, int y)
    {
        if (x < 0 || x >= MapWidth || y < 0 || y >= MapHeight)
            return false;
        if (_map[x, y] == Tile.Wall)
            return false;
        if (x == _player.X && y == _player.Y)
            return false;
        return true;
    }

    private void PickupItem(Item item)
    {
        _items.Remove(item);
        PlaySound("pickup");
        CreateParticles(item.X, item.Y, Brushes.Gold, 8);

        switch (item.Type)
        {
            case ItemType.Gold:
                int gold = _rng.Next(5, 20) * _currentFloor;
                _player.Gold += gold;
                AddMessage(GetCreepyGoldMessage(gold));
                break;

            case ItemType.HealthPotion:
                var existingPotion = _inventory.FirstOrDefault(i => i.Type == ItemType.HealthPotion);
                if (existingPotion != null)
                {
                    existingPotion.Quantity++;
                }
                else
                {
                    _inventory.Add(new InventoryItem { Icon = "🧃", Name = "Forbidden Juice", Type = ItemType.HealthPotion, Quantity = 1 });
                }
                AddMessage(GetCreepyPotionPickupMessage());
                break;

            case ItemType.Weapon:
                var weapon = GetWeaponForFloor();

                // Unequip old weapon if any
                if (_player.EquippedWeapon != null)
                {
                    _player.Attack -= _player.EquippedWeapon.AttackBonus;
                }

                // Equip new weapon
                _player.EquippedWeapon = weapon;
                _player.Attack += weapon.AttackBonus;

                AddMessage($"⚔️ You found {weapon.Name}! {weapon.Description} ATK +{weapon.AttackBonus} YAY! ⚔️");
                AddMessage($"✨ {weapon.Icon} SPECIAL ABILITY: {weapon.AbilityDescription} (Press F to use!) ✨");
                break;

            case ItemType.Armor:
                var (armorName, armorIcon, defBonus, armorMsg) = GetArmorForFloor();
                _player.Defense += defBonus;
                AddMessage($"🛡️ You found {armorName}! {armorMsg} DEF +{defBonus} SAFE! 🛡️");
                break;

            case ItemType.Key:
                _hasKey = true;
                _keyPosition = null;
                _inventory.Add(new InventoryItem { Icon = "K", Name = "Rusty Key", Type = ItemType.Key, Quantity = 1 });
                string[] keyMessages = {
                    "🗝️ A KEY! It's warm! Body temperature! The door has been WAITING! 🗝️",
                    "✨ The key CHOSE you! (How did it know your hand size?) SO SPECIAL! ✨",
                    "🌈 You found a KEY! It fits your pocket PERFECTLY! (Too perfectly) 🌈",
                    "🎈 Key acquired! Something locked NEEDS you! It's been so lonely! 🎈",
                    "💖 The key PULSES! Like a heartbeat! It knows you're the one! 💖"
                };
                AddMessage(keyMessages[_rng.Next(keyMessages.Length)]);
                break;
        }

        UpdateInventoryUI();
    }

    private void TriggerTrap(Trap trap)
    {
        trap.Triggered = true;
        _player.Health -= trap.Damage;

        string trapMessage = trap.Type switch
        {
            TrapType.Spike => "🎈 The floor wanted a HUG! It has so many arms! All pointy! FUN! 🎈",
            TrapType.Poison => "✨ Something KISSED you! It tastes like old medicine and regret! YUM! ✨",
            TrapType.Fire => "🌈 The floor loves you! It's HOT! Burning hot! That means EXTRA love! 🌈",
            TrapType.Hug => "💖 An INVISIBLE FRIEND hugs you! They won't let go! NEVER EVER! 💖",
            TrapType.Lullaby => "☺ A lullaby from your crib! (You don't remember having a crib here) Sleep! ☺",
            _ => "🎉 Something touched you! You can still feel it! WONDERFUL! 🎉"
        };

        AddMessage($"{trapMessage} (-{trap.Damage} HP)");
    }

    private void UseItem(int slot)
    {
        if (slot >= _inventory.Count)
            return;

        var item = _inventory[slot];

        switch (item.Type)
        {
            case ItemType.HealthPotion:
                if (_player.Health >= _player.MaxHealth)
                {
                    AddMessage("💖 You're FULL of HEALTH! The juice understands! It's HAPPY! 💖");
                    return;
                }
                int heal = 30 + _player.Level * 5;
                _player.Health = Math.Min(_player.MaxHealth, _player.Health + heal);
                AddMessage(GetCreepyHealMessage(heal));
                item.Quantity--;
                if (item.Quantity <= 0)
                    _inventory.Remove(item);
                break;
        }

        UpdateInventoryUI();
    }

    private void CheckLevelUp()
    {
        int xpNeeded = _player.Level * 100;
        while (_player.XP >= xpNeeded)
        {
            _player.XP -= xpNeeded;
            _player.Level++;
            _player.MaxHealth += 10 + _playerSkills.GetMaxHPBonus();
            _player.Health = _player.MaxHealth;
            _player.Attack += 2;
            _player.Defense += 1;
            _playerSkills.SkillPoints += 2; // Gain 2 skill points per level
            AddMessage(GetCreepyLevelUpMessage());
            AddMessage($"⭐ You gained 2 SKILL POINTS! Press K to unlock SPECIAL POWERS! ⭐");
            CreateParticles(_player.X, _player.Y, Brushes.Gold, 30);
            xpNeeded = _player.Level * 100;
        }
    }

    private void DescendStairs()
    {
        _currentFloor++;
        
        if (_currentFloor > MaxFloors)
        {
            _victory = true;
            VictoryStats.Text = $"You reached the bottom.\nThere's nothing here.\nThere never was.\n\nGold: {_player.Gold} | Level: {_player.Level}";
            VictoryOverlay.Visibility = Visibility.Visible;
            return;
        }

        AddMessage(GetCreepyFloorMessage());
        GenerateFloor();
        UpdateUI();
    }

    private void CheckGameState()
    {
        if (_player.Health <= 0)
        {
            _gameOver = true;
            GameOverStats.Text = $"☺ You're RESTING now! Forever! You can't leave! ☺\nThe Fun Zone will keep your body! We'll be SUCH good friends!\nFun Level {_currentFloor} | Smile Points: {_player.Gold} | Level: {_player.Level}";
            GameOverOverlay.Visibility = Visibility.Visible;
        }
    }

    private void HandleInteraction()
    {
        var tile = _map[_player.X, _player.Y];
        
        switch (tile)
        {
            case Tile.Stairs:
                DescendStairs();
                break;
            case Tile.Elevator:
                UseElevator();
                break;
            case Tile.Teleporter:
                var tele = _teleporters.FirstOrDefault(t => t.X == _player.X && t.Y == _player.Y);
                if (tele != default)
                    UseTeleporter(tele);
                break;
            case Tile.LockedDoor:
                TryUnlockDoor();
                break;
            case Tile.SecretWall:
                AddMessage("✨ The wall tells you HAPPY SECRETS! It loves your touch! ✨");
                break;
            default:
                AddMessage("☺ Nothing here right now! But that's OKAY! Keep looking! ☺");
                break;
        }
        
        UpdateUI();
        Render();
    }

    private void UseElevator()
    {
        // Elevator can go up or down multiple floors
        string[] options = { "Going up...", "Going down...", "The elevator remembers you." };
        int floorsToMove = _rng.Next(1, 4); // 1-3 floors
        bool goingUp = _currentFloor > floorsToMove && _rng.Next(2) == 0;
        
        if (goingUp)
        {
            _currentFloor = Math.Max(1, _currentFloor - floorsToMove);
            AddMessage($"🎈 The Party Lift takes you UP {floorsToMove} level(s)! WHEEE! 🎈");
        }
        else
        {
            int newFloor = _currentFloor + floorsToMove;
            if (newFloor > MaxFloors)
            {
                _victory = true;
                VictoryStats.Text = $"☺ The Party Lift took you BEYOND!\nYou're in the place where happy things go!\n(You can hear them all smiling)\n\nSmile Points: {_player.Gold} | Level: {_player.Level} ☺";
                VictoryOverlay.Visibility = Visibility.Visible;
                return;
            }
            _currentFloor = newFloor;
            AddMessage($"✨ The Party Lift takes you DOWN {floorsToMove} level(s)! FUN FUN FUN! ✨");
        }
        
        _hasKey = false;
        GenerateFloor();
        AddMessage(GetCreepyFloorMessage());
        UpdateUI();
    }

    private void UseTeleporter((int X, int Y) currentTeleporter)
    {
        var otherTeleporter = _teleporters.FirstOrDefault(t => t != currentTeleporter);
        if (otherTeleporter != default)
        {
            _player.X = otherTeleporter.X;
            _player.Y = otherTeleporter.Y;
            
            string[] messages = {
                "🌈 WHEEE! You're somewhere ELSE now! How EXCITING! 🌈",
                "✨ The Happy Jump HUGS you and sends you FLYING! ✨",
                "🎈 ZZZIP! You're in a NEW place! The Jump says HI! 🎈",
                "💖 Your atoms dance HAPPILY and rearrange! FUN! 💖",
                "☺ The journey was SO FAST! But it felt WONDERFUL! ☺"
            };
            AddMessage(messages[_rng.Next(messages.Length)]);
            
            // Small disorientation damage
            if (_rng.Next(100) < 15)
            {
                int damage = _rng.Next(1, 5);
                _player.Health -= damage;
                AddMessage($"🎈 Tiny Happy Dizzy! It's OKAY! (-{damage} HP) You're FINE! 🎈");
                CheckGameState();
            }
            
            UpdateVisibility();
            Render();
        }
    }

    private void TryUnlockDoor()
    {
        if (_hasKey)
        {
            _map[_player.X, _player.Y] = Tile.Floor;
            _lockedDoorPosition = null;
            _hasKey = false;
            
            // Remove key from inventory
            var key = _inventory.FirstOrDefault(i => i.Type == ItemType.Key);
            if (key != null) _inventory.Remove(key);
            
            string[] messages = {
                "🎈 The door LOVES your offering! It opens HAPPILY! 🎈",
                "✨ Click! The lock is SO GLAD to help! It's SMILING! ✨",
                "🌈 The door swings open! FRIENDS were waiting inside! 🌈",
                "💖 Unlocked! The door says 'YAY FINALLY!' SO EXCITED! 💖"
            };
            AddMessage(messages[_rng.Next(messages.Length)]);
            UpdateInventoryUI();
        }
        else
        {
            string[] messages = {
                "☺ The door wants to play! But you need the key FIRST! ☺",
                "🎈 Locked! The door is WAITING for the key! Find it! 🎈",
                "✨ This door NEEDS a key! Keys are REAL and FUN! Find one! ✨",
                "🌈 The lock looks at you HOPEFULLY! Get the key! It'll WAIT! 🌈"
            };
            AddMessage(messages[_rng.Next(messages.Length)]);
        }
    }

    private bool IsInSafeRoom()
    {
        return _map[_player.X, _player.Y] == Tile.SafeRoom;
    }
    #endregion

    #region Visibility & Rendering
    private void UpdateVisibility()
    {
        // Reset visibility
        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                _visible[x, y] = false;
            }
        }

        // Simple shadowcasting for circular vision
        for (int angle = 0; angle < 360; angle += 2)
        {
            double rad = angle * Math.PI / 180;
            double dx = Math.Cos(rad);
            double dy = Math.Sin(rad);

            double x = _player.X + 0.5;
            double y = _player.Y + 0.5;

            for (int i = 0; i <= VisionRadius; i++)
            {
                int tileX = (int)x;
                int tileY = (int)y;

                if (tileX < 0 || tileX >= MapWidth || tileY < 0 || tileY >= MapHeight)
                    break;

                _visible[tileX, tileY] = true;
                _explored[tileX, tileY] = true;

                if (_map[tileX, tileY] == Tile.Wall)
                    break;

                x += dx;
                y += dy;
            }
        }
    }

    private void Render()
    {
        GameCanvas.Children.Clear();
        _entityLabels.Clear();

        // Update particles
        UpdateParticles();

        // Render first-person 3D view
        RenderFirstPersonView();

        // Render mini-map in bottom-left corner
        RenderMiniMap();

        // Render particles on top
        RenderParticles();
    }

    private void RenderFirstPersonView()
    {
        double canvasWidth = GameCanvas.ActualWidth > 0 ? GameCanvas.ActualWidth : 800;
        double canvasHeight = GameCanvas.ActualHeight > 0 ? GameCanvas.ActualHeight : 600;

        // Draw fancy floor with tiles
        DrawFancyFloor(canvasWidth, canvasHeight);

        // Draw fancy ceiling with patterns
        DrawFancyCeiling(canvasWidth, canvasHeight);

        // Render walls at different distances (up to 4 tiles ahead)
        for (int distance = 4; distance >= 1; distance--)
        {
            RenderWallSlice(distance, canvasWidth, canvasHeight);
        }
    }

    private void DrawFancyFloor(double canvasWidth, double canvasHeight)
    {
        // Base floor color based on current floor theme
        Color baseColor = _currentFloor switch
        {
            <= 2 => Color.FromRgb(255, 240, 250),  // Soft pink (nursery)
            <= 4 => Color.FromRgb(245, 220, 200),  // Sandbox tan (playground)
            <= 6 => Color.FromRgb(220, 220, 230),  // Institutional gray (school)
            <= 8 => Color.FromRgb(200, 180, 170),  // Warm wood (home)
            _ => Color.FromRgb(180, 160, 190)      // Dim purple (the end)
        };

        // Draw checkered floor pattern
        double tileSize = 40;
        int tilesX = (int)(canvasWidth / tileSize) + 1;
        int tilesY = (int)((canvasHeight / 2) / tileSize) + 1;

        for (int y = 0; y < tilesY; y++)
        {
            for (int x = 0; x < tilesX; x++)
            {
                // Alternating pattern
                bool isDark = (x + y) % 2 == 0;
                Color tileColor = isDark
                    ? Color.FromRgb((byte)(baseColor.R * 0.9), (byte)(baseColor.G * 0.9), (byte)(baseColor.B * 0.9))
                    : baseColor;

                // Add depth shading (darker toward horizon)
                double depthFactor = 1.0 - (y / (double)tilesY * 0.4);
                tileColor = Color.FromRgb(
                    (byte)(tileColor.R * depthFactor),
                    (byte)(tileColor.G * depthFactor),
                    (byte)(tileColor.B * depthFactor));

                var tile = new Rectangle
                {
                    Width = tileSize,
                    Height = tileSize,
                    Fill = new SolidColorBrush(tileColor),
                    Stroke = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)),
                    StrokeThickness = 1
                };

                Canvas.SetLeft(tile, x * tileSize);
                Canvas.SetTop(tile, canvasHeight / 2 + y * tileSize);
                GameCanvas.Children.Add(tile);
                _entityLabels.Add(tile);
            }
        }
    }

    private void DrawFancyCeiling(double canvasWidth, double canvasHeight)
    {
        // Base ceiling color based on current floor theme
        Color baseColor = _currentFloor switch
        {
            <= 2 => Color.FromRgb(255, 200, 230),  // Baby pink (nursery)
            <= 4 => Color.FromRgb(180, 220, 255),  // Sky blue (playground)
            <= 6 => Color.FromRgb(200, 200, 210),  // Fluorescent (school)
            <= 8 => Color.FromRgb(180, 160, 150),  // Dusty (home)
            _ => Color.FromRgb(100, 80, 120)       // Deep purple (the end)
        };

        // Draw ceiling with panels
        double panelSize = 60;
        int panelsX = (int)(canvasWidth / panelSize) + 1;
        int panelsY = (int)((canvasHeight / 2) / panelSize) + 1;

        for (int y = 0; y < panelsY; y++)
        {
            for (int x = 0; x < panelsX; x++)
            {
                // Add variation
                bool hasPattern = (x + y) % 3 == 0;
                Color panelColor = hasPattern
                    ? Color.FromRgb((byte)(baseColor.R * 0.95), (byte)(baseColor.G * 0.95), (byte)(baseColor.B * 0.95))
                    : baseColor;

                // Darken near horizon
                double depthFactor = 1.0 - ((panelsY - y) / (double)panelsY * 0.3);
                panelColor = Color.FromRgb(
                    (byte)(panelColor.R * depthFactor),
                    (byte)(panelColor.G * depthFactor),
                    (byte)(panelColor.B * depthFactor));

                var panel = new Rectangle
                {
                    Width = panelSize,
                    Height = panelSize,
                    Fill = new SolidColorBrush(panelColor),
                    Stroke = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
                    StrokeThickness = 2
                };

                Canvas.SetLeft(panel, x * panelSize);
                Canvas.SetTop(panel, y * panelSize);
                GameCanvas.Children.Add(panel);
                _entityLabels.Add(panel);
            }
        }

        // Add creepy details based on floor
        if (_currentFloor >= 7)
        {
            // Add occasional "watching eyes" in ceiling
            for (int i = 0; i < 3; i++)
            {
                if (_rng.Next(100) < 30)
                {
                    var eye = new TextBlock
                    {
                        Text = "👁️",
                        FontSize = 20,
                        Foreground = new SolidColorBrush(Color.FromArgb(100, 255, 100, 100)),
                        FontFamily = new FontFamily("Segoe UI Emoji")
                    };
                    Canvas.SetLeft(eye, _rng.Next((int)canvasWidth - 30));
                    Canvas.SetTop(eye, _rng.Next((int)(canvasHeight / 2) - 30));
                    GameCanvas.Children.Add(eye);
                    _entityLabels.Add(eye);
                }
            }
        }
    }

    private void RenderWallSlice(int distance, double canvasWidth, double canvasHeight)
    {
        var (dx, dy) = GetDirectionDelta(_player.Facing);

        // Calculate positions to check
        int frontX = _player.X + dx * distance;
        int frontY = _player.Y + dy * distance;

        // Get perpendicular directions for left and right
        var (leftDx, leftDy) = GetDirectionDelta(TurnLeft(_player.Facing));
        var (rightDx, rightDy) = GetDirectionDelta(TurnRight(_player.Facing));

        int leftX = frontX + leftDx;
        int leftY = frontY + leftDy;
        int rightX = frontX + rightDx;
        int rightY = frontY + rightDy;

        // Calculate perspective scale (closer = bigger)
        double scale = 1.0 / distance;
        double wallHeight = canvasHeight * scale * 0.8;
        double wallTop = (canvasHeight - wallHeight) / 2;

        // Wall segment widths
        double sideWallWidth = canvasWidth * scale * 0.25;
        double centerWallWidth = canvasWidth * scale * 0.5;

        // Draw left wall
        if (IsWall(leftX, leftY))
        {
            DrawWall3D(0, wallTop, sideWallWidth, wallHeight, distance, true);
        }

        // Draw center wall (straight ahead)
        if (IsWall(frontX, frontY))
        {
            double centerX = (canvasWidth - centerWallWidth) / 2;
            DrawWall3D(centerX, wallTop, centerWallWidth, wallHeight, distance, false);

            // Check for door/stairs/special tiles
            DrawSpecialTile(frontX, frontY, centerX, wallTop, centerWallWidth, wallHeight);
        }
        else
        {
            // Draw corridor/opening
            // Check for entities in this tile
            DrawEntitiesAt(frontX, frontY, distance, canvasWidth, canvasHeight);
        }

        // Draw right wall
        if (IsWall(rightX, rightY))
        {
            DrawWall3D(canvasWidth - sideWallWidth, wallTop, sideWallWidth, wallHeight, distance, true);
        }
    }

    private void DrawWall3D(double x, double y, double width, double height, int distance, bool isSide)
    {
        // Darken walls based on distance
        byte brightness = (byte)(220 - distance * 40);
        brightness = Math.Max(brightness, (byte)60);

        // Themed wall colors based on floor
        Color baseWallColor = _currentFloor switch
        {
            <= 2 => Color.FromRgb(255, 200, 230),  // Pastel pink (nursery)
            <= 4 => Color.FromRgb(200, 220, 180),  // Faded green (playground)
            <= 6 => Color.FromRgb(200, 200, 220),  // Institutional blue-gray (school)
            <= 8 => Color.FromRgb(210, 180, 160),  // Warm beige (home)
            _ => Color.FromRgb(150, 130, 170)      // Purple-gray (the end)
        };

        // Apply brightness and side darkening
        double sideFactor = isSide ? 0.75 : 1.0;
        double distanceFactor = brightness / 220.0;

        Color wallColor = Color.FromRgb(
            (byte)(baseWallColor.R * distanceFactor * sideFactor),
            (byte)(baseWallColor.G * distanceFactor * sideFactor),
            (byte)(baseWallColor.B * distanceFactor * sideFactor));

        // Draw main wall
        var wall = new Rectangle
        {
            Width = width,
            Height = height,
            Fill = new SolidColorBrush(wallColor),
            Stroke = new SolidColorBrush(Color.FromRgb(20, 20, 30)),
            StrokeThickness = 2
        };

        Canvas.SetLeft(wall, x);
        Canvas.SetTop(wall, y);
        GameCanvas.Children.Add(wall);
        _entityLabels.Add(wall);

        // Add wall decorations if close enough and not a side wall
        if (distance <= 2 && !isSide && width > 100)
        {
            AddWallDecorations(x, y, width, height, distance);
        }
    }

    private void AddWallDecorations(double x, double y, double width, double height, int distance)
    {
        // Add themed decorations based on floor
        if (_currentFloor <= 2) // Nursery
        {
            // Add stars or moon stickers
            if (_rng.Next(100) < 40)
            {
                var decoration = new TextBlock
                {
                    Text = _rng.Next(2) == 0 ? "⭐" : "🌙",
                    FontSize = height * 0.15,
                    Foreground = new SolidColorBrush(Color.FromArgb(150, 255, 255, 150)),
                    FontFamily = new FontFamily("Segoe UI Emoji")
                };
                Canvas.SetLeft(decoration, x + width * 0.7);
                Canvas.SetTop(decoration, y + height * 0.3);
                GameCanvas.Children.Add(decoration);
                _entityLabels.Add(decoration);
            }
        }
        else if (_currentFloor <= 4) // Playground
        {
            // Add handprints or chalk marks
            if (_rng.Next(100) < 30)
            {
                var decoration = new TextBlock
                {
                    Text = "👋",
                    FontSize = height * 0.12,
                    Foreground = new SolidColorBrush(Color.FromArgb(120, 200, 150, 100)),
                    FontFamily = new FontFamily("Segoe UI Emoji")
                };
                Canvas.SetLeft(decoration, x + width * 0.6);
                Canvas.SetTop(decoration, y + height * 0.5);
                GameCanvas.Children.Add(decoration);
                _entityLabels.Add(decoration);
            }
        }
        else if (_currentFloor <= 6) // School
        {
            // Add posters or notices
            if (_rng.Next(100) < 35)
            {
                var poster = new Rectangle
                {
                    Width = width * 0.2,
                    Height = height * 0.25,
                    Fill = new SolidColorBrush(Color.FromArgb(150, 240, 240, 200)),
                    Stroke = new SolidColorBrush(Color.FromArgb(100, 100, 100, 100)),
                    StrokeThickness = 1
                };
                Canvas.SetLeft(poster, x + width * 0.4);
                Canvas.SetTop(poster, y + height * 0.3);
                GameCanvas.Children.Add(poster);
                _entityLabels.Add(poster);

                var text = new TextBlock
                {
                    Text = "☺",
                    FontSize = height * 0.15,
                    Foreground = Brushes.Black,
                    FontFamily = new FontFamily("Segoe UI Emoji")
                };
                Canvas.SetLeft(text, x + width * 0.45);
                Canvas.SetTop(text, y + height * 0.35);
                GameCanvas.Children.Add(text);
                _entityLabels.Add(text);
            }
        }
        else if (_currentFloor <= 8) // Home
        {
            // Add picture frames
            if (_rng.Next(100) < 40)
            {
                var frame = new Rectangle
                {
                    Width = width * 0.25,
                    Height = height * 0.2,
                    Fill = new SolidColorBrush(Color.FromArgb(150, 180, 150, 120)),
                    Stroke = new SolidColorBrush(Color.FromArgb(150, 100, 80, 60)),
                    StrokeThickness = 3
                };
                Canvas.SetLeft(frame, x + width * 0.4);
                Canvas.SetTop(frame, y + height * 0.25);
                GameCanvas.Children.Add(frame);
                _entityLabels.Add(frame);
            }
        }
        else // The End
        {
            // Add creepy eyes watching
            if (_rng.Next(100) < 25)
            {
                var eye = new TextBlock
                {
                    Text = "👁️",
                    FontSize = height * 0.1,
                    Foreground = new SolidColorBrush(Color.FromArgb(120, 255, 50, 50)),
                    FontFamily = new FontFamily("Segoe UI Emoji")
                };
                Canvas.SetLeft(eye, x + width * (_rng.NextDouble() * 0.6 + 0.2));
                Canvas.SetTop(eye, y + height * (_rng.NextDouble() * 0.6 + 0.2));
                GameCanvas.Children.Add(eye);
                _entityLabels.Add(eye);
            }
        }

        // Add wall panels/trim
        if (distance == 1)
        {
            // Vertical trim lines
            for (int i = 1; i < 3; i++)
            {
                var trim = new System.Windows.Shapes.Line
                {
                    X1 = x + (width * i / 3.0),
                    Y1 = y,
                    X2 = x + (width * i / 3.0),
                    Y2 = y + height,
                    Stroke = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)),
                    StrokeThickness = 2
                };
                GameCanvas.Children.Add(trim);
                _entityLabels.Add(trim);
            }

            // Horizontal baseboard
            var baseboard = new Rectangle
            {
                Width = width,
                Height = height * 0.1,
                Fill = new SolidColorBrush(Color.FromArgb(80, 100, 80, 60))
            };
            Canvas.SetLeft(baseboard, x);
            Canvas.SetTop(baseboard, y + height * 0.9);
            GameCanvas.Children.Add(baseboard);
            _entityLabels.Add(baseboard);
        }
    }

    private void DrawSpecialTile(int tileX, int tileY, double x, double y, double width, double height)
    {
        string icon = "";
        Brush color = WhiteBrush;

        if (tileX == _stairsPosition.X && tileY == _stairsPosition.Y)
        {
            icon = "🟣";
            color = StairsBrush;
        }
        else if (_elevatorPosition.HasValue && tileX == _elevatorPosition.Value.X && tileY == _elevatorPosition.Value.Y)
        {
            icon = "⬆️";
            color = new SolidColorBrush(Color.FromRgb(100, 200, 255));
        }
        else if (_lockedDoorPosition.HasValue && tileX == _lockedDoorPosition.Value.X && tileY == _lockedDoorPosition.Value.Y)
        {
            icon = _hasKey ? "🚪" : "🔒";
            color = YellowBrush;
        }

        if (!string.IsNullOrEmpty(icon))
        {
            var label = new TextBlock
            {
                Text = icon,
                FontSize = height * 0.4,
                Foreground = color,
                FontFamily = new FontFamily("Segoe UI Emoji")
            };
            Canvas.SetLeft(label, x + width / 2 - 20);
            Canvas.SetTop(label, y + height / 2 - 20);
            GameCanvas.Children.Add(label);
            _entityLabels.Add(label);
        }
    }

    private void DrawEntitiesAt(int tileX, int tileY, int distance, double canvasWidth, double canvasHeight)
    {
        // Check for enemies
        var enemy = _enemies.FirstOrDefault(e => e.X == tileX && e.Y == tileY);
        if (enemy != null && _visible[tileX, tileY])
        {
            double scale = 1.0 / distance;
            double size = canvasHeight * scale * 0.4;
            double centerX = canvasWidth / 2 - size / 2;
            double centerY = canvasHeight / 2 - size / 2;

            // Render pixel art sprite
            var sprite = GetEnemySprite(enemy.Type);
            DrawPixelArtSprite(sprite, centerX, centerY, size);

            // Add enemy name label below sprite
            var label = new TextBlock
            {
                Text = enemy.Name,
                FontSize = size * 0.15,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0))
            };
            Canvas.SetLeft(label, centerX);
            Canvas.SetTop(label, centerY + size + 2);
            GameCanvas.Children.Add(label);
            _entityLabels.Add(label);
        }

        // Check for items
        var item = _items.FirstOrDefault(i => i.X == tileX && i.Y == tileY);
        if (item != null && _visible[tileX, tileY])
        {
            double scale = 1.0 / distance;
            double size = canvasHeight * scale * 0.2;

            var label = new TextBlock
            {
                Text = item.Type == ItemType.Gold ? "💰" : "❤️",
                FontSize = size,
                Foreground = item.Type == ItemType.Gold ? GoldBrush : HealthPotionBrush,
                FontFamily = new FontFamily("Segoe UI Emoji")
            };
            Canvas.SetLeft(label, canvasWidth / 2 - 15);
            Canvas.SetTop(label, canvasHeight - size * 1.5);
            GameCanvas.Children.Add(label);
            _entityLabels.Add(label);
        }
    }

    private bool IsWall(int x, int y)
    {
        if (x < 0 || x >= MapWidth || y < 0 || y >= MapHeight)
            return true;

        return _map[x, y] == Tile.Wall || _map[x, y] == Tile.SecretWall;
    }

    private void DrawPixelArtSprite(SolidColorBrush[,] sprite, double x, double y, double size)
    {
        int spriteHeight = sprite.GetLength(0);
        int spriteWidth = sprite.GetLength(1);
        double pixelSize = size / spriteHeight;

        for (int row = 0; row < spriteHeight; row++)
        {
            for (int col = 0; col < spriteWidth; col++)
            {
                var brush = sprite[row, col];
                if (brush == TransparentBrush) continue;

                var pixel = new Rectangle
                {
                    Width = pixelSize,
                    Height = pixelSize,
                    Fill = brush
                };

                Canvas.SetLeft(pixel, x + col * pixelSize);
                Canvas.SetTop(pixel, y + row * pixelSize);
                GameCanvas.Children.Add(pixel);
                _entityLabels.Add(pixel);
            }
        }
    }

    private void RenderMiniMap()
    {
        // Draw mini-map in bottom-left corner
        const int miniTileSize = 4;
        const int miniMapSize = 15; // Show 15x15 tiles
        const int offsetX = 10;
        const int offsetY = 10;

        double canvasHeight = GameCanvas.ActualHeight > 0 ? GameCanvas.ActualHeight : 600;
        int startY = (int)(canvasHeight - miniMapSize * miniTileSize - offsetY);

        for (int dy = -miniMapSize/2; dy <= miniMapSize/2; dy++)
        {
            for (int dx = -miniMapSize/2; dx <= miniMapSize/2; dx++)
            {
                int x = _player.X + dx;
                int y = _player.Y + dy;

                if (x < 0 || x >= MapWidth || y < 0 || y >= MapHeight)
                    continue;

                if (!_explored[x, y])
                    continue;

                var miniTile = new Rectangle
                {
                    Width = miniTileSize,
                    Height = miniTileSize,
                    Fill = _map[x, y] == Tile.Wall
                        ? new SolidColorBrush(Color.FromArgb(150, 100, 100, 120))
                        : new SolidColorBrush(Color.FromArgb(100, 60, 60, 80)),
                    Stroke = _visible[x, y]
                        ? new SolidColorBrush(Color.FromArgb(200, 150, 150, 180))
                        : new SolidColorBrush(Color.FromArgb(50, 80, 80, 100)),
                    StrokeThickness = 0.5
                };

                Canvas.SetLeft(miniTile, offsetX + (dx + miniMapSize/2) * miniTileSize);
                Canvas.SetTop(miniTile, startY + (dy + miniMapSize/2) * miniTileSize);
                GameCanvas.Children.Add(miniTile);
                _entityLabels.Add(miniTile);
            }
        }

        // Draw player on mini-map
        var playerMarker = new Ellipse
        {
            Width = miniTileSize * 1.5,
            Height = miniTileSize * 1.5,
            Fill = PlayerBrush
        };
        Canvas.SetLeft(playerMarker, offsetX + (miniMapSize/2) * miniTileSize - miniTileSize * 0.25);
        Canvas.SetTop(playerMarker, startY + (miniMapSize/2) * miniTileSize - miniTileSize * 0.25);
        GameCanvas.Children.Add(playerMarker);
        _entityLabels.Add(playerMarker);

        // Draw direction indicator
        var (dirX, dirY) = GetDirectionDelta(_player.Facing);
        var dirLine = new System.Windows.Shapes.Line
        {
            X1 = offsetX + (miniMapSize/2) * miniTileSize + miniTileSize / 2,
            Y1 = startY + (miniMapSize/2) * miniTileSize + miniTileSize / 2,
            X2 = offsetX + (miniMapSize/2 + dirX) * miniTileSize + miniTileSize / 2,
            Y2 = startY + (miniMapSize/2 + dirY) * miniTileSize + miniTileSize / 2,
            Stroke = PlayerBrush,
            StrokeThickness = 2
        };
        GameCanvas.Children.Add(dirLine);
        _entityLabels.Add(dirLine);
    }

    private void DrawSprite(int tileX, int tileY, SolidColorBrush[,] sprite)
    {
        int spriteSize = sprite.GetLength(0);
        int pixelSize = TileSize / spriteSize;
        
        for (int py = 0; py < spriteSize; py++)
        {
            for (int px = 0; px < spriteSize; px++)
            {
                var brush = sprite[py, px];
                if (brush == TransparentBrush || brush == null) continue;
                
                var pixel = new Rectangle
                {
                    Width = pixelSize,
                    Height = pixelSize,
                    Fill = brush
                };
                Canvas.SetLeft(pixel, tileX * TileSize + px * pixelSize);
                Canvas.SetTop(pixel, tileY * TileSize + py * pixelSize);
                GameCanvas.Children.Add(pixel);
                _entityLabels.Add(pixel); // Reuse this list for cleanup
            }
        }
    }

    // Keep this for any text overlays we might need
    private void AddEntityLabel(string text, int x, int y, Brush color)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = TileSize - 2,
            Foreground = color,
            FontFamily = new FontFamily("Segoe UI Emoji")
        };
        Canvas.SetLeft(label, x * TileSize);
        Canvas.SetTop(label, y * TileSize - 2);
        GameCanvas.Children.Add(label);
        _entityLabels.Add(label);
    }

    #region Pixel Sprites (8x8)
    // _ = Transparent, other letters = colors
    // Each sprite is 8x8 pixels
    
    private SolidColorBrush[,] GetPlayerSprite()
    {
        // Little adventurer with green tunic
        var _ = TransparentBrush;
        var S = SkinBrush;
        var H = HairBrush;
        var G = GreenBrush;
        var E = EyeBrush;
        var W = WhiteBrush;
        
        return new SolidColorBrush[,] {
            { _, _, H, H, H, H, _, _ },
            { _, H, H, H, H, H, H, _ },
            { _, H, S, S, S, S, H, _ },
            { _, S, E, S, S, E, S, _ },
            { _, _, S, S, S, S, _, _ },
            { _, G, G, G, G, G, G, _ },
            { _, G, S, G, G, S, G, _ },
            { _, _, S, _, _, S, _, _ },
        };
    }

    private SolidColorBrush[,] GetEnemySprite(EnemyType type)
    {
        var _ = TransparentBrush;
        
        return type switch
        {
            // Nursery enemies
            EnemyType.LostTeddy => GetTeddySprite(),
            EnemyType.CribSpider => GetSpiderSprite(),
            EnemyType.NightLight => GetNightLightSprite(),
            
            // Playground enemies
            EnemyType.SwingChild => GetChildSprite(),
            EnemyType.SandboxThing => GetSandThingSprite(),
            EnemyType.CarouselHorse => GetHorseSprite(),
            
            // School enemies
            EnemyType.SubstituteTeacher => GetTeacherSprite(),
            EnemyType.HallMonitor => GetMonitorSprite(),
            EnemyType.LunchLady => GetLunchLadySprite(),
            
            // Home enemies
            EnemyType.WrongMom => GetMomSprite(),
            EnemyType.AtticDweller => GetAtticSprite(),
            EnemyType.BasementFriend => GetBasementSprite(),
            
            // End enemies
            EnemyType.MirrorYou => GetMirrorSprite(),
            EnemyType.TheHost => GetHostSprite(),
            EnemyType.YourBestFriend => GetBestFriendSprite(),
            
            _ => GetTeddySprite()
        };
    }

    private SolidColorBrush[,] GetTeddySprite()
    {
        var _ = TransparentBrush;
        var B = BrownBrush;
        var D = DarkBrownBrush;
        var E = EyeBrush;
        var P = PinkBrush;
        
        return new SolidColorBrush[,] {
            { _, B, _, _, _, _, B, _ },
            { B, B, B, B, B, B, B, B },
            { B, B, E, B, B, E, B, B },
            { B, B, B, P, P, B, B, B },
            { _, B, B, B, B, B, B, _ },
            { _, _, B, B, B, B, _, _ },
            { _, B, B, _, _, B, B, _ },
            { _, B, _, _, _, _, B, _ },
        };
    }

    private SolidColorBrush[,] GetSpiderSprite()
    {
        var _ = TransparentBrush;
        var B = EyeBrush;
        var R = RedBrush;
        
        return new SolidColorBrush[,] {
            { B, _, _, _, _, _, _, B },
            { _, B, _, B, B, _, B, _ },
            { _, _, B, B, B, B, _, _ },
            { B, B, R, R, R, R, B, B },
            { B, B, R, R, R, R, B, B },
            { _, _, B, B, B, B, _, _ },
            { _, B, _, B, B, _, B, _ },
            { B, _, _, _, _, _, _, B },
        };
    }

    private SolidColorBrush[,] GetNightLightSprite()
    {
        var _ = TransparentBrush;
        var Y = YellowBrush;
        var W = WhiteBrush;
        var G = GrayBrush;
        
        return new SolidColorBrush[,] {
            { _, _, Y, Y, Y, Y, _, _ },
            { _, Y, W, W, W, W, Y, _ },
            { _, Y, W, Y, Y, W, Y, _ },
            { _, Y, W, Y, Y, W, Y, _ },
            { _, Y, W, W, W, W, Y, _ },
            { _, _, Y, Y, Y, Y, _, _ },
            { _, _, _, G, G, _, _, _ },
            { _, _, G, G, G, G, _, _ },
        };
    }

    private SolidColorBrush[,] GetChildSprite()
    {
        var _ = TransparentBrush;
        var S = SkinBrush;
        var H = EyeBrush; // Dark hair
        var E = EyeBrush;
        var B = ClothBrush;
        var W = WhiteBrush;
        
        return new SolidColorBrush[,] {
            { _, _, H, H, H, H, _, _ },
            { _, H, H, H, H, H, H, _ },
            { _, H, S, S, S, S, H, _ },
            { _, S, W, S, S, W, S, _ }, // White eyes, unsettling
            { _, _, S, S, S, S, _, _ },
            { _, B, B, B, B, B, B, _ },
            { _, B, S, B, B, S, B, _ },
            { _, _, S, _, _, S, _, _ },
        };
    }

    private SolidColorBrush[,] GetSandThingSprite()
    {
        var _ = TransparentBrush;
        var Y = YellowBrush;
        var O = OrangeBrush;
        var E = EyeBrush;
        
        return new SolidColorBrush[,] {
            { _, Y, Y, _, _, Y, Y, _ },
            { Y, Y, Y, Y, Y, Y, Y, Y },
            { Y, E, Y, Y, Y, Y, E, Y },
            { Y, Y, Y, Y, Y, Y, Y, Y },
            { O, Y, Y, O, O, Y, Y, O },
            { _, O, Y, Y, Y, Y, O, _ },
            { _, _, O, O, O, O, _, _ },
            { _, _, _, O, O, _, _, _ },
        };
    }

    private SolidColorBrush[,] GetHorseSprite()
    {
        var _ = TransparentBrush;
        var W = WhiteBrush;
        var G = GrayBrush;
        var E = EyeBrush;
        var R = RedBrush;
        var Y = YellowBrush;
        
        return new SolidColorBrush[,] {
            { _, _, W, W, _, _, _, _ },
            { _, W, W, W, W, _, _, _ },
            { _, W, E, W, W, W, W, _ },
            { _, W, W, W, W, W, _, _ },
            { Y, _, W, W, W, _, _, Y },
            { Y, _, G, _, G, _, _, Y },
            { Y, _, G, _, G, _, _, Y },
            { _, _, R, _, R, _, _, _ },
        };
    }

    private SolidColorBrush[,] GetTeacherSprite()
    {
        var _ = TransparentBrush;
        var S = SkinBrush;
        var H = GrayBrush; // Gray hair
        var E = EyeBrush;
        var R = RedBrush;
        var W = WhiteBrush;
        
        return new SolidColorBrush[,] {
            { _, _, H, H, H, H, _, _ },
            { _, H, H, H, H, H, H, _ },
            { _, H, S, S, S, S, H, _ },
            { _, S, E, S, S, E, S, _ },
            { _, _, S, R, R, S, _, _ }, // Red smile
            { _, R, R, R, R, R, R, _ },
            { _, R, S, R, R, S, R, _ },
            { _, _, S, _, _, S, _, _ },
        };
    }

    private SolidColorBrush[,] GetMonitorSprite()
    {
        var _ = TransparentBrush;
        var S = SkinBrush;
        var O = OrangeBrush; // Safety vest
        var E = EyeBrush;
        var W = WhiteBrush;
        
        return new SolidColorBrush[,] {
            { _, _, E, E, E, E, _, _ },
            { _, E, E, E, E, E, E, _ },
            { _, E, S, S, S, S, E, _ },
            { _, S, E, S, S, E, S, _ },
            { _, _, S, S, S, S, _, _ },
            { _, O, O, O, O, O, O, _ },
            { _, O, S, O, O, S, O, _ },
            { _, _, S, _, _, S, _, _ },
        };
    }

    private SolidColorBrush[,] GetLunchLadySprite()
    {
        var _ = TransparentBrush;
        var S = SkinBrush;
        var W = WhiteBrush;
        var E = EyeBrush;
        var G = GrayBrush;
        
        return new SolidColorBrush[,] {
            { _, W, W, W, W, W, W, _ },
            { _, W, W, W, W, W, W, _ },
            { _, W, S, S, S, S, W, _ },
            { _, S, E, S, S, E, S, _ },
            { _, _, S, S, S, S, _, _ },
            { _, W, W, W, W, W, W, _ },
            { _, W, S, G, G, S, W, _ }, // Holding spoon
            { _, _, S, _, _, S, _, _ },
        };
    }

    private SolidColorBrush[,] GetMomSprite()
    {
        var _ = TransparentBrush;
        var S = SkinBrush;
        var H = BrownBrush;
        var E = EyeBrush;
        var P = PinkBrush;
        var W = WhiteBrush;
        
        return new SolidColorBrush[,] {
            { _, H, H, H, H, H, H, _ },
            { H, H, H, H, H, H, H, H },
            { H, H, S, S, S, S, H, H },
            { _, S, W, S, S, W, S, _ }, // Wrong eyes (white)
            { _, _, S, S, S, S, _, _ },
            { _, P, P, P, P, P, P, _ },
            { _, P, S, P, P, S, P, _ },
            { _, _, S, _, _, S, _, _ },
        };
    }

    private SolidColorBrush[,] GetAtticSprite()
    {
        var _ = TransparentBrush;
        var D = DarkGrayBrush;
        var G = GrayBrush;
        var E = EyeBrush;
        var R = RedBrush;
        
        return new SolidColorBrush[,] {
            { _, D, D, D, D, D, D, _ },
            { D, D, D, D, D, D, D, D },
            { D, D, R, D, D, R, D, D }, // Red eyes
            { D, D, D, D, D, D, D, D },
            { _, D, D, D, D, D, D, _ },
            { _, _, D, D, D, D, _, _ },
            { _, _, D, _, _, D, _, _ },
            { _, _, D, _, _, D, _, _ },
        };
    }

    private SolidColorBrush[,] GetBasementSprite()
    {
        var _ = TransparentBrush;
        var D = DarkGrayBrush;
        var E = EyeBrush;
        var W = WhiteBrush;
        
        return new SolidColorBrush[,] {
            { _, _, _, _, _, _, _, _ },
            { _, _, W, W, W, W, _, _ },
            { _, W, E, W, W, E, W, _ }, // Eyes in darkness
            { _, _, W, W, W, W, _, _ },
            { _, _, _, W, W, _, _, _ },
            { _, _, _, _, _, _, _, _ },
            { _, _, _, _, _, _, _, _ },
            { _, _, _, _, _, _, _, _ },
        };
    }

    private SolidColorBrush[,] GetMirrorSprite()
    {
        var _ = TransparentBrush;
        var S = SkinBrush;
        var H = HairBrush;
        var G = GreenBrush;
        var E = EyeBrush;
        var C = CyanBrush; // Mirrored/glitchy
        
        return new SolidColorBrush[,] {
            { _, _, H, C, H, H, _, _ },
            { _, H, C, H, H, C, H, _ },
            { _, H, S, C, S, S, H, _ },
            { _, S, E, S, C, E, S, _ },
            { _, _, S, S, S, C, _, _ },
            { _, G, C, G, G, G, G, _ },
            { _, G, S, G, C, S, G, _ },
            { _, _, S, _, _, C, _, _ },
        };
    }

    private SolidColorBrush[,] GetHostSprite()
    {
        var _ = TransparentBrush;
        var E = EyeBrush;
        var R = RedBrush;
        var W = WhiteBrush;
        var Y = YellowBrush;
        
        return new SolidColorBrush[,] {
            { _, Y, Y, Y, Y, Y, Y, _ }, // Party hat
            { _, _, Y, Y, Y, Y, _, _ },
            { _, E, W, W, W, W, E, _ }, // Mask face
            { _, E, E, W, W, E, E, _ },
            { _, E, W, R, R, W, E, _ }, // Big red smile
            { _, R, R, R, R, R, R, _ },
            { _, R, E, R, R, E, R, _ },
            { _, _, E, _, _, E, _, _ },
        };
    }

    private SolidColorBrush[,] GetBestFriendSprite()
    {
        var _ = TransparentBrush;
        var P = PinkBrush;
        var R = RedBrush;
        var W = WhiteBrush;
        var E = EyeBrush;
        
        return new SolidColorBrush[,] {
            { _, P, _, _, _, _, P, _ },
            { P, P, P, P, P, P, P, P },
            { P, P, R, P, P, R, P, P }, // Heart eyes
            { P, P, P, P, P, P, P, P },
            { _, P, P, W, W, P, P, _ }, // Teeth smile
            { _, _, P, P, P, P, _, _ },
            { _, P, P, _, _, P, P, _ },
            { _, P, _, _, _, _, P, _ },
        };
    }

    private SolidColorBrush[,] GetItemSprite(ItemType type)
    {
        var _ = TransparentBrush;

        return type switch
        {
            ItemType.Gold => GetGoldSprite(),
            ItemType.HealthPotion => GetPotionSprite(),
            ItemType.Weapon => GetWeaponSprite(),
            ItemType.Armor => GetArmorSprite(),
            ItemType.Key => GetKeySprite(),
            _ => GetGoldSprite()
        };
    }

    private SolidColorBrush[,] GetGoldSprite()
    {
        var _ = TransparentBrush;
        var Y = YellowBrush;
        var O = OrangeBrush;
        
        return new SolidColorBrush[,] {
            { _, _, _, _, _, _, _, _ },
            { _, _, Y, Y, Y, Y, _, _ },
            { _, Y, Y, O, O, Y, Y, _ },
            { _, Y, O, Y, Y, O, Y, _ },
            { _, Y, O, Y, Y, O, Y, _ },
            { _, Y, Y, O, O, Y, Y, _ },
            { _, _, Y, Y, Y, Y, _, _ },
            { _, _, _, _, _, _, _, _ },
        };
    }

    private SolidColorBrush[,] GetPotionSprite()
    {
        var _ = TransparentBrush;
        var G = GrayBrush;
        var R = RedBrush;
        var P = PinkBrush;
        
        return new SolidColorBrush[,] {
            { _, _, _, G, G, _, _, _ },
            { _, _, G, G, G, G, _, _ },
            { _, _, _, G, G, _, _, _ },
            { _, _, R, R, R, R, _, _ },
            { _, R, R, P, P, R, R, _ },
            { _, R, P, P, P, P, R, _ },
            { _, R, R, R, R, R, R, _ },
            { _, _, R, R, R, R, _, _ },
        };
    }

    private SolidColorBrush[,] GetWeaponSprite()
    {
        var _ = TransparentBrush;
        var G = GrayBrush;
        var B = BrownBrush;
        var W = WhiteBrush;
        
        return new SolidColorBrush[,] {
            { _, _, _, _, _, _, W, _ },
            { _, _, _, _, _, W, G, _ },
            { _, _, _, _, W, G, _, _ },
            { _, _, _, W, G, _, _, _ },
            { _, _, W, G, _, _, _, _ },
            { _, B, G, _, _, _, _, _ },
            { B, B, _, _, _, _, _, _ },
            { B, _, _, _, _, _, _, _ },
        };
    }

    private SolidColorBrush[,] GetArmorSprite()
    {
        var _ = TransparentBrush;
        var G = GrayBrush;
        var D = DarkGrayBrush;
        var W = WhiteBrush;
        
        return new SolidColorBrush[,] {
            { _, _, G, G, G, G, _, _ },
            { _, G, G, G, G, G, G, _ },
            { G, G, W, G, G, W, G, G },
            { G, G, G, G, G, G, G, G },
            { _, G, G, G, G, G, G, _ },
            { _, G, D, G, G, D, G, _ },
            { _, _, G, G, G, G, _, _ },
            { _, _, G, _, _, G, _, _ },
        };
    }

    private SolidColorBrush[,] GetStairsSprite()
    {
        var _ = TransparentBrush;
        var P = PurpleBrush;
        var D = DarkGrayBrush;
        
        return new SolidColorBrush[,] {
            { P, P, P, P, P, P, P, P },
            { P, D, D, D, D, D, D, P },
            { P, D, P, P, P, P, D, P },
            { P, D, P, D, D, P, D, P },
            { P, D, P, D, D, P, D, P },
            { P, D, P, P, P, P, D, P },
            { P, D, D, D, D, D, D, P },
            { P, P, P, P, P, P, P, P },
        };
    }

    private SolidColorBrush[,] GetElevatorSprite()
    {
        var _ = TransparentBrush;
        var C = CyanBrush;
        var G = GrayBrush;
        var D = DarkGrayBrush;
        
        return new SolidColorBrush[,] {
            { G, G, G, G, G, G, G, G },
            { G, C, C, D, D, C, C, G },
            { G, C, _, D, D, _, C, G },
            { G, C, _, _, _, _, C, G },
            { G, C, _, _, _, _, C, G },
            { G, C, _, D, D, _, C, G },
            { G, C, C, D, D, C, C, G },
            { G, G, G, G, G, G, G, G },
        };
    }

    private SolidColorBrush[,] GetTeleporterSprite()
    {
        var _ = TransparentBrush;
        var M = MagentaBrush;
        var P = PurpleBrush;
        var C = CyanBrush;
        
        return new SolidColorBrush[,] {
            { _, _, M, C, C, M, _, _ },
            { _, M, P, M, M, P, M, _ },
            { M, P, C, P, P, C, P, M },
            { C, M, P, _, _, P, M, C },
            { C, M, P, _, _, P, M, C },
            { M, P, C, P, P, C, P, M },
            { _, M, P, M, M, P, M, _ },
            { _, _, M, C, C, M, _, _ },
        };
    }

    private SolidColorBrush[,] GetLockedDoorSprite()
    {
        var _ = TransparentBrush;
        var B = BrownBrush;
        var Y = YellowBrush;
        var D = DarkGrayBrush;
        
        return new SolidColorBrush[,] {
            { D, D, D, D, D, D, D, D },
            { D, B, B, B, B, B, B, D },
            { D, B, B, B, B, B, B, D },
            { D, B, B, Y, Y, B, B, D },
            { D, B, B, Y, D, B, B, D },
            { D, B, B, B, B, B, B, D },
            { D, B, B, B, B, B, B, D },
            { D, D, D, D, D, D, D, D },
        };
    }

    private SolidColorBrush[,] GetKeySprite()
    {
        var _ = TransparentBrush;
        var Y = YellowBrush;
        var O = OrangeBrush;
        
        return new SolidColorBrush[,] {
            { _, _, Y, Y, Y, _, _, _ },
            { _, Y, O, O, O, Y, _, _ },
            { _, Y, O, _, O, Y, _, _ },
            { _, Y, O, O, O, Y, _, _ },
            { _, _, Y, Y, Y, _, _, _ },
            { _, _, _, Y, _, _, _, _ },
            { _, _, _, Y, _, Y, Y, _ },
            { _, _, _, Y, Y, Y, _, _ },
        };
    }

    private SolidColorBrush[,] GetSafeRoomFloorSprite()
    {
        var G = new SolidColorBrush(Color.FromRgb(60, 90, 60)); // Soft green
        var L = new SolidColorBrush(Color.FromRgb(80, 110, 80)); // Light green
        
        return new SolidColorBrush[,] {
            { G, L, G, L, G, L, G, L },
            { L, G, L, G, L, G, L, G },
            { G, L, G, L, G, L, G, L },
            { L, G, L, G, L, G, L, G },
            { G, L, G, L, G, L, G, L },
            { L, G, L, G, L, G, L, G },
            { G, L, G, L, G, L, G, L },
            { L, G, L, G, L, G, L, G },
        };
    }

    private SolidColorBrush[,] GetSecretWallSprite()
    {
        var D = DarkGrayBrush;
        var G = GrayBrush;
        var H = new SolidColorBrush(Color.FromRgb(70, 60, 70)); // Hint color
        
        return new SolidColorBrush[,] {
            { D, G, D, G, D, G, D, G },
            { G, D, G, D, G, D, G, D },
            { D, G, D, H, H, D, G, D },
            { G, D, H, D, D, H, D, G },
            { G, D, H, D, D, H, D, G },
            { D, G, D, H, H, D, G, D },
            { G, D, G, D, G, D, G, D },
            { D, G, D, G, D, G, D, G },
        };
    }

    private SolidColorBrush[,] GetTrapSprite(TrapType type)
    {
        var _ = TransparentBrush;
        var R = RedBrush;
        var O = OrangeBrush;
        var Y = YellowBrush;
        var G = GrayBrush;
        
        return type switch
        {
            TrapType.Spike => new SolidColorBrush[,] {
                { _, _, _, G, G, _, _, _ },
                { _, _, G, G, G, G, _, _ },
                { _, _, G, G, G, G, _, _ },
                { _, G, G, G, G, G, G, _ },
                { _, G, G, G, G, G, G, _ },
                { G, G, G, G, G, G, G, G },
                { R, R, R, R, R, R, R, R },
                { R, _, R, _, _, R, _, R },
            },
            TrapType.Fire => new SolidColorBrush[,] {
                { _, _, R, _, _, R, _, _ },
                { _, R, O, R, R, O, R, _ },
                { _, R, Y, O, O, Y, R, _ },
                { R, O, Y, Y, Y, Y, O, R },
                { R, O, Y, Y, Y, Y, O, R },
                { _, R, Y, O, O, Y, R, _ },
                { _, R, O, R, R, O, R, _ },
                { _, _, R, _, _, R, _, _ },
            },
            _ => new SolidColorBrush[,] { // Default/poison/hug/lullaby - warning sign
                { _, Y, Y, Y, Y, Y, Y, _ },
                { Y, Y, R, Y, Y, R, Y, Y },
                { Y, R, R, R, R, R, R, Y },
                { Y, Y, R, R, R, R, Y, Y },
                { Y, Y, Y, R, R, Y, Y, Y },
                { Y, Y, Y, Y, Y, Y, Y, Y },
                { Y, Y, Y, R, R, Y, Y, Y },
                { _, Y, Y, Y, Y, Y, Y, _ },
            }
        };
    }
    #endregion
    #endregion

    #region UI Updates
    private void UpdateUI()
    {
        // Health bar
        double healthPercent = (double)_player.Health / _player.MaxHealth;
        HealthBar.Width = 150 * healthPercent;
        HealthBar.Background = healthPercent > 0.3 ? (SolidColorBrush)FindResource("HealthBrush") : (SolidColorBrush)FindResource("HealthLowBrush");
        HealthText.Text = $"{_player.Health}/{_player.MaxHealth}";

        // Stats
        LevelText.Text = _player.Level.ToString();
        XPText.Text = $"{_player.XP}/{_player.Level * 100}";
        AttackText.Text = _player.Attack.ToString();
        DefenseText.Text = _player.Defense.ToString();

        // Dungeon info
        FloorText.Text = _currentFloor.ToString();
        GoldText.Text = _player.Gold.ToString();

        UpdateInventoryUI();
    }

    private void UpdateInventoryUI()
    {
        EmptyInventoryText.Visibility = _inventory.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        InventoryList.Items.Refresh();
    }

    private void AddMessage(string message)
    {
        _messages.Insert(0, message);
        if (_messages.Count > 50)
            _messages.RemoveAt(_messages.Count - 1);
    }
    #endregion

    #region Creepy Messages
    private string GetCreepyEntranceMessage()
    {
        string[] messages = {
            "☺ Welcome to the SUPER HAPPY FUN ZONE! ☺ You're here FOREVER! ☺ We won't let you leave! ☺",
            "🎈 The Fun Zone LOVES you SO MUCH! Everyone who tries to leave comes back! 🎈",
            "🌈 Welcome, friend! Everything is WONDERFUL! (Don't think about the others) 🌈",
            "✨ Fun Level 1! This is the BEST DAY EVER! It repeats! Forever! You'll learn to love it! ✨",
            "🎉 YOU'RE HERE! We've been watching! We're SO HAPPY you finally came! Don't go! DON'T GO! 🎉"
        };
        return messages[_rng.Next(messages.Length)];
    }

    private string GetCreepyAttackMessage(Enemy enemy, int damage)
    {
        string[] messages = {
            $"☺ You give the {enemy.Name} a FRIENDSHIP HUG! ({damage}) They're crying HAPPY tears! ☺",
            $"🎈 You share JOY with the {enemy.Name}! {damage} happiness! (Don't listen to the screaming) 🎈",
            $"✨ The {enemy.Name} receives your {damage} KINDNESS! They can't stop smiling now! ✨",
            $"🌈 You sprinkle {damage} SMILE MAGIC! The {enemy.Name} will NEVER stop smiling! 🌈",
            $"💖 The {enemy.Name} takes {damage} love! They're trying to say thank you! (It sounds like crying) 💖"
        };
        return messages[_rng.Next(messages.Length)];
    }

    private string GetCreepyDeathMessage(Enemy enemy, int xp, int gold)
    {
        string[] messages = {
            $"🎉 The {enemy.Name} goes to FOREVER SLEEP! Their smile is permanent now! ☺ +{xp} XP, +{gold} Smile Points! 🎉",
            $"✨ {enemy.Name} waves goodbye! They can't stop waving! They'll wave FOREVER! +{xp} XP, +{gold} points! ✨",
            $"🌈 The {enemy.Name} becomes JOY! (The screaming is just joy leaving) +{xp} XP, {gold} points! 🌈",
            $"🎈 {enemy.Name} becomes confetti! (Don't look at the confetti too closely) +{gold} points, {xp} XP! 🎈",
            $"💖 The {enemy.Name} is HAPPY NOW! They're still here! Always here! Watching! +{xp} XP, {gold} points! 💖"
        };
        return messages[_rng.Next(messages.Length)];
    }

    private string GetCreepyEnemyAttackMessage(Enemy enemy, int damage)
    {
        // Use the enemy's custom attack message
        string action = enemy.AttackMessages[_rng.Next(enemy.AttackMessages.Length)];
        return $"The {enemy.Name} {action}. (-{damage} HP)";
    }

    private string GetCreepyGoldMessage(int gold)
    {
        string[] messages = {
            $"🌟 You found {gold} Smile Points! Each one is WATCHING YOU! They never blink! 🌟",
            $"✨ {gold} Smile Points! They're SO WARM! (Too warm. Why are they warm?) ✨",
            $"🎈 You collect {gold} Happy Tokens! They whisper when you're not listening! 🎈",
            $"💖 {gold} Joy Points! They stick to your hands! You can't let go! That's GOOD! 💖",
            $"☺ The floor GIVES you {gold} points! It won't say where they came from! ☺"
        };
        return messages[_rng.Next(messages.Length)];
    }

    private string GetCreepyPotionPickupMessage()
    {
        string[] messages = {
            "🧃 You found SUPER HAPPY JUICE! It's warm! Body temperature! It LOVES YOU! 🧃",
            "✨ A juice box! The label is in your handwriting! How did it know?! SO FRIENDLY! ✨",
            "🎈 You found a sippy cup of JOY! It sloshes even when you hold it still! 🎈",
            "🌈 RAINBOW JUICE! It changes color to match your thoughts! (Don't think bad thoughts!) 🌈",
            "💖 You found FRIENDSHIP LIQUID! It hums the song from your childhood! How does it KNOW?! 💖"
        };
        return messages[_rng.Next(messages.Length)];
    }

    private Weapon GetWeaponForFloor()
    {
        // Creepy-cheerful weapons matching the SUPER HAPPY FUN ZONE aesthetic!
        var earlyWeapons = new[] {
            new Weapon { Name = "Plastic Safety Knife", Icon = "🔪", AttackBonus = 3, Type = WeaponType.Melee,
                Ability = WeaponAbility.Bleed, AbilityPower = 2, SpecialMaxCooldown = 3,
                Description = "For spreading FRIENDSHIP JAM! (It's not jam)",
                AbilityDescription = "Special: Happy Spreading - They LEAK happiness!" },
            new Weapon { Name = "Smile Enforcer 3000", Icon = "🔫", AttackBonus = 4, Type = WeaponType.Ranged,
                Ability = WeaponAbility.DoubleDamage, AbilityPower = 2, SpecialMaxCooldown = 4,
                Description = "Helps friends remember to SMILE! ☺",
                AbilityDescription = "Special: Permanent Grin - Smile SO HARD!" },
            new Weapon { Name = "Friendship Spreader", Icon = "💥", AttackBonus = 5, Type = WeaponType.Ranged,
                Ability = WeaponAbility.AreaDamage, AbilityPower = 4, SpecialMaxCooldown = 5,
                Description = "Shares love with EVERYONE nearby!",
                AbilityDescription = "Special: Group Hug - Love for all friends!" },
        };

        var midWeapons = new[] {
            new Weapon { Name = "Joy Distributor", Icon = "🔫", AttackBonus = 6, Type = WeaponType.Ranged,
                Ability = WeaponAbility.Stun, AbilityPower = 2, SpecialMaxCooldown = 4,
                Description = "Delivers happiness SO FAST they can't move!",
                AbilityDescription = "Special: Overwhelming Joy - They're TOO HAPPY!" },
            new Weapon { Name = "Kindness Blade", Icon = "🔪", AttackBonus = 5, Type = WeaponType.Melee,
                Ability = WeaponAbility.Lifesteal, AbilityPower = 3, SpecialMaxCooldown = 3,
                Description = "Shares their energy! They're GIVING!",
                AbilityDescription = "Special: Energy Sharing - They give YOU life!" },
            new Weapon { Name = "Giggle Generator", Icon = "🔫", AttackBonus = 7, Type = WeaponType.Ranged,
                Ability = WeaponAbility.DoubleDamage, AbilityPower = 3, SpecialMaxCooldown = 4,
                Description = "Makes them laugh SO MUCH!",
                AbilityDescription = "Special: Laughing Fit - They can't stop EVER!" },
            new Weapon { Name = "Party Confetti Cannon", Icon = "🎉", AttackBonus = 8, Type = WeaponType.Ranged,
                Ability = WeaponAbility.Knockback, AbilityPower = 3, SpecialMaxCooldown = 4,
                Description = "It's PARTY TIME! (The confetti is sharp)",
                AbilityDescription = "Special: CELEBRATION - They FLY with joy!" },
        };

        var advancedWeapons = new[] {
            new Weapon { Name = "MEGA HAPPINESS BLASTER", Icon = "🎈", AttackBonus = 9, Type = WeaponType.Ranged,
                Ability = WeaponAbility.DoubleDamage, AbilityPower = 4, SpecialMaxCooldown = 4,
                Description = "MAXIMUM JOY CAPACITY! Can't handle it!",
                AbilityDescription = "Special: JOY OVERLOAD - TOO MUCH HAPPY!" },
            new Weapon { Name = "Fun Times Rapidfire", Icon = "✨", AttackBonus = 10, Type = WeaponType.Ranged,
                Ability = WeaponAbility.AreaDamage, AbilityPower = 5, SpecialMaxCooldown = 5,
                Description = "Shares fun with EVERYONE you can see!",
                AbilityDescription = "Special: FUN FOR ALL - Nobody escapes fun!" },
            new Weapon { Name = "Smile Carver", Icon = "🔪", AttackBonus = 8, Type = WeaponType.Melee,
                Ability = WeaponAbility.Bleed, AbilityPower = 4, SpecialMaxCooldown = 3,
                Description = "Helps friends smile PERMANENTLY! ☺",
                AbilityDescription = "Special: Forever Smile - Smiling NEVER STOPS!" },
            new Weapon { Name = "Glitter Bomb Launcher", Icon = "✨", AttackBonus = 11, Type = WeaponType.Ranged,
                Ability = WeaponAbility.AreaDamage, AbilityPower = 6, SpecialMaxCooldown = 5,
                Description = "Sparkles EVERYWHERE! (It burns)",
                AbilityDescription = "Special: SPARKLE STORM - Beautiful agony!" },
        };

        var eliteWeapons = new[] {
            new Weapon { Name = "Precision Love Beam", Icon = "💖", AttackBonus = 12, Type = WeaponType.Ranged,
                Ability = WeaponAbility.DoubleDamage, AbilityPower = 5, SpecialMaxCooldown = 5,
                Description = "Sends love DIRECTLY to the heart!",
                AbilityDescription = "Special: True Love - Love HURTS SO GOOD!" },
            new Weapon { Name = "Endless Laughter Machine", Icon = "😂", AttackBonus = 11, Type = WeaponType.Ranged,
                Ability = WeaponAbility.AreaDamage, AbilityPower = 7, SpecialMaxCooldown = 6,
                Description = "Makes EVERYONE laugh forever and ever!",
                AbilityDescription = "Special: Comedy Gold - They'll DIE laughing!" },
            new Weapon { Name = "Birthday Surprise Cannon", Icon = "🎂", AttackBonus = 13, Type = WeaponType.Ranged,
                Ability = WeaponAbility.AreaDamage, AbilityPower = 8, SpecialMaxCooldown = 6,
                Description = "SURPRISE! It's your party! (It's not good)",
                AbilityDescription = "Special: HAPPY BIRTHDAY - Best day EVER!" },
            new Weapon { Name = "Friendship Forever Blade", Icon = "🔪", AttackBonus = 10, Type = WeaponType.Melee,
                Ability = WeaponAbility.Bleed, AbilityPower = 5, SpecialMaxCooldown = 4,
                Description = "Makes PERMANENT friends! They NEVER leave!",
                AbilityDescription = "Special: Eternal Bond - Friends FOREVER!" },
        };

        var legendaryWeapons = new[] {
            new Weapon { Name = "The ULTIMATE JOY CANNON", Icon = "🌈", AttackBonus = 15, Type = WeaponType.Ranged,
                Ability = WeaponAbility.DoubleDamage, AbilityPower = 6, SpecialMaxCooldown = 5,
                Description = "MORE JOY THAN PHYSICALLY POSSIBLE!",
                AbilityDescription = "Special: MAXIMUM HAPPINESS - Reality can't handle it!" },
            new Weapon { Name = "Party Time Infinity Gun", Icon = "🎊", AttackBonus = 14, Type = WeaponType.Ranged,
                Ability = WeaponAbility.AreaDamage, AbilityPower = 10, SpecialMaxCooldown = 7,
                Description = "The party NEVER ENDS! NEVER EVER!",
                AbilityDescription = "Special: ETERNAL CELEBRATION - Dance forever!" },
            new Weapon { Name = "Love Explosion Launcher", Icon = "💝", AttackBonus = 16, Type = WeaponType.Ranged,
                Ability = WeaponAbility.AreaDamage, AbilityPower = 12, SpecialMaxCooldown = 8,
                Description = "Spreads SO MUCH LOVE it explodes!",
                AbilityDescription = "Special: LOVE BOMB - Violent affection!" },
            new Weapon { Name = "The Hugger", Icon = "🤗", AttackBonus = 13, Type = WeaponType.Melee,
                Ability = WeaponAbility.Lifesteal, AbilityPower = 8, SpecialMaxCooldown = 4,
                Description = "Gives INFINITE HUGS! You take their warmth!",
                AbilityDescription = "Special: EMBRACE - Hug so tight they SHARE life!" },
        };

        var weapons = _currentFloor switch
        {
            <= 2 => earlyWeapons,
            <= 4 => midWeapons,
            <= 6 => advancedWeapons,
            <= 8 => eliteWeapons,
            _ => legendaryWeapons
        };

        var weapon = weapons[_rng.Next(weapons.Length)];
        weapon.SpecialCooldown = 0; // Ready to use immediately
        return weapon;
    }

    private (string Name, string Icon, int Bonus, string Message) GetArmorForFloor()
    {
        var nurseryArmor = new[] {
            ("Swaddle of Protection", "🧸", 2, "Wrapped too tight. Can't escape."),
            ("Blankie Shield", "🛏️", 2, "The monsters can't see you now."),
            ("Onesie of Resilience", "👶", 3, "It grows with you. You never stopped wearing it."),
        };
        
        var playgroundArmor = new[] {
            ("Knee Pads of Experience", "🦵", 3, "Scuffed with memories of falls."),
            ("Helmet of Denial", "⛑️", 4, "The cracks aren't real if you can't see them."),
            ("Jacket Left Behind", "🧥", 3, "Someone's mom is still looking for this."),
        };
        
        var schoolArmor = new[] {
            ("Participation Trophy", "🏆", 4, "You showed up. That's enough."),
            ("Locker Armor", "🚪", 5, "The combination was your birthday. You forgot it."),
            ("Yearbook Shield", "📚", 4, "All the signatures are the same name."),
        };
        
        var homeArmor = new[] {
            ("Hand-Knit Sweater", "🧶", 5, "Grandma made it. She's not Grandma anymore."),
            ("Dad's Old Coat", "🥼", 6, "Still smells like him. Who was he?"),
            ("Family Quilt", "🛏️", 5, "Each patch is a memory. Some are missing."),
        };
        
        var endArmor = new[] {
            ("Emotional Walls", "🧱", 7, "You built these yourself."),
            ("Skin of Your Past Self", "🎭", 8, "It still fits. Barely."),
            ("Armor of Acceptance", "💝", 9, "You finally stopped running."),
        };

        var armors = _currentFloor switch
        {
            <= 2 => nurseryArmor,
            <= 4 => playgroundArmor,
            <= 6 => schoolArmor,
            <= 8 => homeArmor,
            _ => endArmor
        };

        return armors[_rng.Next(armors.Length)];
    }

    private string GetCreepyHealMessage(int heal)
    {
        string[] messages = {
            $"🧃 You drink the HAPPY JUICE! Tastes like copper and birthday cake! +{heal} HP! ☺",
            $"✨ The liquid giggles as you swallow! +{heal} HP! (It's still giggling inside you) ✨",
            $"🎈 Gulp! The juice is PART OF YOU NOW! +{heal} HP! Forever friends! 🎈",
            $"🌈 It tastes like memories you don't remember having! +{heal} HP! 🌈",
            $"💖 You feel AMAZING! The juice is watching from inside! +{heal} HP! 💖"
        };
        return messages[_rng.Next(messages.Length)];
    }

    private string GetCreepyLevelUpMessage()
    {
        string[] messages = {
            $"🎈 You got STRONGER! Level {_player.Level}! EVERYONE is watching you grow! They're SO CLOSE now! 🎈",
            $"✨ Level {_player.Level}! You're CHANGING! (Don't look in a mirror) Keep going FOREVER! ✨",
            $"🌟 LEVEL {_player.Level}! You're ONE OF US now! SO SO SPECIAL! 🌟",
            $"🎉 Level {_player.Level}! The walls are CLAPPING! (They shouldn't have hands) They LOVE YOU! 🎉",
            $"⭐ You are now Level {_player.Level}! YOU CAN NEVER LEAVE! ISN'T THAT WONDERFUL?! ⭐"
        };
        return messages[_rng.Next(messages.Length)];
    }

    private string GetCreepyFloorMessage()
    {
        string[] messages = {
            $"🎈 Fun Level {_currentFloor}! The stairs remembered your footsteps! They've been counting! 🎈",
            $"✨ You reach Fun Level {_currentFloor}! (You've been here before. Don't you remember?) MORE FUN! ✨",
            $"🌈 Fun Level {_currentFloor}! It smells like OLD CAKE and childhood parties! 🌈",
            $"🎉 Welcome to Fun Level {_currentFloor}! We've ALWAYS been waiting! Time doesn't work here! 🎉",
            $"💖 Fun Level {_currentFloor}! Everyone here knows your name! (You never told them) 💖"
        };
        return messages[_rng.Next(messages.Length)];
    }
    #endregion

    #region Direction Helpers
    private (int dx, int dy) GetDirectionDelta(Direction dir)
    {
        return dir switch
        {
            Direction.North => (0, -1),
            Direction.South => (0, 1),
            Direction.East => (1, 0),
            Direction.West => (-1, 0),
            _ => (0, 0)
        };
    }

    private Direction TurnLeft(Direction dir)
    {
        return dir switch
        {
            Direction.North => Direction.West,
            Direction.West => Direction.South,
            Direction.South => Direction.East,
            Direction.East => Direction.North,
            _ => dir
        };
    }

    private Direction TurnRight(Direction dir)
    {
        return dir switch
        {
            Direction.North => Direction.East,
            Direction.East => Direction.South,
            Direction.South => Direction.West,
            Direction.West => Direction.North,
            _ => dir
        };
    }
    #endregion

    #region Skill System
    private void ShowSkillMenu()
    {
        if (_playerSkills.SkillPoints <= 0)
        {
            AddMessage("💫 No skill points available! Level up to gain more! 💫");
            return;
        }

        var skillOptions = $@"🌟 SKILL MENU 🌟
Available Points: {_playerSkills.SkillPoints}

1. Critical Hit [{_playerSkills.CriticalHitLevel}/5] - +10% crit chance
2. Vitality [{_playerSkills.VitalityLevel}/5] - +20 max HP
3. Power [{_playerSkills.PowerLevel}/5] - +3 attack
4. Resilience [{_playerSkills.ResilienceLevel}/5] - +2 defense
5. Vampiric [{_playerSkills.VampiricLevel}/5] - +5% lifesteal
6. Berserker [{_playerSkills.BerserkerLevel}/5] - +5 damage when low HP
7. Treasure Hunter [{_playerSkills.TreasureHunterLevel}/5] - +25% gold
8. Cancel

Choose a skill to upgrade (1-7):";

        var result = MessageBox.Show(skillOptions, "Skill Upgrade", MessageBoxButton.OK);

        // For now, let's add a simple message-based system
        AddMessage("💫 Press 1-7 in the game to upgrade skills! Press 0 to cancel! 💫");
    }

    private void TryUpgradeSkill(int skillIndex)
    {
        if (_playerSkills.SkillPoints <= 0)
        {
            AddMessage("💫 No skill points! Level up first! 💫");
            return;
        }

        bool upgraded = false;
        string skillName = "";

        switch (skillIndex)
        {
            case 1 when _playerSkills.CriticalHitLevel < 5:
                _playerSkills.CriticalHitLevel++;
                skillName = "Critical Hit";
                upgraded = true;
                break;
            case 2 when _playerSkills.VitalityLevel < 5:
                _playerSkills.VitalityLevel++;
                _player.MaxHealth += 20;
                _player.Health += 20;
                skillName = "Vitality";
                upgraded = true;
                break;
            case 3 when _playerSkills.PowerLevel < 5:
                _playerSkills.PowerLevel++;
                skillName = "Power";
                upgraded = true;
                break;
            case 4 when _playerSkills.ResilienceLevel < 5:
                _playerSkills.ResilienceLevel++;
                skillName = "Resilience";
                upgraded = true;
                break;
            case 5 when _playerSkills.VampiricLevel < 5:
                _playerSkills.VampiricLevel++;
                skillName = "Vampiric";
                upgraded = true;
                break;
            case 6 when _playerSkills.BerserkerLevel < 5:
                _playerSkills.BerserkerLevel++;
                skillName = "Berserker";
                upgraded = true;
                break;
            case 7 when _playerSkills.TreasureHunterLevel < 5:
                _playerSkills.TreasureHunterLevel++;
                skillName = "Treasure Hunter";
                upgraded = true;
                break;
        }

        if (upgraded)
        {
            _playerSkills.SkillPoints--;
            AddMessage($"⭐ {skillName} upgraded! Points remaining: {_playerSkills.SkillPoints} ⭐");
            CreateParticles(_player.X, _player.Y, Brushes.Cyan, 15);
            PlaySound("levelup");
        }
        else
        {
            AddMessage("❌ Can't upgrade that skill! (Max level or invalid choice)");
        }

        UpdateUI();
    }
    #endregion

    #region Boss System
    private void SpawnBoss(List<Room> rooms, int floor)
    {
        // Boss spawns on floors 3, 6, 9
        if (floor % 3 != 0) return;

        var endRoom = rooms[^1];
        int x = endRoom.CenterX;
        int y = endRoom.CenterY;

        Enemy boss = floor switch
        {
            3 => new Enemy
            {
                X = x, Y = y,
                Type = EnemyType.CarouselHorse,
                Name = "MEGA CAROUSEL KING",
                Icon = "👑",
                Health = 150,
                MaxHealth = 150,
                Attack = 15,
                Defense = 8,
                XPValue = 200,
                AttackMessages = new[] {
                    "GALLOPS through your memories at MAXIMUM SPEED",
                    "The music NEVER STOPS and it HURTS SO GOOD",
                    "Spins and spins and YOU'RE SPINNING TOO NOW"
                }
            },
            6 => new Enemy
            {
                X = x, Y = y,
                Type = EnemyType.SubstituteTeacher,
                Name = "THE PRINCIPAL",
                Icon = "📜",
                Health = 300,
                MaxHealth = 300,
                Attack = 22,
                Defense = 12,
                XPValue = 400,
                AttackMessages = new[] {
                    "Assigns PERMANENT DETENTION to your soul",
                    "Calls your parents (they don't recognize your name)",
                    "Marks you ABSENT from existence"
                }
            },
            9 => new Enemy
            {
                X = x, Y = y,
                Type = EnemyType.YourBestFriend,
                Name = "THE ONE WHO WAITED",
                Icon = "💝",
                Health = 500,
                MaxHealth = 500,
                Attack = 30,
                Defense = 18,
                XPValue = 800,
                AttackMessages = new[] {
                    "Hugs you with INFINITE ARMS",
                    "Says 'I MISSED YOU' with EVERY VOICE YOU EVER KNEW",
                    "Promises to NEVER LET GO EVER AGAIN"
                }
            },
            _ => null!
        };

        if (boss != null)
        {
            _enemies.Add(boss);
            _bossEnemies[floor] = boss;
            AddMessage($"🚨 WARNING! {boss.Name} appears! This is SO MUCH FUN! 🚨");
        }
    }
    #endregion

    #region Sound System
    private void PlaySound(string soundName)
    {
        try
        {
            // Simple beep sounds for now (can be replaced with actual audio files later)
            switch (soundName)
            {
                case "attack":
                    System.Console.Beep(800, 50);
                    break;
                case "hit":
                    System.Console.Beep(400, 100);
                    break;
                case "pickup":
                    System.Console.Beep(1000, 80);
                    break;
                case "levelup":
                    System.Console.Beep(1200, 100);
                    System.Console.Beep(1400, 100);
                    System.Console.Beep(1600, 150);
                    break;
                case "death":
                    System.Console.Beep(200, 200);
                    break;
            }
        }
        catch
        {
            // Sound not available, silently fail
        }
    }

    private void PlayBackgroundMusic(int floor)
    {
        // Placeholder for background music system
        // Would load and play different music tracks based on floor theme
        // Implementation would use MediaPlayer with .mp3 or .wav files
    }
    #endregion

    #region AI Pathfinding
    private List<(int X, int Y)>? FindPathAStar(int startX, int startY, int goalX, int goalY)
    {
        var openSet = new PriorityQueue<(int X, int Y), double>();
        var cameFrom = new Dictionary<(int, int), (int, int)>();
        var gScore = new Dictionary<(int, int), double>();
        var fScore = new Dictionary<(int, int), double>();

        var start = (startX, startY);
        var goal = (goalX, goalY);

        gScore[start] = 0;
        fScore[start] = Heuristic(start, goal);
        openSet.Enqueue(start, fScore[start]);

        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();

            if (current == goal)
            {
                return ReconstructPath(cameFrom, current);
            }

            foreach (var neighbor in GetNeighbors(current.X, current.Y))
            {
                double tentativeGScore = gScore[current] + 1;

                if (!gScore.ContainsKey(neighbor) || tentativeGScore < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGScore;
                    fScore[neighbor] = gScore[neighbor] + Heuristic(neighbor, goal);

                    if (!openSet.UnorderedItems.Any(item => item.Element == neighbor))
                    {
                        openSet.Enqueue(neighbor, fScore[neighbor]);
                    }
                }
            }
        }

        return null; // No path found
    }

    private double Heuristic((int X, int Y) a, (int X, int Y) b)
    {
        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y); // Manhattan distance
    }

    private List<(int X, int Y)> GetNeighbors(int x, int y)
    {
        var neighbors = new List<(int X, int Y)>();
        var directions = new[] { (0, 1), (1, 0), (0, -1), (-1, 0) };

        foreach (var (dx, dy) in directions)
        {
            int newX = x + dx;
            int newY = y + dy;
            if (CanMoveTo(newX, newY))
            {
                neighbors.Add((newX, newY));
            }
        }

        return neighbors;
    }

    private List<(int X, int Y)> ReconstructPath(Dictionary<(int, int), (int, int)> cameFrom, (int X, int Y) current)
    {
        var path = new List<(int X, int Y)> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Insert(0, current);
        }
        return path;
    }
    #endregion

    #region Particle System
    private void CreateParticles(int x, int y, Brush color, int count)
    {
        for (int i = 0; i < count; i++)
        {
            _particles.Add(new ParticleEffect
            {
                X = x,
                Y = y,
                VelocityX = (_rng.NextDouble() - 0.5) * 0.3,
                VelocityY = (_rng.NextDouble() - 0.5) * 0.3,
                Color = color,
                Lifetime = 0.5 + _rng.NextDouble() * 0.5,
                CreatedTime = DateTime.Now
            });
        }
    }

    private void UpdateParticles()
    {
        var now = DateTime.Now;
        _particles.RemoveAll(p => (now - p.CreatedTime).TotalSeconds > p.Lifetime);

        foreach (var particle in _particles)
        {
            particle.X += particle.VelocityX;
            particle.Y += particle.VelocityY;
            particle.VelocityY += 0.01; // Gravity
        }
    }

    private void RenderParticles()
    {
        double canvasWidth = GameCanvas.ActualWidth > 0 ? GameCanvas.ActualWidth : 800;
        double canvasHeight = GameCanvas.ActualHeight > 0 ? GameCanvas.ActualHeight : 600;

        foreach (var particle in _particles)
        {
            var age = (DateTime.Now - particle.CreatedTime).TotalSeconds / particle.Lifetime;
            var opacity = 1.0 - age;

            var ellipse = new System.Windows.Shapes.Ellipse
            {
                Width = 4,
                Height = 4,
                Fill = particle.Color,
                Opacity = opacity
            };

            // Convert world position to screen position
            double screenX = canvasWidth / 2 + (particle.X - _player.X) * TileSize;
            double screenY = canvasHeight / 2 + (particle.Y - _player.Y) * TileSize;

            Canvas.SetLeft(ellipse, screenX);
            Canvas.SetTop(ellipse, screenY);
            GameCanvas.Children.Add(ellipse);
            _entityLabels.Add(ellipse);
        }
    }
    #endregion

    #region Data Classes
    private enum Tile { Wall, Floor, Stairs, Elevator, Teleporter, LockedDoor, SafeRoom, SecretWall }
    private enum EnemyType { 
        // Floor 1-2: Nursery
        LostTeddy, CribSpider, NightLight,
        // Floor 3-4: Playground  
        SwingChild, SandboxThing, CarouselHorse,
        // Floor 5-6: School
        SubstituteTeacher, HallMonitor, LunchLady,
        // Floor 7-8: Home
        WrongMom, AtticDweller, BasementFriend,
        // Floor 9-10: The End
        MirrorYou, TheHost, YourBestFriend
    }
    private enum ItemType { None, Gold, HealthPotion, Weapon, Armor, Key }
    private enum TrapType { Spike, Poison, Fire, Hug, Lullaby }
    private enum Direction { North, East, South, West }
    private enum WeaponType { Melee, Ranged, Magic }
    private enum WeaponAbility { None, Bleed, Stun, Lifesteal, AreaDamage, Knockback, DoubleDamage }

    private class Player
    {
        public int X { get; set; }
        public int Y { get; set; }
        public Direction Facing { get; set; } = Direction.North;
        public int Health { get; set; }
        public int MaxHealth { get; set; }
        public int Attack { get; set; }
        public int Defense { get; set; }
        public int Level { get; set; }
        public int XP { get; set; }
        public int Gold { get; set; }
        public Weapon? EquippedWeapon { get; set; }
    }

    private class Enemy
    {
        public int X { get; set; }
        public int Y { get; set; }
        public EnemyType Type { get; set; }
        public string Name { get; set; } = "";
        public string Icon { get; set; } = "?";
        public int Health { get; set; }
        public int MaxHealth { get; set; }
        public int Attack { get; set; }
        public int Defense { get; set; }
        public int XPValue { get; set; }
        public string[] AttackMessages { get; set; } = { "attacks you" };

        // Status effects
        public int StunTurnsRemaining { get; set; }
        public int BleedDamage { get; set; }
        public int BleedTurnsRemaining { get; set; }
    }

    private class Item
    {
        public int X { get; set; }
        public int Y { get; set; }
        public ItemType Type { get; set; }
    }

    private class Weapon
    {
        public string Name { get; set; } = "";
        public string Icon { get; set; } = "⚔️";
        public int AttackBonus { get; set; }
        public WeaponType Type { get; set; }
        public WeaponAbility Ability { get; set; }
        public int AbilityPower { get; set; } // Magnitude of ability effect
        public int SpecialCooldown { get; set; } // Turns until special is ready
        public int SpecialMaxCooldown { get; set; } // Base cooldown for special
        public string Description { get; set; } = "";
        public string AbilityDescription { get; set; } = "";
    }

    private class Trap
    {
        public int X { get; set; }
        public int Y { get; set; }
        public TrapType Type { get; set; }
        public int Damage { get; set; }
        public bool Triggered { get; set; }
    }

    private class InventoryItem
    {
        public string Icon { get; set; } = "";
        public string Name { get; set; } = "";
        public ItemType Type { get; set; }
        public int Quantity { get; set; } = 1;
        public Visibility ShowQuantity => Quantity > 1 ? Visibility.Visible : Visibility.Collapsed;
    }

    private class Room
    {
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
        public int CenterX => X + Width / 2;
        public int CenterY => Y + Height / 2;

        public Room(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public bool Intersects(Room other)
        {
            return X <= other.X + other.Width + 1 &&
                   X + Width + 1 >= other.X &&
                   Y <= other.Y + other.Height + 1 &&
                   Y + Height + 1 >= other.Y;
        }
    }

    private class ParticleEffect
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double VelocityX { get; set; }
        public double VelocityY { get; set; }
        public Brush Color { get; set; } = Brushes.White;
        public double Lifetime { get; set; }
        public DateTime CreatedTime { get; set; }
    }

    private class PlayerSkills
    {
        public int SkillPoints { get; set; } = 0;
        public int CriticalHitLevel { get; set; } = 0; // +10% crit chance per level
        public int VitalityLevel { get; set; } = 0; // +20 max HP per level
        public int PowerLevel { get; set; } = 0; // +3 attack per level
        public int ResilienceLevel { get; set; } = 0; // +2 defense per level
        public int SwiftnessLevel { get; set; } = 0; // Extra turn every N turns
        public int VampiricLevel { get; set; } = 0; // Lifesteal % on attacks
        public int BerserkerLevel { get; set; } = 0; // More damage at low HP
        public int TreasureHunterLevel { get; set; } = 0; // Extra gold drops

        public int GetCritChance() => CriticalHitLevel * 10;
        public int GetMaxHPBonus() => VitalityLevel * 20;
        public int GetAttackBonus() => PowerLevel * 3;
        public int GetDefenseBonus() => ResilienceLevel * 2;
        public int GetLifestealPercent() => VampiricLevel * 5;
        public int GetBerserkerBonus(int currentHP, int maxHP) =>
            currentHP < maxHP / 2 ? BerserkerLevel * 5 : 0;
        public double GetGoldMultiplier() => 1.0 + (TreasureHunterLevel * 0.25);
    }

    private class PowerUp
    {
        public int X { get; set; }
        public int Y { get; set; }
        public PowerUpType Type { get; set; }
        public string Name { get; set; } = "";
        public string Icon { get; set; } = "";
        public int Duration { get; set; } // Turns active
    }

    private enum PowerUpType
    {
        SpeedBoost,      // Move twice per turn
        DamageBoost,     // 2x damage
        Shield,          // Immunity to damage
        Invisibility,    // Enemies can't see you
        Regeneration,    // Heal over time
        DoubleXP,        // 2x XP gain
        MagnetGold       // Auto-collect nearby gold
    }

    private class ActivePowerUp
    {
        public PowerUpType Type { get; set; }
        public int RemainingTurns { get; set; }
    }
    #endregion
}
