using System;
using System.Collections.Generic;
using System.Linq;
using Blacksmith.Core;
using Godot;

namespace Blacksmith;

/// <summary>
/// 제작 미니게임 화면. 미니게임 목록에서 무작위로 세 판을 한다.
/// 같은 게 여러 번 나올 수 있고, 품질은 세 판의 평균이다.
/// 점수 계산은 Core의 ForgingMinigame이 하고, 여기서는 시간 흐름과 입력만 다룬다.
/// </summary>
public partial class ForgingPanel : Control
{
    private static readonly Dictionary<Minigame, string> Names = new()
    {
        [Minigame.Hammer] = "망치질",
        [Minigame.Bellows] = "풀무질",
        [Minigame.Arrows] = "세공",
        [Minigame.Furnace] = "온도 유지",
        [Minigame.Whetstone] = "숫돌질",
        [Minigame.Release] = "담금질",
        [Minigame.Memory] = "각인",
    };

    private static readonly Dictionary<Arrow, string> ArrowGlyphs = new()
    {
        [Arrow.Up] = "↑", [Arrow.Down] = "↓", [Arrow.Left] = "←", [Arrow.Right] = "→",
    };

    private static readonly Dictionary<Arrow, string> ArrowActions = new()
    {
        [Arrow.Up] = "ui_up", [Arrow.Down] = "ui_down", [Arrow.Left] = "ui_left", [Arrow.Right] = "ui_right",
    };

    /// <summary>판이 바뀐 직후 잠깐 입력을 막는다. 연타하던 스페이스가 다음 판이나 완료로 새지 않게.</summary>
    private const double InputGrace = 0.4;

    private string _itemName = "";
    private Stats _stats = new();
    private Action<double> _onDone = _ => { };
    private Action _onCancel = () => { };

    private IReadOnlyList<Minigame> _rounds = Array.Empty<Minigame>();
    private readonly List<double> _scores = new();
    private Minigame Current => _rounds[_scores.Count];
    private double _roundAge;
    private double _elapsed;
    private bool _started;
    private bool _finished;
    private double _quality;

    // 판마다 쓰는 상태. StartRound에서 초기화한다.
    private int _presses;
    private bool _holding;
    private double _value;
    private double _velocity;
    private double _inZone;
    private int _lastSide;
    private double _stunnedUntil;
    private bool _memoryHidden;
    private IReadOnlyList<Arrow> _arrows = Array.Empty<Arrow>();
    private readonly List<bool> _arrowHits = new();

    private Label _instruction = null!;
    private Label _scoreLabel = null!;
    private Label _note = null!;
    private GaugeBar _bar = null!;
    private RichTextLabel _arrowView = null!;
    private HBoxContainer _arrowButtons = null!;
    private HBoxContainer _sideButtons = null!;
    private Button _actionButton = null!;
    private Button _cancelButton = null!;

    private double Strength => _stats[StatType.Strength];
    private double Finesse => _stats[StatType.Finesse];
    private double Boldness => _stats[StatType.Boldness];
    private double Martial => _stats[StatType.Martial];
    private double Scholarship => _stats[StatType.Scholarship];

    public static ForgingPanel Create(ItemDef item, Stats stats, Action<double> onDone, Action onCancel) =>
        new() { _itemName = item.Name, _stats = stats, _onDone = onDone, _onCancel = onCancel };

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        var box = UiText.CenteredPanel(this, 640, dim: true);

