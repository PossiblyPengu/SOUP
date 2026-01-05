using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SOUP.Windows;

/// <summary>
/// A first-person, turn-based dungeon crawler in the style of Legend of Grimrock.
/// Grid-based movement with single character.
/// </summary>
public partial class DungeonCrawler : Window
{
    #region Constants
    private const int MapWidth = 24;
    private const int MapHeight = 24;
    private const int ViewDistance = 4;
    #endregion

    #region Enums
    private enum Direction { North, East, South, West }
    private enum TileType { Floor, Wall, Door, StairsDown, Chest, Trap, Shrine }
    private enum EnemyType { SmileDog, MeatChild, GrandmasTwin, ManInWall, FriendlyHelper, YourReflection, ItsListening, TheHost }
    #endregion

    #region Animation State
    private DispatcherTimer _animationTimer = null!;
    private double _animationTime = 0;
    private double _torchFlicker = 1.0;
    private double _eyePulse = 1.0;
    private double _chainSway = 0;
    private double _dripOffset = 0;
    private double _dustFloat = 0;
    private double _creepyBreathing = 0;
    private List<(double x, double y, double speed, double phase)> _dustParticles = new();
    private List<(double x, double offset, double speed)> _dripAnimations = new();
    #endregion

    #region Player State
    private int _playerX, _playerY;
    private Direction _playerDir = Direction.North;
    private int _currentFloor = 1;

    private int _maxHealth = 100;
    private int _health = 100;
    private int _maxMana = 50;
    private int _mana = 50;
    private int _attack = 12;
    private int _defense = 8;
    private int _level = 1;
    private int _xp = 0;
    private int _xpToLevel = 100;
    private int _gold = 0;
    private bool _isDefending = false;
    #endregion

    #region Map State
    private TileType[,] _map = new TileType[MapWidth, MapHeight];
    private bool[,] _explored = new bool[MapWidth, MapHeight];
    private Dictionary<(int, int), Enemy> _enemies = new();
    private HashSet<(int, int)> _openedChests = new();
    private HashSet<(int, int)> _usedShrines = new();
    #endregion

    #region Combat State
    private Enemy? _currentEnemy;
    private bool _inCombat = false;
    #endregion

    #region Collections
    private readonly ObservableCollection<InventoryItem> _inventory = new();
    private readonly ObservableCollection<string> _messages = new();
    private readonly Random _random = new();
    #endregion

