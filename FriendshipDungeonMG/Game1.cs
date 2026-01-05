using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FriendshipDungeonMG;

public class Game1 : Game
{
    #region Constants
    private const int MapWidth = 24;
    private const int MapHeight = 24;
    private const int TextureSize = 64;
    #endregion

    #region Screen Dimensions (set at runtime for fullscreen)
    private int ScreenWidth;
    private int ScreenHeight;
    private int RaycastWidth;
    private int RaycastHeight;
    #endregion

    #region Graphics
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private SpriteFont _font = null!;
    private TextureGenerator _textureGen = null!;
    private RenderTarget2D _viewport = null!;
    private Texture2D _pixelTexture = null!;
    
    // Raycasting buffers
    private Color[] _screenBuffer = null!;
    private double[] _zBuffer = null!;
    private Texture2D _screenTexture = null!;
    #endregion

    #region Player State (Grid-based with smooth animation)
    // Grid position (integer tile coordinates)
    private int _playerX = 1, _playerY = 1;
    private Direction _playerDir = Direction.North;
    
    // Smooth animation interpolation
    private double _posX = 1.5, _posY = 1.5;    // Rendered position (smooth)
    private double _targetX = 1.5, _targetY = 1.5; // Target position
    private double _dirX = 0.0, _dirY = -1.0;   // Direction vector
    private double _targetDirX = 0.0, _targetDirY = -1.0;
    private double _planeX = 0.66, _planeY = 0.0; // Camera plane (FOV)
    private double _targetPlaneX = 0.66, _targetPlaneY = 0.0;
    private double _pitch = 0.0;
    private double _headBob = 0.0;
    private bool _isAnimating = false;
    private const double MoveAnimSpeed = 8.0;  // Speed of position interpolation
    private const double TurnAnimSpeed = 10.0; // Speed of rotation interpolation
    #endregion

    #region Game State
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
    private int[,] _worldMap = new int[MapWidth, MapHeight];
    private bool[,] _explored = new bool[MapWidth, MapHeight];
    private Dictionary<(int, int), Enemy> _enemies = new();
    private Dictionary<(int, int), Sprite> _sprites = new();
    private HashSet<(int, int)> _openedChests = new();
    private HashSet<(int, int)> _usedShrines = new();
    #endregion

    #region Combat & UI
    private GameState _gameState = GameState.Exploring;
    private Enemy? _currentEnemy;
    private (int x, int y) _currentEnemyPos;
    private List<string> _messages = new();
    private List<InventoryItem> _inventory = new();
    private List<Weapon> _weapons = new();
    private int _equippedWeaponIndex = 0;
    private Weapon _equippedWeapon = null!;
    private readonly Random _random = new();
    
    // Combat timing
    private float _playerAttackCooldown = 0f;
    private float _enemyAttackTimer = 0f;
    private const float PlayerAttackDelay = 0.5f;  // Time between player attacks
    private const float EnemyAttackDelay = 1.2f;   // Time between enemy attacks
    private bool _combatStarted = false;
    
    // Combat animations
    private float _weaponSwingTimer = 0f;
    private const float WeaponSwingDuration = 0.3f;
    private float _enemyHitFlash = 0f;
    private const float HitFlashDuration = 0.15f;
    private float _screenShake = 0f;
    private const float ShakeDuration = 0.2f;
    private float _damageFlash = 0f;
    private const float DamageFlashDuration = 0.3f;
    #endregion

    #region Input
    private KeyboardState _previousKeyState;
    private KeyboardState _currentKeyState;
    private MouseState _previousMouseState;
    private MouseState _currentMouseState;
    private float _moveTimer = 0f;
    private const float MoveDelay = 0.15f; // Time between continuous moves
    #endregion

    #region Animation
    private double _animationTime = 0;
    private double _torchFlicker = 1.0;
    #endregion

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        // Windowed mode with reasonable resolution
        _graphics.IsFullScreen = false;
        ScreenWidth = 1280;
        ScreenHeight = 720;
        RaycastWidth = ScreenWidth;
        RaycastHeight = ScreenHeight;
        _graphics.PreferredBackBufferWidth = ScreenWidth;
        _graphics.PreferredBackBufferHeight = ScreenHeight;
        _graphics.ApplyChanges();

