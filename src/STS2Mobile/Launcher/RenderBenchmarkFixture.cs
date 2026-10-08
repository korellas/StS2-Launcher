using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Test;
using MegaCrit.Sts2.Core.Unlocks;
using STS2Mobile.Patches;

namespace STS2Mobile.Launcher;

public sealed class RenderBenchmarkFixture : IDisposable
{
    // Game updates can insert enum members; resolve the loaded game's value by name.
    private static readonly ActionSynchronizerCombatState PlayerPlayPhase =
        Enum.Parse<ActionSynchronizerCombatState>(nameof(ActionSynchronizerCombatState.PlayPhase));
    private readonly Action _check;
    private readonly BenchmarkTrace _trace;
    private readonly HashSet<string> _visited = new();
    private Player _player;
    private RunState _state;
    private NCombatRoom _room;
    private Task _combatSequence = Task.CompletedTask;
    private int _turnsPlayed;
    private int _cardsPlayed;
    private string _deck;
    private int _initialPlayerHp;
    private string _initialEnemyHp;
    public string SequenceSummary =>
        $"turns={_turnsPlayed}, cards={_cardsPlayed}, HP={_initialPlayerHp}->{_player.Creature.CurrentHp}, enemiesAtStart={_initialEnemyHp}, combatEnded={!CombatManager.Instance.IsInProgress}, deck={_deck}";
    private string _scenario;
    private int _effectStep = -1;
    private ulong _animationStart;
    private string _currentScreen = "Reset";
    public List<BenchmarkLoadTiming> LoadTimings { get; } = new();

    public RenderBenchmarkFixture(Action check)
        : this(check, null) { }

    internal RenderBenchmarkFixture(Action check, BenchmarkTrace trace)
    {
        _check = check;
        _trace = trace;
    }

    public static void ValidateEntry()
    {
        if (NGame.Instance == null || RunManager.Instance.IsInProgress)
            throw new InvalidOperationException("Benchmark requires a fresh game launcher boot");
    }

    public async Task Initialize()
    {
        using var trace = _trace?.Span("Initialize");
        ValidateEntry();
        _check();
        // GameStartup migrates and synchronizes real profiles. Use the game's
        // initialization and in-memory save APIs directly for this isolated boot.
        var saves = new SaveManager(new MockGodotFileIo("user://render-benchmark"));
        SaveManager.MockInstanceForTesting(saves);
        await OneTimeInitialization.ExecuteVeryEarly();
        saves.SettingsSave.AspectRatioSetting = MegaCrit.Sts2.Core.Settings.AspectRatioSetting.Auto;
        NCard.InitPool();
        NGridCardHolder.InitPool();
        OneTimeInitialization.ExecuteEssential();
        saves.InitProfileId();
        saves.InitProgressData();
        saves.InitPrefsData();
        saves.SetFtuesEnabled(false);
        OneTimeInitialization.ExecuteDeferred();
        await PreloadManager.LoadCommonAndMainMenuAssets();
        _check();
        GraphicsPatches.StartRuntime(NGame.Instance.GetTree());
    }

