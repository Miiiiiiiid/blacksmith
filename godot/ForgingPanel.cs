using System;
using System.Collections.Generic;
using System.Linq;
using Blacksmith.Core;
using Godot;

namespace Blacksmith;

/// <summary>
/// 제작 미니게임 화면. 망치질(타이밍)·풀무질(스페이스 연타)·세공(방향키 따라 누르기) 중에서
/// 무작위로 세 판을 한다. 같은 게 여러 번 나올 수 있고, 품질은 세 판의 평균이다.
/// 점수 계산은 Core의 ForgingMinigame이 하고, 여기서는 시간 흐름과 입력만 다룬다.
/// </summary>
public partial class ForgingPanel : Control
{
    private static readonly Dictionary<Minigame, string> Names = new()
    {
        [Minigame.Hammer] = "망치질", [Minigame.Bellows] = "풀무질", [Minigame.Arrows] = "세공",
    };

    private static readonly Dictionary<Arrow, string> ArrowGlyphs = new()
    {
        [Arrow.Up] = "↑", [Arrow.Down] = "↓", [Arrow.Left] = "←", [Arrow.Right] = "→",
    };

    private static readonly Dictionary<Arrow, string> ArrowActions = new()
    {
        [Arrow.Up] = "ui_up", [Arrow.Down] = "ui_down", [Arrow.Left] = "ui_left", [Arrow.Right] = "ui_right",
    };

    private string _itemName = "";
    private double _strength;
    private double _finesse;
    private Action<double> _onDone = _ => { };
    private Action _onCancel = () => { };

    private IReadOnlyList<Minigame> _rounds = Array.Empty<Minigame>();
    private readonly List<double> _scores = new();
    private Minigame Current => _rounds[_scores.Count];
    private double _elapsed;
    private bool _started;
    private bool _finished;
    private double _quality;
    /// <summary>판이 바뀐 직후 잠깐 입력을 막는다. 연타하던 스페이스가 다음 판이나 완료로 새지 않게.</summary>
    private const double InputGrace = 0.4;
    private double _roundAge;

    private int _presses;
    private IReadOnlyList<Arrow> _arrows = Array.Empty<Arrow>();
    private readonly List<bool> _arrowHits = new();

    private Label _instruction = null!;
    private Label _scoreLabel = null!;
    private GaugeBar _bar = null!;
    private RichTextLabel _arrowView = null!;
    private HBoxContainer _arrowButtons = null!;
    private Button _actionButton = null!;
    private Button _cancelButton = null!;

    public static ForgingPanel Create(ItemDef item, double strength, double finesse, Action<double> onDone, Action onCancel) =>
        new()
        {
            _itemName = item.Name,
            _strength = strength,
            _finesse = finesse,
            _onDone = onDone,
            _onCancel = onCancel,
        };

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        var box = UiText.CenteredPanel(this, 640, dim: true);

