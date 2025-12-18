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
    private List<Rectangle> _tileRectangles = new();
    private List<UIElement> _entityLabels = new();
    #endregion

    #region Brushes
    private static readonly SolidColorBrush WallBrush = new(Color.FromRgb(45, 45, 58));
    private static readonly SolidColorBrush FloorBrush = new(Color.FromRgb(26, 26, 36));
    private static readonly SolidColorBrush PlayerBrush = new(Color.FromRgb(34, 197, 94));
    private static readonly SolidColorBrush EnemyBrush = new(Color.FromRgb(239, 68, 68));
    private static readonly SolidColorBrush TreasureBrush = new(Color.FromRgb(251, 191, 36));
    private static readonly SolidColorBrush StairsBrush = new(Color.FromRgb(139, 92, 246));
    private static readonly SolidColorBrush TrapBrush = new(Color.FromRgb(249, 115, 22));
    private static readonly SolidColorBrush FogBrush = new(Color.FromRgb(5, 5, 8));
    private static readonly SolidColorBrush ExploredFogBrush = new(Color.FromRgb(13, 13, 20));
    private static readonly SolidColorBrush HealthPotionBrush = new(Color.FromRgb(239, 68, 68));
    
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

        switch (e.Key)
        {
            case Key.W:
            case Key.Up:
                dy = -1;
                acted = true;
                break;
            case Key.S:
            case Key.Down:
                dy = 1;
                acted = true;
                break;
            case Key.A:
            case Key.Left:
                dx = -1;
                acted = true;
                break;
            case Key.D:
            case Key.Right:
                dx = 1;
                acted = true;
                break;
            case Key.Space:
                // Wait/Rest - heal more in safe room
                int healAmount = IsInSafeRoom() ? 5 : 1;
                if (_player.Health < _player.MaxHealth)
                {
                    _player.Health = Math.Min(_player.MaxHealth, _player.Health + healAmount);
                    if (IsInSafeRoom())
                        AddMessage($"The safe room embraces you. +{healAmount} HP.");
                    else
                        AddMessage("You rest and recover 1 HP.");
                }
                else
                {
                    AddMessage(IsInSafeRoom() ? "You're at peace here. Full health." : "You wait...");
                }
                acted = true;
                break;
            case Key.E:
                // Interact with special tiles
                HandleInteraction();
                return;
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
                    AddMessage("No elevator here.");
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
                        AddMessage("No teleporter here.");
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
        }

        if (acted && (dx != 0 || dy != 0))
        {
            TryMove(dx, dy);
        }

        if (acted)
        {
            ProcessEnemyTurns();
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
        AddMessage("The stairs (🟣) miss you. They're waiting.");
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

        UpdateVisibility();
        CreateTileRectangles();
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
                AddMessage("The key dissolves. The door relents.");
                UpdateInventoryUI();
            }
            else
            {
                AddMessage("A locked door blocks your path. Find the key.");
                return;
            }
        }

        // Secret wall - reveal it and pass through
        if (targetTile == Tile.SecretWall)
        {
            _map[newX, newY] = Tile.Floor;
            AddMessage("The wall was never real. You pass through.");
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
                AddMessage("The stairs whisper your name. Press E to descend.");
                break;
            case Tile.Elevator:
                AddMessage("An elevator! Press Q or E to use it. Destination unknown.");
                break;
            case Tile.Teleporter:
                AddMessage("A teleporter hums beneath you. Press T or E to vanish.");
                break;
            case Tile.SafeRoom:
                if (_rng.Next(100) < 30)
                    AddMessage("This room feels... safe? Nothing bad here. Promise.");
                break;
        }
    }

    private void AttackEnemy(Enemy enemy)
    {
        int damage = Math.Max(1, _player.Attack - enemy.Defense + _rng.Next(-2, 3));
        enemy.Health -= damage;
        AddMessage(GetCreepyAttackMessage(enemy, damage));

        if (enemy.Health <= 0)
        {
            _enemies.Remove(enemy);
            int xpGain = enemy.XPValue;
            _player.XP += xpGain;
            int goldDrop = _rng.Next(1, 10) * _currentFloor;
            _player.Gold += goldDrop;
            AddMessage(GetCreepyDeathMessage(enemy, xpGain, goldDrop));

            // Check for level up
            CheckLevelUp();
        }
    }

    private void ProcessEnemyTurns()
    {
        foreach (var enemy in _enemies.ToList())
        {
            // Simple AI: move toward player if visible
            if (!_visible[enemy.X, enemy.Y])
                continue;

            int dx = Math.Sign(_player.X - enemy.X);
            int dy = Math.Sign(_player.Y - enemy.Y);

            // Try to move toward player
            int newX = enemy.X + dx;
            int newY = enemy.Y + dy;

            // Check if adjacent to player - attack!
            if (Math.Abs(_player.X - enemy.X) <= 1 && Math.Abs(_player.Y - enemy.Y) <= 1 &&
                (_player.X != enemy.X || _player.Y != enemy.Y))
            {
                int damage = Math.Max(1, enemy.Attack - _player.Defense + _rng.Next(-2, 3));
                _player.Health -= damage;
                AddMessage(GetCreepyEnemyAttackMessage(enemy, damage));
            }
            else if (CanMoveTo(newX, newY) && !_enemies.Any(e => e != enemy && e.X == newX && e.Y == newY))
            {
                enemy.X = newX;
                enemy.Y = newY;
            }
            else
            {
                // Try alternative moves
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
                var (weaponName, weaponIcon, atkBonus, weaponMsg) = GetWeaponForFloor();
                _player.Attack += atkBonus;
                AddMessage($"You found {weaponName}! {weaponMsg} ATK +{atkBonus}");
                break;

            case ItemType.Armor:
                var (armorName, armorIcon, defBonus, armorMsg) = GetArmorForFloor();
                _player.Defense += defBonus;
                AddMessage($"You found {armorName}! {armorMsg} DEF +{defBonus}");
                break;

            case ItemType.Key:
                _hasKey = true;
                _keyPosition = null;
                _inventory.Add(new InventoryItem { Icon = "K", Name = "Rusty Key", Type = ItemType.Key, Quantity = 1 });
                string[] keyMessages = {
                    "A key! It hums with purpose. Somewhere, a door feels nervous.",
                    "The key chose you. Or did you choose it? Does it matter?",
                    "You picked up a key. It's been waiting for you.",
                    "Key acquired. Something locked wants to meet you.",
                    "The key is warm. Someone was holding it recently."
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
            TrapType.Spike => "The floor gives you a hug. A sharp, pointy hug.",
            TrapType.Poison => "Something blew you a kiss. It tasted like regret.",
            TrapType.Fire => "The floor loved you so much it got warm and fuzzy.",
            TrapType.Hug => "Something invisible hugged you. It's still there.",
            TrapType.Lullaby => "A lullaby plays. You forget how to be awake.",
            _ => "Something happened. We don't talk about it."
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
                    AddMessage("You're full. The juice is disappointed.");
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
            _player.MaxHealth += 10;
            _player.Health = _player.MaxHealth;
            _player.Attack += 2;
            _player.Defense += 1;
            AddMessage(GetCreepyLevelUpMessage());
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
            GameOverStats.Text = $"You fell asleep. Forever.\nThe dungeon will remember you fondly.\nFloor {_currentFloor} | Gold: {_player.Gold} | Level: {_player.Level}";
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
                AddMessage("The wall whispers secrets. It remembers your touch.");
                break;
            default:
                AddMessage("Nothing to interact with here.");
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
            AddMessage($"The elevator takes you UP {floorsToMove} floor(s). It smiles.");
        }
        else
        {
            int newFloor = _currentFloor + floorsToMove;
            if (newFloor > MaxFloors)
            {
                _victory = true;
                VictoryStats.Text = $"The elevator took you beyond.\nIt knew where you needed to go.\n\nGold: {_player.Gold} | Level: {_player.Level}";
                VictoryOverlay.Visibility = Visibility.Visible;
                return;
            }
            _currentFloor = newFloor;
            AddMessage($"The elevator takes you DOWN {floorsToMove} floor(s). It giggles.");
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
                "Reality blinks. You're somewhere else now.",
                "The teleporter swallows you. Then spits you out.",
                "ZZzzzap! The other teleporter missed you.",
                "You feel your atoms rearrange. Mostly correctly.",
                "The journey took 0.0001 seconds. It felt like forever."
            };
            AddMessage(messages[_rng.Next(messages.Length)]);
            
            // Small disorientation damage
            if (_rng.Next(100) < 15)
            {
                int damage = _rng.Next(1, 5);
                _player.Health -= damage;
                AddMessage($"Teleportation sickness. (-{damage} HP)");
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
                "The door accepts your offering. It opens... reluctantly.",
                "Click. The lock remembers when it was made. It's tired now.",
                "The door swings open. Something was waiting on the other side.",
                "Unlocked. The door whispers 'finally' as it opens."
            };
            AddMessage(messages[_rng.Next(messages.Length)]);
            UpdateInventoryUI();
        }
        else
        {
            string[] messages = {
                "The door won't budge. It knows you don't have the key.",
                "Locked. The door laughs. Have you tried finding the key?",
                "This door requires a key. Keys are real. Find one.",
                "The lock stares at you. You don't have what it wants."
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

    private void CreateTileRectangles()
    {
        GameCanvas.Children.Clear();
        _tileRectangles.Clear();
        _entityLabels.Clear();

        for (int y = 0; y < MapHeight; y++)
        {
            for (int x = 0; x < MapWidth; x++)
            {
                var rect = new Rectangle
                {
                    Width = TileSize,
                    Height = TileSize,
                    Fill = FogBrush
                };
                Canvas.SetLeft(rect, x * TileSize);
                Canvas.SetTop(rect, y * TileSize);
                GameCanvas.Children.Add(rect);
                _tileRectangles.Add(rect);
            }
        }
    }

    private void Render()
    {
        // Remove old entity sprites
        foreach (var label in _entityLabels)
        {
            GameCanvas.Children.Remove(label);
        }
        _entityLabels.Clear();

        // Render tiles
        for (int y = 0; y < MapHeight; y++)
        {
            for (int x = 0; x < MapWidth; x++)
            {
                int index = y * MapWidth + x;
                var rect = _tileRectangles[index];

                if (_visible[x, y])
                {
                    rect.Fill = _map[x, y] switch
                    {
                        Tile.Wall => WallBrush,
                        Tile.Floor => FloorBrush,
                        Tile.Stairs => FloorBrush, // We'll draw stairs sprite on top
                        Tile.Elevator => FloorBrush, // Draw elevator sprite on top
                        Tile.Teleporter => FloorBrush, // Draw teleporter sprite on top
                        Tile.LockedDoor => FloorBrush, // Draw door sprite on top
                        Tile.SafeRoom => new SolidColorBrush(Color.FromRgb(40, 60, 40)), // Soft green tint
                        Tile.SecretWall => WallBrush, // Looks like wall until discovered
                        _ => FloorBrush
                    };
                }
                else if (_explored[x, y])
                {
                    rect.Fill = ExploredFogBrush;
                }
                else
                {
                    rect.Fill = FogBrush;
                }
            }
        }

        // Render traps (only if visible and triggered)
        foreach (var trap in _traps)
        {
            if (_visible[trap.X, trap.Y] && trap.Triggered)
            {
                DrawSprite(trap.X, trap.Y, GetTrapSprite(trap.Type));
            }
        }

        // Render items
        foreach (var item in _items)
        {
            if (_visible[item.X, item.Y])
            {
                DrawSprite(item.X, item.Y, GetItemSprite(item.Type));
            }
        }

        // Render stairs
        if (_visible[_stairsPosition.X, _stairsPosition.Y])
        {
            DrawSprite(_stairsPosition.X, _stairsPosition.Y, GetStairsSprite());
        }

        // Render elevator
        if (_elevatorPosition.HasValue && _visible[_elevatorPosition.Value.X, _elevatorPosition.Value.Y])
        {
            DrawSprite(_elevatorPosition.Value.X, _elevatorPosition.Value.Y, GetElevatorSprite());
        }

        // Render teleporters
        foreach (var tele in _teleporters)
        {
            if (_visible[tele.X, tele.Y])
            {
                DrawSprite(tele.X, tele.Y, GetTeleporterSprite());
            }
        }

        // Render locked door
        if (_lockedDoorPosition.HasValue && _visible[_lockedDoorPosition.Value.X, _lockedDoorPosition.Value.Y])
        {
            DrawSprite(_lockedDoorPosition.Value.X, _lockedDoorPosition.Value.Y, GetLockedDoorSprite());
        }

        // Render secret walls (only visible when explored but appear as slightly different)
        for (int y = 0; y < MapHeight; y++)
        {
            for (int x = 0; x < MapWidth; x++)
            {
                if (_map[x, y] == Tile.SecretWall && _visible[x, y])
                {
                    DrawSprite(x, y, GetSecretWallSprite());
                }
            }
        }

        // Render enemies
        foreach (var enemy in _enemies)
        {
            if (_visible[enemy.X, enemy.Y])
            {
                DrawSprite(enemy.X, enemy.Y, GetEnemySprite(enemy.Type));
            }
        }

        // Render player
        DrawSprite(_player.X, _player.Y, GetPlayerSprite());
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
            "You wake up here. You don't remember entering.",
            "The dungeon was expecting you. It made snacks.",
            "Welcome back. We missed you. We always miss you.",
            "Floor 1. The walls hum a lullaby you almost remember.",
            "You've been here before. You just don't remember leaving."
        };
        return messages[_rng.Next(messages.Length)];
    }

    private string GetCreepyAttackMessage(Enemy enemy, int damage)
    {
        string[] messages = {
            $"You gently bonk the {enemy.Name}. It hurts them ({damage}). They look confused.",
            $"You poke the {enemy.Name} with malice. {damage} damage. It smiles anyway.",
            $"The {enemy.Name} receives your {damage} damage like a gift.",
            $"You inflict {damage} friendship upon the {enemy.Name}.",
            $"The {enemy.Name} takes {damage} damage. It thanks you with its eyes."
        };
        return messages[_rng.Next(messages.Length)];
    }

    private string GetCreepyDeathMessage(Enemy enemy, int xp, int gold)
    {
        string[] messages = {
            $"The {enemy.Name} stops moving. It's still smiling. +{xp} XP, +{gold} teeth.",
            $"{enemy.Name} melts into the floor, waving goodbye. +{xp} memories, +{gold} gold.",
            $"The {enemy.Name} was never real. But the {xp} XP is. And so is the {gold} gold.",
            $"{enemy.Name} pops like a balloon. Confetti? No, that's {gold} gold and {xp} regrets.",
            $"The {enemy.Name} thanks you for releasing it. +{xp} XP. Its {gold} gold was for you all along."
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
            $"You found {gold} gold. Each coin has your face on it.",
            $"{gold} gold! It's warm and slightly damp.",
            $"You collect {gold} gold. The previous owner won't need it anymore.",
            $"{gold} teeth- I mean gold. Definitely gold.",
            $"The floor coughs up {gold} gold for you. Good floor."
        };
        return messages[_rng.Next(messages.Length)];
    }

    private string GetCreepyPotionPickupMessage()
    {
        string[] messages = {
            "You found Forbidden Juice. It's warm. Why is it warm?",
            "A juice box! The label says 'Drink Me'. In your handwriting.",
            "You found a sippy cup of something. It sloshes eagerly.",
            "Mystery juice! It changes color when you're not looking.",
            "You found liquid comfort. It hums a lullaby."
        };
        return messages[_rng.Next(messages.Length)];
    }

    private (string Name, string Icon, int Bonus, string Message) GetWeaponForFloor()
    {
        // Weapons themed by floor area
        var nurseryWeapons = new[] {
            ("Baby Rattle of Doom", "🍼", 3, "It makes soothing sounds as it strikes."),
            ("Teething Ring", "⭕", 3, "Someone else's teeth marks are on it."),
            ("Mobile Star", "⭐", 4, "It fell from above your crib. Finally."),
        };
        
        var playgroundWeapons = new[] {
            ("Rusty Swing Chain", "⛓️", 5, "Still has momentum from the last child."),
            ("Jump Rope of Binding", "🪢", 5, "It ties itself to enemies."),
            ("Tetherball of Regret", "🏐", 6, "It always comes back."),
            ("Splinter Stick", "🪵", 4, "From the old wooden playground. The one they tore down."),
        };
        
        var schoolWeapons = new[] {
            ("Hall Pass", "📝", 7, "Allows you to go anywhere. ANYWHERE."),
            ("Detention Slip", "📄", 7, "Write someone's name. They stay."),
            ("Safety Scissors", "✂️", 8, "Not safe. Never were."),
            ("Cafeteria Spork", "🥄", 6, "It's seen things."),
        };
        
        var homeWeapons = new[] {
            ("Wooden Spoon", "🥄", 9, "Mom's. Still has the sting."),
            ("Photo Album", "📔", 9, "The faces blur when you strike."),
            ("Attic Key", "🗝️", 10, "Opens things that should stay closed."),
            ("Family Recipe", "📜", 8, "The secret ingredient is violence."),
        };
        
        var endWeapons = new[] {
            ("Mirror Shard", "🔮", 12, "Your reflection keeps attacking."),
            ("Yesterday's Regret", "💭", 11, "Weaponized nostalgia."),
            ("The Truth", "👁️", 14, "Nobody can handle it."),
            ("Goodbye Letter", "💌", 13, "You never sent it. Until now."),
        };

        var weapons = _currentFloor switch
        {
            <= 2 => nurseryWeapons,
            <= 4 => playgroundWeapons,
            <= 6 => schoolWeapons,
            <= 8 => homeWeapons,
            _ => endWeapons
        };

        return weapons[_rng.Next(weapons.Length)];
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
            $"You drink the juice. It tastes like nostalgia. +{heal} HP.",
            $"The liquid whispers encouragement as you drink. +{heal} HP.",
            $"Gulp. The juice liked being inside you. +{heal} HP.",
            $"It tastes like strawberries and static. +{heal} HP.",
            $"You feel better! The juice is proud of you. +{heal} HP."
        };
        return messages[_rng.Next(messages.Length)];
    }

    private string GetCreepyLevelUpMessage()
    {
        string[] messages = {
            $"🎈 You grew stronger! Level {_player.Level}! The dungeon noticed.",
            $"✨ Level {_player.Level}! Your cells rearranged themselves. Don't think about it.",
            $"🌟 LEVEL {_player.Level}! Something in you woke up. It's hungry.",
            $"🎉 Level {_player.Level}! The walls applaud. Thousands of tiny hands.",
            $"⭐ You are now Level {_player.Level}. The old you would be so proud. Or scared."
        };
        return messages[_rng.Next(messages.Length)];
    }

    private string GetCreepyFloorMessage()
    {
        string[] messages = {
            $"Floor {_currentFloor}. The stairs thanked you for using them.",
            $"You descend to Floor {_currentFloor}. The previous floor is already forgetting you.",
            $"Floor {_currentFloor}. It smells like childhood birthday parties.",
            $"Welcome to Floor {_currentFloor}. Someone was just here. They looked like you.",
            $"Floor {_currentFloor}. The darkness down here is softer. Friendlier."
        };
        return messages[_rng.Next(messages.Length)];
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

    private class Player
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Health { get; set; }
        public int MaxHealth { get; set; }
        public int Attack { get; set; }
        public int Defense { get; set; }
        public int Level { get; set; }
        public int XP { get; set; }
        public int Gold { get; set; }
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
    }

    private class Item
    {
        public int X { get; set; }
        public int Y { get; set; }
        public ItemType Type { get; set; }
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
    #endregion
}