        var title = new Label { Text = $"{_itemName} 제작" };
        title.AddThemeFontSizeOverride("font_size", 22);
        _instruction = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _bar = new GaugeBar { CustomMinimumSize = new Vector2(0, 48) };
        _note = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _note.AddThemeColorOverride("font_color", new Color(0.95f, 0.5f, 0.4f));
        _arrowView = new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false, CustomMinimumSize = new Vector2(0, 64) };
        _arrowView.AddThemeFontSizeOverride("normal_font_size", 44);

        _arrowButtons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        foreach (var arrow in Enum.GetValues<Arrow>())
        {
            var button = SmallButton(_arrowButtons, ArrowGlyphs[arrow]);
            button.Pressed += () => OnArrow(arrow);
        }
        _sideButtons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        SmallButton(_sideButtons, "◀").Pressed += () => OnSide(-1);
        SmallButton(_sideButtons, "▶").Pressed += () => OnSide(1);

        _scoreLabel = new Label();
        _actionButton = new Button { FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 56) };
        // 누르고 있는 게임도 마우스로 할 수 있게 누름과 뗌을 따로 받는다.
        _actionButton.ButtonDown += () => OnPress();
        _actionButton.ButtonUp += () => OnRelease();
        // 결과를 보고 다시 하는 걸 막으려고, 그만두기는 첫 동작 전에만 할 수 있다.
        _cancelButton = new Button { Text = "그만두기", FocusMode = FocusModeEnum.None };
        _cancelButton.Pressed += () => { _onCancel(); QueueFree(); };

        foreach (var node in new Control[] { title, _instruction, _bar, _note, _arrowView, _arrowButtons, _sideButtons, _scoreLabel, _actionButton, _cancelButton })
            box.AddChild(node);

        _rounds = ForgingMinigame.PickRounds(new Random((int)GD.Randi()));
        StartRound();
    }

    private static Button SmallButton(Container parent, string text)
    {
        var button = new Button { Text = text, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(72, 48) };
        parent.AddChild(button);
        return button;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        var handled = true;
        if (e.IsActionPressed("ui_accept")) OnPress();
        else if (e.IsActionReleased("ui_accept")) OnRelease();
        else if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.A } or InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.LeftShoulder }) OnSide(-1);
        else if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.D } or InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.RightShoulder }) OnSide(1);
        else if (ArrowActions.FirstOrDefault(kv => e.IsActionPressed(kv.Value)) is { Value: not null } hit)
        {
            if (!_finished && Current == Minigame.Whetstone) OnSide(hit.Key == Arrow.Left ? -1 : hit.Key == Arrow.Right ? 1 : 0);
            else OnArrow(hit.Key);
        }
        else handled = false;
        if (handled) GetViewport().SetInputAsHandled();
    }

    public override void _Process(double delta)
    {
        _roundAge += delta;
        if (_finished || !_started) return;
        _elapsed += delta;

        switch (Current)
        {
            case Minigame.Hammer:
                _bar.Marker = (float)ForgingMinigame.MarkerPosition(_elapsed, ForgingMinigame.HammerPeriod);
                break;

            case Minigame.Bellows:
                _bar.Fill = (float)ForgingMinigame.ScoreBellows(_presses, Strength);
                _bar.TimeLeft = TimeLeft(ForgingMinigame.BellowsSeconds);
                if (_elapsed >= ForgingMinigame.BellowsSeconds) EndRound(ForgingMinigame.ScoreBellows(_presses, Strength));
                break;

            case Minigame.Arrows:
                _bar.TimeLeft = TimeLeft(ForgingMinigame.ArrowSeconds(Finesse));
                // 시간이 다 되면 남은 방향키는 놓친 것으로 친다.
                if (_elapsed >= ForgingMinigame.ArrowSeconds(Finesse)) EndRound(ForgingMinigame.ScoreArrows(ArrowCorrect()));
                break;

            case Minigame.Furnace:
                // 누르면 달궈지고 떼면 식는다. 섬세함이 낮으면 저절로 출렁인다.
                _velocity += (_holding ? 1.6 : -1.2) * delta;
                _velocity += (GD.Randf() - 0.5) * ForgingMinigame.FurnaceWobble(Finesse) * 8 * delta;
                _velocity *= Math.Exp(-1.5 * delta);
                _value += _velocity * delta;
                if (_value is < 0 or > 1) { _value = Math.Clamp(_value, 0, 1); _velocity = 0; }
                if (ForgingMinigame.InFurnaceZone(_value)) _inZone += delta;
                _bar.Marker = (float)_value;
                _bar.Fill = (float)ForgingMinigame.ScoreFurnace(_inZone);
                _bar.TimeLeft = TimeLeft(ForgingMinigame.FurnaceSeconds);
                if (_elapsed >= ForgingMinigame.FurnaceSeconds) EndRound(ForgingMinigame.ScoreFurnace(_inZone));
                break;

            case Minigame.Whetstone:
                _note.Text = _elapsed < _stunnedUntil ? "같은 쪽을 두 번 눌렀어요! 잠깐 멈춤" : "";
                _bar.Fill = (float)ForgingMinigame.ScoreWhetstone(_presses, Boldness);
                _bar.TimeLeft = TimeLeft(ForgingMinigame.WhetstoneSeconds);
                if (_elapsed >= ForgingMinigame.WhetstoneSeconds) EndRound(ForgingMinigame.ScoreWhetstone(_presses, Boldness));
                break;

            case Minigame.Release:
                if (!_holding) break;
                _value = _elapsed / ForgingMinigame.ReleaseFillSeconds;
                _bar.Marker = (float)Math.Min(_value, 1);
                // 끝까지 차도록 붙잡고 있으면 실패다.
                if (_value >= 1) { _holding = false; EndRound(0); }
                break;

            case Minigame.Memory:
                var show = ForgingMinigame.MemoryShowSeconds(Scholarship);
                if (!_memoryHidden && _elapsed >= show)
                {
                    _memoryHidden = true;
                    _instruction.Text = $"{RoundTag()} 각인: 방금 본 방향을 {UiText.ArrowKeys}로 순서대로 누르세요";
                    DrawArrows();
                }
                _bar.TimeLeft = _memoryHidden
                    ? (float)Math.Max(0, 1 - (_elapsed - show) / ForgingMinigame.MemoryInputSeconds)
                    : TimeLeft(show);
                if (_memoryHidden && _elapsed - show >= ForgingMinigame.MemoryInputSeconds)
                    EndRound(ForgingMinigame.ScoreMemory(ArrowCorrect()));
                break;
        }
        _bar.QueueRedraw();
    }

    private float TimeLeft(double limit) => (float)Math.Max(0, 1 - _elapsed / limit);

    private string RoundTag() => $"{_scores.Count + 1}/{_rounds.Count}판";

    private void StartRound()
    {
        _roundAge = 0;
        _elapsed = 0;
        _started = false;
        _presses = 0;
        _holding = false;
        _value = 0;
        _velocity = 0;
        _inZone = 0;
        _lastSide = 0;
        _stunnedUntil = 0;
        _memoryHidden = false;
        _arrowHits.Clear();
        _note.Text = "";

        var game = Current;
        _bar.Reset();
        _arrowView.Visible = game is Minigame.Arrows or Minigame.Memory;
        _arrowButtons.Visible = game is Minigame.Arrows or Minigame.Memory;
        _sideButtons.Visible = game == Minigame.Whetstone;
        _actionButton.Visible = game is Minigame.Hammer or Minigame.Bellows or Minigame.Furnace or Minigame.Release;
        _note.Visible = game == Minigame.Whetstone;

        var tag = RoundTag();
        switch (game)
        {
            case Minigame.Hammer:
                _bar.SetZone(ForgingMinigame.HammerTarget, ForgingMinigame.ZoneHalfWidth(Strength));
                // 시작 위치를 섞어서 박자를 외우지 못하게 한다.
                _elapsed = GD.Randf() * ForgingMinigame.HammerPeriod;
                _started = true;
                _instruction.Text = $"{tag} 망치질: 흰 선이 초록 구간에 올 때 {UiText.ConfirmKey}로 내리치세요";
                _actionButton.Text = "내리치기";
                break;
            case Minigame.Bellows:
                _bar.ShowFill = true;
                _instruction.Text = $"{tag} 풀무질: 누르면 시작해요. {ForgingMinigame.BellowsSeconds:0}초 동안 {UiText.ConfirmKey}를 빠르게 연타하세요 (만점 {ForgingMinigame.BellowsTarget(Strength)}번)";
                _actionButton.Text = "풀무질";
                break;
            case Minigame.Arrows:
                _bar.ShowFill = true;
                _started = true;
                _arrows = ForgingMinigame.RandomArrows(new Random((int)GD.Randi()));
                _instruction.Text = $"{tag} 세공: {UiText.ArrowKeys}로 순서대로 누르세요 ({ForgingMinigame.ArrowSeconds(Finesse):0.#}초)";
                DrawArrows();
                break;
            case Minigame.Furnace:
                _bar.SetZone(ForgingMinigame.FurnaceTarget, ForgingMinigame.FurnaceHalfWidth);
                _bar.ShowFill = true;
                _value = 0.25;
                _bar.Marker = (float)_value;
                _instruction.Text = $"{tag} 온도 유지: {UiText.ConfirmKey}를 누르고 있으면 달궈지고 떼면 식어요. 바늘을 초록 구간에 {ForgingMinigame.FurnaceSeconds:0}초 동안 붙잡아 두세요";
                _actionButton.Text = "풀무 밟기 (누르고 있기)";
                break;
            case Minigame.Whetstone:
                _bar.ShowFill = true;
                _instruction.Text = $"{tag} 숫돌질: {UiText.SideKeys}를 번갈아 누르세요. 같은 쪽을 두 번 누르면 잠깐 멈춰요 (만점 {ForgingMinigame.WhetstoneTarget(Boldness)}번)";
                break;
            case Minigame.Release:
                _bar.SetZone(ForgingMinigame.ReleaseTarget, ForgingMinigame.ReleaseHalfWidth(Martial));
                _instruction.Text = $"{tag} 담금질: {UiText.ConfirmKey}를 누르고 있으면 게이지가 차요. 초록 칸에서 떼세요. 끝까지 차면 실패예요";
                _actionButton.Text = "물에 담그기 (누르고 있다가 떼기)";
                break;
            case Minigame.Memory:
                _bar.ShowFill = true;
                _started = true;
                _arrows = ForgingMinigame.RandomArrows(new Random((int)GD.Randi()), ForgingMinigame.MemoryCount);
                _instruction.Text = $"{tag} 각인: 방향키 {ForgingMinigame.MemoryCount}개를 외우세요 ({ForgingMinigame.MemoryShowSeconds(Scholarship):0.#}초 뒤에 사라져요)";
                DrawArrows();
                break;
        }
        UpdateScores();
    }

    private void EndRound(double score)
    {
        _scores.Add(score);
        if (_scores.Count < _rounds.Count)
        {
            StartRound();
            return;
        }

        _quality = ForgingMinigame.Combine(_scores);
        _finished = true;
        _roundAge = 0;
        _instruction.Text = $"완성! 품질 {_quality:P0}";
        _actionButton.Text = "완료";
        _actionButton.Visible = true;
        _arrowButtons.Visible = false;
        _sideButtons.Visible = false;
        _note.Visible = false;
        UpdateScores();
    }

    private void OnPress()
    {
        if (_roundAge < InputGrace) return;
        if (_finished) return; // 완료는 뗄 때 처리해서, 누르고 있던 손이 결과창을 바로 닫지 않게 한다.

        switch (Current)
        {
            case Minigame.Hammer:
                _cancelButton.Visible = false;
                EndRound(ForgingMinigame.ScoreTiming(_bar.Marker, ForgingMinigame.HammerTarget, ForgingMinigame.ZoneHalfWidth(Strength)));
                break;
            case Minigame.Bellows:
                _cancelButton.Visible = false;
                _started = true;
                _presses++;
                UpdateScores();
                break;
            case Minigame.Furnace:
                _cancelButton.Visible = false;
                _started = true;
                _holding = true;
                break;
            case Minigame.Release:
                if (_holding) break;
                _cancelButton.Visible = false;
                _started = true;
                _holding = true;
                _elapsed = 0;
                break;
        }
    }

    private void OnRelease()
    {
        if (_finished)
        {
            if (_roundAge < InputGrace) return;
            _onDone(_quality);
            QueueFree();
            return;
        }
        if (!_holding) return;
        _holding = false;
        if (Current == Minigame.Release)
            EndRound(ForgingMinigame.ScoreTiming(_value, ForgingMinigame.ReleaseTarget, ForgingMinigame.ReleaseHalfWidth(Martial)));
    }

    private void OnSide(int side)
    {
        if (side == 0 || _roundAge < InputGrace || _finished || Current != Minigame.Whetstone) return;
        _cancelButton.Visible = false;
        _started = true;
        if (_elapsed < _stunnedUntil) return;
        if (side == _lastSide)
        {
            _stunnedUntil = _elapsed + ForgingMinigame.WhetstoneStunSeconds;
            return;
        }
        _lastSide = side;
        _presses++;
        UpdateScores();
    }

    private void OnArrow(Arrow arrow)
    {
        if (_roundAge < InputGrace || _finished) return;
        if (Current is not (Minigame.Arrows or Minigame.Memory) || _arrowHits.Count >= _arrows.Count) return;
        // 각인은 방향키가 사라진 뒤에만 입력을 받는다.
        if (Current == Minigame.Memory && !_memoryHidden) return;
        _cancelButton.Visible = false;
        _arrowHits.Add(_arrows[_arrowHits.Count] == arrow);
        _bar.Fill = (float)_arrowHits.Count / _arrows.Count;
        DrawArrows();
        if (_arrowHits.Count < _arrows.Count) return;
        EndRound(Current == Minigame.Arrows
            ? ForgingMinigame.ScoreArrows(ArrowCorrect())
            : ForgingMinigame.ScoreMemory(ArrowCorrect()));
    }

    private int ArrowCorrect() => _arrowHits.Count(hit => hit);

    private void DrawArrows()
    {
        var hide = Current == Minigame.Memory && _memoryHidden;
        var parts = _arrows.Select((arrow, i) =>
        {
            var glyph = ArrowGlyphs[arrow];
            if (i < _arrowHits.Count) return _arrowHits[i] ? $"[color=#6c6]{glyph}[/color]" : $"[color=#d55]{glyph}[/color]";
            if (hide) return i == _arrowHits.Count ? "[color=#fff]?[/color]" : "[color=#777]?[/color]";
            return i == _arrowHits.Count ? $"[color=#fff]{glyph}[/color]" : $"[color=#777]{glyph}[/color]";
        });
        _arrowView.Text = "[center]" + string.Join("  ", parts) + "[/center]";
    }

    private void UpdateScores()
    {
        var parts = _rounds.Select((game, i) =>
        {
            if (i < _scores.Count) return $"{Names[game]} {_scores[i]:P0}";
            if (i > _scores.Count || _finished) return "?";
            return game is Minigame.Bellows or Minigame.Whetstone ? $"{Names[game]} {_presses}번" : $"{Names[game]} …";
        });
        _scoreLabel.Text = string.Join(" · ", parts);
    }

    /// <summary>
    /// 게이지 막대. 초록 구간과 흰 선, 주황 채움, 아래쪽 남은 시간 줄을 필요한 것만 그린다.
    /// </summary>
    private partial class GaugeBar : Control
    {
        public bool ShowZone { get; set; }
        public bool ShowFill { get; set; }
        public float Target { get; set; }
        public float HalfWidth { get; set; }
        public float Marker { get; set; } = -1;
        public float Fill { get; set; }
        public float TimeLeft { get; set; } = -1;

        public void Reset()
        {
            ShowZone = false;
            ShowFill = false;
            Marker = -1;
            Fill = 0;
            TimeLeft = -1;
            QueueRedraw();
        }

        public void SetZone(double target, double halfWidth)
        {
            ShowZone = true;
            Target = (float)target;
            HalfWidth = (float)halfWidth;
            Marker = 0;
        }

        public override void _Draw()
        {
            var w = Size.X;
            var h = Size.Y;
            DrawRect(new Rect2(0, 0, w, h), new Color(0.15f, 0.15f, 0.17f));
            if (ShowFill)
                DrawRect(new Rect2(0, 0, Fill * w, ShowZone ? h * 0.25f : h), Fill >= 1 ? new Color(0.3f, 0.75f, 0.35f) : new Color(0.9f, 0.5f, 0.2f));
            if (ShowZone)
            {
                // 점수가 0이 되는 바깥 구간까지 옅게 보여준다.
                DrawRect(new Rect2((Target - HalfWidth * 2) * w, 0, HalfWidth * 4 * w, h), new Color(0.85f, 0.6f, 0.2f, 0.25f));
                DrawRect(new Rect2((Target - HalfWidth) * w, 0, HalfWidth * 2 * w, h), new Color(0.3f, 0.75f, 0.35f, 0.85f));
            }
            if (Marker >= 0) DrawRect(new Rect2(Marker * w - 3, -4, 6, h + 8), Colors.White);
            if (TimeLeft >= 0) DrawRect(new Rect2(0, h - 6, TimeLeft * w, 6), new Color(0.8f, 0.8f, 0.85f));
        }
    }
}
