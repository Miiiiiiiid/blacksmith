using System;
using System.Collections.Generic;
using Blacksmith.Core;
using Godot;

namespace Blacksmith;

/// <summary>
/// 무기 제작 미니게임 화면. 가열 → 망치질 5번 → 담금질 순서로, 움직이는 표시를 초록 구간에서 멈춘다.
/// 점수 계산은 Core의 ForgingMinigame이 하고, 여기서는 시간 흐름과 입력만 다룬다.
/// </summary>
public partial class ForgingPanel : Control
{
    private static readonly Dictionary<ForgingStage, string> StageNames = new()
    {
        [ForgingStage.Heat] = "가열: 쇠가 알맞게 달궈졌을 때 꺼내세요",
        [ForgingStage.Hammer] = "망치질: 표시가 가운데 올 때 내리치세요",
        [ForgingStage.Quench] = "담금질: 알맞은 순간에 물에서 건지세요",
    };

    private string _itemName = "";
    private double _halfWidth;
    private Action<double> _onDone = _ => { };
    private Action _onCancel = () => { };

    private ForgingStage _stage = ForgingStage.Heat;
    private double _elapsed;
    private bool _running = true;
    private double _heat;
    private readonly List<double> _hammers = new();
    private double _quench;
    private double _quality;

    private Label _title = null!;
    private Label _instruction = null!;
    private Label _scores = null!;
    private GaugeBar _bar = null!;
    private Button _actionButton = null!;
    private Button _cancelButton = null!;

    public static ForgingPanel Create(string itemName, double strength, Action<double> onDone, Action onCancel)
    {
        var panel = new ForgingPanel
        {
            _itemName = itemName,
            _halfWidth = ForgingMinigame.ZoneHalfWidth(strength),
            _onDone = onDone,
            _onCancel = onCancel,
        };
        return panel;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var backdrop = new ColorRect { Color = new Color(0, 0, 0, 0.6f) };
        AddChild(backdrop);
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var center = new CenterContainer();
        AddChild(center);
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(640, 0) };
        center.AddChild(panel);
        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{side}", 24);
        panel.AddChild(margin);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 16);
        margin.AddChild(box);

        _title = new Label { Text = $"{_itemName} 제작" };
        _title.AddThemeFontSizeOverride("font_size", 22);
        _instruction = new Label();
        _bar = new GaugeBar { CustomMinimumSize = new Vector2(0, 48), HalfWidth = (float)_halfWidth };
        _scores = new Label();
        _actionButton = new Button { Text = "멈추기 (스페이스)", FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 56) };
        _actionButton.Pressed += OnAction;
        // 결과를 보고 다시 하는 걸 막으려고, 그만두기는 첫 동작 전에만 할 수 있다.
        _cancelButton = new Button { Text = "그만두기", FocusMode = FocusModeEnum.None };
        _cancelButton.Pressed += () => { _onCancel(); QueueFree(); };

        box.AddChild(_title);
        box.AddChild(_instruction);
        box.AddChild(_bar);
        box.AddChild(_scores);
        box.AddChild(_actionButton);
        box.AddChild(_cancelButton);

        StartStage(ForgingStage.Heat);
    }

    public override void _Process(double delta)
    {
        if (!_running) return;
        _elapsed += delta;
        var (_, period) = ForgingMinigame.StageSetup(_stage);
        _bar.Marker = (float)ForgingMinigame.MarkerPosition(_elapsed, period);
        _bar.QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("ui_accept"))
        {
            OnAction();
            GetViewport().SetInputAsHandled();
        }
    }

    private void StartStage(ForgingStage stage)
    {
        _stage = stage;
        // 가열은 차가운 쇠에서 시작하고, 나머지는 시작 위치를 섞어서 박자를 외우지 못하게 한다.
        var (target, period) = ForgingMinigame.StageSetup(stage);
        _elapsed = stage == ForgingStage.Heat ? 0 : GD.Randf() * period;
        _bar.Target = (float)target;
        _instruction.Text = stage == ForgingStage.Hammer
            ? $"{StageNames[stage]} ({_hammers.Count + 1}/{ForgingMinigame.HammerStrikes})"
            : StageNames[stage];
        _running = true;
        UpdateScores();
    }

    private void OnAction()
    {
        if (!_running)
        {
            _onDone(_quality);
            QueueFree();
            return;
        }

        _cancelButton.Visible = false;
        var (target, _) = ForgingMinigame.StageSetup(_stage);
        var score = ForgingMinigame.ScoreTiming(_bar.Marker, target, _halfWidth);
        switch (_stage)
        {
            case ForgingStage.Heat:
                _heat = score;
                StartStage(ForgingStage.Hammer);
                break;
            case ForgingStage.Hammer:
                _hammers.Add(score);
                StartStage(_hammers.Count < ForgingMinigame.HammerStrikes ? ForgingStage.Hammer : ForgingStage.Quench);
                break;
            case ForgingStage.Quench:
                _quench = score;
                _quality = ForgingMinigame.Combine(_heat, _hammers, _quench);
                _running = false;
                _instruction.Text = $"완성! 품질 {_quality:P0}";
                _actionButton.Text = "완료 (스페이스)";
                UpdateScores();
                break;
        }
    }

    private void UpdateScores()
    {
        var hammers = _hammers.Count == 0 ? "-" : string.Join(" ", _hammers.ConvertAll(h => $"{h:P0}"));
        var heat = _stage == ForgingStage.Heat ? "-" : $"{_heat:P0}";
        var quench = _running ? "-" : $"{_quench:P0}";
        _scores.Text = $"가열 {heat} · 망치질 {hammers} · 담금질 {quench}";
    }

    /// <summary>게이지 막대. 초록 구간이 적정 범위이고 흰 선이 움직이는 표시다.</summary>
    private partial class GaugeBar : Control
    {
        public float Target { get; set; }
        public float HalfWidth { get; set; }
        public float Marker { get; set; }

        public override void _Draw()
        {
            var w = Size.X;
            var h = Size.Y;
            DrawRect(new Rect2(0, 0, w, h), new Color(0.15f, 0.15f, 0.17f));
            // 점수가 0이 되는 바깥 구간까지 옅게 보여준다.
            DrawRect(new Rect2((Target - HalfWidth * 2) * w, 0, HalfWidth * 4 * w, h), new Color(0.85f, 0.6f, 0.2f, 0.25f));
            DrawRect(new Rect2((Target - HalfWidth) * w, 0, HalfWidth * 2 * w, h), new Color(0.3f, 0.75f, 0.35f));
            DrawRect(new Rect2(Marker * w - 3, -4, 6, h + 8), Colors.White);
        }
    }
}