    public async Task Build(RenderBenchmarkCase test)
    {
        using var trace = _trace?.Span($"Build.{test.Scene}.{test.Name}");
        await FinishActions();
        Dispose();
        LoadTimings.Clear();
        _currentScreen = "Reset";
        _check();
        var settings = GraphicsPatches.Settings;
        settings.RenderScale = test.Scale;
        SaveManager.Instance.SettingsSave.Msaa = test.Msaa;
        SaveManager.Instance.SettingsSave.FpsLimit = test.Fps;
        SaveManager.Instance.SettingsSave.VSync = MegaCrit.Sts2.Core.Settings.VSyncType.Off;
        settings.Hdr = test.Hdr ? 1 : 0;
        settings.TextureFilter = test.Filter;
        settings.DirectCardPortraits = test.Direct;
        settings.RadialBlurSamples = test.Blur;
        settings.ScreenDistortion = test.Distortion;
        settings.BackgroundParticles = test.Particles;
        GraphicsPatches.GraphicsPreferencesPostfix();

        _player = Player.CreateForNewRun<Defect>(UnlockState.all, 1);
        _state = RunState.CreateForNewRun(
            new[] { _player },
            ActModel.GetDefaultList().Select(act => act.ToMutable()).ToArray(),
            Array.Empty<ModifierModel>(),
            GameMode.Standard,
            0,
            RenderBenchmarkCase.Seed
        );
        if (test.Scene is "CombatTurns" or "KaiserCrabTurns" or "WaterfallGiantTurns")
        {
            _player.MaxEnergy = RenderBenchmarkCase.MaxEnergy;
            await CardPileCmd.RemoveFromDeck(_player.Deck.Cards.ToArray(), showPreview: false);
            await CardPileCmd.Add(
                new CardModel[]
                {
                    _state.CreateCard<StrikeDefect>(_player),
                    _state.CreateCard<DefendDefect>(_player),
                    _state.CreateCard<BallLightning>(_player),
                    _state.CreateCard<BallLightning>(_player),
                    _state.CreateCard<SweepingBeam>(_player),
                    _state.CreateCard<SweepingBeam>(_player),
                    _state.CreateCard<Glacier>(_player),
                    _state.CreateCard<Coolheaded>(_player),
                    _state.CreateCard<MeteorStrike>(_player),
                    _state.CreateCard<Hyperbeam>(_player),
                },
                PileType.Deck,
                skipVisuals: true
            );
            _deck = string.Join(",", _player.Deck.Cards.Select(card => card.Id.Entry));
        }
        _turnsPlayed = _cardsPlayed = 0;
        await TimeScreen(
            "Run",
            async () =>
            {
                RunManager.Instance.SetUpNewSingleplayer(_state, shouldSave: false);
                RunManager.Instance.CombatReplayWriter.IsEnabled = false;
                await PreloadManager.LoadRunAssets(new[] { _player.Character });
                RunManager.Instance.Launch();
                NGame.Instance.RootSceneContainer.SetCurrentScene(NRun.Create(_state));
                await RunManager.Instance.SetActInternal(test.Scene == "KaiserCrabTurns" ? 1 : 0);
            }
        );
        _scenario = test.Scene;
        _effectStep = -1;
        switch (test.Scene)
        {
            case "CombatIdle":
            case "CombatTurns":
            case "CombatCards":
            case "CombatEffects":
            case "KaiserCrabTurns":
            case "WaterfallGiantTurns":
                await OpenCombat(test.Scene, transition: false);
                break;
            case "Merchant":
                await OpenMerchant(transition: false);
                break;
            case "Map":
            case "Transitions":
                await OpenMap();
                break;
            case "Deck":
                await OpenMap();
                await OpenDeck();
                break;
            default:
                throw new InvalidOperationException($"Unknown benchmark scenario: {test.Scene}");
        }
        _animationStart = Time.GetTicksUsec();
        PatchHelper.Log(
            $"[Benchmark] Actual game screen ready: {test.Scene}, seed={RenderBenchmarkCase.Seed}"
        );
    }

    private async Task OpenCombat(string scenario, bool transition)
    {
        await TimeScreen(
            scenario,
            async () =>
            {
                if (transition)
                    await NGame.Instance.Transition.RoomFadeOut();
                bool boss = scenario is "KaiserCrabTurns" or "WaterfallGiantTurns";
                EncounterModel encounter = scenario switch
                {
                    "KaiserCrabTurns" => ModelDb.Encounter<KaiserCrabBoss>().ToMutable(),
                    "WaterfallGiantTurns" => ModelDb.Encounter<WaterfallGiantBoss>().ToMutable(),
                    _ => ModelDb.Encounter<ConstructMenagerieNormal>().ToMutable(),
                };
                await RunManager.Instance.EnterRoomDebug(
                    boss ? RoomType.Boss : RoomType.Monster,
                    model: encounter,
                    showTransition: transition
                );
                _room =
                    NCombatRoom.Instance
                    ?? throw new InvalidOperationException("Actual combat scene was not created");
                // Room loading starts combat asynchronously; wait for the actual hand
                // and turn setup before measuring, rather than time the initial deal.
                ulong started = Time.GetTicksMsec();
                using var trace = _trace?.Span("Combat.WaitForHand");
                while (
                    CombatManager.Instance.IsStarting
                    || RunManager.Instance.ActionExecutor.IsPaused
                    || _room.Ui.Hand.ActiveHolders.Count == 0
                )
                {
                    _check();
                    if (Time.GetTicksMsec() - started > 30000)
                        throw new InvalidOperationException(
                            "Actual combat hand did not become ready"
                        );
                    await NGame.Instance.ToSignal(
                        NGame.Instance.GetTree(),
                        SceneTree.SignalName.ProcessFrame
                    );
                }
                if (scenario == "CombatCards")
                    _room.Ui.Hand.ActiveHolders[0].Call("OnFocus");
                if (scenario is "CombatTurns" or "KaiserCrabTurns" or "WaterfallGiantTurns")
                {
                    _initialPlayerHp = _player.Creature.CurrentHp;
                    _initialEnemyHp = string.Join(
                        ",",
                        _player.Creature.CombatState.Enemies.Select(enemy =>
                            $"{enemy.Monster.Id.Entry}:{enemy.CurrentHp}/{enemy.MaxHp}"
                        )
                    );
                }
            }
        );
    }

