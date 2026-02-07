using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MechaRogue.Models;
using MechaRogue.Rendering;
using MechaRogue.Screens;
using MechaRogue.Services;

namespace MechaRogue;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;

    // Rendering
    private DrawHelper _draw = null!;
    private PixelFont _font = null!;
    private BattlefieldRenderer _battlefield = null!;

    // Screens
    private TitleScreen _titleScreen = null!;
    private MapScreen _mapScreen = null!;
    private BattleScreen _battleScreen = null!;
    private ShopScreen _shopScreen = null!;
    private RestScreen _restScreen = null!;
    private EventScreen _eventScreen = null!;
    private EndScreen _endScreen = null!;

    // State
    private ScreenState _currentScreen = ScreenState.Title;
    private RunState? _run;
    private List<RunNode> _availableNodes = [];
    private readonly Random _rng = new();

    // Input
    private KeyboardState _prevKb;
    private KeyboardState _kb;
    private MouseState _prevMouse;
    private MouseState _mouse;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        _graphics.PreferredBackBufferWidth = 960;
        _graphics.PreferredBackBufferHeight = 540;
        _graphics.ApplyChanges();

        Window.Title = "MechaRogue - Medabots Roguelike";
        Window.AllowUserResizing = true;

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _draw = new DrawHelper(GraphicsDevice);
        _font = new PixelFont(GraphicsDevice);
        _battlefield = new BattlefieldRenderer(GraphicsDevice, _draw, _font);

        // Create screens
        _titleScreen = new TitleScreen(GraphicsDevice, _draw, _font);
        _mapScreen = new MapScreen(GraphicsDevice, _draw, _font);
        _battleScreen = new BattleScreen(GraphicsDevice, _draw, _font, _battlefield);
        _shopScreen = new ShopScreen(GraphicsDevice, _draw, _font);
        _restScreen = new RestScreen(GraphicsDevice, _draw, _font);
        _eventScreen = new EventScreen(GraphicsDevice, _draw, _font);
        _endScreen = new EndScreen(GraphicsDevice, _draw, _font);

        // Wire events
        _titleScreen.OnStarterSelected += starter =>
        {
            // Give the player a 2-bot starting squad (like the GBA games)
            var second = starter.Name == "Metabee"
                ? PartCatalog.MakeCyandog()
                : PartCatalog.MakePeppercat();
            second.IsPlayerOwned = true;
            second.IsLeader = false;
            starter.IsPlayerOwned = true;
            starter.IsLeader = true;

            _run = new RunState
            {
                Floor = 1,
                Credits = 120,
                Squad = [starter, second],
                Map = MapGenerator.Generate(15)
            };
            RefreshAvailableNodes();
            _currentScreen = ScreenState.Map;
        };

        _mapScreen.OnNodeSelected += node =>
        {
            node.Visited = true;
            node.IsCurrent = true;
            if (_run != null) _run.CurrentNodeId = node.Id;

            switch (node.Type)
            {
                case NodeType.Battle: StartBattle(false, false); break;
                case NodeType.EliteBattle: StartBattle(false, true); break;
                case NodeType.Boss: StartBattle(true, false); break;
                case NodeType.Shop: EnterShop(); break;
                case NodeType.Rest: EnterRest(); break;
                case NodeType.Event: EnterEvent(); break;
            }
        };

        _battleScreen.OnBattleWon += () =>
        {
            if (_run != null)
            {
                foreach (var m in _run.Squad)
                    m.RestHeal(0.25);
            }
            AdvanceFloor();
        };

        _battleScreen.OnBattleLost += () =>
        {
            if (_run?.IsGameOver == true)
            {
                _endScreen.Setup(false, _run.Wins, _run.Losses, _run.Floor);
                _currentScreen = ScreenState.GameOver;
            }
            else
            {
                AdvanceFloor();
            }
        };

        _shopScreen.OnLeaveShop += () => AdvanceFloor();
        _restScreen.OnRestComplete += () => AdvanceFloor();
        _eventScreen.OnEventComplete += () => AdvanceFloor();
        _endScreen.OnReturnToTitle += () => _currentScreen = ScreenState.Title;
    }

    private void RefreshAvailableNodes()
    {
        if (_run == null) return;
        _availableNodes = _run.Map.Where(n => n.Depth == _run.Floor && !n.Visited).ToList();
    }

    private void StartBattle(bool isBoss, bool isElite)
    {
        if (_run == null) return;
        int floor = _run.Floor;
        // 3v3: normal = 1-2, elite = 2-3, boss = 1 boss + 1-2 minions
        int enemyCount = isBoss ? 1 : (isElite ? _rng.Next(2, 4) : _rng.Next(1, 3));

        var enemySquad = isBoss
            ? new List<Medabot> { PartCatalog.RandomBoss(floor) }
            : PartCatalog.RandomEnemySquad(floor, enemyCount);

        // Boss gets minions too at higher floors
        if (isBoss && floor >= 5)
            enemySquad.AddRange(PartCatalog.RandomEnemySquad(floor, _rng.Next(1, 3)));

        _battleScreen.StartBattle(_run, enemySquad);
        _currentScreen = ScreenState.Battle;
    }

    private void EnterShop()
    {
        if (_run == null) return;
        _shopScreen.Setup(_run, PartCatalog.GetShopParts(_run.Floor, 4));
        _currentScreen = ScreenState.Shop;
    }

    private void EnterRest()
    {
        if (_run == null) return;
        _restScreen.Setup(_run);
        _currentScreen = ScreenState.Rest;
    }

    private void EnterEvent()
    {
        if (_run == null) return;
        _eventScreen.Setup(_run);
        _currentScreen = ScreenState.Event;
    }

    private void AdvanceFloor()
    {
        if (_run == null) return;
        _run.Floor++;

        if (_run.Floor > _run.MaxFloors)
        {
            _endScreen.Setup(true, _run.Wins, _run.Losses, _run.Floor);
            _currentScreen = ScreenState.Victory;
            return;
        }

        RefreshAvailableNodes();
        _currentScreen = ScreenState.Map;
    }

    protected override void Update(GameTime gameTime)
    {
        // Store previous BEFORE reading new state so Draw() sees the correct prev
        _prevKb = _kb;
        _prevMouse = _mouse;
        _kb = Keyboard.GetState();
        _mouse = Mouse.GetState();

        switch (_currentScreen)
        {
            case ScreenState.Title:
                _titleScreen.Update(gameTime, _kb, _prevKb, _mouse, _prevMouse);
                break;
            case ScreenState.Map:
                _mapScreen.Update(gameTime, _kb, _prevKb, _mouse, _prevMouse);
                break;
            case ScreenState.Battle:
                _battleScreen.Update(gameTime, _kb, _prevKb, _mouse, _prevMouse);
                break;
            case ScreenState.Shop:
                _shopScreen.Update(gameTime, _kb, _prevKb, _mouse, _prevMouse);
                break;
            case ScreenState.Rest:
                _restScreen.Update(gameTime, _kb, _prevKb, _mouse, _prevMouse);
                break;
            case ScreenState.Event:
                _eventScreen.Update(gameTime, _kb, _prevKb, _mouse, _prevMouse);
                break;
            case ScreenState.GameOver:
            case ScreenState.Victory:
                _endScreen.Update(gameTime, _kb, _prevKb, _mouse, _prevMouse);
                break;
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(0x0A, 0x0C, 0x15));

        int sw = GraphicsDevice.Viewport.Width;
        int sh = GraphicsDevice.Viewport.Height;

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

        switch (_currentScreen)
        {
            case ScreenState.Title:
                _titleScreen.RenderWithInput(_spriteBatch, sw, sh, _mouse, _prevMouse);
                break;
            case ScreenState.Map:
                if (_run != null)
                    _mapScreen.RenderWithInput(_spriteBatch, sw, sh, _run, _availableNodes, _mouse, _prevMouse);
                break;
            case ScreenState.Battle:
                _battleScreen.RenderWithInput(_spriteBatch, sw, sh, _mouse, _prevMouse);
                break;
            case ScreenState.Shop:
                _shopScreen.RenderWithInput(_spriteBatch, sw, sh, _mouse, _prevMouse);
                break;
            case ScreenState.Rest:
                _restScreen.Render(_spriteBatch, sw, sh);
                break;
            case ScreenState.Event:
                _eventScreen.Render(_spriteBatch, sw, sh);
                break;
            case ScreenState.GameOver:
            case ScreenState.Victory:
                _endScreen.Render(_spriteBatch, sw, sh);
                break;
        }

        _spriteBatch.End();
        base.Draw(gameTime);
    }
}