        var title = new Label { Text = $"{_itemName} 제작" };
        title.AddThemeFontSizeOverride("font_size", 22);
        _instruction = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _bar = new GaugeBar { CustomMinimumSize = new Vector2(0, 48), HalfWidth = (float)ForgingMinigame.ZoneHalfWidth(_strength), Target = (float)ForgingMinigame.HammerTarget };
        _arrowView = new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false, CustomMinimumSize = new Vector2(0, 64) };
        _arrowView.AddThemeFontSizeOverride("normal_font_size", 44);
        _arrowButtons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        foreach (var arrow in Enum.GetValues<Arrow>())
        {
            var button = new Button { Text = ArrowGlyphs[arrow], FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(64, 48) };
            button.Pressed += () => OnArrow(arrow);
            _arrowButtons.AddChild(button);
        }
        _scoreLabel = new Label();
        _actionButton = new Button { FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 56) };
        _actionButton.Pressed += OnAction;
        // 결과를 보고 다시 하는 걸 막으려고, 그만두기는 첫 동작 전에만 할 수 있다.
        _cancelButton = new Button { Text = "그만두기", FocusMode = FocusModeEnum.None };
        _cancelButton.Pressed += () => { _onCancel(); QueueFree(); };

        box.AddChild(title);
        box.AddChild(_instruction);
        box.AddChild(_bar);
        box.AddChild(_arrowView);
        box.AddChild(_arrowButtons);
        box.AddChild(_scoreLabel);
        box.AddChild(_actionButton);
        box.AddChild(_cancelButton);

        _rounds = ForgingMinigame.PickRounds(new Random((int)GD.Randi()));
        StartRound();
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
                _bar.Fill = (float)ForgingMinigame.ScoreBellows(_presses, _strength);
                _bar.TimeLeft = (float)Math.Max(0, 1 - _elapsed / ForgingMinigame.BellowsSeconds);
                if (_elapsed >= ForgingMinigame.BellowsSeconds)
                    EndRound(ForgingMinigame.ScoreBellows(_presses, _strength));
                break;
            case Minigame.Arrows:
                _bar.TimeLeft = (float)Math.Max(0, 1 - _elapsed / ForgingMinigame.ArrowSeconds(_finesse));
                // 시간이 다 되면 남은 방향키는 놓친 것으로 친다.
                if (_elapsed >= ForgingMinigame.ArrowSeconds(_finesse))
                    EndRound(ForgingMinigame.ScoreArrows(_arrowHits.Count(hit => hit)));
                break;
        }
        _bar.QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("ui_accept"))
        {
            OnAction();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (_finished || Current != Minigame.Arrows) return;
        foreach (var (arrow, action) in ArrowActions)
            if (e.IsActionPressed(action))
            {
                OnArrow(arrow);
                GetViewport().SetInputAsHandled();
                return;
            }
    }

    private void StartRound()
    {
        _roundAge = 0;
        _elapsed = 0;
        _presses = 0;
        _arrowHits.Clear();
        _bar.Mode = Current == Minigame.Hammer ? GaugeMode.Timing : GaugeMode.Fill;
        _bar.Fill = 0;
        _bar.TimeLeft = 1;
        _arrowView.Visible = Current == Minigame.Arrows;
        _arrowButtons.Visible = Current == Minigame.Arrows;
        _actionButton.Visible = Current != Minigame.Arrows;

        var round = $"{_scores.Count + 1}/{_rounds.Count}판";
        switch (Current)
        {
            case Minigame.Hammer:
                // 시작 위치를 섞어서 박자를 외우지 못하게 한다.
                _elapsed = GD.Randf() * ForgingMinigame.HammerPeriod;
                _started = true;
                _instruction.Text = $"{round} 망치질: 흰 선이 초록 구간에 올 때 내리치세요";
                _actionButton.Text = "내리치기 (스페이스)";
                break;
            case Minigame.Bellows:
                // 연타는 첫 입력부터 시간을 잰다.
                _started = false;
                _instruction.Text = $"{round} 풀무질: 스페이스를 누르면 시작해요. {ForgingMinigame.BellowsSeconds:0}초 동안 빠르게 연타하세요 (만점 {ForgingMinigame.BellowsTarget(_strength)}번)";
                _actionButton.Text = "풀무질 (스페이스)";
                break;
            case Minigame.Arrows:
                _started = true;
                _arrows = ForgingMinigame.RandomArrows(new Random((int)GD.Randi()));
                _instruction.Text = $"{round} 세공: 방향키를 순서대로 누르세요 ({ForgingMinigame.ArrowSeconds(_finesse):0.#}초)";
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
        _actionButton.Text = "완료 (스페이스)";
        _actionButton.Visible = true;
        _arrowButtons.Visible = false;
        UpdateScores();
    }

    private void OnAction()
    {
        if (_roundAge < InputGrace) return;
        if (_finished)
        {
            _onDone(_quality);
            QueueFree();
            return;
        }

        switch (Current)
        {
            case Minigame.Hammer:
                _cancelButton.Visible = false;
                EndRound(ForgingMinigame.ScoreTiming(_bar.Marker, ForgingMinigame.HammerTarget, _bar.HalfWidth));
                break;
            case Minigame.Bellows:
                _cancelButton.Visible = false;
                _started = true;
                _presses++;
                UpdateScores();
                break;
        }
    }

    private void OnArrow(Arrow arrow)
    {
        if (_roundAge < InputGrace || _finished || Current != Minigame.Arrows || _arrowHits.Count >= _arrows.Count) return;
        _cancelButton.Visible = false;
        _arrowHits.Add(_arrows[_arrowHits.Count] == arrow);
        _bar.Fill = (float)_arrowHits.Count / _arrows.Count;
        DrawArrows();
        if (_arrowHits.Count == _arrows.Count)
            EndRound(ForgingMinigame.ScoreArrows(_arrowHits.Count(hit => hit)));
    }

    private void DrawArrows()
    {
        var parts = _arrows.Select((arrow, i) =>
        {
            var glyph = ArrowGlyphs[arrow];
            if (i < _arrowHits.Count) return _arrowHits[i] ? $"[color=#6c6]{glyph}[/color]" : $"[color=#d55]{glyph}[/color]";
            return i == _arrowHits.Count ? $"[color=#fff]{glyph}[/color]" : $"[color=#777]{glyph}[/color]";
        });
        _arrowView.Text = "[center]" + string.Join("  ", parts) + "[/center]";
    }

    private void UpdateScores()
    {
        var parts = _rounds.Select((game, i) =>
        {
            if (i < _scores.Count) return $"{Names[game]} {_scores[i]:P0}";
            if (i == _scores.Count && !_finished && game == Minigame.Bellows) return $"{Names[game]} {_presses}번";
            return i == _scores.Count && !_finished ? $"{Names[game]} …" : "?";
        });
        _scoreLabel.Text = string.Join(" · ", parts);
    }

    private enum GaugeMode { Timing, Fill }

    /// <summary>
    /// 게이지 막대. 망치질에서는 초록 구간과 움직이는 흰 선을,
    /// 풀무질과 세공에서는 채워진 정도와 남은 시간을 그린다.
    /// </summary>
    private partial class GaugeBar : Control
    {
        public GaugeMode Mode { get; set; }
        public float Target { get; set; }
        public float HalfWidth { get; set; }
        public float Marker { get; set; }
        public float Fill { get; set; }
        public float TimeLeft { get; set; } = 1;

        public override void _Draw()
        {
            var w = Size.X;
            var h = Size.Y;
            DrawRect(new Rect2(0, 0, w, h), new Color(0.15f, 0.15f, 0.17f));
            if (Mode == GaugeMode.Timing)
            {
                // 점수가 0이 되는 바깥 구간까지 옅게 보여준다.
                DrawRect(new Rect2((Target - HalfWidth * 2) * w, 0, HalfWidth * 4 * w, h), new Color(0.85f, 0.6f, 0.2f, 0.25f));
                DrawRect(new Rect2((Target - HalfWidth) * w, 0, HalfWidth * 2 * w, h), new Color(0.3f, 0.75f, 0.35f));
                DrawRect(new Rect2(Marker * w - 3, -4, 6, h + 8), Colors.White);
                return;
            }
            DrawRect(new Rect2(0, 0, Fill * w, h), Fill >= 1 ? new Color(0.3f, 0.75f, 0.35f) : new Color(0.9f, 0.5f, 0.2f));
            // 아래쪽 얇은 줄이 남은 시간이다.
            DrawRect(new Rect2(0, h - 6, TimeLeft * w, 6), new Color(0.8f, 0.8f, 0.85f));
        }
    }
}