    private Task OpenMerchant(bool transition) =>
        TimeScreen(
            "Merchant",
            async () =>
            {
                if (transition)
                    await NGame.Instance.Transition.RoomFadeOut();
                await RunManager.Instance.EnterRoomDebug(RoomType.Shop, showTransition: transition);
                var merchant =
                    NMerchantRoom.Instance
                    ?? throw new InvalidOperationException("Actual merchant room was not created");
                merchant.OpenInventory();
            }
        );

    private Task OpenMap() =>
        TimeScreen(
            "Map",
            async () =>
            {
                NRun.Instance.GlobalUi.CapstoneContainer.Close();
                if (_state.CurrentRoom == null)
                    await RunManager.Instance.EnterRoomDebug(RoomType.Map, showTransition: false);
                NRun.Instance.GlobalUi.MapScreen.Open(isOpenedFromTopBar: true);
            }
        );

    private Task OpenDeck() =>
        TimeScreen(
            "Deck",
            () =>
            {
                NRun.Instance.GlobalUi.MapScreen.Close(animateOut: false);
                if (NDeckViewScreen.ShowScreen(_player) == null)
                    throw new InvalidOperationException("Actual deck screen was not created");
                return Task.CompletedTask;
            }
        );

    private async Task TimeScreen(string target, Func<Task> open)
    {
        using var trace = _trace?.Span("Screen." + target);
        _check();
        string from = _currentScreen;
        ulong start = Time.GetTicksUsec();
        await open();
        _check();
        await NGame.Instance.ToSignal(NGame.Instance.GetTree(), SceneTree.SignalName.ProcessFrame);
        await NGame.Instance.ToSignal(
            RenderingServer.Singleton,
            RenderingServer.SignalName.FramePostDraw
        );
        _check();
        LoadTimings.Add(
            new BenchmarkLoadTiming
            {
                From = from,
                To = target,
                DurationMs = (Time.GetTicksUsec() - start) / 1000d,
                FirstVisit = _visited.Add(target),
            }
        );
        _currentScreen = target;
    }

    public async Task RunTransitions()
    {
        // Yield before the first load so the frame sampler also includes any
        // synchronous scene construction or shader compilation in that load.
        await NGame.Instance.ToSignal(NGame.Instance.GetTree(), SceneTree.SignalName.ProcessFrame);
        await OpenCombat("CombatIdle", transition: true);
        await HoldScreen();
        await OpenMerchant(transition: true);
        await HoldScreen();
        await OpenMap();
        await HoldScreen();
        await OpenDeck();
        await HoldScreen();
        await OpenMap();
        await HoldScreen();
        await OpenCombat("CombatIdle", transition: true);
        await HoldScreen();
    }

    private async Task HoldScreen()
    {
        _check();
        var timer = NGame.Instance.GetTree().CreateTimer(.9);
        await NGame.Instance.ToSignal(timer, SceneTreeTimer.SignalName.Timeout);
        _check();
    }

    public void Animate()
    {
        if (_scenario != "CombatEffects")
            return;
        int step = (int)(
            (Time.GetTicksUsec() - _animationStart)
            / (RenderBenchmarkCase.EffectIntervalSeconds * 1_000_000UL)
        );
        if (step == _effectStep)
            return;
        _effectStep = step;
        var target = _room.CreatureNodes.Last(node => !node.Entity.IsPlayer);
        var source = _room.GetCreatureNode(_player.Creature);
        source.SetAnimationTrigger("Cast");
        _room.CombatVfxContainer.AddChild(NHyperbeamVfx.Create(_player.Creature, target.Entity));
        _room.CombatVfxContainer.AddChild(NHitSparkVfx.Create(target.Entity));
        _room.CombatVfxContainer.AddChild(NScreamVfx.Create(target.VfxSpawnPosition));
        _room.RadialBlur(VfxPosition.Center);
    }