        Window.Title = "Friendship Dungeon - BUILD ENGINE STYLE";
        Window.AllowUserResizing = false;

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        // Create pixel texture
        _pixelTexture = new Texture2D(GraphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });

        // Create viewport render target
        _viewport = new RenderTarget2D(GraphicsDevice, RaycastWidth, RaycastHeight);

        // Initialize screen buffer for raycasting
        _screenBuffer = new Color[RaycastWidth * RaycastHeight];
        _zBuffer = new double[RaycastWidth];
        _screenTexture = new Texture2D(GraphicsDevice, RaycastWidth, RaycastHeight);

        // Generate textures
        _textureGen = new TextureGenerator(GraphicsDevice);
        _textureGen.GenerateAllTextures();

        // Load font
        _font = Content.Load<SpriteFont>("Font");

        StartNewGame();
    }

    private void StartNewGame()
    {
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
        _gameState = GameState.Exploring;

        _inventory.Clear();
        _messages.Clear();
        _openedChests.Clear();
        _usedShrines.Clear();
        _enemies.Clear();
        _sprites.Clear();
        _weapons.Clear();

        // Starting equipment
        _weapons.Add(Weapon.SharpCrayon());
        _equippedWeaponIndex = 0;
        _equippedWeapon = _weapons[0];
        
        _inventory.Add(new InventoryItem("P", "Mystery Pills", 2));

        // Reset player (grid-based)
        _playerX = 1;
        _playerY = 1;
        _playerDir = Direction.North;
        SetDirectionVectors(_playerDir);
        _posX = _targetX = _playerX + 0.5;
        _posY = _targetY = _playerY + 0.5;
        _pitch = 0.0;
        _isAnimating = false;

        GenerateDungeon();

        AddMessage("Welcome back, friend. We missed you. :)");
        AddMessage("WASD=Move, Mouse=Look, SPACE=Interact");
    }

    #region Dungeon Generation
    private void GenerateDungeon()
    {
        // Clear map (1 = wall, 0 = empty)
        for (int x = 0; x < MapWidth; x++)
            for (int y = 0; y < MapHeight; y++)
            {
                _worldMap[x, y] = 1;
                _explored[x, y] = false;
            }

        GenerateMaze(1, 1);

        // Find starting position (grid-based)
        _playerX = 1;
        _playerY = 1;
        _posX = _targetX = _playerX + 0.5;
        _posY = _targetY = _playerY + 0.5;

        PlaceStairs();
        
        int featureCount = 5 + _currentFloor * 2;
        for (int i = 0; i < featureCount; i++)
            PlaceRandomFeature();

        int enemyCount = 3 + _currentFloor * 2;
        _enemies.Clear();
        for (int i = 0; i < enemyCount; i++)
            PlaceEnemy();

        BuildSpriteList();
    }

    private void GenerateMaze(int startX, int startY)
    {
        var stack = new Stack<(int x, int y)>();
        _worldMap[startX, startY] = 0;
        stack.Push((startX, startY));

        var directions = new (int dx, int dy)[] { (0, -2), (2, 0), (0, 2), (-2, 0) };

        while (stack.Count > 0)
        {
            var (cx, cy) = stack.Peek();
            var unvisited = directions
                .Select(d => (nx: cx + d.dx, ny: cy + d.dy))
                .Where(p => p.nx > 0 && p.nx < MapWidth - 1 && p.ny > 0 && p.ny < MapHeight - 1)
                .Where(p => _worldMap[p.nx, p.ny] == 1)
                .ToList();

            if (unvisited.Count > 0)
            {
                var (nx, ny) = unvisited[_random.Next(unvisited.Count)];
                _worldMap[(cx + nx) / 2, (cy + ny) / 2] = 0;
                _worldMap[nx, ny] = 0;
                stack.Push((nx, ny));
            }
            else
            {
                stack.Pop();
            }
        }

        // Add extra paths
        for (int i = 0; i < MapWidth * MapHeight / 20; i++)
        {
            int x = _random.Next(2, MapWidth - 2);
            int y = _random.Next(2, MapHeight - 2);
            if (_worldMap[x, y] == 1)
            {
                int floorNeighbors = 0;
                if (_worldMap[x - 1, y] == 0) floorNeighbors++;
                if (_worldMap[x + 1, y] == 0) floorNeighbors++;
                if (_worldMap[x, y - 1] == 0) floorNeighbors++;
                if (_worldMap[x, y + 1] == 0) floorNeighbors++;
                if (floorNeighbors >= 2)
                    _worldMap[x, y] = 0;
            }
        }
    }

    private void PlaceStairs()
    {
        int playerX = (int)_posX;
        int playerY = (int)_posY;
        int bestX = 1, bestY = 1;
        int bestDist = 0;

        for (int x = 1; x < MapWidth - 1; x++)
        {
            for (int y = 1; y < MapHeight - 1; y++)
            {
                if (_worldMap[x, y] == 0)
                {
                    int dist = Math.Abs(x - playerX) + Math.Abs(y - playerY);
                    if (dist > bestDist)
                    {
                        bestDist = dist;
                        bestX = x;
                        bestY = y;
                    }
                }
            }
        }

        _sprites[(bestX, bestY)] = new Sprite(bestX + 0.5, bestY + 0.5, SpriteType.Stairs);
    }

    private void PlaceRandomFeature()
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            int x = _random.Next(1, MapWidth - 1);
            int y = _random.Next(1, MapHeight - 1);

            if (_worldMap[x, y] == 0 && !_sprites.ContainsKey((x, y)) && 
                (x != (int)_posX || y != (int)_posY))
            {
                int roll = _random.Next(100);
                SpriteType type = roll < 40 ? SpriteType.Chest : 
                                  roll < 70 ? SpriteType.Trap : SpriteType.Shrine;
                _sprites[(x, y)] = new Sprite(x + 0.5, y + 0.5, type);
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

            if (_worldMap[x, y] == 0 && !_enemies.ContainsKey((x, y)) && !_sprites.ContainsKey((x, y)))
            {
                double dist = Math.Sqrt(Math.Pow(x - _posX, 2) + Math.Pow(y - _posY, 2));
                if (dist > 3)
                {
                    _enemies[(x, y)] = CreateEnemy();
                    return;
                }
            }
        }
    }

    private void BuildSpriteList()
    {
        // Sprites are stored in _sprites dictionary
        // Enemies will be rendered separately
    }

    private Enemy CreateEnemy()
    {
        var availableTypes = new List<EnemyType> { EnemyType.SmileDog, EnemyType.MeatChild };

        if (_currentFloor >= 2) availableTypes.AddRange(new[] { EnemyType.GrandmasTwin, EnemyType.ManInWall });
        if (_currentFloor >= 3) availableTypes.AddRange(new[] { EnemyType.FriendlyHelper, EnemyType.YourReflection });
        if (_currentFloor >= 5) availableTypes.Add(EnemyType.ItsListening);
        if (_currentFloor >= 7) availableTypes.Add(EnemyType.TheHost);

        var type = availableTypes[_random.Next(availableTypes.Count)];
        int floorBonus = (_currentFloor - 1) * 5;

        return type switch
        {
            EnemyType.SmileDog => new Enemy("Smile Dog :)", 15 + floorBonus, 15 + floorBonus, 5 + _currentFloor, 2, 10, 5, type),
            EnemyType.MeatChild => new Enemy("The Meat Child", 20 + floorBonus, 20 + floorBonus, 7 + _currentFloor, 3, 15, 8, type),
            EnemyType.GrandmasTwin => new Enemy("Grandma's Twin", 35 + floorBonus, 35 + floorBonus, 10 + _currentFloor, 5, 25, 15, type),
            EnemyType.ManInWall => new Enemy("Man In The Wall", 25 + floorBonus, 25 + floorBonus, 12 + _currentFloor, 8, 30, 20, type),
            EnemyType.FriendlyHelper => new Enemy("Your Friendly Helper", 50 + floorBonus, 50 + floorBonus, 14 + _currentFloor, 6, 35, 25, type),
            EnemyType.YourReflection => new Enemy("Your Reflection", 40 + floorBonus, 40 + floorBonus, 16 + _currentFloor, 10, 45, 35, type),
            EnemyType.ItsListening => new Enemy("It's Listening", 80 + floorBonus, 80 + floorBonus, 18 + _currentFloor, 15, 60, 50, type),
            EnemyType.TheHost => new Enemy("THE HOST", 120 + floorBonus, 120 + floorBonus, 25 + _currentFloor, 12, 100, 100, type),
            _ => new Enemy("Friend :)", 20 + floorBonus, 20 + floorBonus, 8 + _currentFloor, 4, 20, 10, type)
        };
    }
    #endregion

    #region Update
    protected override void Update(GameTime gameTime)
    {
        _previousKeyState = _currentKeyState;
        _currentKeyState = Keyboard.GetState();
        _previousMouseState = _currentMouseState;
        _currentMouseState = Mouse.GetState();

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _animationTime += dt;

        // Torch flicker
        double targetFlicker = 0.8 + _random.NextDouble() * 0.2;
        _torchFlicker = _torchFlicker * 0.9 + targetFlicker * 0.1;

        // Combat animation timers (always update)
        if (_weaponSwingTimer > 0) _weaponSwingTimer -= dt;
        if (_enemyHitFlash > 0) _enemyHitFlash -= dt;
        if (_screenShake > 0) _screenShake -= dt;
        if (_damageFlash > 0) _damageFlash -= dt;

        // Smooth animation interpolation (always runs)
        UpdateAnimation(dt);

        if (_gameState == GameState.Exploring)
        {
            UpdateExploring(dt);
            
            // Handle combat while exploring (no overlay)
            if (_currentEnemy != null)
            {
                UpdateInWorldCombat(dt);
            }
        }
        else if (_gameState == GameState.GameOver || _gameState == GameState.Victory)
        {
            if (KeyPressed(Keys.Space) || KeyPressed(Keys.Enter))
                StartNewGame();
        }

        // Head bob during movement animation
        if (_isAnimating)
            _headBob += dt * 12;
        else
            _headBob *= 0.85;

        base.Update(gameTime);
    }

    private void UpdateAnimation(float dt)
    {
        // Smooth position interpolation
        double posDiff = Math.Abs(_posX - _targetX) + Math.Abs(_posY - _targetY);
        if (posDiff > 0.01)
        {
            _posX += (_targetX - _posX) * MoveAnimSpeed * dt;
            _posY += (_targetY - _posY) * MoveAnimSpeed * dt;
            _isAnimating = true;
        }
        else
        {
            _posX = _targetX;
            _posY = _targetY;
        }

        // Smooth direction interpolation
        double dirDiff = Math.Abs(_dirX - _targetDirX) + Math.Abs(_dirY - _targetDirY);
        if (dirDiff > 0.01)
        {
            _dirX += (_targetDirX - _dirX) * TurnAnimSpeed * dt;
            _dirY += (_targetDirY - _dirY) * TurnAnimSpeed * dt;
            _planeX += (_targetPlaneX - _planeX) * TurnAnimSpeed * dt;
            _planeY += (_targetPlaneY - _planeY) * TurnAnimSpeed * dt;
            _isAnimating = true;
        }
        else
        {
            _dirX = _targetDirX;
            _dirY = _targetDirY;
            _planeX = _targetPlaneX;
            _planeY = _targetPlaneY;
        }

        // Check if animation is complete
        if (posDiff <= 0.01 && dirDiff <= 0.01)
            _isAnimating = false;
    }

    private void SetDirectionVectors(Direction dir)
    {
        switch (dir)
        {
            case Direction.North:
                _targetDirX = 0; _targetDirY = -1;
                _targetPlaneX = 0.66; _targetPlaneY = 0;
                break;
            case Direction.South:
                _targetDirX = 0; _targetDirY = 1;
                _targetPlaneX = -0.66; _targetPlaneY = 0;
                break;
            case Direction.East:
                _targetDirX = 1; _targetDirY = 0;
                _targetPlaneX = 0; _targetPlaneY = 0.66;
                break;
            case Direction.West:
                _targetDirX = -1; _targetDirY = 0;
                _targetPlaneX = 0; _targetPlaneY = -0.66;
                break;
        }
    }

    private void UpdateExploring(float dt)
    {
        // Update move timer
        if (_moveTimer > 0) _moveTimer -= dt;
        
        // Don't accept new input while animating
        if (_isAnimating) return;

        // LEFT CLICK TO ATTACK - Check for enemy in front
        if (_currentMouseState.LeftButton == ButtonState.Pressed &&
            _previousMouseState.LeftButton == ButtonState.Released)
        {
            TryAttackInFront();
        }

        // Turn left (Q or Left arrow) - continuous
        if (KeyHeld(Keys.Q) || KeyHeld(Keys.Left))
        {
            if (_moveTimer <= 0)
            {
                _playerDir = _playerDir switch
                {
                    Direction.North => Direction.West,
                    Direction.West => Direction.South,
                    Direction.South => Direction.East,
                    Direction.East => Direction.North,
                    _ => _playerDir
                };
                SetDirectionVectors(_playerDir);
                _moveTimer = MoveDelay;
            }
        }

        // Turn right (E or Right arrow) - continuous
        if (KeyHeld(Keys.E) || KeyHeld(Keys.Right))
        {
            if (_moveTimer <= 0)
            {
                _playerDir = _playerDir switch
                {
                    Direction.North => Direction.East,
                    Direction.East => Direction.South,
                    Direction.South => Direction.West,
                    Direction.West => Direction.North,
                    _ => _playerDir
                };
                SetDirectionVectors(_playerDir);
                _moveTimer = MoveDelay;
            }
        }

        // Move forward (W or Up) - continuous
        if (KeyHeld(Keys.W) || KeyHeld(Keys.Up))
        {
            if (_moveTimer <= 0)
            {
                var (dx, dy) = GetDirectionOffset(_playerDir);
                TryGridMove(dx, dy);
                _moveTimer = MoveDelay;
            }
        }

        // Move backward (S or Down) - continuous
        if (KeyHeld(Keys.S) || KeyHeld(Keys.Down))
        {
            if (_moveTimer <= 0)
            {
                var (dx, dy) = GetDirectionOffset(_playerDir);
                TryGridMove(-dx, -dy);
                _moveTimer = MoveDelay;
            }
        }

        // Strafe left (A) - continuous
        if (KeyHeld(Keys.A))
        {
            if (_moveTimer <= 0)
            {
                var leftDir = _playerDir switch
                {
                    Direction.North => Direction.West,
                    Direction.West => Direction.South,
                    Direction.South => Direction.East,
                    Direction.East => Direction.North,
                    _ => _playerDir
                };
                var (dx, dy) = GetDirectionOffset(leftDir);
                TryGridMove(dx, dy);
                _moveTimer = MoveDelay;
            }
        }

        // Strafe right (D) - continuous
        if (KeyHeld(Keys.D))
        {
            if (_moveTimer <= 0)
            {
                var rightDir = _playerDir switch
                {
                    Direction.North => Direction.East,
                    Direction.East => Direction.South,
                    Direction.South => Direction.West,
                    Direction.West => Direction.North,
                    _ => _playerDir
                };
                var (dx, dy) = GetDirectionOffset(rightDir);
                TryGridMove(dx, dy);
                _moveTimer = MoveDelay;
            }
        }

        // Update explored tiles
        for (int ddx = -2; ddx <= 2; ddx++)
            for (int ddy = -2; ddy <= 2; ddy++)
            {
                int nx = _playerX + ddx, ny = _playerY + ddy;
                if (nx >= 0 && nx < MapWidth && ny >= 0 && ny < MapHeight)
                    _explored[nx, ny] = true;
            }

        // Weapon switching (1-5 keys)
        for (int i = 0; i < Math.Min(5, _weapons.Count); i++)
        {
            if (KeyPressed(Keys.D1 + i))
            {
                _equippedWeaponIndex = i;
                _equippedWeapon = _weapons[i];
                AddMessage($"Equipped: {_equippedWeapon.Name}");
            }
        }

        // Interaction
        if (KeyPressed(Keys.Space))
            TryInteract();

        // Escape to quit
        if (KeyPressed(Keys.Escape))
            Exit();
    }

    private (int dx, int dy) GetDirectionOffset(Direction dir)
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

    private void TryGridMove(int dx, int dy)
    {
        int newX = _playerX + dx;
        int newY = _playerY + dy;

        // Check bounds and walls
        if (newX < 0 || newX >= MapWidth || newY < 0 || newY >= MapHeight)
            return;
        if (_worldMap[newX, newY] != 0)
            return;

        // Check for enemy collision BEFORE moving - engage in-world combat
        if (_enemies.TryGetValue((newX, newY), out var enemy))
        {
            _currentEnemy = enemy;
            _currentEnemyPos = (newX, newY);
            _playerAttackCooldown = 0f;
            _enemyAttackTimer = 0f;
            _combatStarted = true;
            AddMessage($"A {_currentEnemy.Name} blocks your path!");
            return;
        }

        // Move to new position
        _playerX = newX;
        _playerY = newY;
        _targetX = _playerX + 0.5;
        _targetY = _playerY + 0.5;
        _isAnimating = true;

        // Check for sprite interactions on the new tile
        CheckTileInteraction();
    }

    private void TryInteract()
    {
        // Check for sprite in front of player (one tile ahead)
        var (dx, dy) = GetDirectionOffset(_playerDir);
        int tileX = _playerX + dx;
        int tileY = _playerY + dy;

        if (_sprites.TryGetValue((tileX, tileY), out var sprite))
        {
            switch (sprite.Type)
            {
                case SpriteType.Stairs:
                    _currentFloor++;
                    if (_currentFloor > 10)
                    {
                        _gameState = GameState.Victory;
                        AddMessage("You escaped! But did you really?");
                    }
                    else
                    {
                        AddMessage($"Descending to floor {_currentFloor}...");
                        GenerateDungeon();
                    }
                    break;

                case SpriteType.Chest:
                    if (!_openedChests.Contains((tileX, tileY)))
                    {
                        _openedChests.Add((tileX, tileY));
                        int goldFound = _random.Next(10, 30) * _currentFloor;
                        _gold += goldFound;
                        AddMessage($"Found {goldFound} gold!");
                        
                        // Chance for weapon drop (higher on deeper floors)
                        if (_random.Next(100) < 25 + _currentFloor * 5)
                        {
                            var newWeapon = Weapon.GetRandomWeapon(_random, _currentFloor);
                            _weapons.Add(newWeapon);
                            AddMessage($"Found: {newWeapon.Name}!");
                            AddMessage($"[{_weapons.Count}] to equip");
                        }
                        else if (_random.Next(100) < 30)
                        {
                            _inventory.Add(new InventoryItem("P", "Mystery Pills", 1));
                            AddMessage("Found Mystery Pills!");
                        }
                    }
                    else
                    {
                        AddMessage("The chest is empty.");
                    }
                    break;

                case SpriteType.Shrine:
                    if (!_usedShrines.Contains((tileX, tileY)))
                    {
                        _usedShrines.Add((tileX, tileY));
                        _health = Math.Min(_health + 30, _maxHealth);
                        _mana = Math.Min(_mana + 20, _maxMana);
                        AddMessage("The shrine restores you...");
                    }
                    else
                    {
                        AddMessage("The shrine is dormant.");
                    }
                    break;

                case SpriteType.Trap:
                    AddMessage("You see a trap ahead. Careful!");
                    break;
            }
        }
        else if (_enemies.TryGetValue((tileX, tileY), out var enemy))
        {
            // Attack enemy in front - in-world combat
            _currentEnemy = enemy;
            _currentEnemyPos = (tileX, tileY);
            _playerAttackCooldown = 0f;
            _enemyAttackTimer = 0f;
            _combatStarted = true;
            AddMessage($"You engage {_currentEnemy.Name}!");
        }
        else
        {
            AddMessage("Nothing to interact with.");
        }
    }

    private void CheckTileInteraction()
    {
        // Auto-trigger traps when stepping on them
        if (_sprites.TryGetValue((_playerX, _playerY), out var sprite))
        {
            if (sprite.Type == SpriteType.Trap)
            {
                int damage = _random.Next(5, 15);
                _health -= damage;
                AddMessage($"TRAP! Took {damage} damage!");
                if (_health <= 0)
                {
                    _health = 0;
                    _gameState = GameState.GameOver;
                }
            }
        }
    }

    private void UpdateInWorldCombat(float dt)
    {
        if (_currentEnemy == null) return;
        
        // Check if enemy is still adjacent (can disengage by moving away)
        var (dx, dy) = GetDirectionOffset(_playerDir);
        int frontX = _playerX + dx;
        int frontY = _playerY + dy;
        bool enemyInFront = (_currentEnemyPos.x == frontX && _currentEnemyPos.y == frontY);
        
        // Also check if enemy is adjacent in any direction
        bool enemyAdjacent = Math.Abs(_playerX - _currentEnemyPos.x) <= 1 && 
                            Math.Abs(_playerY - _currentEnemyPos.y) <= 1 &&
                            !(_playerX == _currentEnemyPos.x && _playerY == _currentEnemyPos.y);
        
        if (!enemyAdjacent)
        {
            // Disengaged from combat
            _currentEnemy = null;
            _combatStarted = false;
            AddMessage("Disengaged from combat.");
            return;
        }
        
        // Update cooldowns
        if (_playerAttackCooldown > 0) _playerAttackCooldown -= dt;
        _enemyAttackTimer += dt;
        
        // Enemy attacks on timer (only if adjacent)
        if (_enemyAttackTimer >= EnemyAttackDelay && _currentEnemy.Health > 0)
        {
            EnemyAttack();
            _enemyAttackTimer = 0f;
        }
        
        // LEFT CLICK = ATTACK (when ready and enemy in front)
        bool wantsAttack = (_currentMouseState.LeftButton == ButtonState.Pressed &&
                           _previousMouseState.LeftButton == ButtonState.Released);
        
        if (wantsAttack && _playerAttackCooldown <= 0 && enemyInFront)
        {
            PlayerAttack();
            _playerAttackCooldown = PlayerAttackDelay;
            _weaponSwingTimer = WeaponSwingDuration;
        }
        
        // RIGHT CLICK = Defend
        bool wantsDefend = (_currentMouseState.RightButton == ButtonState.Pressed &&
                           _previousMouseState.RightButton == ButtonState.Released);
        if (wantsDefend)
        {
            _isDefending = true;
            AddMessage("Bracing for impact!");
        }
        
        // E = Heal
        if (KeyPressed(Keys.E))
        {
            if (_mana >= 10)
            {
                _mana -= 10;
                int heal = _random.Next(10, 25);
                _health = Math.Min(_health + heal, _maxHealth);
                AddMessage($"Dark prayer heals {heal} HP!");
            }
            else
            {
                AddMessage("Not enough mana!");
            }
        }
    }
    
    private void UpdateCombat(float dt)
    {
        // Legacy - no longer used, combat is in-world now
    }
    
    private void PlayerAttack()
    {
        if (_currentEnemy == null) return;
        
        _isDefending = false;
        _weaponSwingTimer = WeaponSwingDuration; // Trigger weapon swing animation
        _enemyHitFlash = HitFlashDuration; // Trigger enemy flash
        
        int baseDmg = _equippedWeapon.BaseDamage + _equippedWeapon.AttackBonus + _attack;
        bool isCrit = _random.Next(100) < _equippedWeapon.CritChance;
        int damage = Math.Max(1, baseDmg - _currentEnemy.Defense + _random.Next(-3, 4));
        if (isCrit)
        {
            damage = (int)(damage * _equippedWeapon.CritMultiplier);
            AddMessage("CRITICAL HIT!");
            _screenShake = ShakeDuration; // Extra shake on crit
        }
        _currentEnemy.Health -= damage;
        
        // Weapon sound flavor
        string attackMsg = _equippedWeapon.Type switch
        {
            WeaponType.SharpCrayon => $"SCRIBBLE! {damage} damage!",
            WeaponType.TeddyMaw => $"CHOMP! {damage} damage!",
            WeaponType.JackInTheGun => $"POP! {damage} damage!",
            WeaponType.MyFirstNailer => $"THUNK! {damage} damage!",
            WeaponType.SippyCannon => $"SPLURT! {damage} damage!",
            WeaponType.MusicBoxDancer => $"*tinkle tinkle* {damage} damage.",
            _ => $"{damage} damage!"
        };
        AddMessage(attackMsg);
        
        // Special effects
        if (_equippedWeapon.SpecialEffect == "bleed" && _random.Next(100) < 30)
        {
            int bleedDmg = _random.Next(3, 8);
            _currentEnemy.Health -= bleedDmg;
            AddMessage($"Bleeding for {bleedDmg} more!");
        }
        if (_equippedWeapon.SpecialEffect == "lifesteal")
        {
            int heal = damage / 4;
            _health = Math.Min(_health + heal, _maxHealth);
            AddMessage($"Drained {heal} HP!");
        }
        
        // Check victory
        if (_currentEnemy.Health <= 0)
        {
            Victory();
        }
    }
    
    private void EnemyAttack()
    {
        if (_currentEnemy == null) return;
        
        int enemyDamage = Math.Max(1, _currentEnemy.Attack - _defense + _random.Next(-2, 3));
        if (_isDefending)
        {
            enemyDamage /= 2;
            _isDefending = false;
            AddMessage("Blocked some damage!");
        }
        _health -= enemyDamage;
        _damageFlash = DamageFlashDuration; // Screen damage flash
        _screenShake = ShakeDuration; // Screen shake on hit
        AddMessage($"{_currentEnemy.Name} hits for {enemyDamage}!");

        if (_health <= 0)
        {
            _health = 0;
            _gameState = GameState.GameOver;
            AddMessage("You have been consumed...");
        }
    }
    
    private void Victory()
    {
        _xp += _currentEnemy!.XPReward;
        _gold += _currentEnemy.GoldReward;
        AddMessage($"Victory! +{_currentEnemy.XPReward} XP, +{_currentEnemy.GoldReward} gold");
        _enemies.Remove(_currentEnemyPos);

        // Level up check
        while (_xp >= _xpToLevel)
        {
            _xp -= _xpToLevel;
            _level++;
            _xpToLevel = (int)(_xpToLevel * 1.5);
            _maxHealth += 10;
            _maxMana += 5;
            _health = _maxHealth;
            _mana = _maxMana;
            _attack += 2;
            _defense += 1;
            AddMessage($"LEVEL UP! Now level {_level}!");
        }

        // Already in Exploring state - just clear combat
        _currentEnemy = null;
        _combatStarted = false;
    }
    
    private void TryAttackInFront()
    {
        // Check for enemy in front of player
        var (dx, dy) = GetDirectionOffset(_playerDir);
        int tileX = _playerX + dx;
        int tileY = _playerY + dy;

        if (_enemies.TryGetValue((tileX, tileY), out var enemy))
        {
            // Start in-world combat and immediately attack
            _currentEnemy = enemy;
            _currentEnemyPos = (tileX, tileY);
            _playerAttackCooldown = 0f;
            _enemyAttackTimer = 0f;
            _combatStarted = true;
            
            // Execute attack immediately
            PlayerAttack();
            _playerAttackCooldown = PlayerAttackDelay;
        }
        else
        {
            // Swing at nothing - flavor text
            _weaponSwingTimer = WeaponSwingDuration; // Still animate
            string swingMsg = _equippedWeapon.Type switch
            {
                WeaponType.SharpCrayon => "You scribble at the air...",
                WeaponType.TeddyMaw => "Mr. Huggles chomps nothing.",
                WeaponType.JackInTheGun => "POP! The clown laughs at emptiness.",
                WeaponType.MyFirstNailer => "THUNK! Nails hit the wall.",
                WeaponType.SippyCannon => "SPLURT! Just spills on the floor.",
                WeaponType.MusicBoxDancer => "She pirouettes into nothing.",
                _ => "You swing at nothing..."
            };
            AddMessage(swingMsg);
        }
    }

    private bool KeyPressed(Keys key) =>
        _currentKeyState.IsKeyDown(key) && !_previousKeyState.IsKeyDown(key);
    
    private bool KeyHeld(Keys key) =>
        _currentKeyState.IsKeyDown(key);

    private void AddMessage(string msg)
    {
        _messages.Add(msg);
        if (_messages.Count > 8) _messages.RemoveAt(0);
    }
    #endregion

    #region Raycasting Rendering
    protected override void Draw(GameTime gameTime)
    {
        // Render 3D view to buffer
        RenderRaycast();
        
        // Copy buffer to texture
        _screenTexture.SetData(_screenBuffer);

        GraphicsDevice.Clear(Color.Black);
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

        // Draw 3D viewport - fills entire screen
        // Apply screen shake
        int shakeX = 0, shakeY = 0;
        if (_screenShake > 0)
        {
            shakeX = _random.Next(-8, 9);
            shakeY = _random.Next(-8, 9);
        }
        _spriteBatch.Draw(_screenTexture, new Rectangle(shakeX, shakeY, ScreenWidth, ScreenHeight), Color.White);

        // Draw weapon/hand with attack animation
        DrawWeapon();

        // Draw overlay HUD (stats, minimap, messages)
        DrawOverlayHUD();

        // Draw in-world combat HUD (enemy health, combat hints) when fighting
        if (_currentEnemy != null)
            DrawInWorldCombatHUD();
        
        // Damage flash overlay (red tint when hit)
        if (_damageFlash > 0)
        {
            float alpha = (_damageFlash / DamageFlashDuration) * 0.4f;
            DrawRect(new Rectangle(0, 0, ScreenWidth, ScreenHeight), new Color(255, 0, 0, (int)(alpha * 255)));
        }

        // Game over / victory screens
        if (_gameState == GameState.GameOver)
            DrawGameOver();
        else if (_gameState == GameState.Victory)
            DrawVictory();

        _spriteBatch.End();
        base.Draw(gameTime);
    }

    private void RenderRaycast()
    {
        int w = RaycastWidth;
        int h = RaycastHeight;
        double bobOffset = Math.Sin(_headBob) * 5;

        // Clear buffer with ceiling/floor gradient
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // Get raycast info for floor/ceiling
                double rayDirX0 = _dirX - _planeX;
                double rayDirY0 = _dirY - _planeY;
                double rayDirX1 = _dirX + _planeX;
                double rayDirY1 = _dirY + _planeY;

                int p = (int)(y - h / 2 - _pitch - bobOffset);
                double posZ = 0.5 * h;
                
                if (p != 0)
                {
                    double rowDistance = posZ / Math.Abs(p);
                    
                    double floorStepX = rowDistance * (rayDirX1 - rayDirX0) / w;
                    double floorStepY = rowDistance * (rayDirY1 - rayDirY0) / w;
                    
                    double floorX = _posX + rowDistance * rayDirX0 + floorStepX * x;
                    double floorY = _posY + rowDistance * rayDirY0 + floorStepY * x;
                    
                    int cellX = (int)floorX;
                    int cellY = (int)floorY;
                    
                    int tx = (int)(TextureSize * (floorX - cellX)) & (TextureSize - 1);
                    int ty = (int)(TextureSize * (floorY - cellY)) & (TextureSize - 1);

                    // Distance fog
                    double fog = Math.Min(1.0, rowDistance / 8.0);
                    float flicker = (float)(_torchFlicker * 0.3 + 0.7);
                    
                    if (y > h / 2 + _pitch + bobOffset) // Floor
                    {
                        Color texColor = _textureGen.GetFloorPixel(tx, ty);
                        _screenBuffer[y * w + x] = ApplyFog(texColor, fog, flicker);
                    }
                    else // Ceiling
                    {
                        Color texColor = _textureGen.GetCeilingPixel(tx, ty);
                        _screenBuffer[y * w + x] = ApplyFog(texColor, fog, flicker);
                    }
                }
                else
                {
                    _screenBuffer[y * w + x] = Color.Black;
                }
            }
        }

        // Raycast walls
        for (int x = 0; x < w; x++)
        {
            double cameraX = 2 * x / (double)w - 1;
            double rayDirX = _dirX + _planeX * cameraX;
            double rayDirY = _dirY + _planeY * cameraX;

            int mapX = (int)_posX;
            int mapY = (int)_posY;

            double sideDistX, sideDistY;
            double deltaDistX = rayDirX == 0 ? 1e30 : Math.Abs(1 / rayDirX);
            double deltaDistY = rayDirY == 0 ? 1e30 : Math.Abs(1 / rayDirY);
            double perpWallDist;

            int stepX, stepY;
            int hit = 0;
            int side = 0;

            if (rayDirX < 0) { stepX = -1; sideDistX = (_posX - mapX) * deltaDistX; }
            else { stepX = 1; sideDistX = (mapX + 1.0 - _posX) * deltaDistX; }
            if (rayDirY < 0) { stepY = -1; sideDistY = (_posY - mapY) * deltaDistY; }
            else { stepY = 1; sideDistY = (mapY + 1.0 - _posY) * deltaDistY; }

            // DDA
            while (hit == 0)
            {
                if (sideDistX < sideDistY)
                {
                    sideDistX += deltaDistX;
                    mapX += stepX;
                    side = 0;
                }
                else
                {
                    sideDistY += deltaDistY;
                    mapY += stepY;
                    side = 1;
                }
                if (mapX >= 0 && mapX < MapWidth && mapY >= 0 && mapY < MapHeight)
                {
                    if (_worldMap[mapX, mapY] > 0) hit = 1;
                }
                else hit = 1;
            }

            if (side == 0)
                perpWallDist = sideDistX - deltaDistX;
            else
                perpWallDist = sideDistY - deltaDistY;

            _zBuffer[x] = perpWallDist;

            int lineHeight = (int)(h / perpWallDist);
            int drawStart = (int)(-lineHeight / 2 + h / 2 + _pitch + bobOffset);
            int drawEnd = (int)(lineHeight / 2 + h / 2 + _pitch + bobOffset);

            // Calculate texture X coordinate
            double wallX;
            if (side == 0) wallX = _posY + perpWallDist * rayDirY;
            else wallX = _posX + perpWallDist * rayDirX;
            wallX -= Math.Floor(wallX);

            int texX = (int)(wallX * TextureSize);
            if (side == 0 && rayDirX > 0) texX = TextureSize - texX - 1;
            if (side == 1 && rayDirY < 0) texX = TextureSize - texX - 1;

            // Distance fog factor
            double fog = Math.Min(1.0, perpWallDist / 10.0);
            float flicker = (float)(_torchFlicker * 0.3 + 0.7);
            float sideDim = side == 1 ? 0.7f : 1.0f;

            // Draw vertical stripe
            double step = (double)TextureSize / lineHeight;
            double texPos = (drawStart - _pitch - bobOffset - h / 2 + lineHeight / 2) * step;

            for (int y = Math.Max(0, drawStart); y < Math.Min(h, drawEnd); y++)
            {
                int texY = (int)texPos & (TextureSize - 1);
                texPos += step;

                Color texColor = _textureGen.GetWallPixel(texX, texY);
                Color finalColor = ApplyFog(texColor, fog, flicker * sideDim);
                _screenBuffer[y * w + x] = finalColor;
            }
        }

        // Render sprites (enemies, items, etc.)
        RenderSprites(w, h, bobOffset);
    }

    private void RenderSprites(int w, int h, double bobOffset)
    {
        // Collect all sprites (enemies + objects) with extra info for hit flash
        var allSprites = new List<(double x, double y, Texture2D tex, double scale, bool isCurrentEnemy)>();

        foreach (var kvp in _enemies)
        {
            var (ex, ey) = kvp.Key;
            var enemy = kvp.Value;
            var tex = _textureGen.GetEnemyTexture(enemy.Type);
            bool isCurrent = (_currentEnemy != null && ex == _currentEnemyPos.x && ey == _currentEnemyPos.y);
            allSprites.Add((ex + 0.5, ey + 0.5, tex, 1.0, isCurrent));
        }

        foreach (var kvp in _sprites)
        {
            var sprite = kvp.Value;
            var tex = _textureGen.GetSpriteTexture(sprite.Type);
            allSprites.Add((sprite.X, sprite.Y, tex, 0.7, false));
        }

        // Sort by distance (far to near)
        allSprites = allSprites.OrderByDescending(s => 
            Math.Pow(s.x - _posX, 2) + Math.Pow(s.y - _posY, 2)).ToList();

        foreach (var (sx, sy, tex, scale, isCurrentEnemy) in allSprites)
        {
            double spriteX = sx - _posX;
            double spriteY = sy - _posY;

            double invDet = 1.0 / (_planeX * _dirY - _dirX * _planeY);
            double transformX = invDet * (_dirY * spriteX - _dirX * spriteY);
            double transformY = invDet * (-_planeY * spriteX + _planeX * spriteY);

            if (transformY <= 0.1) continue;

            int spriteScreenX = (int)((w / 2) * (1 + transformX / transformY));

            int spriteHeight = (int)Math.Abs((h / transformY) * scale);
            int drawStartY = (int)(-spriteHeight / 2 + h / 2 + _pitch + bobOffset);
            int drawEndY = (int)(spriteHeight / 2 + h / 2 + _pitch + bobOffset);

            int spriteWidth = (int)Math.Abs((h / transformY) * scale);
            int drawStartX = -spriteWidth / 2 + spriteScreenX;
            int drawEndX = spriteWidth / 2 + spriteScreenX;

            double fog = Math.Min(1.0, transformY / 8.0);
            float flicker = (float)(_torchFlicker * 0.3 + 0.7);
            
            // Hit flash - make enemy flash white when damaged
            bool applyHitFlash = isCurrentEnemy && _enemyHitFlash > 0;

            for (int stripe = Math.Max(0, drawStartX); stripe < Math.Min(w, drawEndX); stripe++)
            {
                if (transformY < _zBuffer[stripe])
                {
                    int texX = (int)((stripe - drawStartX) * tex.Width / spriteWidth);
                    if (texX < 0 || texX >= tex.Width) continue;

                    for (int y = Math.Max(0, drawStartY); y < Math.Min(h, drawEndY); y++)
                    {
                        int texY = (int)((y - drawStartY) * tex.Height / spriteHeight);
                        if (texY < 0 || texY >= tex.Height) continue;

                        Color pixel = _textureGen.GetTexturePixel(tex, texX, texY);
                        if (pixel.A > 128)
                        {
                            Color finalColor = ApplyFog(pixel, fog, flicker);
                            
                            // Apply hit flash effect - blend toward white
                            if (applyHitFlash)
                            {
                                float flashIntensity = _enemyHitFlash / HitFlashDuration;
                                finalColor = new Color(
                                    (int)(finalColor.R + (255 - finalColor.R) * flashIntensity),
                                    (int)(finalColor.G + (255 - finalColor.G) * flashIntensity),
                                    (int)(finalColor.B + (255 - finalColor.B) * flashIntensity),
                                    255
                                );
                            }
                            
                            _screenBuffer[y * w + stripe] = finalColor;
                        }
                    }
                }
            }
        }
    }

    private Color ApplyFog(Color c, double fog, float flicker)
    {
        float fogF = (float)fog;
        float light = (1 - fogF) * flicker;
        return new Color(
            (int)(c.R * light),
            (int)(c.G * light),
            (int)(c.B * light * 0.9f), // Slightly blue tint in shadows
            255
        );
    }

    private void DrawWeapon()
    {
        // Bob during movement animation
        float bobX = _isAnimating ? (float)Math.Sin(_headBob) * 5 : 0;
        float bobY = _isAnimating ? (float)Math.Abs(Math.Cos(_headBob)) * 8 : 0;

        // Get the weapon texture
        var weaponTexture = _textureGen.GetWeaponTexture(_equippedWeapon.Type);
        
        // 64x64 sprites scaled 4x = 256px display with chunky pixels
        float scale = 4.0f;
        int weaponWidth = (int)(weaponTexture.Width * scale);
        int weaponHeight = (int)(weaponTexture.Height * scale);
        
        // Position weapon at BOTTOM CENTER-RIGHT like modern FPS (DOOM style)
        // Centered horizontally but slightly right, fully visible above bottom edge
        int baseX = (ScreenWidth / 2) - (weaponWidth / 2) + 80 + (int)bobX;
        int baseY = ScreenHeight - weaponHeight - 10 + (int)bobY;
        
        // Attack animation - weapon swings up and forward
        float rotation = 0f;
        float attackOffsetX = 0f;
        float attackOffsetY = 0f;
        if (_weaponSwingTimer > 0)
        {
            float swingProgress = 1f - (_weaponSwingTimer / WeaponSwingDuration);
            // Quick swing up then return - use sin curve for smooth motion
            float swing = (float)Math.Sin(swingProgress * Math.PI);
            rotation = -swing * 0.4f; // Rotate up
            attackOffsetX = swing * 30; // Move forward (right in screen space)
            attackOffsetY = -swing * 60; // Move up
        }
        
        // Special effect for Music Box Dancer - color shifting
        Color tint = Color.White;
        if (_equippedWeapon.Type == WeaponType.MusicBoxDancer)
        {
            float pulse = (float)Math.Sin(_animationTime * 3) * 0.15f + 0.85f;
            int r = (int)(200 + Math.Sin(_animationTime * 2) * 55);
            int g = (int)(180 + Math.Sin(_animationTime * 3) * 40);
            int b = (int)(220 + Math.Sin(_animationTime * 2.5) * 35);
            tint = new Color(r, g, b);
        }
        
        // Draw weapon texture with rotation for attack animation
        Vector2 origin = new Vector2(weaponTexture.Width / 2, weaponTexture.Height); // Rotate from bottom center
        Vector2 position = new Vector2(baseX + weaponWidth / 2 + attackOffsetX, baseY + weaponHeight + attackOffsetY);
        
        _spriteBatch.Draw(
            weaponTexture,
            position,
            null,
            tint,
            rotation,
            origin,
            scale,
            SpriteEffects.None,
            0f
        );
    }

    private void DrawOverlayHUD()
    {
        Color panelBg = new Color(0, 0, 0, 180);
        Color textShadow = new Color(0, 0, 0, 200);
        
        // === TOP-LEFT: Stats Panel ===
        int statsX = 15;
        int statsY = 15;
        DrawRect(new Rectangle(statsX - 5, statsY - 5, 280, 140), panelBg);
        
        // Health bar
        DrawRect(new Rectangle(statsX, statsY, 200, 28), new Color(40, 20, 20, 200));
        int healthWidth = (int)(196 * (_health / (float)_maxHealth));
        Color healthColor = _health > 60 ? new Color(50, 200, 50) : _health > 30 ? Color.Yellow : Color.Red;
        DrawRect(new Rectangle(statsX + 2, statsY + 2, healthWidth, 24), healthColor);
        _spriteBatch.DrawString(_font, $"HP: {_health}/{_maxHealth}", new Vector2(statsX + 6, statsY + 5), Color.White);

        // Mana bar
        DrawRect(new Rectangle(statsX, statsY + 32, 200, 22), new Color(20, 20, 60, 200));
        int manaWidth = (int)(196 * (_mana / (float)_maxMana));
        DrawRect(new Rectangle(statsX + 2, statsY + 34, manaWidth, 18), new Color(80, 80, 220));
        _spriteBatch.DrawString(_font, $"MP: {_mana}/{_maxMana}", new Vector2(statsX + 6, statsY + 34), Color.White);

        // Stats text
        _spriteBatch.DrawString(_font, $"LVL {_level}  XP: {_xp}/{_xpToLevel}", new Vector2(statsX, statsY + 60), Color.Cyan);
        _spriteBatch.DrawString(_font, $"ATK: {_attack}+{_equippedWeapon.AttackBonus}  DEF: {_defense}", new Vector2(statsX, statsY + 78), Color.Orange);
        
        // Weapon
        Color weaponColor = _equippedWeapon.Rarity switch
        {
            WeaponRarity.Uncommon => Color.LimeGreen,
            WeaponRarity.Rare => Color.DodgerBlue,
            WeaponRarity.Epic => Color.MediumPurple,
            WeaponRarity.Legendary => Color.Gold,
            _ => Color.White
        };
        _spriteBatch.DrawString(_font, $"{_equippedWeapon.Name} (DMG:{_equippedWeapon.BaseDamage})", new Vector2(statsX, statsY + 100), weaponColor);

        // === TOP-RIGHT: Floor & Gold ===
        int topRightX = ScreenWidth - 150;
        DrawRect(new Rectangle(topRightX - 5, 10, 145, 55), panelBg);
        _spriteBatch.DrawString(_font, $"FLOOR {_currentFloor}", new Vector2(topRightX, 15), Color.Yellow);
        _spriteBatch.DrawString(_font, $"${_gold}", new Vector2(topRightX, 38), Color.Gold);

        // === BOTTOM-LEFT: Message Log ===
        int logX = 15;
        int logY = ScreenHeight - 130;
        int logWidth = 450;
        DrawRect(new Rectangle(logX - 5, logY - 5, logWidth, 125), panelBg);
        
        int msgY = logY;
        for (int i = Math.Max(0, _messages.Count - 5); i < _messages.Count; i++)
        {
            string msg = _messages[i];
            if (msg.Length > 50) msg = msg.Substring(0, 47) + "...";
            _spriteBatch.DrawString(_font, msg, new Vector2(logX, msgY), Color.LightGray);
            msgY += 20;
        }

        // === BOTTOM-RIGHT: Minimap (positioned above weapon area) ===
        int mapSize = 140;
        int cellSize = 5;
        int mapX = ScreenWidth - mapSize - 15;
        int mapY = ScreenHeight - mapSize - 200;  // Moved up to avoid weapon overlap
        
        DrawRect(new Rectangle(mapX - 5, mapY - 25, mapSize + 10, mapSize + 30), panelBg);
        _spriteBatch.DrawString(_font, $"{_playerDir}", new Vector2(mapX, mapY - 20), Color.Gray);
        
        DrawRect(new Rectangle(mapX, mapY, mapSize, mapSize), new Color(10, 10, 15, 220));

        int viewRange = mapSize / cellSize / 2;
        for (int dx = -viewRange; dx <= viewRange; dx++)
        {
            for (int dy = -viewRange; dy <= viewRange; dy++)
            {
                int wx = _playerX + dx;
                int wy = _playerY + dy;
                
                if (wx >= 0 && wx < MapWidth && wy >= 0 && wy < MapHeight && _explored[wx, wy])
                {
                    int sx = mapX + (dx + viewRange) * cellSize;
                    int sy = mapY + (dy + viewRange) * cellSize;
                    
                    Color cellColor = _worldMap[wx, wy] > 0 ? new Color(70, 55, 80) : new Color(35, 30, 40);
                    
                    if (_sprites.ContainsKey((wx, wy)))
                        cellColor = new Color(200, 200, 50);
                    if (_enemies.ContainsKey((wx, wy)))
                        cellColor = new Color(220, 50, 50);
                        
                    DrawRect(new Rectangle(sx, sy, cellSize - 1, cellSize - 1), cellColor);
                }
            }
        }

        // Player marker
        int playerMapX = mapX + viewRange * cellSize;
        int playerMapY = mapY + viewRange * cellSize;
        DrawRect(new Rectangle(playerMapX, playerMapY, cellSize, cellSize), Color.Lime);
        
        // Direction indicator
        var (ddx, ddy) = GetDirectionOffset(_playerDir);
        DrawLine(playerMapX + cellSize / 2, playerMapY + cellSize / 2,
                 playerMapX + cellSize / 2 + ddx * cellSize,
                 playerMapY + cellSize / 2 + ddy * cellSize, Color.White);

        // === CENTER-RIGHT: Items (small) ===
        if (_inventory.Count > 0)
        {
            int itemsX = ScreenWidth - 180;
            int itemsY = 80;
            DrawRect(new Rectangle(itemsX - 5, itemsY - 5, 175, 20 + _inventory.Count * 18), panelBg);
            foreach (var item in _inventory.Take(4))
            {
                _spriteBatch.DrawString(_font, $"[{item.Icon}] {item.Name} x{item.Quantity}", 
                    new Vector2(itemsX, itemsY), Color.LightGray);
                itemsY += 18;
            }
        }
    }

    private void DrawInWorldCombatHUD()
    {
        if (_currentEnemy == null) return;
        
        Color panelBg = new Color(0, 0, 0, 180);
        int centerX = ScreenWidth / 2;
        
        // Enemy info panel at top center
        int panelWidth = 300;
        int panelX = centerX - panelWidth / 2;
        int panelY = 10;
        
        DrawRect(new Rectangle(panelX - 5, panelY - 5, panelWidth + 10, 75), panelBg);
        
        // Enemy name
        _spriteBatch.DrawString(_font, _currentEnemy.Name, new Vector2(panelX + 10, panelY), Color.Red);
        
        // Enemy health bar
        DrawRect(new Rectangle(panelX + 10, panelY + 22, panelWidth - 20, 22), new Color(40, 0, 0));
        int enemyHealthWidth = (int)((panelWidth - 24) * (_currentEnemy.Health / (float)_currentEnemy.MaxHealth));
        Color healthBarColor = _enemyHitFlash > 0 ? Color.White : Color.DarkRed; // Flash white on hit
        DrawRect(new Rectangle(panelX + 12, panelY + 24, enemyHealthWidth, 18), healthBarColor);
        _spriteBatch.DrawString(_font, $"{_currentEnemy.Health}/{_currentEnemy.MaxHealth}", 
            new Vector2(panelX + panelWidth/2 - 30, panelY + 24), Color.White);
        
        // Attack cooldown bar
        bool canAttack = _playerAttackCooldown <= 0;
        string attackText = canAttack ? "[LMB] ATTACK!" : "Reloading...";
        Color attackColor = canAttack ? Color.Yellow : Color.Gray;
        _spriteBatch.DrawString(_font, attackText, new Vector2(panelX + 10, panelY + 48), attackColor);
        
        if (!canAttack)
        {
            float cooldownPct = 1f - (_playerAttackCooldown / PlayerAttackDelay);
            DrawRect(new Rectangle(panelX + 130, panelY + 50, 100, 12), Color.DarkGray);
            DrawRect(new Rectangle(panelX + 132, panelY + 52, (int)(96 * cooldownPct), 8), Color.Yellow);
        }
        
        // Enemy attack warning
        float enemyAttackPct = _enemyAttackTimer / EnemyAttackDelay;
        if (enemyAttackPct > 0.6f)
        {
            Color warningColor = ((int)(_animationTime * 10) % 2 == 0) ? Color.Red : Color.Orange;
            string warning = enemyAttackPct > 0.85f ? "!! INCOMING !!" : "! Enemy winding up !";
            _spriteBatch.DrawString(_font, warning, new Vector2(centerX - 70, panelY + 80), warningColor);
        }
        
        // Controls hint at bottom
        string hint = "[RMB] Defend  [E] Heal  [Move] Disengage";
        _spriteBatch.DrawString(_font, hint, new Vector2(centerX - 140, ScreenHeight - 40), new Color(180, 180, 180));
    }

    private void DrawCombatUI()
    {
        // Legacy overlay - no longer used
        // Combat is now in-world via DrawInWorldCombatHUD
    }

    private void DrawGameOver()
    {
        DrawRect(new Rectangle(0, 0, ScreenWidth, ScreenHeight), new Color(80, 0, 0, 200));
        
        string msg = "YOU DIED";
        _spriteBatch.DrawString(_font, msg, new Vector2(ScreenWidth/2 - 50, ScreenHeight/2 - 20), Color.White);
        _spriteBatch.DrawString(_font, "Press SPACE to try again", 
            new Vector2(ScreenWidth/2 - 100, ScreenHeight/2 + 20), Color.Gray);
    }

    private void DrawVictory()
    {
        DrawRect(new Rectangle(0, 0, ScreenWidth, ScreenHeight), new Color(0, 50, 0, 200));
        
        _spriteBatch.DrawString(_font, "ESCAPED?", new Vector2(ScreenWidth/2 - 50, ScreenHeight/2 - 20), Color.White);
        _spriteBatch.DrawString(_font, "Or did you?", new Vector2(ScreenWidth/2 - 40, ScreenHeight/2 + 10), Color.Red);
        _spriteBatch.DrawString(_font, "Press SPACE", new Vector2(ScreenWidth/2 - 50, ScreenHeight/2 + 50), Color.Gray);
    }

    private void DrawRect(Rectangle rect, Color color)
    {
        _spriteBatch.Draw(_pixelTexture, rect, color);
    }

    private void DrawLine(int x1, int y1, int x2, int y2, Color color)
    {
        int dx = Math.Abs(x2 - x1);
        int dy = Math.Abs(y2 - y1);
        int sx = x1 < x2 ? 1 : -1;
        int sy = y1 < y2 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            _spriteBatch.Draw(_pixelTexture, new Rectangle(x1, y1, 1, 1), color);
            if (x1 == x2 && y1 == y2) break;
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x1 += sx; }
            if (e2 < dx) { err += dx; y1 += sy; }
        }
    }
    #endregion
}