    public DungeonCrawler()
    {
        InitializeComponent();
        InventoryList.ItemsSource = _inventory;
        MessageLog.ItemsSource = _messages;
        
        // Initialize animation timer
        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50) // 20 FPS for animations
        };
        _animationTimer.Tick += AnimationTick;
        
        // Initialize dust particles
        InitializeParticles();
    }

    private void InitializeParticles()
    {
        _dustParticles.Clear();
        _dripAnimations.Clear();
        
        // Create floating dust particles
        for (int i = 0; i < 20; i++)
        {
            _dustParticles.Add((
                _random.NextDouble() * 700,
                _random.NextDouble() * 400,
                0.3 + _random.NextDouble() * 0.7,
                _random.NextDouble() * Math.PI * 2
            ));
        }
        
        // Create drip animations
        for (int i = 0; i < 8; i++)
        {
            _dripAnimations.Add((
                50 + _random.NextDouble() * 600,
                _random.NextDouble() * 50,
                0.5 + _random.NextDouble() * 1.5
            ));
        }
    }

    private void AnimationTick(object? sender, EventArgs e)
    {
        _animationTime += 0.05;
        
        // Torch flicker (random with smoothing)
        double targetFlicker = 0.7 + _random.NextDouble() * 0.3;
        _torchFlicker = _torchFlicker * 0.8 + targetFlicker * 0.2;
        
        // Pulsing eyes
        _eyePulse = 0.6 + Math.Sin(_animationTime * 3) * 0.4;
        
        // Chain sway
        _chainSway = Math.Sin(_animationTime * 1.5) * 5;
        
        // Drip animation
        _dripOffset = (_dripOffset + 2) % 60;
        
        // Dust floating
        _dustFloat = Math.Sin(_animationTime * 0.5) * 10;
        
        // Creepy breathing effect for ambient
        _creepyBreathing = Math.Sin(_animationTime * 0.8) * 0.5 + 0.5;
        
        // Update dust particle positions
        for (int i = 0; i < _dustParticles.Count; i++)
        {
            var (x, y, speed, phase) = _dustParticles[i];
            double newX = x + Math.Sin(_animationTime * speed + phase) * 0.5;
            double newY = y - speed * 0.3;
            if (newY < -10) newY = 410;
            _dustParticles[i] = (newX, newY, speed, phase);
        }
        
        // Re-render with animations
        if (!_inCombat)
        {
            RenderDungeonView();
        }
        else if (_currentEnemy != null)
        {
            // Animate combat sprite
            DrawEnemySprite(_currentEnemy);
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        StartNewGame();
        _animationTimer.Start();
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        _animationTimer.Stop();
    }

    #region Game Initialization
    private void StartNewGame()
    {
        // Reset player stats
        _currentFloor = 1;
        _maxHealth = 100;
        _health = 100;
        _maxMana = 50;
        _mana = 50;
        _attack = 12;
        _defense = 8;
        _level = 1;
        _xp = 0;
        _xpToLevel = 100;
        _gold = 0;
        _playerDir = Direction.North;

        // Clear collections
        _inventory.Clear();
        _messages.Clear();
        _openedChests.Clear();
        _usedShrines.Clear();
        _enemies.Clear();

        // Starting items
        _inventory.Add(new InventoryItem("�", "Rusty Knife", 1));
        _inventory.Add(new InventoryItem("💊", "Mystery Pills", 2));

        // Generate first floor
        GenerateDungeon();
        
        // Hide overlays
        GameOverOverlay.Visibility = Visibility.Collapsed;
        CombatOverlay.Visibility = Visibility.Collapsed;
        
        // Initial message
        AddMessage("Welcome back, friend. We missed you. :)");
        AddMessage("Go deeper. You belong here.");
        
        UpdateUI();
        RenderDungeonView();
    }

    private void GenerateDungeon()
    {
        // Initialize map with walls
        for (int x = 0; x < MapWidth; x++)
            for (int y = 0; y < MapHeight; y++)
            {
                _map[x, y] = TileType.Wall;
                _explored[x, y] = false;
            }

        // Use recursive backtracking to generate maze
        GenerateMaze(1, 1);

        // Place player at start
        _playerX = 1;
        _playerY = 1;

        // Place stairs down (far from player)
        PlaceStairs();

        // Place features
        int featureCount = 5 + _currentFloor * 2;
        for (int i = 0; i < featureCount; i++)
        {
            PlaceRandomFeature();
        }

        // Place enemies
        int enemyCount = 3 + _currentFloor * 2;
        _enemies.Clear();
        for (int i = 0; i < enemyCount; i++)
        {
            PlaceEnemy();
        }
    }

    private void GenerateMaze(int startX, int startY)
    {
        var stack = new Stack<(int x, int y)>();
        _map[startX, startY] = TileType.Floor;
        stack.Push((startX, startY));

        var directions = new (int dx, int dy)[] { (0, -2), (2, 0), (0, 2), (-2, 0) };

        while (stack.Count > 0)
        {
            var (cx, cy) = stack.Peek();
            var unvisited = directions
                .Select(d => (nx: cx + d.dx, ny: cy + d.dy))
                .Where(p => p.nx > 0 && p.nx < MapWidth - 1 && p.ny > 0 && p.ny < MapHeight - 1)
                .Where(p => _map[p.nx, p.ny] == TileType.Wall)
                .ToList();

            if (unvisited.Count > 0)
            {
                var (nx, ny) = unvisited[_random.Next(unvisited.Count)];
                // Carve passage
                _map[(cx + nx) / 2, (cy + ny) / 2] = TileType.Floor;
                _map[nx, ny] = TileType.Floor;
                stack.Push((nx, ny));
            }
            else
            {
                stack.Pop();
            }
        }

        // Add some extra passages for less linear maze
        for (int i = 0; i < MapWidth * MapHeight / 20; i++)
        {
            int x = _random.Next(2, MapWidth - 2);
            int y = _random.Next(2, MapHeight - 2);
            if (_map[x, y] == TileType.Wall)
            {
                int floorNeighbors = 0;
                if (_map[x - 1, y] == TileType.Floor) floorNeighbors++;
                if (_map[x + 1, y] == TileType.Floor) floorNeighbors++;
                if (_map[x, y - 1] == TileType.Floor) floorNeighbors++;
                if (_map[x, y + 1] == TileType.Floor) floorNeighbors++;
                if (floorNeighbors >= 2)
                    _map[x, y] = TileType.Floor;
            }
        }
    }

    private void PlaceStairs()
    {
        // Find floor tile farthest from player
        int bestX = 1, bestY = 1;
        int bestDist = 0;

        for (int x = 1; x < MapWidth - 1; x++)
        {
            for (int y = 1; y < MapHeight - 1; y++)
            {
                if (_map[x, y] == TileType.Floor)
                {
                    int dist = Math.Abs(x - _playerX) + Math.Abs(y - _playerY);
                    if (dist > bestDist)
                    {
                        bestDist = dist;
                        bestX = x;
                        bestY = y;
                    }
                }
            }
        }

        _map[bestX, bestY] = TileType.StairsDown;
    }

    private void PlaceRandomFeature()
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            int x = _random.Next(1, MapWidth - 1);
            int y = _random.Next(1, MapHeight - 1);

            if (_map[x, y] == TileType.Floor && (x != _playerX || y != _playerY))
            {
                int roll = _random.Next(100);
                if (roll < 40)
                    _map[x, y] = TileType.Chest;
                else if (roll < 70)
                    _map[x, y] = TileType.Trap;
                else
                    _map[x, y] = TileType.Shrine;
                return;
            }
        }
    }

    private void PlaceEnemy()
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            int x = _random.Next(1, MapWidth - 1);
            int y = _random.Next(1, MapHeight - 1);

            if (_map[x, y] == TileType.Floor && !_enemies.ContainsKey((x, y)))
            {
                int dist = Math.Abs(x - _playerX) + Math.Abs(y - _playerY);
                if (dist > 3)  // Not too close to start
                {
                    _enemies[(x, y)] = CreateEnemy();
                    return;
                }
            }
        }
    }

    private Enemy CreateEnemy()
    {
        // Enemy types based on floor
        var availableTypes = new List<EnemyType> { EnemyType.SmileDog, EnemyType.MeatChild };
        
        if (_currentFloor >= 2) availableTypes.AddRange(new[] { EnemyType.GrandmasTwin, EnemyType.ManInWall });
        if (_currentFloor >= 3) availableTypes.AddRange(new[] { EnemyType.FriendlyHelper, EnemyType.YourReflection });
        if (_currentFloor >= 5) availableTypes.Add(EnemyType.ItsListening);
        if (_currentFloor >= 7) availableTypes.Add(EnemyType.TheHost);

        var type = availableTypes[_random.Next(availableTypes.Count)];
        int floorBonus = (_currentFloor - 1) * 5;

        return type switch
        {
            EnemyType.SmileDog => new Enemy("Smile Dog :)", 15 + floorBonus, 15 + floorBonus, 5 + _currentFloor, 2, 10, 5),
            EnemyType.MeatChild => new Enemy("The Meat Child", 20 + floorBonus, 20 + floorBonus, 7 + _currentFloor, 3, 15, 8),
            EnemyType.GrandmasTwin => new Enemy("Grandma's Twin", 35 + floorBonus, 35 + floorBonus, 10 + _currentFloor, 5, 25, 15),
            EnemyType.ManInWall => new Enemy("Man In The Wall", 25 + floorBonus, 25 + floorBonus, 12 + _currentFloor, 8, 30, 20),
            EnemyType.FriendlyHelper => new Enemy("Your Friendly Helper", 50 + floorBonus, 50 + floorBonus, 14 + _currentFloor, 6, 35, 25),
            EnemyType.YourReflection => new Enemy("Your Reflection", 40 + floorBonus, 40 + floorBonus, 16 + _currentFloor, 10, 45, 35),
            EnemyType.ItsListening => new Enemy("It's Listening", 80 + floorBonus, 80 + floorBonus, 18 + _currentFloor, 15, 60, 50),
            EnemyType.TheHost => new Enemy("THE HOST", 120 + floorBonus, 120 + floorBonus, 25 + _currentFloor, 12, 100, 100),
            _ => new Enemy("Friend :)", 20 + floorBonus, 20 + floorBonus, 8 + _currentFloor, 4, 20, 10)
        };
    }
    #endregion

    #region Movement & Actions
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (_inCombat) return;
        if (GameOverOverlay.Visibility == Visibility.Visible) return;

        switch (e.Key)
        {
            case Key.W: MoveForward(); break;
            case Key.S: MoveBackward(); break;
            case Key.A: StrafeLeft(); break;
            case Key.D: StrafeRight(); break;
            case Key.Q: TurnLeft(); break;
            case Key.E: TurnRight(); break;
            case Key.Space: Interact(); break;
        }
    }

    private void MoveForward_Click(object sender, RoutedEventArgs e) { if (!_inCombat) MoveForward(); }
    private void MoveBackward_Click(object sender, RoutedEventArgs e) { if (!_inCombat) MoveBackward(); }
    private void StrafeLeft_Click(object sender, RoutedEventArgs e) { if (!_inCombat) StrafeLeft(); }
    private void StrafeRight_Click(object sender, RoutedEventArgs e) { if (!_inCombat) StrafeRight(); }
    private void TurnLeft_Click(object sender, RoutedEventArgs e) { if (!_inCombat) TurnLeft(); }
    private void TurnRight_Click(object sender, RoutedEventArgs e) { if (!_inCombat) TurnRight(); }

    private (int dx, int dy) GetDirectionVector(Direction dir)
    {
        return dir switch
        {
            Direction.North => (0, -1),
            Direction.East => (1, 0),
            Direction.South => (0, 1),
            Direction.West => (-1, 0),
            _ => (0, 0)
        };
    }

    private void MoveForward()
    {
        var (dx, dy) = GetDirectionVector(_playerDir);
        TryMove(_playerX + dx, _playerY + dy);
    }

    private void MoveBackward()
    {
        var (dx, dy) = GetDirectionVector(_playerDir);
        TryMove(_playerX - dx, _playerY - dy);
    }

    private void StrafeLeft()
    {
        var leftDir = (Direction)(((int)_playerDir + 3) % 4);
        var (dx, dy) = GetDirectionVector(leftDir);
        TryMove(_playerX + dx, _playerY + dy);
    }

    private void StrafeRight()
    {
        var rightDir = (Direction)(((int)_playerDir + 1) % 4);
        var (dx, dy) = GetDirectionVector(rightDir);
        TryMove(_playerX + dx, _playerY + dy);
    }

    private void TurnLeft()
    {
        _playerDir = (Direction)(((int)_playerDir + 3) % 4);
        UpdateCompass();
        RenderDungeonView();
    }

    private void TurnRight()
    {
        _playerDir = (Direction)(((int)_playerDir + 1) % 4);
        UpdateCompass();
        RenderDungeonView();
    }

    private void TryMove(int newX, int newY)
    {
        if (newX < 0 || newX >= MapWidth || newY < 0 || newY >= MapHeight)
            return;

        var tile = _map[newX, newY];
        if (tile == TileType.Wall)
        {
            AddMessage("A solid wall blocks your path.");
            return;
        }

        // Check for enemy
        if (_enemies.TryGetValue((newX, newY), out var enemy))
        {
            StartCombat(enemy, newX, newY);
            return;
        }

        // Move player
        _playerX = newX;
        _playerY = newY;
        _explored[newX, newY] = true;
        _isDefending = false;

        // Check tile effects
        HandleTileEffect(tile);

        UpdateUI();
        RenderDungeonView();
    }

    private void HandleTileEffect(TileType tile)
    {
        switch (tile)
        {
            case TileType.Trap:
                if (_random.Next(100) < 60)
                {
                    int damage = 5 + _currentFloor * 3;
                    _health -= damage;
                    AddMessage($"You triggered a trap! Took {damage} damage.");
                    _map[_playerX, _playerY] = TileType.Floor; // Disarm
                    CheckDeath();
                }
                else
                {
                    AddMessage("You carefully avoid a trap.");
                    _map[_playerX, _playerY] = TileType.Floor;
                }
                break;

            case TileType.Chest:
                if (!_openedChests.Contains((_playerX, _playerY)))
                {
                    OpenChest();
                    _openedChests.Add((_playerX, _playerY));
                }
                break;

            case TileType.Shrine:
                ShowInteractionPrompt("Press SPACE to pray at the shrine");
                break;

            case TileType.StairsDown:
                ShowInteractionPrompt("Press SPACE to descend");
                break;
        }
    }

    private void Interact()
    {
        var tile = _map[_playerX, _playerY];

        if (tile == TileType.StairsDown)
        {
            DescendFloor();
        }
        else if (tile == TileType.Shrine && !_usedShrines.Contains((_playerX, _playerY)))
        {
            UseShrine();
        }

        HideInteractionPrompt();
    }

    private static readonly string[] _firearmNames = {
        "Grandpa's Revolver", "Tactical Glock", "Suspicious Shotgun", "Mall Ninja AR-15",
        "Haunted Derringer", "Flesh Pistol", "Teeth Launcher", "Friendship Ender",
        "The Negotiator", "Uncle's 'Nam Gun", "Cursed Musket", "Smile Deleter"
    };
    
    private static readonly string[] _armorNames = {
        "Stained Hoodie", "Grandma's Sweater", "Meat Vest", "Friend's Skin",
        "Tactical Bathrobe", "Cursed Crocs", "Mall Security Vest", "Tooth Necklace"
    };

    private void OpenChest()
    {
        int roll = _random.Next(100);
        
        if (roll < 25)
        {
            int goldFound = 10 + _random.Next(_currentFloor * 15);
            _gold += goldFound;
            AddMessage($"Found {goldFound} teeth! (they work as currency here)");
        }
        else if (roll < 50)
        {
            var potion = _inventory.FirstOrDefault(i => i.Name == "Mystery Pills");
            if (potion != null)
                potion.Quantity++;
            else
                _inventory.Add(new InventoryItem("💊", "Mystery Pills", 1));
            AddMessage("Found some unlabeled pills! They look friendly.");
        }
        else if (roll < 70)
        {
            var potion = _inventory.FirstOrDefault(i => i.Name == "Suspicious Juice");
            if (potion != null)
                potion.Quantity++;
            else
                _inventory.Add(new InventoryItem("🧃", "Suspicious Juice", 1));
            AddMessage("Found a juice box! It's warm. And pulsing.");
        }
        else
        {
            // Equipment upgrade - firearms!
            int bonus = _random.Next(2, 5);
            if (_random.Next(2) == 0)
            {
                string gun = _firearmNames[_random.Next(_firearmNames.Length)];
                _attack += bonus;
                _inventory.Add(new InventoryItem("🔫", gun, 1));
                AddMessage($"Found {gun}! ATK +{bonus}. It whispers your name.");
            }
            else
            {
                string armor = _armorNames[_random.Next(_armorNames.Length)];
                _defense += bonus;
                _inventory.Add(new InventoryItem("🧥", armor, 1));
                AddMessage($"Found {armor}! DEF +{bonus}. It fits perfectly. Too perfectly.");
            }
        }

        _map[_playerX, _playerY] = TileType.Floor;
        UpdateUI();
    }

    private static readonly string[] _shrineMessages = {
        "The shrine restores you. It remembers you. It always has.",
        "You feel better! Something else feels better too. Inside you.",
        "Healed! The shrine smiles. You didn't know shrines could smile.",
        "Vitality restored! You hear distant applause.",
        "The shrine whispers 'good job, friend' as it heals you.",
        "You're whole again! The shrine says 'see you soon :)'"
    };
    
    private void UseShrine()
    {
        _health = _maxHealth;
        _mana = _maxMana;
        AddMessage(_shrineMessages[_random.Next(_shrineMessages.Length)]);
        _usedShrines.Add((_playerX, _playerY));
        UpdateUI();
    }

    private void DescendFloor()
    {
        _currentFloor++;
        _openedChests.Clear();
        _usedShrines.Clear();
        GenerateDungeon();
        var floorMessages = new[] {
            $"Floor {_currentFloor}. The walls here look familiar.",
            $"Floor {_currentFloor}. Was that breathing?",
            $"Floor {_currentFloor}. Home sweet home :)",
            $"Floor {_currentFloor}. You've been here before. You know you have.",
            $"Floor {_currentFloor}. The darkness waves hello."
        };
        AddMessage(floorMessages[_random.Next(floorMessages.Length)]);
        UpdateUI();
        RenderDungeonView();
    }

    private void ShowInteractionPrompt(string text)
    {
        InteractionText.Text = text;
        InteractionPrompt.Visibility = Visibility.Visible;
    }

    private void HideInteractionPrompt()
    {
        InteractionPrompt.Visibility = Visibility.Collapsed;
    }
    #endregion

    #region Combat System
    private void StartCombat(Enemy enemy, int ex, int ey)
    {
        _currentEnemy = enemy;
        _inCombat = true;
        _isDefending = false;

        EnemyNameText.Text = enemy.Name;
        UpdateEnemyHealth();
        DrawEnemySprite(enemy);

        CombatOverlay.Visibility = Visibility.Visible;
        AddMessage($"A {enemy.Name} attacks!");
    }

    private void CombatAttack_Click(object sender, RoutedEventArgs e)
    {
        if (_currentEnemy == null) return;
        PlayerAttack();
    }

    private void CombatDefend_Click(object sender, RoutedEventArgs e)
    {
        if (_currentEnemy == null) return;
        _isDefending = true;
        AddMessage("You curl into a ball and whimper softly.");
        EnemyTurn();
    }

    private void CombatSpell_Click(object sender, RoutedEventArgs e)
    {
        if (_currentEnemy == null) return;

        if (_mana < 10)
        {
            AddMessage("Not enough mana!");
            return;
        }

        _mana -= 10;
        int damage = _attack + _level * 3 + _random.Next(10, 20);
        _currentEnemy.Health -= damage;
        var prayerResults = new[] {
            $"Your prayers are answered! {damage} damage!",
            $"Something hears you. It helps. {damage} damage.",
            $"The void responds with {damage} points of friendship.",
            $"A warm hand touches your shoulder. {damage} damage dealt."
        };
        AddMessage(prayerResults[_random.Next(prayerResults.Length)]);

        UpdateUI();
        CheckCombatEnd();
    }

    private void CombatFlee_Click(object sender, RoutedEventArgs e)
    {
        if (_random.Next(100) < 40 + _level * 5)
        {
            AddMessage("You flee from combat!");
            EndCombat(false);
        }
        else
        {
            AddMessage("Failed to escape!");
            EnemyTurn();
        }
    }

    private void PlayerAttack()
    {
        if (_currentEnemy == null) return;

        int damage = Math.Max(1, _attack - _currentEnemy.Defense + _random.Next(-3, 6));
        _currentEnemy.Health -= damage;
        var attackMessages = new[] {
            $"BANG! {damage} damage! The sound echoes forever.",
            $"You shoot. {damage} damage. It thanks you.",
            $"PEW PEW! {damage} damage! Was that giggling?",
            $"{damage} damage! The gun feels warm and happy."
        };
        AddMessage(attackMessages[_random.Next(attackMessages.Length)]);

        UpdateEnemyHealth();
        CheckCombatEnd();
    }

    private void EnemyTurn()
    {
        if (_currentEnemy == null || !_inCombat) return;

        int damage = Math.Max(1, _currentEnemy.Attack - _defense + _random.Next(-2, 4));
        if (_isDefending)
        {
            damage /= 2;
            AddMessage($"You block! {_currentEnemy.Name} deals {damage} damage.");
        }
        else
        {
            AddMessage($"{_currentEnemy.Name} attacks for {damage} damage!");
        }

        _health -= damage;
        _isDefending = false;
        UpdateUI();
        CheckDeath();
    }

    private void CheckCombatEnd()
    {
        if (_currentEnemy == null) return;

        if (_currentEnemy.Health <= 0)
        {
            var defeatMessages = new[] {
                $"The {_currentEnemy.Name} stops moving. It's still smiling.",
                $"{_currentEnemy.Name} dissolves. 'See you tomorrow,' it whispers.",
                $"You 'befriended' the {_currentEnemy.Name}. Permanently.",
                $"The {_currentEnemy.Name} is gone. The silence is worse."
            };
            AddMessage(defeatMessages[_random.Next(defeatMessages.Length)]);
            int xpGain = _currentEnemy.XPReward;
            int goldGain = _currentEnemy.GoldReward;
            _xp += xpGain;
            _gold += goldGain;
            AddMessage($"Gained {xpGain} XP and {goldGain} teeth.");

            // Remove enemy from map
            var enemyPos = _enemies.FirstOrDefault(e => e.Value == _currentEnemy).Key;
            if (enemyPos != default)
                _enemies.Remove(enemyPos);

            CheckLevelUp();
            EndCombat(true);
        }
        else
        {
            EnemyTurn();
        }
    }

    private void EndCombat(bool victory)
    {
        _inCombat = false;
        _currentEnemy = null;
        CombatOverlay.Visibility = Visibility.Collapsed;
        UpdateUI();
        RenderDungeonView();
    }

    private void CheckLevelUp()
    {
        while (_xp >= _xpToLevel)
        {
            _xp -= _xpToLevel;
            _level++;
            _xpToLevel = (int)(_xpToLevel * 1.5);

            _maxHealth += 15;
            _health = _maxHealth;
            _maxMana += 10;
            _mana = _maxMana;
            _attack += 3;
            _defense += 2;

            var levelMessages = new[] {
                $"Level {_level}! You're becoming one of us :)",
                $"Level {_level}! The dungeon is so proud of you!",
                $"Level {_level}! Your teeth feel sharper.",
                $"Level {_level}! Something inside you grows stronger."
            };
            AddMessage(levelMessages[_random.Next(levelMessages.Length)]);
        }
    }

    private void UpdateEnemyHealth()
    {
        if (_currentEnemy == null) return;

        double healthPercent = (double)_currentEnemy.Health / _currentEnemy.MaxHealth;
        EnemyHealthBar.Width = 200 * Math.Max(0, healthPercent);
        EnemyHealthText.Text = $"{Math.Max(0, _currentEnemy.Health)}/{_currentEnemy.MaxHealth}";
    }

    private void DrawEnemySprite(Enemy enemy)
    {
        EnemySpriteCanvas.Children.Clear();
        
        // Use the same detailed sprite drawing for combat
        double cx = 64;
        double y = 10;
        double size = 100;

        switch (enemy.Name)
        {
            case "Smile Dog :)":
                DrawCombatSmileDog(cx, y, size);
                break;
            case "The Meat Child":
                DrawCombatMeatChild(cx, y, size);
                break;
            case "Grandma's Twin":
                DrawCombatGrandmasTwin(cx, y, size);
                break;
            case "Man In The Wall":
                DrawCombatManInWall(cx, y, size);
                break;
            case "Your Friendly Helper":
                DrawCombatFriendlyHelper(cx, y, size);
                break;
            case "Your Reflection":
                DrawCombatYourReflection(cx, y, size);
                break;
            case "It's Listening":
                DrawCombatItsListening(cx, y, size);
                break;
            case "THE HOST":
                DrawCombatTheHost(cx, y, size);
                break;
            default:
                DrawCombatGeneric(cx, y, size, Colors.Pink);
                break;
        }
    }

    private void DrawCombatSmileDog(double cx, double y, double size)
    {
        // Dog body
        var body = new Ellipse { Width = size * 0.8, Height = size * 0.6, Fill = new LinearGradientBrush(Colors.Tan, Colors.SaddleBrown, 90) };
        EnemySpriteCanvas.Children.Add(body);
        Canvas.SetLeft(body, cx - size * 0.4);
        Canvas.SetTop(body, y + size * 0.4);

        // Head
        var head = new Ellipse { Width = size * 0.6, Height = size * 0.5, Fill = new SolidColorBrush(Colors.Tan), Stroke = Brushes.SaddleBrown, StrokeThickness = 2 };
        EnemySpriteCanvas.Children.Add(head);
        Canvas.SetLeft(head, cx - size * 0.3);
        Canvas.SetTop(head, y);

        // Wide creepy smile
        var smile = new Path { Data = Geometry.Parse($"M {cx - size * 0.2},{y + size * 0.3} Q {cx},{y + size * 0.45} {cx + size * 0.2},{y + size * 0.3}"), Stroke = Brushes.DarkRed, StrokeThickness = 3, Fill = new SolidColorBrush(Color.FromRgb(40, 0, 0)) };
        EnemySpriteCanvas.Children.Add(smile);

        // Teeth
        for (int i = 0; i < 6; i++)
        {
            var tooth = new Rectangle { Width = 6, Height = 10, Fill = Brushes.White };
            EnemySpriteCanvas.Children.Add(tooth);
            Canvas.SetLeft(tooth, cx - size * 0.15 + i * 8);
            Canvas.SetTop(tooth, y + size * 0.3);
        }

        // Wide staring eyes
        var leftEye = new Ellipse { Width = 18, Height = 22, Fill = Brushes.White, Stroke = Brushes.Red, StrokeThickness = 2 };
        EnemySpriteCanvas.Children.Add(leftEye);
        Canvas.SetLeft(leftEye, cx - size * 0.2);
        Canvas.SetTop(leftEye, y + size * 0.1);

        var rightEye = new Ellipse { Width = 18, Height = 22, Fill = Brushes.White, Stroke = Brushes.Red, StrokeThickness = 2 };
        EnemySpriteCanvas.Children.Add(rightEye);
        Canvas.SetLeft(rightEye, cx + size * 0.05);
        Canvas.SetTop(rightEye, y + size * 0.1);

        var leftPupil = new Ellipse { Width = 8, Height = 14, Fill = Brushes.Black };
        EnemySpriteCanvas.Children.Add(leftPupil);
        Canvas.SetLeft(leftPupil, cx - size * 0.15);
        Canvas.SetTop(leftPupil, y + size * 0.14);

        var rightPupil = new Ellipse { Width = 8, Height = 14, Fill = Brushes.Black };
        EnemySpriteCanvas.Children.Add(rightPupil);
        Canvas.SetLeft(rightPupil, cx + size * 0.1);
        Canvas.SetTop(rightPupil, y + size * 0.14);
    }

    private void DrawCombatMeatChild(double cx, double y, double size)
    {
        var body = new Ellipse { Width = size * 0.7, Height = size * 0.9, Fill = new RadialGradientBrush(Color.FromRgb(200, 100, 100), Color.FromRgb(120, 40, 40)) };
        EnemySpriteCanvas.Children.Add(body);
        Canvas.SetLeft(body, cx - size * 0.35);
        Canvas.SetTop(body, y + size * 0.15);

        // Veins
        for (int i = 0; i < 4; i++)
        {
            var vein = new Line { X1 = cx - size * 0.2 + i * 15, Y1 = y + size * 0.3, X2 = cx + i * 10, Y2 = y + size * 0.9, Stroke = new SolidColorBrush(Color.FromRgb(80, 20, 20)), StrokeThickness = 2 };
            EnemySpriteCanvas.Children.Add(vein);
        }

        var head = new Ellipse { Width = size * 0.35, Height = size * 0.3, Fill = new SolidColorBrush(Color.FromRgb(180, 120, 120)) };
        EnemySpriteCanvas.Children.Add(head);
        Canvas.SetLeft(head, cx - size * 0.175);
        Canvas.SetTop(head, y);

        // Sad eyes
        var leftEye = new Ellipse { Width = 10, Height = 8, Fill = Brushes.Black };
        EnemySpriteCanvas.Children.Add(leftEye);
        Canvas.SetLeft(leftEye, cx - size * 0.1);
        Canvas.SetTop(leftEye, y + size * 0.08);

        var rightEye = new Ellipse { Width = 10, Height = 8, Fill = Brushes.Black };
        EnemySpriteCanvas.Children.Add(rightEye);
        Canvas.SetLeft(rightEye, cx + size * 0.02);
        Canvas.SetTop(rightEye, y + size * 0.08);
    }

    private void DrawCombatGrandmasTwin(double cx, double y, double size)
    {
        var dress = new Polygon { Points = new PointCollection { new Point(cx - 15, y + 40), new Point(cx + 15, y + 40), new Point(cx + 35, y + 110), new Point(cx - 35, y + 110) }, Fill = new SolidColorBrush(Color.FromRgb(80, 60, 80)) };
        EnemySpriteCanvas.Children.Add(dress);

        var face = new Ellipse { Width = 45, Height = 50, Fill = new SolidColorBrush(Color.FromRgb(240, 230, 240)) };
        EnemySpriteCanvas.Children.Add(face);
        Canvas.SetLeft(face, cx - 22);
        Canvas.SetTop(face, y);

        // Gray hair
        for (int i = 0; i < 5; i++)
        {
            var hair = new Line { X1 = cx - 15 + i * 8, Y1 = y, X2 = cx - 20 + i * 10, Y2 = y + 35, Stroke = new SolidColorBrush(Color.FromRgb(180, 180, 190)), StrokeThickness = 4 };
            EnemySpriteCanvas.Children.Add(hair);
        }

        // Black pit eyes
        var leftEye = new Ellipse { Width = 12, Height = 14, Fill = Brushes.Black };
        EnemySpriteCanvas.Children.Add(leftEye);
        Canvas.SetLeft(leftEye, cx - 15);
        Canvas.SetTop(leftEye, y + 15);

        var rightEye = new Ellipse { Width = 12, Height = 14, Fill = Brushes.Black };
        EnemySpriteCanvas.Children.Add(rightEye);
        Canvas.SetLeft(rightEye, cx + 3);
        Canvas.SetTop(rightEye, y + 15);

        var smile = new Path { Data = Geometry.Parse($"M {cx - 12},{y + 35} Q {cx},{y + 42} {cx + 12},{y + 35}"), Stroke = new SolidColorBrush(Color.FromRgb(150, 100, 100)), StrokeThickness = 2 };
        EnemySpriteCanvas.Children.Add(smile);
    }

    private void DrawCombatManInWall(double cx, double y, double size)
    {
        var torso = new Ellipse { Width = 50, Height = 80, Fill = new LinearGradientBrush(Color.FromRgb(60, 60, 70), Color.FromArgb(100, 60, 60, 70), 90) };
        EnemySpriteCanvas.Children.Add(torso);
        Canvas.SetLeft(torso, cx - 25);
        Canvas.SetTop(torso, y + 30);

        // Reaching arm
        var arm = new Path { Data = Geometry.Parse($"M {cx + 20},{y + 50} Q {cx + 50},{y + 30} {cx + 60},{y + 20}"), Stroke = new SolidColorBrush(Color.FromRgb(80, 80, 90)), StrokeThickness = 10 };
        EnemySpriteCanvas.Children.Add(arm);

        // Fingers
        for (int i = 0; i < 5; i++)
        {
            var finger = new Line { X1 = cx + 60, Y1 = y + 20, X2 = cx + 70 + i * 3, Y2 = y + 5 + i * 4, Stroke = new SolidColorBrush(Color.FromRgb(70, 70, 80)), StrokeThickness = 3 };
            EnemySpriteCanvas.Children.Add(finger);
        }

        var face = new Ellipse { Width = 35, Height = 40, Fill = new SolidColorBrush(Color.FromRgb(50, 50, 60)) };
        EnemySpriteCanvas.Children.Add(face);
        Canvas.SetLeft(face, cx - 17);
        Canvas.SetTop(face, y);

        var eye = new Ellipse { Width = 10, Height = 12, Fill = new RadialGradientBrush(Colors.White, Color.FromRgb(150, 150, 200)) };
        EnemySpriteCanvas.Children.Add(eye);
        Canvas.SetLeft(eye, cx - 5);
        Canvas.SetTop(eye, y + 15);
    }

    private void DrawCombatFriendlyHelper(double cx, double y, double size)
    {
        var body = new Ellipse { Width = 60, Height = 85, Fill = new RadialGradientBrush(Colors.LightYellow, Colors.Gold) };
        EnemySpriteCanvas.Children.Add(body);
        Canvas.SetLeft(body, cx - 30);
        Canvas.SetTop(body, y + 30);

        var head = new Ellipse { Width = 50, Height = 50, Fill = new SolidColorBrush(Colors.LightYellow), Stroke = Brushes.Gold, StrokeThickness = 2 };
        EnemySpriteCanvas.Children.Add(head);
        Canvas.SetLeft(head, cx - 25);
        Canvas.SetTop(head, y);

        var smile = new Path { Data = Geometry.Parse($"M {cx - 18},{y + 25} Q {cx},{y + 40} {cx + 18},{y + 25}"), Stroke = Brushes.Black, StrokeThickness = 3, Fill = new SolidColorBrush(Color.FromRgb(255, 150, 150)) };
        EnemySpriteCanvas.Children.Add(smile);

        // Unblinking eyes
        var leftEye = new Ellipse { Width = 14, Height = 18, Fill = Brushes.White, Stroke = Brushes.Red, StrokeThickness = 2 };
        EnemySpriteCanvas.Children.Add(leftEye);
        Canvas.SetLeft(leftEye, cx - 18);
        Canvas.SetTop(leftEye, y + 10);

        var rightEye = new Ellipse { Width = 14, Height = 18, Fill = Brushes.White, Stroke = Brushes.Red, StrokeThickness = 2 };
        EnemySpriteCanvas.Children.Add(rightEye);
        Canvas.SetLeft(rightEye, cx + 4);
        Canvas.SetTop(rightEye, y + 10);

        var leftPupil = new Ellipse { Width = 6, Height = 10, Fill = Brushes.Black };
        EnemySpriteCanvas.Children.Add(leftPupil);
        Canvas.SetLeft(leftPupil, cx - 14);
        Canvas.SetTop(leftPupil, y + 14);

        var rightPupil = new Ellipse { Width = 6, Height = 10, Fill = Brushes.Black };
        EnemySpriteCanvas.Children.Add(rightPupil);
        Canvas.SetLeft(rightPupil, cx + 8);
        Canvas.SetTop(rightPupil, y + 14);
    }

    private void DrawCombatYourReflection(double cx, double y, double size)
    {
        var body = new Ellipse { Width = 50, Height = 95, Fill = new RadialGradientBrush(Color.FromArgb(150, 150, 100, 200), Color.FromArgb(50, 100, 50, 150)), Stroke = new SolidColorBrush(Color.FromArgb(100, 200, 150, 255)), StrokeThickness = 2 };
        EnemySpriteCanvas.Children.Add(body);
        Canvas.SetLeft(body, cx - 25);
        Canvas.SetTop(body, y + 20);

        var face = new Ellipse { Width = 40, Height = 45, Fill = new LinearGradientBrush(Color.FromArgb(180, 200, 200, 220), Color.FromArgb(100, 150, 150, 180), 45) };
        EnemySpriteCanvas.Children.Add(face);
        Canvas.SetLeft(face, cx - 20);
        Canvas.SetTop(face, y);

        var leftEye = new Ellipse { Width = 10, Height = 12, Fill = new RadialGradientBrush(Colors.White, Colors.LightGray) };
        EnemySpriteCanvas.Children.Add(leftEye);
        Canvas.SetLeft(leftEye, cx - 12);
        Canvas.SetTop(leftEye, y + 12);

        var rightEye = new Ellipse { Width = 10, Height = 12, Fill = new RadialGradientBrush(Colors.White, Colors.LightGray) };
        EnemySpriteCanvas.Children.Add(rightEye);
        Canvas.SetLeft(rightEye, cx + 2);
        Canvas.SetTop(rightEye, y + 12);

        var leftPupil = new Ellipse { Width = 5, Height = 7, Fill = Brushes.Black };
        EnemySpriteCanvas.Children.Add(leftPupil);
        Canvas.SetLeft(leftPupil, cx - 9);
        Canvas.SetTop(leftPupil, y + 15);

        var rightPupil = new Ellipse { Width = 5, Height = 7, Fill = Brushes.Black };
        EnemySpriteCanvas.Children.Add(rightPupil);
        Canvas.SetLeft(rightPupil, cx + 5);
        Canvas.SetTop(rightPupil, y + 15);
    }

    private void DrawCombatItsListening(double cx, double y, double size)
    {
        var mass = new Ellipse { Width = 80, Height = 110, Fill = new RadialGradientBrush(Colors.Black, Color.FromArgb(200, 0, 0, 0)) };
        EnemySpriteCanvas.Children.Add(mass);
        Canvas.SetLeft(mass, cx - 40);
        Canvas.SetTop(mass, y);

        // Multiple ears
        for (int i = 0; i < 5; i++)
        {
            double earX = cx - 30 + _random.Next(60);
            double earY = y + 10 + _random.Next(80);
            var ear = new Ellipse { Width = 18, Height = 22, Fill = new SolidColorBrush(Color.FromRgb(60, 40, 50)), Stroke = new SolidColorBrush(Color.FromRgb(80, 50, 60)), StrokeThickness = 1 };
            EnemySpriteCanvas.Children.Add(ear);
            Canvas.SetLeft(ear, earX);
            Canvas.SetTop(ear, earY);

            var canal = new Ellipse { Width = 6, Height = 8, Fill = Brushes.Black };
            EnemySpriteCanvas.Children.Add(canal);
            Canvas.SetLeft(canal, earX + 6);
            Canvas.SetTop(canal, earY + 7);
        }

        var eye = new Ellipse { Width = 14, Height = 18, Fill = new RadialGradientBrush(Colors.Red, Colors.DarkRed) };
        EnemySpriteCanvas.Children.Add(eye);
        Canvas.SetLeft(eye, cx - 7);
        Canvas.SetTop(eye, y + 45);

        var pupil = new Ellipse { Width = 5, Height = 10, Fill = Brushes.Black };
        EnemySpriteCanvas.Children.Add(pupil);
        Canvas.SetLeft(pupil, cx - 2);
        Canvas.SetTop(pupil, y + 49);
    }

    private void DrawCombatTheHost(double cx, double y, double size)
    {
        var body = new Ellipse { Width = 110, Height = 120, Fill = new RadialGradientBrush(Colors.Crimson, Colors.DarkRed) };
        EnemySpriteCanvas.Children.Add(body);
        Canvas.SetLeft(body, cx - 55);
        Canvas.SetTop(body, y + 5);

        // Horns
        for (int i = 0; i < 5; i++)
        {
            double hornX = cx - 30 + i * 15;
            var horn = new Polygon { Points = new PointCollection { new Point(hornX - 5, y + 15), new Point(hornX, y - 15 - (i % 2) * 10), new Point(hornX + 5, y + 15) }, Fill = new SolidColorBrush(Color.FromRgb(40, 20, 20)) };
            EnemySpriteCanvas.Children.Add(horn);
        }

        // Multiple eyes
        for (int row = 0; row < 2; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                double eyeX = cx - 25 + col * 20;
                double eyeY = y + 35 + row * 20;
                var eye = new Ellipse { Width = 12, Height = 14, Fill = new RadialGradientBrush(Colors.Yellow, Colors.Orange) };
                EnemySpriteCanvas.Children.Add(eye);
                Canvas.SetLeft(eye, eyeX);
                Canvas.SetTop(eye, eyeY);

                var pupil = new Ellipse { Width = 4, Height = 8, Fill = Brushes.Black };
                EnemySpriteCanvas.Children.Add(pupil);
                Canvas.SetLeft(pupil, eyeX + 4);
                Canvas.SetTop(pupil, eyeY + 3);
            }
        }

        var mouth = new Ellipse { Width = 50, Height = 30, Fill = Brushes.Black };
        EnemySpriteCanvas.Children.Add(mouth);
        Canvas.SetLeft(mouth, cx - 25);
        Canvas.SetTop(mouth, y + 80);

        // Sharp teeth
        for (int i = 0; i < 7; i++)
        {
            var tooth = new Polygon { Points = new PointCollection { new Point(cx - 20 + i * 6, y + 80), new Point(cx - 17 + i * 6, y + 92), new Point(cx - 14 + i * 6, y + 80) }, Fill = Brushes.White };
            EnemySpriteCanvas.Children.Add(tooth);
        }
    }

    private void DrawCombatGeneric(double cx, double y, double size, Color baseColor)
    {
        var body = new Ellipse { Width = 80, Height = 100, Fill = new SolidColorBrush(baseColor), Stroke = Brushes.Black, StrokeThickness = 2 };
        EnemySpriteCanvas.Children.Add(body);
        Canvas.SetLeft(body, cx - 40);
        Canvas.SetTop(body, y + 10);

        var leftEye = new Ellipse { Width = 14, Height = 18, Fill = Brushes.White, Stroke = Brushes.Red, StrokeThickness = 2 };
        EnemySpriteCanvas.Children.Add(leftEye);
        Canvas.SetLeft(leftEye, cx - 20);
        Canvas.SetTop(leftEye, y + 35);

        var rightEye = new Ellipse { Width = 14, Height = 18, Fill = Brushes.White, Stroke = Brushes.Red, StrokeThickness = 2 };
        EnemySpriteCanvas.Children.Add(rightEye);
        Canvas.SetLeft(rightEye, cx + 6);
        Canvas.SetTop(rightEye, y + 35);

        var leftPupil = new Ellipse { Width = 6, Height = 10, Fill = Brushes.DarkRed };
        EnemySpriteCanvas.Children.Add(leftPupil);
        Canvas.SetLeft(leftPupil, cx - 16);
        Canvas.SetTop(leftPupil, y + 39);

        var rightPupil = new Ellipse { Width = 6, Height = 10, Fill = Brushes.DarkRed };
        EnemySpriteCanvas.Children.Add(rightPupil);
        Canvas.SetLeft(rightPupil, cx + 10);
        Canvas.SetTop(rightPupil, y + 39);
    }
    #endregion

    #region Rendering
    private void RenderDungeonView()
    {
        DungeonViewCanvas.Children.Clear();

        double w = DungeonViewCanvas.ActualWidth;
        double h = DungeonViewCanvas.ActualHeight;

        if (w <= 0 || h <= 0)
        {
            w = 700;
            h = 400;
        }

        // Draw pixel art ceiling with creepy details
        DrawPixelCeiling(w, h);
        
        // Draw pixel art floor with creepy details
        DrawPixelFloor(w, h);

        // Draw walls using proper layered approach (far to near)
        for (int depth = ViewDistance; depth >= 1; depth--)
        {
            DrawPixelWallLayer(w, h, depth);
        }

        // Draw creepy ambient details
        DrawAmbientCreepiness(w, h);

        // Draw objects/enemies in view
        DrawEntitiesInView(w, h, w / 2, h * 0.5);

        // Check for interaction prompt
        var currentTile = _map[_playerX, _playerY];
        if (currentTile == TileType.StairsDown)
            ShowInteractionPrompt("Press SPACE to descend to the next floor");
        else if (currentTile == TileType.Shrine && !_usedShrines.Contains((_playerX, _playerY)))
            ShowInteractionPrompt("Press SPACE to pray at the shrine");
        else
            HideInteractionPrompt();
    }

    private void DrawPixelCeiling(double w, double h)
    {
        double pixelSize = 8;
        double ceilingH = h * 0.5;
        
        // Base dark ceiling
        var baseCeiling = new Rectangle
        {
            Width = w,
            Height = ceilingH,
            Fill = new SolidColorBrush(Color.FromRgb(18, 12, 24))
        };
        DungeonViewCanvas.Children.Add(baseCeiling);

        // Pixel-art stone blocks on ceiling
        for (double py = 0; py < ceilingH; py += pixelSize * 3)
        {
            double brightness = 20 + (py / ceilingH) * 25; // Gets brighter toward horizon
            for (double px = 0; px < w; px += pixelSize * 4)
            {
                double offset = ((int)(py / (pixelSize * 3)) % 2) * pixelSize * 2;
                double variation = ((_playerX + _playerY + (int)(px / pixelSize)) % 7) * 3;
                
                byte r = (byte)Math.Min(255, brightness + variation);
                byte g = (byte)Math.Min(255, brightness * 0.8 + variation * 0.5);
                byte b = (byte)Math.Min(255, brightness * 1.2 + variation);

                var stone = new Rectangle
                {
                    Width = pixelSize * 3.8,
                    Height = pixelSize * 2.8,
                    Fill = new SolidColorBrush(Color.FromRgb(r, g, b))
                };
                Canvas.SetLeft(stone, px + offset);
                Canvas.SetTop(stone, py);
                DungeonViewCanvas.Children.Add(stone);
            }
        }

        // Creepy dripping stuff from ceiling (ANIMATED)
        int drips = 8 + (_currentFloor % 5);
        for (int i = 0; i < drips; i++)
        {
            double dx = (w / drips) * i + (_playerX * 13 + i * 47) % (w / drips);
            double baseDripLen = 15 + (_playerY + i * 31) % 35;
            
            // Animate drip - each drip has different phase
            double dripPhase = (_dripOffset + i * 15) % 60;
            double animatedLen = baseDripLen * (0.5 + dripPhase / 120.0);
            
            // Greenish drip (mysterious goo) with animated length
            for (int d = 0; d < animatedLen; d += 4)
            {
                byte alpha = (byte)Math.Max(0, 200 - d * 4);
                byte glow = (byte)(180 + Math.Sin(_animationTime * 2 + i) * 40);
                var drip = new Rectangle
                {
                    Width = 4,
                    Height = 6,
                    Fill = new SolidColorBrush(Color.FromArgb(alpha, 80, glow, 60))
                };
                Canvas.SetLeft(drip, dx);
                Canvas.SetTop(drip, ceilingH - animatedLen + d);
                DungeonViewCanvas.Children.Add(drip);
            }
            
            // Animated drip drop at bottom (pulses)
            double dropSize = 6 + Math.Sin(_animationTime * 4 + i * 0.5) * 2;
            var drop = new Ellipse
            {
                Width = dropSize,
                Height = dropSize * 1.3,
                Fill = new SolidColorBrush(Color.FromArgb(180, 60, 200, 40))
            };
            Canvas.SetLeft(drop, dx - dropSize / 2 + 2);
            Canvas.SetTop(drop, ceilingH - animatedLen - 2);
            DungeonViewCanvas.Children.Add(drop);
            
            // Occasional falling drop
            if (dripPhase > 50)
            {
                double fallY = ceilingH + (dripPhase - 50) * 8;
                if (fallY < h)
                {
                    var fallingDrop = new Ellipse
                    {
                        Width = 4,
                        Height = 6,
                        Fill = new SolidColorBrush(Color.FromArgb((byte)(200 - (dripPhase - 50) * 15), 60, 200, 40))
                    };
                    Canvas.SetLeft(fallingDrop, dx);
                    Canvas.SetTop(fallingDrop, fallY);
                    DungeonViewCanvas.Children.Add(fallingDrop);
                }
            }
        }

        // Cobwebs in corners
        DrawPixelCobweb(0, 0, 60, false);
        DrawPixelCobweb(w - 60, 0, 60, true);
    }

    private void DrawPixelCobweb(double x, double y, double size, bool flipX)
    {
        var webColor = new SolidColorBrush(Color.FromArgb(120, 200, 200, 200));
        
        // Draw radiating web lines
        for (int i = 0; i < 5; i++)
        {
            double angle = (flipX ? 180 : 0) + i * 22;
            double rad = angle * Math.PI / 180;
            double ex = x + (flipX ? 0 : size) + Math.Cos(rad) * size * (flipX ? 1 : -1);
            double ey = y + Math.Sin(rad) * size;
            
            var line = new Line
            {
                X1 = x + (flipX ? 0 : size),
                Y1 = y,
                X2 = ex,
                Y2 = ey,
                Stroke = webColor,
                StrokeThickness = 1
            };
            DungeonViewCanvas.Children.Add(line);
        }

        // Concentric web arcs (simplified as lines)
        for (int ring = 1; ring <= 3; ring++)
        {
            double r = size * ring / 4;
            for (int seg = 0; seg < 4; seg++)
            {
                double a1 = (flipX ? 180 : 0) + seg * 22;
                double a2 = (flipX ? 180 : 0) + (seg + 1) * 22;
                double r1 = a1 * Math.PI / 180;
                double r2 = a2 * Math.PI / 180;
                
                var arcLine = new Line
                {
                    X1 = x + (flipX ? 0 : size) + Math.Cos(r1) * r * (flipX ? 1 : -1),
                    Y1 = y + Math.Sin(r1) * r,
                    X2 = x + (flipX ? 0 : size) + Math.Cos(r2) * r * (flipX ? 1 : -1),
                    Y2 = y + Math.Sin(r2) * r,
                    Stroke = webColor,
                    StrokeThickness = 1
                };
                DungeonViewCanvas.Children.Add(arcLine);
            }
        }

        // Tiny spider!
        if ((_playerX + _playerY) % 3 == 0)
        {
            double spiderX = x + (flipX ? size * 0.3 : size * 0.7);
            double spiderY = y + size * 0.4;
            
            // Body
            var body = new Ellipse { Width = 6, Height = 8, Fill = Brushes.Black };
            Canvas.SetLeft(body, spiderX - 3);
            Canvas.SetTop(body, spiderY);
            DungeonViewCanvas.Children.Add(body);
            
            // Eyes (red and creepy!)
            var eye1 = new Ellipse { Width = 2, Height = 2, Fill = Brushes.Red };
            var eye2 = new Ellipse { Width = 2, Height = 2, Fill = Brushes.Red };
            Canvas.SetLeft(eye1, spiderX - 2);
            Canvas.SetTop(eye1, spiderY + 1);
            Canvas.SetLeft(eye2, spiderX + 1);
            Canvas.SetTop(eye2, spiderY + 1);
            DungeonViewCanvas.Children.Add(eye1);
            DungeonViewCanvas.Children.Add(eye2);
        }
    }

    private void DrawPixelFloor(double w, double h)
    {
        double pixelSize = 8;
        double floorY = h * 0.5;
        double floorH = h * 0.5;

        // Draw perspective checkered tiles
        int rows = 10;
        for (int row = 0; row < rows; row++)
        {
            double progress = (double)row / rows;
            double y1 = floorY + floorH * progress;
            double y2 = floorY + floorH * ((row + 1.0) / rows);
            double rowHeight = y2 - y1;
            
            // Perspective narrowing
            double narrowFactor = 0.15 + progress * 0.85;
            double leftX = w * (0.5 - narrowFactor * 0.5);
            double rightX = w * (0.5 + narrowFactor * 0.5);
            double rowWidth = rightX - leftX;
            
            int tilesInRow = 4 + row;
            double tileWidth = rowWidth / tilesInRow;

            for (int t = 0; t < tilesInRow; t++)
            {
                bool isDark = (t + row) % 2 == 0;
                byte brightness = isDark ? (byte)(25 + progress * 20) : (byte)(45 + progress * 25);
                
                // Add some creepy color variation
                byte r = brightness;
                byte g = (byte)(brightness * 0.85);
                byte b = (byte)(brightness * 0.75);
                
                // Occasional blood-red tile
                if ((_playerX * 7 + _playerY * 11 + row + t) % 47 == 0)
                {
                    r = (byte)(brightness + 40);
                    g = (byte)(brightness * 0.3);
                    b = (byte)(brightness * 0.3);
                }

                var tile = new Rectangle
                {
                    Width = Math.Max(1, tileWidth - 1),
                    Height = Math.Max(1, rowHeight - 1),
                    Fill = new SolidColorBrush(Color.FromRgb(r, g, b))
                };
                Canvas.SetLeft(tile, leftX + t * tileWidth);
                Canvas.SetTop(tile, y1);
                DungeonViewCanvas.Children.Add(tile);
            }
        }

        // Cracks in floor
        for (int i = 0; i < 4; i++)
        {
            double crackX = w * 0.2 + ((_playerX + i * 17) % 60) / 100.0 * w * 0.6;
            double crackY = floorY + floorH * 0.3 + ((_playerY + i * 23) % 40) / 100.0 * floorH * 0.5;
            DrawPixelCrack(crackX, crackY);
        }

        // Mysterious puddles
        if (_currentFloor % 2 == 0)
        {
            double puddleX = w * 0.3 + (_playerX % 20) * 5;
            double puddleY = floorY + floorH * 0.6;
            DrawPixelPuddle(puddleX, puddleY);
        }
    }

    private void DrawPixelCrack(double x, double y)
    {
        var crackColor = new SolidColorBrush(Color.FromRgb(15, 12, 10));
        
        // Main crack line (pixelated)
        double cx = x;
        double cy = y;
        for (int i = 0; i < 8; i++)
        {
            var pixel = new Rectangle { Width = 3, Height = 3, Fill = crackColor };
            Canvas.SetLeft(pixel, cx);
            Canvas.SetTop(pixel, cy);
            DungeonViewCanvas.Children.Add(pixel);
            
            cx += (i % 2 == 0) ? 3 : 1;
            cy += (i % 3 == 0) ? 2 : -1;
        }
    }

    private void DrawPixelPuddle(double x, double y)
    {
        // Dark mysterious puddle
        var puddleColors = new[] {
            Color.FromArgb(180, 30, 50, 40),
            Color.FromArgb(150, 40, 70, 50),
            Color.FromArgb(120, 50, 90, 60)
        };

        for (int ring = 2; ring >= 0; ring--)
        {
            var puddle = new Ellipse
            {
                Width = 30 + ring * 15,
                Height = 12 + ring * 6,
                Fill = new SolidColorBrush(puddleColors[ring])
            };
            Canvas.SetLeft(puddle, x - (15 + ring * 7.5));
            Canvas.SetTop(puddle, y - (6 + ring * 3));
            DungeonViewCanvas.Children.Add(puddle);
        }

        // Reflection/shimmer
        var shimmer = new Ellipse
        {
            Width = 8,
            Height = 3,
            Fill = new SolidColorBrush(Color.FromArgb(100, 150, 200, 150))
        };
        Canvas.SetLeft(shimmer, x - 10);
        Canvas.SetTop(shimmer, y - 2);
        DungeonViewCanvas.Children.Add(shimmer);
    }

    private void DrawAmbientCreepiness(double w, double h)
    {
        // ANIMATED Glowing eyes in the darkness
        int eyePairs = (_playerX + _playerY + _currentFloor) % 3 + 1;
        for (int i = 0; i < eyePairs; i++)
        {
            double eyeX = w * 0.1 + ((_playerX * 13 + i * 97) % 80) / 100.0 * w * 0.8;
            double eyeY = h * 0.2 + ((_playerY * 17 + i * 53) % 30) / 100.0 * h * 0.2;
            
            // Only show if there's a wall there (peering from darkness)
            var (dx, dy) = GetDirectionVector(_playerDir);
            int checkDist = 3 + i;
            int checkX = _playerX + dx * checkDist + (i % 2 == 0 ? 1 : -1);
            int checkY = _playerY + dy * checkDist;
            
            if (IsWall(checkX, checkY))
            {
                DrawAnimatedCreepyEyes(eyeX, eyeY, 4 + i, i);
            }
        }

        // ANIMATED Floating dust particles using stored positions
        for (int i = 0; i < _dustParticles.Count && i < 20; i++)
        {
            var (px, py, speed, phase) = _dustParticles[i];
            
            // Clamp to visible area
            if (px < 0 || px > w || py < 0 || py > h) continue;
            
            byte alpha = (byte)(40 + Math.Sin(_animationTime * speed + phase) * 30);
            double size = 2 + Math.Sin(_animationTime * 0.5 + phase) * 1;
            
            var dust = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = new SolidColorBrush(Color.FromArgb(alpha, 220, 200, 170))
            };
            Canvas.SetLeft(dust, px);
            Canvas.SetTop(dust, py);
            DungeonViewCanvas.Children.Add(dust);
        }

        // Vignette shadow effect (darker at edges)
        DrawVignetteShadow(w, h);
        
        // Ambient fog wisps
        DrawAnimatedFog(w, h);
    }

    private void DrawVignetteShadow(double w, double h)
    {
        // Left shadow
        var leftShadow = new Rectangle
        {
            Width = w * 0.15,
            Height = h,
            Fill = new LinearGradientBrush(
                Color.FromArgb(180, 0, 0, 0),
                Color.FromArgb(0, 0, 0, 0),
                0)
        };
        DungeonViewCanvas.Children.Add(leftShadow);

        // Right shadow
        var rightShadow = new Rectangle
        {
            Width = w * 0.15,
            Height = h,
            Fill = new LinearGradientBrush(
                Color.FromArgb(0, 0, 0, 0),
                Color.FromArgb(180, 0, 0, 0),
                0)
        };
        Canvas.SetLeft(rightShadow, w * 0.85);
        DungeonViewCanvas.Children.Add(rightShadow);

        // Top shadow (ceiling darkness)
        var topShadow = new Rectangle
        {
            Width = w,
            Height = h * 0.1,
            Fill = new LinearGradientBrush(
                Color.FromArgb(200, 0, 0, 0),
                Color.FromArgb(0, 0, 0, 0),
                90)
        };
        DungeonViewCanvas.Children.Add(topShadow);

        // Bottom shadow (floor darkness)
        var bottomShadow = new Rectangle
        {
            Width = w,
            Height = h * 0.08,
            Fill = new LinearGradientBrush(
                Color.FromArgb(0, 0, 0, 0),
                Color.FromArgb(150, 0, 0, 0),
                90)
        };
        Canvas.SetTop(bottomShadow, h * 0.92);
        DungeonViewCanvas.Children.Add(bottomShadow);

        // Pulsing darkness overlay (breathing effect)
        byte breathAlpha = (byte)(10 + _creepyBreathing * 20);
        var breathingDarkness = new Rectangle
        {
            Width = w,
            Height = h,
            Fill = new SolidColorBrush(Color.FromArgb(breathAlpha, 0, 0, 0))
        };
        DungeonViewCanvas.Children.Add(breathingDarkness);
    }

    private void DrawAnimatedFog(double w, double h)
    {
        // Wispy fog that moves
        for (int i = 0; i < 5; i++)
        {
            double fogX = (i * 150 + _animationTime * 20 * (i % 2 == 0 ? 1 : -1)) % (w + 100) - 50;
            double fogY = h * 0.3 + Math.Sin(_animationTime * 0.5 + i) * 30;
            byte fogAlpha = (byte)(15 + Math.Sin(_animationTime + i * 0.7) * 10);
            
            var fog = new Ellipse
            {
                Width = 120 + i * 20,
                Height = 40 + i * 10,
                Fill = new RadialGradientBrush(
                    Color.FromArgb(fogAlpha, 100, 100, 120),
                    Color.FromArgb(0, 80, 80, 100))
            };
            Canvas.SetLeft(fog, fogX);
            Canvas.SetTop(fog, fogY);
            DungeonViewCanvas.Children.Add(fog);
        }
    }

    private void DrawAnimatedCreepyEyes(double x, double y, double size, int index)
    {
        // Animated glow (pulses)
        double glowIntensity = _eyePulse;
        byte glowAlpha = (byte)(30 + glowIntensity * 50);
        
        var glow = new Ellipse
        {
            Width = size * 6 * (0.8 + glowIntensity * 0.4),
            Height = size * 4 * (0.8 + glowIntensity * 0.4),
            Fill = new RadialGradientBrush(
                Color.FromArgb(glowAlpha, 255, 50, 50),
                Color.FromArgb(0, 255, 0, 0))
        };
        Canvas.SetLeft(glow, x - size * 3);
        Canvas.SetTop(glow, y - size * 2);
        DungeonViewCanvas.Children.Add(glow);

        // Animated eye size (breathing)
        double eyeScale = 1.0 + Math.Sin(_animationTime * 2 + index) * 0.1;
        
        // Eye color shifts between yellow and red
        byte redAmount = (byte)(200 + Math.Sin(_animationTime * 3 + index) * 55);
        byte yellowAmount = (byte)(200 - Math.Sin(_animationTime * 3 + index) * 100);

        // Left eye
        var leftEye = new Ellipse
        {
            Width = size * eyeScale,
            Height = size * 1.5 * eyeScale,
            Fill = new RadialGradientBrush(
                Color.FromRgb(redAmount, yellowAmount, 0),
                Color.FromRgb(180, 0, 0))
        };
        Canvas.SetLeft(leftEye, x - size * 1.5);
        Canvas.SetTop(leftEye, y);
        DungeonViewCanvas.Children.Add(leftEye);

        // Right eye
        var rightEye = new Ellipse
        {
            Width = size * eyeScale,
            Height = size * 1.5 * eyeScale,
            Fill = new RadialGradientBrush(
                Color.FromRgb(redAmount, yellowAmount, 0),
                Color.FromRgb(180, 0, 0))
        };
        Canvas.SetLeft(rightEye, x + size * 0.5);
        Canvas.SetTop(rightEye, y);
        DungeonViewCanvas.Children.Add(rightEye);

        // Animated pupils (track slightly, like they're watching)
        double pupilOffset = Math.Sin(_animationTime * 1.5) * size * 0.15;
        
        var leftPupil = new Ellipse
        {
            Width = size * 0.3,
            Height = size * 1.2,
            Fill = Brushes.Black
        };
        Canvas.SetLeft(leftPupil, x - size * 1.15 + pupilOffset);
        Canvas.SetTop(leftPupil, y + size * 0.15);
        DungeonViewCanvas.Children.Add(leftPupil);

        var rightPupil = new Ellipse
        {
            Width = size * 0.3,
            Height = size * 1.2,
            Fill = Brushes.Black
        };
        Canvas.SetLeft(rightPupil, x + size * 0.85 + pupilOffset);
        Canvas.SetTop(rightPupil, y + size * 0.15);
        DungeonViewCanvas.Children.Add(rightPupil);
        
        // Occasional blink (eyes close briefly)
        if (Math.Sin(_animationTime * 0.3 + index * 2) > 0.95)
        {
            // Eyelid overlay
            var leftLid = new Ellipse
            {
                Width = size * eyeScale * 1.1,
                Height = size * 1.5 * eyeScale,
                Fill = Brushes.Black
            };
            Canvas.SetLeft(leftLid, x - size * 1.55);
            Canvas.SetTop(leftLid, y - size * 0.05);
            DungeonViewCanvas.Children.Add(leftLid);
            
            var rightLid = new Ellipse
            {
                Width = size * eyeScale * 1.1,
                Height = size * 1.5 * eyeScale,
                Fill = Brushes.Black
            };
            Canvas.SetLeft(rightLid, x + size * 0.45);
            Canvas.SetTop(rightLid, y - size * 0.05);
            DungeonViewCanvas.Children.Add(rightLid);
        }
    }

    private void DrawCreepyEyes(double x, double y, double size)
    {
        // Glow
        var glow = new Ellipse
        {
            Width = size * 6,
            Height = size * 4,
            Fill = new RadialGradientBrush(
                Color.FromArgb(40, 255, 50, 50),
                Color.FromArgb(0, 255, 0, 0))
        };
        Canvas.SetLeft(glow, x - size * 3);
        Canvas.SetTop(glow, y - size * 2);
        DungeonViewCanvas.Children.Add(glow);

        // Left eye
        var leftEye = new Ellipse
        {
            Width = size,
            Height = size * 1.5,
            Fill = new RadialGradientBrush(Colors.Yellow, Colors.Red)
        };
        Canvas.SetLeft(leftEye, x - size * 1.5);
        Canvas.SetTop(leftEye, y);
        DungeonViewCanvas.Children.Add(leftEye);

        // Right eye
        var rightEye = new Ellipse
        {
            Width = size,
            Height = size * 1.5,
            Fill = new RadialGradientBrush(Colors.Yellow, Colors.Red)
        };
        Canvas.SetLeft(rightEye, x + size * 0.5);
        Canvas.SetTop(rightEye, y);
        DungeonViewCanvas.Children.Add(rightEye);

        // Pupils (slit like a cat/demon)
        var leftPupil = new Ellipse
        {
            Width = size * 0.3,
            Height = size * 1.2,
            Fill = Brushes.Black
        };
        Canvas.SetLeft(leftPupil, x - size * 1.15);
        Canvas.SetTop(leftPupil, y + size * 0.15);
        DungeonViewCanvas.Children.Add(leftPupil);

        var rightPupil = new Ellipse
        {
            Width = size * 0.3,
            Height = size * 1.2,
            Fill = Brushes.Black
        };
        Canvas.SetLeft(rightPupil, x + size * 0.85);
        Canvas.SetTop(rightPupil, y + size * 0.15);
        DungeonViewCanvas.Children.Add(rightPupil);
    }

    // Perspective calculation - returns screen coordinates for a given depth layer
    private (double left, double right, double top, double bottom) GetLayerBounds(double w, double h, int depth)
    {
        // Each layer is a smaller rectangle toward the center
        double factor = 1.0 / (depth + 0.5);
        double centerX = w / 2;
        double centerY = h / 2;
        
        double halfWidth = w * 0.48 * factor;
        double halfHeight = h * 0.48 * factor;

        return (
            centerX - halfWidth,
            centerX + halfWidth,
            centerY - halfHeight,
            centerY + halfHeight
        );
    }

    private void DrawPixelWallLayer(double w, double h, int depth)
    {
        var (dx, dy) = GetDirectionVector(_playerDir);
        var (leftDx, leftDy) = GetDirectionVector((Direction)(((int)_playerDir + 3) % 4));
        var (rightDx, rightDy) = GetDirectionVector((Direction)(((int)_playerDir + 1) % 4));

        // Get bounds for this layer and next layer
        var (l1, r1, t1, b1) = GetLayerBounds(w, h, depth);
        var (l2, r2, t2, b2) = GetLayerBounds(w, h, depth - 1);

        // Position directly ahead at this depth
        int fx = _playerX + dx * depth;
        int fy = _playerY + dy * depth;

        // Brightness decreases with distance
        byte brightness = (byte)(100 - depth * 15);

        // Check cell to the left at this depth
        int leftX = fx + leftDx;
        int leftY = fy + leftDy;
        
        // Check cell to the right at this depth
        int rightX = fx + rightDx;
        int rightY = fy + rightDy;

        // Draw left side wall (receding into distance)
        if (IsWall(leftX, leftY))
        {
            DrawPixelSideWall(l1, t1, l2, t2, l1, b1, l2, b2, brightness, depth, true);
        }

        // Draw right side wall (receding into distance)
        if (IsWall(rightX, rightY))
        {
            DrawPixelSideWall(r2, t2, r1, t1, r2, b2, r1, b1, brightness, depth, false);
        }

        // Draw front wall if blocked
        if (IsWall(fx, fy))
        {
            DrawPixelFrontWall(l1, t1, r1 - l1, b1 - t1, brightness, depth, fx, fy);
        }
    }

    private void DrawPixelSideWall(double x1, double y1, double x2, double y2, 
                                    double x3, double y3, double x4, double y4, 
                                    byte brightness, int depth, bool isLeft)
    {
        // Darker for side walls
        byte r = (byte)(brightness * 0.6);
        byte g = (byte)(brightness * 0.55);
        byte b = (byte)(brightness * 0.5);

        var panel = new Polygon
        {
            Points = new PointCollection
            {
                new Point(x1, y1),
                new Point(x2, y2),
                new Point(x4, y4),
                new Point(x3, y3)
            },
            Fill = new SolidColorBrush(Color.FromRgb(r, g, b)),
            Stroke = new SolidColorBrush(Color.FromRgb(20, 18, 15)),
            StrokeThickness = 1
        };
        DungeonViewCanvas.Children.Add(panel);

        // Add brick lines on side wall if close enough
        if (depth <= 2)
        {
            int brickRows = 4;
            for (int i = 1; i < brickRows; i++)
            {
                double t = (double)i / brickRows;
                double ly = y1 + (y3 - y1) * t;
                double ry = y2 + (y4 - y2) * t;
                
                var brickLine = new Line
                {
                    X1 = x1,
                    Y1 = ly,
                    X2 = x2,
                    Y2 = ry,
                    Stroke = new SolidColorBrush(Color.FromRgb(30, 25, 20)),
                    StrokeThickness = 2,
                    Opacity = 0.6
                };
                DungeonViewCanvas.Children.Add(brickLine);
            }
        }
    }

    private void DrawPixelFrontWall(double x, double y, double width, double height, byte brightness, int depth, int wallX, int wallY)
    {
        byte r = brightness;
        byte g = (byte)(brightness * 0.9);
        byte b = (byte)(brightness * 0.8);

        // Main wall rectangle
        var wall = new Rectangle
        {
            Width = width,
            Height = height,
            Fill = new SolidColorBrush(Color.FromRgb(r, g, b)),
            Stroke = new SolidColorBrush(Color.FromRgb(30, 25, 20)),
            StrokeThickness = 2
        };
        Canvas.SetLeft(wall, x);
        Canvas.SetTop(wall, y);
        DungeonViewCanvas.Children.Add(wall);

        // Pixel art brick pattern
        if (depth <= 3)
        {
            DrawPixelBricks(x, y, width, height, depth);
        }

        // Add creepy wall decorations based on position
        int decorType = (wallX * 7 + wallY * 13 + _currentFloor) % 10;
        
        if (depth <= 2)
        {
            if (decorType == 0)
                DrawWallSkull(x + width / 2, y + height * 0.3, depth);
            else if (decorType == 1)
                DrawWallChains(x + width * 0.2, y, height, depth);
            else if (decorType == 2)
                DrawWallBloodSplatter(x + width * 0.6, y + height * 0.4, depth);
            else if (decorType == 3)
                DrawPixelTorch(x + width / 2, y + height * 0.3, depth);
            else if (decorType == 4)
                DrawWallCrack(x + width * 0.3, y + height * 0.2, width * 0.4, height * 0.6);
            else if (decorType == 5)
                DrawCreepyWriting(x + width * 0.1, y + height * 0.5, width * 0.8, depth);
        }
    }

    private void DrawPixelBricks(double x, double y, double width, double height, int depth)
    {
        int rows = Math.Max(3, 6 - depth);
        int cols = Math.Max(4, 8 - depth);
        double brickH = height / rows;
        double brickW = width / cols;

        // Draw individual bricks with variation
        for (int row = 0; row < rows; row++)
        {
            double offsetX = (row % 2 == 0) ? 0 : brickW / 2;
            
            for (int col = 0; col < cols + 1; col++)
            {
                double bx = x + col * brickW + offsetX - brickW / 4;
                double by = y + row * brickH;
                
                if (bx >= x - brickW / 2 && bx + brickW <= x + width + brickW / 2)
                {
                    // Color variation for each brick
                    int variation = ((row * 7 + col * 11 + _playerX) % 20) - 10;
                    byte br = (byte)Math.Clamp(50 + variation, 30, 70);
                    byte bg = (byte)Math.Clamp(42 + variation * 0.8, 25, 60);
                    byte bb = (byte)Math.Clamp(35 + variation * 0.6, 20, 50);
                    
                    var brick = new Rectangle
                    {
                        Width = Math.Min(brickW - 2, width - (bx - x)),
                        Height = brickH - 2,
                        Fill = new SolidColorBrush(Color.FromRgb(br, bg, bb)),
                        Opacity = 0.4
                    };
                    Canvas.SetLeft(brick, Math.Max(x, bx));
                    Canvas.SetTop(brick, by + 1);
                    DungeonViewCanvas.Children.Add(brick);
                }
            }
        }

        // Mortar lines
        for (int row = 1; row < rows; row++)
        {
            var hLine = new Line
            {
                X1 = x + 2,
                Y1 = y + row * brickH,
                X2 = x + width - 2,
                Y2 = y + row * brickH,
                Stroke = new SolidColorBrush(Color.FromRgb(25, 20, 15)),
                StrokeThickness = 2,
                Opacity = 0.7
            };
            DungeonViewCanvas.Children.Add(hLine);
        }
    }

    private void DrawWallSkull(double cx, double cy, int depth)
    {
        double scale = 1.0 / (depth * 0.5 + 0.5);
        double size = 25 * scale;

        // Skull shape
        var skull = new Ellipse
        {
            Width = size * 1.6,
            Height = size * 1.4,
            Fill = new SolidColorBrush(Color.FromRgb(200, 190, 170)),
            Stroke = new SolidColorBrush(Color.FromRgb(60, 50, 40)),
            StrokeThickness = 2
        };
        Canvas.SetLeft(skull, cx - size * 0.8);
        Canvas.SetTop(skull, cy);
        DungeonViewCanvas.Children.Add(skull);

        // Eye sockets (dark and empty)
        var leftSocket = new Ellipse
        {
            Width = size * 0.4,
            Height = size * 0.5,
            Fill = Brushes.Black
        };
        Canvas.SetLeft(leftSocket, cx - size * 0.5);
        Canvas.SetTop(leftSocket, cy + size * 0.3);
        DungeonViewCanvas.Children.Add(leftSocket);

        var rightSocket = new Ellipse
        {
            Width = size * 0.4,
            Height = size * 0.5,
            Fill = Brushes.Black
        };
        Canvas.SetLeft(rightSocket, cx + size * 0.1);
        Canvas.SetTop(rightSocket, cy + size * 0.3);
        DungeonViewCanvas.Children.Add(rightSocket);

        // Glowing red eyes inside sockets (creepy!)
        var leftGlow = new Ellipse
        {
            Width = size * 0.15,
            Height = size * 0.15,
            Fill = new RadialGradientBrush(Colors.Red, Color.FromRgb(100, 0, 0))
        };
        Canvas.SetLeft(leftGlow, cx - size * 0.4);
        Canvas.SetTop(leftGlow, cy + size * 0.45);
        DungeonViewCanvas.Children.Add(leftGlow);

        var rightGlow = new Ellipse
        {
            Width = size * 0.15,
            Height = size * 0.15,
            Fill = new RadialGradientBrush(Colors.Red, Color.FromRgb(100, 0, 0))
        };
        Canvas.SetLeft(rightGlow, cx + size * 0.2);
        Canvas.SetTop(rightGlow, cy + size * 0.45);
        DungeonViewCanvas.Children.Add(rightGlow);

        // Nose hole
        var nose = new Polygon
        {
            Points = new PointCollection
            {
                new Point(cx - size * 0.1, cy + size * 0.7),
                new Point(cx + size * 0.1, cy + size * 0.7),
                new Point(cx, cy + size * 0.9)
            },
            Fill = Brushes.Black
        };
        DungeonViewCanvas.Children.Add(nose);

        // Teeth
        for (int i = 0; i < 6; i++)
        {
            var tooth = new Rectangle
            {
                Width = size * 0.15,
                Height = size * 0.2,
                Fill = new SolidColorBrush(Color.FromRgb(220, 210, 190))
            };
            Canvas.SetLeft(tooth, cx - size * 0.5 + i * size * 0.18);
            Canvas.SetTop(tooth, cy + size * 1.0);
            DungeonViewCanvas.Children.Add(tooth);
        }
    }

    private void DrawWallChains(double x, double y, double height, int depth)
    {
        double scale = 1.0 / (depth * 0.5 + 0.5);
        double chainWidth = 8 * scale;
        
        // ANIMATED chain sway
        double sway = _chainSway * scale;
        
        // Draw chain links with sway animation
        int links = (int)(height / (chainWidth * 1.5));
        for (int i = 0; i < links; i++)
        {
            double ly = y + i * chainWidth * 1.5;
            // Each link sways more the lower it is
            double linkSway = sway * (i / (double)links);
            
            var link = new Ellipse
            {
                Width = chainWidth,
                Height = chainWidth * 1.5,
                Stroke = new SolidColorBrush(Color.FromRgb(100, 90, 70)),
                StrokeThickness = 2 * scale,
                Fill = Brushes.Transparent
            };
            Canvas.SetLeft(link, x + (i % 2) * chainWidth * 0.3 + linkSway);
            Canvas.SetTop(link, ly);
            DungeonViewCanvas.Children.Add(link);
        }

        // Shackle at bottom (with full sway)
        double shackleX = x + sway;
        var shackle = new Path
        {
            Data = Geometry.Parse($"M {shackleX},{y + height * 0.8} A 10,10 0 1 0 {shackleX + chainWidth * 2},{y + height * 0.8}"),
            Stroke = new SolidColorBrush(Color.FromRgb(80, 70, 50)),
            StrokeThickness = 3 * scale
        };
        DungeonViewCanvas.Children.Add(shackle);
    }

    private void DrawWallBloodSplatter(double x, double y, int depth)
    {
        double scale = 1.0 / (depth * 0.5 + 0.5);
        var bloodColor = Color.FromRgb(120, 20, 20);
        var darkBlood = Color.FromRgb(60, 10, 10);

        // Main splatter
        var mainSplat = new Ellipse
        {
            Width = 35 * scale,
            Height = 25 * scale,
            Fill = new RadialGradientBrush(bloodColor, darkBlood)
        };
        Canvas.SetLeft(mainSplat, x);
        Canvas.SetTop(mainSplat, y);
        DungeonViewCanvas.Children.Add(mainSplat);

        // Drips running down
        for (int i = 0; i < 4; i++)
        {
            double dripX = x + 5 + i * 8 * scale;
            double dripLen = 20 + (i * 13) % 30;
            
            for (int d = 0; d < dripLen * scale; d += 4)
            {
                byte alpha = (byte)(180 - d * 3);
                var drip = new Rectangle
                {
                    Width = 4 * scale,
                    Height = 5 * scale,
                    Fill = new SolidColorBrush(Color.FromArgb(alpha, bloodColor.R, bloodColor.G, bloodColor.B))
                };
                Canvas.SetLeft(drip, dripX);
                Canvas.SetTop(drip, y + 20 * scale + d);
                DungeonViewCanvas.Children.Add(drip);
            }
        }

        // Small splatters
        for (int i = 0; i < 5; i++)
        {
            var smallSplat = new Ellipse
            {
                Width = (5 + i % 4) * scale,
                Height = (4 + i % 3) * scale,
                Fill = new SolidColorBrush(bloodColor),
                Opacity = 0.8
            };
            Canvas.SetLeft(smallSplat, x - 15 * scale + i * 12 * scale);
            Canvas.SetTop(smallSplat, y - 10 * scale + (i * 7) % 20 * scale);
            DungeonViewCanvas.Children.Add(smallSplat);
        }
    }

    private void DrawWallCrack(double x, double y, double width, double height)
    {
        var crackColor = new SolidColorBrush(Color.FromRgb(15, 12, 10));
        
        // Main crack
        var crack = new Path
        {
            Data = Geometry.Parse($"M {x},{y} L {x + width * 0.3},{y + height * 0.3} L {x + width * 0.2},{y + height * 0.5} L {x + width * 0.5},{y + height * 0.7} L {x + width * 0.4},{y + height}"),
            Stroke = crackColor,
            StrokeThickness = 3
        };
        DungeonViewCanvas.Children.Add(crack);

        // Branch cracks
        var branch1 = new Line
        {
            X1 = x + width * 0.3,
            Y1 = y + height * 0.3,
            X2 = x + width * 0.6,
            Y2 = y + height * 0.4,
            Stroke = crackColor,
            StrokeThickness = 2
        };
        DungeonViewCanvas.Children.Add(branch1);

        var branch2 = new Line
        {
            X1 = x + width * 0.5,
            Y1 = y + height * 0.7,
            X2 = x + width * 0.8,
            Y2 = y + height * 0.65,
            Stroke = crackColor,
            StrokeThickness = 2
        };
        DungeonViewCanvas.Children.Add(branch2);

        // Mysterious glow from crack (something is behind the wall...)
        var glow = new Ellipse
        {
            Width = width * 0.6,
            Height = height * 0.3,
            Fill = new RadialGradientBrush(
                Color.FromArgb(30, 100, 200, 100),
                Color.FromArgb(0, 50, 100, 50))
        };
        Canvas.SetLeft(glow, x + width * 0.1);
        Canvas.SetTop(glow, y + height * 0.35);
        DungeonViewCanvas.Children.Add(glow);
    }

    private void DrawCreepyWriting(double x, double y, double width, int depth)
    {
        if (depth > 2) return;
        
        double scale = 1.0 / (depth * 0.5 + 0.5);
        var bloodColor = new SolidColorBrush(Color.FromRgb(100, 20, 20));
        
        // Creepy messages
        string[] messages = { "HELP", "RUN", "IT SEES", "BEHIND U", "NO EXIT", "HUNGRY", "SMILE :)" };
        string msg = messages[(_playerX + _playerY + _currentFloor) % messages.Length];

        var text = new TextBlock
        {
            Text = msg,
            FontFamily = new FontFamily("Courier New"),
            FontSize = 14 * scale,
            FontWeight = FontWeights.Bold,
            Foreground = bloodColor
        };
        Canvas.SetLeft(text, x);
        Canvas.SetTop(text, y);
        DungeonViewCanvas.Children.Add(text);

        // Drips from text
        for (int i = 0; i < msg.Length; i++)
        {
            if ((i + _playerX) % 3 == 0)
            {
                double dripX = x + i * 9 * scale;
                for (int d = 0; d < 15; d += 3)
                {
                    var drip = new Rectangle
                    {
                        Width = 2,
                        Height = 3,
                        Fill = bloodColor,
                        Opacity = 1.0 - d / 20.0
                    };
                    Canvas.SetLeft(drip, dripX + 3);
                    Canvas.SetTop(drip, y + 14 * scale + d);
                    DungeonViewCanvas.Children.Add(drip);
                }
            }
        }
    }

    private void DrawPixelTorch(double x, double y, int depth)
    {
        double scale = 1.0 / (depth * 0.5 + 0.5);

        // ANIMATED torch flicker
        double flicker = _torchFlicker;
        double flameSway = Math.Sin(_animationTime * 8) * 3 * scale;

        // Torch bracket (pixel art style)
        var bracket = new Rectangle
        {
            Width = 10 * scale,
            Height = 25 * scale,
            Fill = new SolidColorBrush(Color.FromRgb(70, 50, 30))
        };
        Canvas.SetLeft(bracket, x - 5 * scale);
        Canvas.SetTop(bracket, y);
        DungeonViewCanvas.Children.Add(bracket);

        // ANIMATED Flame glow (flickers and pulses)
        byte glowAlpha = (byte)(40 + flicker * 40);
        double glowSize = 0.8 + flicker * 0.4;
        var glow = new Ellipse
        {
            Width = 80 * scale * glowSize,
            Height = 90 * scale * glowSize,
            Fill = new RadialGradientBrush(
                Color.FromArgb(glowAlpha, 255, 180, 50),
                Color.FromArgb(0, 255, 100, 0))
        };
        Canvas.SetLeft(glow, x - 40 * scale * glowSize + flameSway);
        Canvas.SetTop(glow, y - 55 * scale * glowSize);
        DungeonViewCanvas.Children.Add(glow);

        // Secondary ambient glow (lights up nearby area)
        var ambientGlow = new Ellipse
        {
            Width = 150 * scale * glowSize,
            Height = 120 * scale * glowSize,
            Fill = new RadialGradientBrush(
                Color.FromArgb((byte)(20 + flicker * 15), 255, 150, 50),
                Color.FromArgb(0, 255, 100, 0))
        };
        Canvas.SetLeft(ambientGlow, x - 75 * scale * glowSize);
        Canvas.SetTop(ambientGlow, y - 60 * scale * glowSize);
        DungeonViewCanvas.Children.Add(ambientGlow);

        // ANIMATED Pixel flame layers
        var flameColors = new[] {
            Color.FromRgb(255, (byte)(80 + flicker * 40), 20),   // Outer orange (varies)
            Color.FromRgb(255, (byte)(160 + flicker * 40), 50),  // Middle yellow
            Color.FromRgb(255, 255, (byte)(180 + flicker * 40))  // Inner white-yellow
        };

        double[] baseFlameSizes = { 20, 14, 8 };
        for (int f = 0; f < 3; f++)
        {
            // Each flame layer flickers slightly differently
            double layerFlicker = flicker + Math.Sin(_animationTime * 10 + f * 2) * 0.15;
            double fsize = baseFlameSizes[f] * scale * (0.85 + layerFlicker * 0.3);
            double layerSway = flameSway * (1 - f * 0.2);
            
            var flame = new Polygon
            {
                Points = new PointCollection
                {
                    new Point(x - fsize * 0.6 + layerSway, y),
                    new Point(x - fsize * 0.3 + layerSway * 0.7, y - fsize * 0.5),
                    new Point(x + layerSway * 0.5, y - fsize * (0.9 + layerFlicker * 0.2)),
                    new Point(x + fsize * 0.3 + layerSway * 0.3, y - fsize * 0.5),
                    new Point(x + fsize * 0.6, y)
                },
                Fill = new SolidColorBrush(flameColors[f])
            };
            DungeonViewCanvas.Children.Add(flame);
        }

        // ANIMATED Sparks (float upward and fade)
        int sparkCount = 3 + (int)(_animationTime * 2) % 3;
        for (int s = 0; s < sparkCount; s++)
        {
            double sparkPhase = (_animationTime * 3 + s * 1.5) % 3;
            double sx = x + Math.Sin(_animationTime * 5 + s * 2) * 15 * scale;
            double sy = y - 20 * scale - sparkPhase * 20 * scale;
            double sparkAlpha = Math.Max(0, 1 - sparkPhase / 3);
            double sparkSize = (3 - sparkPhase) * scale;
            
            if (sparkSize > 0)
            {
                var spark = new Ellipse
                {
                    Width = sparkSize,
                    Height = sparkSize,
                    Fill = new SolidColorBrush(Color.FromArgb(
                        (byte)(sparkAlpha * 255),
                        255,
                        (byte)(200 + _random.Next(55)),
                        (byte)(50 + _random.Next(100))))
                };
                Canvas.SetLeft(spark, sx);
                Canvas.SetTop(spark, sy);
                DungeonViewCanvas.Children.Add(spark);
            }
        }
        
        // Smoke wisps rising
        for (int smoke = 0; smoke < 2; smoke++)
        {
            double smokePhase = (_animationTime * 1.5 + smoke * 1.5) % 4;
            double smokeX = x + Math.Sin(_animationTime * 2 + smoke) * 8 * scale;
            double smokeY = y - 30 * scale - smokePhase * 25 * scale;
            byte smokeAlpha = (byte)(30 * (1 - smokePhase / 4));
            double smokeSize = (5 + smokePhase * 3) * scale;
            
            var smokeWisp = new Ellipse
            {
                Width = smokeSize,
                Height = smokeSize * 1.5,
                Fill = new SolidColorBrush(Color.FromArgb(smokeAlpha, 80, 80, 90))
            };
            Canvas.SetLeft(smokeWisp, smokeX - smokeSize / 2);
            Canvas.SetTop(smokeWisp, smokeY);
            DungeonViewCanvas.Children.Add(smokeWisp);
        }
    }

    private void DrawEntitiesInView(double canvasWidth, double canvasHeight, double centerX, double horizonY)
    {
        var (dx, dy) = GetDirectionVector(_playerDir);

        for (int dist = 1; dist <= ViewDistance; dist++)
        {
            int checkX = _playerX + dx * dist;
            int checkY = _playerY + dy * dist;

            if (!IsInBounds(checkX, checkY)) continue;
            if (IsWall(checkX, checkY)) break; // Can't see past walls

            // Draw stairs if present
            if (_map[checkX, checkY] == TileType.StairsDown)
            {
                DrawStairs(centerX, horizonY, canvasHeight, dist);
            }

            // Draw shrine if present
            if (_map[checkX, checkY] == TileType.Shrine)
            {
                DrawShrine(centerX, horizonY, canvasHeight, dist);
            }

            // Draw chest if present
            if (_map[checkX, checkY] == TileType.Chest && !_openedChests.Contains((checkX, checkY)))
            {
                DrawChest(centerX, horizonY, canvasHeight, dist);
            }

            // Draw enemy if present
            if (_enemies.TryGetValue((checkX, checkY), out var enemy))
            {
                DrawEnemyInView(enemy, centerX, horizonY, canvasHeight, dist);
            }
        }
    }

    private void DrawStairs(double centerX, double horizonY, double canvasHeight, int distance)
    {
        double scale = 1.0 / (distance + 1);
        double size = 100 * scale;
        double y = horizonY + canvasHeight * 0.15 * scale;

        // Dark pit background
        var pit = new Ellipse
        {
            Width = size * 1.4,
            Height = size * 0.6,
            Fill = new RadialGradientBrush(Colors.Black, Color.FromRgb(20, 15, 10))
        };
        Canvas.SetLeft(pit, centerX - size * 0.7);
        Canvas.SetTop(pit, y + size * 0.4);
        DungeonViewCanvas.Children.Add(pit);

        // Stone stairs
        for (int step = 0; step < 5; step++)
        {
            double stepY = y + step * size * 0.15;
            double stepWidth = size * (1.3 - step * 0.15);
            byte brightness = (byte)(70 - step * 10);
            
            var stair = new Rectangle
            {
                Width = stepWidth,
                Height = size * 0.12,
                Fill = new LinearGradientBrush(
                    Color.FromRgb(brightness, (byte)(brightness * 0.9), (byte)(brightness * 0.8)),
                    Color.FromRgb((byte)(brightness * 0.7), (byte)(brightness * 0.6), (byte)(brightness * 0.5)),
                    90),
                Stroke = new SolidColorBrush(Color.FromRgb(40, 35, 30)),
                StrokeThickness = 1
            };
            Canvas.SetLeft(stair, centerX - stepWidth / 2);
            Canvas.SetTop(stair, stepY);
            DungeonViewCanvas.Children.Add(stair);
        }

        // Ominous glow from below
        var glow = new Ellipse
        {
            Width = size * 0.8,
            Height = size * 0.4,
            Fill = new RadialGradientBrush(
                Color.FromArgb(60, 100, 50, 150),
                Color.FromArgb(0, 50, 0, 100))
        };
        Canvas.SetLeft(glow, centerX - size * 0.4);
        Canvas.SetTop(glow, y + size * 0.5);
        DungeonViewCanvas.Children.Add(glow);

        // Arrow indicator
        var arrow = new Polygon
        {
            Points = new PointCollection
            {
                new Point(centerX - size * 0.15, y + size * 0.25),
                new Point(centerX + size * 0.15, y + size * 0.25),
                new Point(centerX, y + size * 0.5)
            },
            Fill = new SolidColorBrush(Color.FromRgb(200, 180, 100)),
            Opacity = 0.8
        };
        DungeonViewCanvas.Children.Add(arrow);
    }

    private void DrawShrine(double centerX, double horizonY, double canvasHeight, int distance)
    {
        double scale = 1.0 / (distance + 1);
        double size = 80 * scale;
        double y = horizonY + canvasHeight * 0.1 * scale;

        // Stone pedestal
        var pedestal = new Polygon
        {
            Points = new PointCollection
            {
                new Point(centerX - size * 0.4, y + size * 0.9),
                new Point(centerX + size * 0.4, y + size * 0.9),
                new Point(centerX + size * 0.3, y + size * 0.6),
                new Point(centerX - size * 0.3, y + size * 0.6)
            },
            Fill = new LinearGradientBrush(
                Color.FromRgb(80, 70, 90),
                Color.FromRgb(50, 45, 60), 90)
        };
        DungeonViewCanvas.Children.Add(pedestal);

        // Shrine orb
        var orb = new Ellipse
        {
            Width = size * 0.5,
            Height = size * 0.5,
            Fill = new RadialGradientBrush(
                Color.FromRgb(200, 150, 255),
                Color.FromRgb(100, 50, 150))
        };
        Canvas.SetLeft(orb, centerX - size * 0.25);
        Canvas.SetTop(orb, y + size * 0.15);
        DungeonViewCanvas.Children.Add(orb);

        // Inner glow
        var innerGlow = new Ellipse
        {
            Width = size * 0.25,
            Height = size * 0.25,
            Fill = new RadialGradientBrush(Colors.White, Color.FromArgb(0, 255, 255, 255))
        };
        Canvas.SetLeft(innerGlow, centerX - size * 0.125);
        Canvas.SetTop(innerGlow, y + size * 0.27);
        DungeonViewCanvas.Children.Add(innerGlow);

        // Outer glow aura
        var aura = new Ellipse
        {
            Width = size * 1.2,
            Height = size * 1.2,
            Fill = new RadialGradientBrush(
                Color.FromArgb(50, 200, 100, 255),
                Color.FromArgb(0, 150, 50, 200))
        };
        Canvas.SetLeft(aura, centerX - size * 0.6);
        Canvas.SetTop(aura, y - size * 0.1);
        DungeonViewCanvas.Children.Add(aura);

        // Floating particles
        for (int i = 0; i < 5; i++)
        {
            double px = centerX - size * 0.3 + _random.NextDouble() * size * 0.6;
            double py = y + _random.NextDouble() * size * 0.5;
            var particle = new Ellipse
            {
                Width = size * 0.05,
                Height = size * 0.05,
                Fill = new SolidColorBrush(Color.FromArgb(150, 255, 200, 255))
            };
            Canvas.SetLeft(particle, px);
            Canvas.SetTop(particle, py);
            DungeonViewCanvas.Children.Add(particle);
        }
    }

    private void DrawChest(double centerX, double horizonY, double canvasHeight, int distance)
    {
        double scale = 1.0 / (distance + 1);
        double size = 60 * scale;
        double y = horizonY + canvasHeight * 0.15 * scale;

        // Shadow
        var shadow = new Ellipse
        {
            Width = size * 1.2,
            Height = size * 0.3,
            Fill = new RadialGradientBrush(
                Color.FromArgb(80, 0, 0, 0),
                Color.FromArgb(0, 0, 0, 0))
        };
        Canvas.SetLeft(shadow, centerX - size * 0.6);
        Canvas.SetTop(shadow, y + size * 0.65);
        DungeonViewCanvas.Children.Add(shadow);

        // Chest body
        var body = new Rectangle
        {
            Width = size,
            Height = size * 0.6,
            Fill = new LinearGradientBrush(
                Color.FromRgb(160, 100, 50),
                Color.FromRgb(100, 60, 30), 90),
            Stroke = new SolidColorBrush(Color.FromRgb(80, 50, 25)),
            StrokeThickness = 2,
            RadiusX = 3,
            RadiusY = 3
        };
        Canvas.SetLeft(body, centerX - size * 0.5);
        Canvas.SetTop(body, y + size * 0.2);
        DungeonViewCanvas.Children.Add(body);

        // Chest lid
        var lid = new Rectangle
        {
            Width = size * 1.05,
            Height = size * 0.25,
            Fill = new LinearGradientBrush(
                Color.FromRgb(140, 90, 45),
                Color.FromRgb(110, 70, 35), 90),
            Stroke = new SolidColorBrush(Color.FromRgb(80, 50, 25)),
            StrokeThickness = 2,
            RadiusX = 5,
            RadiusY = 5
        };
        Canvas.SetLeft(lid, centerX - size * 0.525);
        Canvas.SetTop(lid, y);
        DungeonViewCanvas.Children.Add(lid);

        // Metal bands
        for (int i = 0; i < 3; i++)
        {
            var band = new Rectangle
            {
                Width = size * 0.08,
                Height = size * 0.6,
                Fill = new SolidColorBrush(Color.FromRgb(180, 150, 50))
            };
            Canvas.SetLeft(band, centerX - size * 0.4 + i * size * 0.35);
            Canvas.SetTop(band, y + size * 0.2);
            DungeonViewCanvas.Children.Add(band);
        }

        // Lock
        var lockBase = new Ellipse
        {
            Width = size * 0.15,
            Height = size * 0.15,
            Fill = new RadialGradientBrush(Colors.Gold, Color.FromRgb(180, 140, 20))
        };
        Canvas.SetLeft(lockBase, centerX - size * 0.075);
        Canvas.SetTop(lockBase, y + size * 0.35);
        DungeonViewCanvas.Children.Add(lockBase);

        // Keyhole
        var keyhole = new Ellipse
        {
            Width = size * 0.04,
            Height = size * 0.06,
            Fill = Brushes.Black
        };
        Canvas.SetLeft(keyhole, centerX - size * 0.02);
        Canvas.SetTop(keyhole, y + size * 0.38);
        DungeonViewCanvas.Children.Add(keyhole);

        // Sparkle
        var sparkle = new Ellipse
        {
            Width = size * 0.08,
            Height = size * 0.08,
            Fill = new RadialGradientBrush(Colors.White, Color.FromArgb(0, 255, 255, 255))
        };
        Canvas.SetLeft(sparkle, centerX + size * 0.25);
        Canvas.SetTop(sparkle, y + size * 0.1);
        DungeonViewCanvas.Children.Add(sparkle);
    }

    private void DrawEnemyInView(Enemy enemy, double centerX, double horizonY, double canvasHeight, int distance)
    {
        double scale = 1.0 / (distance + 1);
        double size = 100 * scale;
        double y = horizonY - size * 0.5;

        // Draw shadow underneath
        var shadow = new Ellipse
        {
            Width = size * 1.2,
            Height = size * 0.3,
            Fill = new RadialGradientBrush(
                Color.FromArgb(100, 0, 0, 0),
                Color.FromArgb(0, 0, 0, 0))
        };
        Canvas.SetLeft(shadow, centerX - size * 0.6);
        Canvas.SetTop(shadow, y + size * 1.1);
        DungeonViewCanvas.Children.Add(shadow);

        // Enemy-specific rendering
        switch (enemy.Name)
        {
            case "Smile Dog :)":
                DrawSmileDog(centerX, y, size);
                break;
            case "The Meat Child":
                DrawMeatChild(centerX, y, size);
                break;
            case "Grandma's Twin":
                DrawGrandmasTwin(centerX, y, size);
                break;
            case "Man In The Wall":
                DrawManInWall(centerX, y, size);
                break;
            case "Your Friendly Helper":
                DrawFriendlyHelper(centerX, y, size);
                break;
            case "Your Reflection":
                DrawYourReflection(centerX, y, size);
                break;
            case "It's Listening":
                DrawItsListening(centerX, y, size);
                break;
            case "THE HOST":
                DrawTheHost(centerX, y, size);
                break;
            default:
                DrawGenericCreep(centerX, y, size, Colors.Pink);
                break;
        }

        // Creepy aura for close enemies
        if (distance <= 1)
        {
            var aura = new Ellipse
            {
                Width = size * 1.8,
                Height = size * 2,
                Fill = new RadialGradientBrush(
                    Color.FromArgb(30, 150, 0, 0),
                    Color.FromArgb(0, 0, 0, 0))
            };
            Canvas.SetLeft(aura, centerX - size * 0.9);
            Canvas.SetTop(aura, y - size * 0.3);
            DungeonViewCanvas.Children.Add(aura);
        }
    }

    private void DrawSmileDog(double cx, double y, double size)
    {
        // Dog body
        var body = new Ellipse
        {
            Width = size * 0.9,
            Height = size * 0.7,
            Fill = new LinearGradientBrush(Colors.Tan, Colors.SaddleBrown, 90)
        };
        Canvas.SetLeft(body, cx - size * 0.45);
        Canvas.SetTop(body, y + size * 0.4);
        DungeonViewCanvas.Children.Add(body);

        // Head
        var head = new Ellipse
        {
            Width = size * 0.7,
            Height = size * 0.6,
            Fill = new SolidColorBrush(Colors.Tan),
            Stroke = Brushes.SaddleBrown,
            StrokeThickness = 2
        };
        Canvas.SetLeft(head, cx - size * 0.35);
        Canvas.SetTop(head, y);
        DungeonViewCanvas.Children.Add(head);

        // Creepy wide smile
        var smile = new Path
        {
            Data = Geometry.Parse($"M {cx - size * 0.25},{y + size * 0.35} Q {cx},{y + size * 0.55} {cx + size * 0.25},{y + size * 0.35}"),
            Stroke = Brushes.DarkRed,
            StrokeThickness = 3,
            Fill = new SolidColorBrush(Color.FromRgb(40, 0, 0))
        };
        DungeonViewCanvas.Children.Add(smile);

        // Teeth
        for (int i = 0; i < 8; i++)
        {
            double toothX = cx - size * 0.2 + i * size * 0.05;
            var tooth = new Rectangle
            {
                Width = size * 0.04,
                Height = size * 0.08,
                Fill = Brushes.White
            };
            Canvas.SetLeft(tooth, toothX);
            Canvas.SetTop(tooth, y + size * 0.35);
            DungeonViewCanvas.Children.Add(tooth);
        }

        // Wide staring eyes
        DrawCreepyEyes(cx, y + size * 0.15, size * 0.15, true);
    }

    private void DrawMeatChild(double cx, double y, double size)
    {
        // Fleshy mass body
        var body = new Ellipse
        {
            Width = size * 0.8,
            Height = size,
            Fill = new RadialGradientBrush(
                Color.FromRgb(200, 100, 100),
                Color.FromRgb(120, 40, 40))
        };
        Canvas.SetLeft(body, cx - size * 0.4);
        Canvas.SetTop(body, y + size * 0.2);
        DungeonViewCanvas.Children.Add(body);

        // Veins
        for (int i = 0; i < 5; i++)
        {
            var vein = new Path
            {
                Data = Geometry.Parse($"M {cx - size * 0.3 + i * size * 0.15},{y + size * 0.3} Q {cx},{y + size * 0.8} {cx + size * 0.2},{y + size * 1.1}"),
                Stroke = new SolidColorBrush(Color.FromRgb(80, 20, 20)),
                StrokeThickness = 2,
                Opacity = 0.6
            };
            DungeonViewCanvas.Children.Add(vein);
        }

        // Small misshapen head
        var head = new Ellipse
        {
            Width = size * 0.4,
            Height = size * 0.35,
            Fill = new SolidColorBrush(Color.FromRgb(180, 120, 120))
        };
        Canvas.SetLeft(head, cx - size * 0.2);
        Canvas.SetTop(head, y);
        DungeonViewCanvas.Children.Add(head);

        // Sad/creepy eyes
        DrawCreepyEyes(cx, y + size * 0.1, size * 0.08, false);

        // Dripping
        for (int i = 0; i < 3; i++)
        {
            var drip = new Ellipse
            {
                Width = size * 0.05,
                Height = size * 0.12,
                Fill = new SolidColorBrush(Color.FromRgb(150, 50, 50))
            };
            Canvas.SetLeft(drip, cx - size * 0.2 + i * size * 0.15);
            Canvas.SetTop(drip, y + size * 1.15 + i * size * 0.05);
            DungeonViewCanvas.Children.Add(drip);
        }
    }

    private void DrawGrandmasTwin(double cx, double y, double size)
    {
        // Dress/robe
        var dress = new Polygon
        {
            Points = new PointCollection
            {
                new Point(cx - size * 0.2, y + size * 0.4),
                new Point(cx + size * 0.2, y + size * 0.4),
                new Point(cx + size * 0.4, y + size * 1.2),
                new Point(cx - size * 0.4, y + size * 1.2)
            },
            Fill = new SolidColorBrush(Color.FromRgb(80, 60, 80))
        };
        DungeonViewCanvas.Children.Add(dress);

        // Face (unnaturally pale)
        var face = new Ellipse
        {
            Width = size * 0.5,
            Height = size * 0.55,
            Fill = new SolidColorBrush(Color.FromRgb(240, 230, 240)),
            Stroke = new SolidColorBrush(Color.FromRgb(200, 180, 200)),
            StrokeThickness = 1
        };
        Canvas.SetLeft(face, cx - size * 0.25);
        Canvas.SetTop(face, y);
        DungeonViewCanvas.Children.Add(face);

        // Gray hair
        for (int i = 0; i < 7; i++)
        {
            var hair = new Path
            {
                Data = Geometry.Parse($"M {cx - size * 0.2 + i * size * 0.07},{y - size * 0.05} Q {cx - size * 0.25 + i * size * 0.08},{y + size * 0.15} {cx - size * 0.3 + i * size * 0.1},{y + size * 0.4}"),
                Stroke = new SolidColorBrush(Color.FromRgb(180, 180, 190)),
                StrokeThickness = 3
            };
            DungeonViewCanvas.Children.Add(hair);
        }

        // Black pit eyes
        var leftEye = new Ellipse { Width = size * 0.1, Height = size * 0.12, Fill = Brushes.Black };
        Canvas.SetLeft(leftEye, cx - size * 0.15);
        Canvas.SetTop(leftEye, y + size * 0.15);
        DungeonViewCanvas.Children.Add(leftEye);

        var rightEye = new Ellipse { Width = size * 0.1, Height = size * 0.12, Fill = Brushes.Black };
        Canvas.SetLeft(rightEye, cx + size * 0.05);
        Canvas.SetTop(rightEye, y + size * 0.15);
        DungeonViewCanvas.Children.Add(rightEye);

        // Unsettling smile
        var smile = new Path
        {
            Data = Geometry.Parse($"M {cx - size * 0.12},{y + size * 0.35} Q {cx},{y + size * 0.42} {cx + size * 0.12},{y + size * 0.35}"),
            Stroke = new SolidColorBrush(Color.FromRgb(150, 100, 100)),
            StrokeThickness = 2
        };
        DungeonViewCanvas.Children.Add(smile);
    }

    private void DrawManInWall(double cx, double y, double size)
    {
        // Partial figure emerging from nothing
        var torso = new Ellipse
        {
            Width = size * 0.5,
            Height = size * 0.8,
            Fill = new LinearGradientBrush(
                Color.FromRgb(60, 60, 70),
                Color.FromArgb(0, 60, 60, 70), 90),
            Opacity = 0.8
        };
        Canvas.SetLeft(torso, cx - size * 0.25);
        Canvas.SetTop(torso, y + size * 0.3);
        DungeonViewCanvas.Children.Add(torso);

        // Reaching arm
        var arm = new Path
        {
            Data = Geometry.Parse($"M {cx + size * 0.2},{y + size * 0.5} Q {cx + size * 0.5},{y + size * 0.3} {cx + size * 0.6},{y + size * 0.2}"),
            Stroke = new SolidColorBrush(Color.FromRgb(80, 80, 90)),
            StrokeThickness = size * 0.08
        };
        DungeonViewCanvas.Children.Add(arm);

        // Hand with long fingers
        for (int i = 0; i < 5; i++)
        {
            var finger = new Line
            {
                X1 = cx + size * 0.6,
                Y1 = y + size * 0.2,
                X2 = cx + size * 0.7 + i * size * 0.03,
                Y2 = y + size * 0.05 + i * size * 0.02,
                Stroke = new SolidColorBrush(Color.FromRgb(70, 70, 80)),
                StrokeThickness = 3
            };
            DungeonViewCanvas.Children.Add(finger);
        }

        // Featureless face
        var face = new Ellipse
        {
            Width = size * 0.35,
            Height = size * 0.4,
            Fill = new SolidColorBrush(Color.FromRgb(50, 50, 60))
        };
        Canvas.SetLeft(face, cx - size * 0.175);
        Canvas.SetTop(face, y);
        DungeonViewCanvas.Children.Add(face);

        // Single glowing eye
        var eye = new Ellipse
        {
            Width = size * 0.08,
            Height = size * 0.1,
            Fill = new RadialGradientBrush(Colors.White, Color.FromRgb(150, 150, 200))
        };
        Canvas.SetLeft(eye, cx - size * 0.04);
        Canvas.SetTop(eye, y + size * 0.15);
        DungeonViewCanvas.Children.Add(eye);
    }

    private void DrawFriendlyHelper(double cx, double y, double size)
    {
        // Bright yellow humanoid - too cheerful
        var body = new Ellipse
        {
            Width = size * 0.6,
            Height = size * 0.9,
            Fill = new RadialGradientBrush(Colors.LightYellow, Colors.Gold)
        };
        Canvas.SetLeft(body, cx - size * 0.3);
        Canvas.SetTop(body, y + size * 0.3);
        DungeonViewCanvas.Children.Add(body);

        // Head
        var head = new Ellipse
        {
            Width = size * 0.5,
            Height = size * 0.5,
            Fill = new SolidColorBrush(Colors.LightYellow),
            Stroke = Brushes.Gold,
            StrokeThickness = 2
        };
        Canvas.SetLeft(head, cx - size * 0.25);
        Canvas.SetTop(head, y - size * 0.05);
        DungeonViewCanvas.Children.Add(head);

        // Overly wide smile
        var smile = new Path
        {
            Data = Geometry.Parse($"M {cx - size * 0.2},{y + size * 0.2} Q {cx},{y + size * 0.35} {cx + size * 0.2},{y + size * 0.2}"),
            Stroke = Brushes.Black,
            StrokeThickness = 3,
            Fill = new SolidColorBrush(Color.FromRgb(255, 150, 150))
        };
        DungeonViewCanvas.Children.Add(smile);

        // Unblinking eyes
        DrawCreepyEyes(cx, y + size * 0.1, size * 0.1, true);

        // Waving arms
        var leftArm = new Path
        {
            Data = Geometry.Parse($"M {cx - size * 0.3},{y + size * 0.5} Q {cx - size * 0.5},{y + size * 0.2} {cx - size * 0.4},{y}"),
            Stroke = Brushes.Gold,
            StrokeThickness = size * 0.08
        };
        DungeonViewCanvas.Children.Add(leftArm);

        var rightArm = new Path
        {
            Data = Geometry.Parse($"M {cx + size * 0.3},{y + size * 0.5} Q {cx + size * 0.5},{y + size * 0.2} {cx + size * 0.4},{y}"),
            Stroke = Brushes.Gold,
            StrokeThickness = size * 0.08
        };
        DungeonViewCanvas.Children.Add(rightArm);
    }

    private void DrawYourReflection(double cx, double y, double size)
    {
        // Translucent purple humanoid
        var body = new Ellipse
        {
            Width = size * 0.5,
            Height = size,
            Fill = new RadialGradientBrush(
                Color.FromArgb(150, 150, 100, 200),
                Color.FromArgb(50, 100, 50, 150)),
            Stroke = new SolidColorBrush(Color.FromArgb(100, 200, 150, 255)),
            StrokeThickness = 2
        };
        Canvas.SetLeft(body, cx - size * 0.25);
        Canvas.SetTop(body, y + size * 0.2);
        DungeonViewCanvas.Children.Add(body);

        // Mirror-like face
        var face = new Ellipse
        {
            Width = size * 0.4,
            Height = size * 0.45,
            Fill = new LinearGradientBrush(
                Color.FromArgb(180, 200, 200, 220),
                Color.FromArgb(100, 150, 150, 180), 45)
        };
        Canvas.SetLeft(face, cx - size * 0.2);
        Canvas.SetTop(face, y);
        DungeonViewCanvas.Children.Add(face);

        // Your face staring back (simplified creepy version)
        var leftEye = new Ellipse
        {
            Width = size * 0.08,
            Height = size * 0.1,
            Fill = new RadialGradientBrush(Colors.White, Colors.LightGray)
        };
        Canvas.SetLeft(leftEye, cx - size * 0.1);
        Canvas.SetTop(leftEye, y + size * 0.12);
        DungeonViewCanvas.Children.Add(leftEye);

        var rightEye = new Ellipse
        {
            Width = size * 0.08,
            Height = size * 0.1,
            Fill = new RadialGradientBrush(Colors.White, Colors.LightGray)
        };
        Canvas.SetLeft(rightEye, cx + size * 0.02);
        Canvas.SetTop(rightEye, y + size * 0.12);
        DungeonViewCanvas.Children.Add(rightEye);

        // Pupils following you
        var leftPupil = new Ellipse { Width = size * 0.04, Height = size * 0.05, Fill = Brushes.Black };
        Canvas.SetLeft(leftPupil, cx - size * 0.08);
        Canvas.SetTop(leftPupil, y + size * 0.14);
        DungeonViewCanvas.Children.Add(leftPupil);

        var rightPupil = new Ellipse { Width = size * 0.04, Height = size * 0.05, Fill = Brushes.Black };
        Canvas.SetLeft(rightPupil, cx + size * 0.04);
        Canvas.SetTop(rightPupil, y + size * 0.14);
        DungeonViewCanvas.Children.Add(rightPupil);
    }

    private void DrawItsListening(double cx, double y, double size)
    {
        // Pure black mass
        var mass = new Ellipse
        {
            Width = size * 0.8,
            Height = size * 1.2,
            Fill = new RadialGradientBrush(Colors.Black, Color.FromArgb(200, 0, 0, 0))
        };
        Canvas.SetLeft(mass, cx - size * 0.4);
        Canvas.SetTop(mass, y);
        DungeonViewCanvas.Children.Add(mass);

        // Multiple ears scattered on the mass
        for (int i = 0; i < 7; i++)
        {
            double earX = cx - size * 0.3 + _random.NextDouble() * size * 0.6;
            double earY = y + size * 0.1 + _random.NextDouble() * size * 0.8;
            var ear = new Ellipse
            {
                Width = size * 0.15,
                Height = size * 0.2,
                Fill = new SolidColorBrush(Color.FromRgb(60, 40, 50)),
                Stroke = new SolidColorBrush(Color.FromRgb(80, 50, 60)),
                StrokeThickness = 1
            };
            Canvas.SetLeft(ear, earX);
            Canvas.SetTop(ear, earY);
            DungeonViewCanvas.Children.Add(ear);

            // Ear canal
            var canal = new Ellipse
            {
                Width = size * 0.05,
                Height = size * 0.07,
                Fill = Brushes.Black
            };
            Canvas.SetLeft(canal, earX + size * 0.05);
            Canvas.SetTop(canal, earY + size * 0.06);
            DungeonViewCanvas.Children.Add(canal);
        }

        // Single red eye in center
        var eye = new Ellipse
        {
            Width = size * 0.12,
            Height = size * 0.15,
            Fill = new RadialGradientBrush(Colors.Red, Colors.DarkRed)
        };
        Canvas.SetLeft(eye, cx - size * 0.06);
        Canvas.SetTop(eye, y + size * 0.4);
        DungeonViewCanvas.Children.Add(eye);

        var pupil = new Ellipse { Width = size * 0.04, Height = size * 0.08, Fill = Brushes.Black };
        Canvas.SetLeft(pupil, cx - size * 0.02);
        Canvas.SetTop(pupil, y + size * 0.44);
        DungeonViewCanvas.Children.Add(pupil);
    }

    private void DrawTheHost(double cx, double y, double size)
    {
        // Large crimson entity
        var body = new Ellipse
        {
            Width = size * 1.2,
            Height = size * 1.4,
            Fill = new RadialGradientBrush(Colors.Crimson, Colors.DarkRed)
        };
        Canvas.SetLeft(body, cx - size * 0.6);
        Canvas.SetTop(body, y);
        DungeonViewCanvas.Children.Add(body);

        // Crown of horns
        for (int i = 0; i < 5; i++)
        {
            double hornX = cx - size * 0.3 + i * size * 0.15;
            var horn = new Polygon
            {
                Points = new PointCollection
                {
                    new Point(hornX - size * 0.05, y + size * 0.1),
                    new Point(hornX, y - size * 0.2 - i % 2 * size * 0.1),
                    new Point(hornX + size * 0.05, y + size * 0.1)
                },
                Fill = new SolidColorBrush(Color.FromRgb(40, 20, 20))
            };
            DungeonViewCanvas.Children.Add(horn);
        }

        // Multiple glowing eyes
        for (int row = 0; row < 2; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                double eyeX = cx - size * 0.25 + col * size * 0.2;
                double eyeY = y + size * 0.3 + row * size * 0.2;
                var eye = new Ellipse
                {
                    Width = size * 0.1,
                    Height = size * 0.12,
                    Fill = new RadialGradientBrush(Colors.Yellow, Colors.Orange)
                };
                Canvas.SetLeft(eye, eyeX);
                Canvas.SetTop(eye, eyeY);
                DungeonViewCanvas.Children.Add(eye);

                var pupil = new Ellipse { Width = size * 0.03, Height = size * 0.06, Fill = Brushes.Black };
                Canvas.SetLeft(pupil, eyeX + size * 0.035);
                Canvas.SetTop(pupil, eyeY + size * 0.03);
                DungeonViewCanvas.Children.Add(pupil);
            }
        }

        // Gaping maw
        var mouth = new Ellipse
        {
            Width = size * 0.5,
            Height = size * 0.3,
            Fill = Brushes.Black
        };
        Canvas.SetLeft(mouth, cx - size * 0.25);
        Canvas.SetTop(mouth, y + size * 0.8);
        DungeonViewCanvas.Children.Add(mouth);

        // Sharp teeth
        for (int i = 0; i < 8; i++)
        {
            var tooth = new Polygon
            {
                Points = new PointCollection
                {
                    new Point(cx - size * 0.2 + i * size * 0.05, y + size * 0.8),
                    new Point(cx - size * 0.18 + i * size * 0.05, y + size * 0.9),
                    new Point(cx - size * 0.16 + i * size * 0.05, y + size * 0.8)
                },
                Fill = Brushes.White
            };
            DungeonViewCanvas.Children.Add(tooth);
        }

        // Dark aura
        var aura = new Ellipse
        {
            Width = size * 2,
            Height = size * 2.2,
            Fill = new RadialGradientBrush(
                Color.FromArgb(0, 50, 0, 0),
                Color.FromArgb(80, 100, 0, 0))
        };
        Canvas.SetLeft(aura, cx - size);
        Canvas.SetTop(aura, y - size * 0.3);
        DungeonViewCanvas.Children.Add(aura);
    }

    private void DrawGenericCreep(double cx, double y, double size, Color baseColor)
    {
        var body = new Ellipse
        {
            Width = size,
            Height = size * 1.2,
            Fill = new SolidColorBrush(baseColor),
            Stroke = Brushes.Black,
            StrokeThickness = 2
        };
        Canvas.SetLeft(body, cx - size / 2);
        Canvas.SetTop(body, y);
        DungeonViewCanvas.Children.Add(body);

        DrawCreepyEyes(cx, y + size * 0.3, size * 0.12, true);
    }

    private void DrawCreepyEyes(double cx, double y, double eyeSize, bool wide)
    {
        double spacing = eyeSize * 2;
        
        // Eye whites
        var leftWhite = new Ellipse
        {
            Width = eyeSize * (wide ? 1.5 : 1),
            Height = eyeSize * (wide ? 1.8 : 1.2),
            Fill = Brushes.White,
            Stroke = wide ? Brushes.Red : Brushes.Gray,
            StrokeThickness = wide ? 2 : 1
        };
        Canvas.SetLeft(leftWhite, cx - spacing);
        Canvas.SetTop(leftWhite, y);
        DungeonViewCanvas.Children.Add(leftWhite);

        var rightWhite = new Ellipse
        {
            Width = eyeSize * (wide ? 1.5 : 1),
            Height = eyeSize * (wide ? 1.8 : 1.2),
            Fill = Brushes.White,
            Stroke = wide ? Brushes.Red : Brushes.Gray,
            StrokeThickness = wide ? 2 : 1
        };
        Canvas.SetLeft(rightWhite, cx + spacing * 0.3);
        Canvas.SetTop(rightWhite, y);
        DungeonViewCanvas.Children.Add(rightWhite);

        // Pupils (red for wide/creepy)
        var pupilColor = wide ? Brushes.DarkRed : Brushes.Black;
        var leftPupil = new Ellipse
        {
            Width = eyeSize * 0.5,
            Height = eyeSize * (wide ? 0.9 : 0.6),
            Fill = pupilColor
        };
        Canvas.SetLeft(leftPupil, cx - spacing + eyeSize * 0.3);
        Canvas.SetTop(leftPupil, y + eyeSize * 0.3);
        DungeonViewCanvas.Children.Add(leftPupil);

        var rightPupil = new Ellipse
        {
            Width = eyeSize * 0.5,
            Height = eyeSize * (wide ? 0.9 : 0.6),
            Fill = pupilColor
        };
        Canvas.SetLeft(rightPupil, cx + spacing * 0.3 + eyeSize * 0.3);
        Canvas.SetTop(rightPupil, y + eyeSize * 0.3);
        DungeonViewCanvas.Children.Add(rightPupil);

        // Bloodshot lines for wide eyes
        if (wide)
        {
            for (int i = 0; i < 3; i++)
            {
                var vein = new Line
                {
                    X1 = cx - spacing + eyeSize * 0.1,
                    Y1 = y + eyeSize * 0.2 + i * eyeSize * 0.4,
                    X2 = cx - spacing + eyeSize * 0.5,
                    Y2 = y + eyeSize * 0.5,
                    Stroke = Brushes.Red,
                    StrokeThickness = 0.5,
                    Opacity = 0.7
                };
                DungeonViewCanvas.Children.Add(vein);
            }
        }
    }

    private bool IsWall(int x, int y)
    {
        if (!IsInBounds(x, y)) return true;
        return _map[x, y] == TileType.Wall;
    }

    private bool IsInBounds(int x, int y)
    {
        return x >= 0 && x < MapWidth && y >= 0 && y < MapHeight;
    }
    #endregion

    #region UI Updates
    private void UpdateUI()
    {
        // Health bar
        double healthPercent = (double)_health / _maxHealth;
        HealthBar.Width = HealthBar.Parent is Border parent ? parent.ActualWidth * healthPercent : 100 * healthPercent;
        HealthText.Text = $"{_health}/{_maxHealth}";

        // Mana bar
        double manaPercent = (double)_mana / _maxMana;
        ManaBar.Width = ManaBar.Parent is Border parent2 ? parent2.ActualWidth * manaPercent : 100 * manaPercent;
        ManaText.Text = $"{_mana}/{_maxMana}";

        // XP bar
        double xpPercent = (double)_xp / _xpToLevel;
        XPBar.Width = XPBar.Parent is Border parent3 ? parent3.ActualWidth * xpPercent : 100 * xpPercent;

        // Stats
        AttackText.Text = _attack.ToString();
        DefenseText.Text = _defense.ToString();
        LevelText.Text = _level.ToString();
        GoldText.Text = _gold.ToString();

        // Floor
        FloorText.Text = $"Floor {_currentFloor}";

        // Update compass
        UpdateCompass();

        // Refresh inventory display
        InventoryList.Items.Refresh();
    }

    private void UpdateCompass()
    {
        CompassText.Text = _playerDir.ToString().ToUpper();
    }

    private void AddMessage(string message)
    {
        _messages.Insert(0, message);
        if (_messages.Count > 50)
            _messages.RemoveAt(_messages.Count - 1);
    }

    private void CheckDeath()
    {
        if (_health <= 0)
        {
            _health = 0;
            UpdateUI();

            var deathTitles = new[] { "YOU BECAME ONE OF US", "FRIENDSHIP ACHIEVED", "YOU'RE HOME NOW :)", "FINALLY" };
            var deathSubtitles = new[] {
                $"Floor {_currentFloor}, Level {_level}. We'll keep your teeth.",
                $"You made it to floor {_currentFloor}! Don't worry, we remember you.",
                $"Floor {_currentFloor}. You were always here. You know that, right?",
                $"Level {_level} wasn't enough. Nothing is ever enough. :)"
            };
            GameOverTitle.Text = deathTitles[_random.Next(deathTitles.Length)];
            GameOverSubtitle.Text = deathSubtitles[_random.Next(deathSubtitles.Length)];
            GameOverOverlay.Visibility = Visibility.Visible;

            if (_inCombat)
            {
                EndCombat(false);
            }
        }
    }

    private void RestartGame_Click(object sender, RoutedEventArgs e)
    {
        StartNewGame();
    }
    #endregion

    #region Helper Classes
    private class Enemy
    {
        public string Name { get; }
        public int MaxHealth { get; }
        public int Health { get; set; }
        public int Attack { get; }
        public int Defense { get; }
        public int XPReward { get; }
        public int GoldReward { get; }

        public Enemy(string name, int maxHealth, int health, int attack, int defense, int xpReward, int goldReward)
        {
            Name = name;
            MaxHealth = maxHealth;
            Health = health;
            Attack = attack;
            Defense = defense;
            XPReward = xpReward;
            GoldReward = goldReward;
        }
    }

    private class InventoryItem
    {
        public string Icon { get; }
        public string Name { get; }
        public int Quantity { get; set; }

        public InventoryItem(string icon, string name, int quantity)
        {
            Icon = icon;
            Name = name;
            Quantity = quantity;
        }
    }
    #endregion
}