    public Task RunCombatTurns() => _combatSequence = RunCombatTurnsCore();

    private async Task RunCombatTurnsCore()
    {
        // Start after the sampler is attached, including the first queued action.
        await NGame.Instance.ToSignal(NGame.Instance.GetTree(), SceneTree.SignalName.ProcessFrame);
        while (
            _turnsPlayed < RenderBenchmarkCase.CombatTurnLimit
            && CombatManager.Instance.IsInProgress
        )
        {
            await WaitForPlayerTurn(0);
            if (!CombatManager.Instance.IsInProgress)
                break;
            int turn = _player.PlayerCombatState.TurnNumber;
            _turnsPlayed++;
            while (CombatManager.Instance.IsInProgress && !CombatManager.Instance.IsOverOrEnding)
            {
                _check();
                var target = _player.Creature.CombatState.HittableEnemies.FirstOrDefault();
                var card = _player
                    .PlayerCombatState.Hand.Cards.Where(card =>
                        card.CanPlayTargeting(
                            card.TargetType == TargetType.AnyEnemy ? target : null
                        )
                    )
                    .OrderByDescending(card =>
                        card is MeteorStrike ? 2
                        : card is Hyperbeam ? 1
                        : 0
                    )
                    .FirstOrDefault();
                if (card == null)
                    break;
                await PlayCard(card);
            }
            if (!CombatManager.Instance.IsInProgress)
                break;
            _check();
            PlayerCmd.EndTurn(_player, canBackOut: false);
            await WaitForPlayerTurn(turn);
        }
        var tail = NGame.Instance.GetTree().CreateTimer(RenderBenchmarkCase.CombatTailSeconds);
        await NGame.Instance.ToSignal(tail, SceneTreeTimer.SignalName.Timeout);
        _check();
    }

    private async Task WaitForPlayerTurn(int previousTurn)
    {
        ulong started = Time.GetTicksMsec();
        while (
            CombatManager.Instance.IsInProgress
            && (
                _player.PlayerCombatState.TurnNumber <= previousTurn
                || RunManager.Instance.ActionQueueSynchronizer.CombatState != PlayerPlayPhase
                || RunManager.Instance.ActionExecutor.IsPaused
            )
        )
        {
            _check();
            if (Time.GetTicksMsec() - started > 45000)
                throw new InvalidOperationException("Benchmark enemy turn did not finish");
            await NGame.Instance.ToSignal(
                NGame.Instance.GetTree(),
                SceneTree.SignalName.ProcessFrame
            );
        }
        _check();
    }

    private async Task PlayCard(CardModel card)
    {
        var target =
            card.TargetType == TargetType.AnyEnemy
                ? _player.Creature.CombatState.HittableEnemies.First()
                : null;
        if (!card.CanPlayTargeting(target))
            throw new InvalidOperationException(
                $"Benchmark attack card cannot be played: {card.Id}"
            );
        await card.OnEnqueuePlayVfx(target);
        _check();
        var action = new PlayCardAction(card, target);
        RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(action);
        await action.CompletionTask.WaitAsync(TimeSpan.FromSeconds(20));
        if (action.Exception != null)
            throw new InvalidOperationException("Benchmark attack failed", action.Exception);
        if (CombatManager.Instance.IsInProgress && card.Pile?.Type == PileType.Hand)
            throw new InvalidOperationException($"Benchmark card action was cancelled: {card.Id}");
        _cardsPlayed++;
        _check();
    }

    public Task FinishActions() => _combatSequence;

    public void Dispose()
    {
        _combatSequence = Task.CompletedTask;
        _room = null;
        if (!RunManager.Instance.IsInProgress)
            return;
        RunManager.Instance.CleanUp(graceful: true);
        NGame.Instance.RootSceneContainer.SetCurrentScene(new Control());
        // Keep the mock SaveManager active until the cold restart so quit
        // handlers cannot write benchmark data to the user's real profiles.
    }
}
