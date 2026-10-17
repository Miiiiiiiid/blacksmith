using System;
using System.Linq;
using Blacksmith.Core;
using Godot;

namespace Blacksmith;

/// <summary>
/// 하루 행동 화면. 게임 규칙은 전부 Core에 있고, 여기서는 보여주고 입력만 받는다.
/// 버튼만 있는 임시 UI이며 도트 아트는 나중에 입힌다.
/// </summary>
public partial class PlayView : Control
{
    private GameData _data = null!;
    private Game _game = null!;
    private Action _save = () => { };
    private Action _onEndDay = () => { };
    private Action _onTitle = () => { };

    private Label _news = null!;
    private Label _status = null!;
    private Label _stats = null!;
    private VBoxContainer _actions = null!;
    private VBoxContainer _inventory = null!;
    private RichTextLabel _log = null!;

    public static PlayView Create(Game game, Action save, Action onEndDay, Action onTitle) =>
        new() { _game = game, _data = game.Data, _save = save, _onEndDay = onEndDay, _onTitle = onTitle };

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildLayout();
        Refresh();
    }

    private void Do(Func<ActionResult> action)
    {
        var result = action();
        if (!result.Ok) _log.AppendText($"[color=#d66]{result.Message}[/color]\n");
        _save();
        Refresh();
        // 행동을 다 쓰면 하루를 저절로 마친다. 하루 마치기 버튼은 일찍 끝낼 때 쓴다.
        if (result.Ok && _game.State.ActionsLeft <= 0 && _game.State.Status == GameStatus.Playing)
            _onEndDay();
    }

    private void BuildLayout()
    {
        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{side}", 16);
        AddChild(margin);

        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 24);
        margin.AddChild(columns);

        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        columns.AddChild(left);
        columns.AddChild(right);

        _news = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _news.AddThemeColorOverride("font_color", new Color(0.95f, 0.8f, 0.5f));
        left.AddChild(_news);
        _status = new Label();
        _stats = new Label();
        _actions = new VBoxContainer();
        left.AddChild(_status);
        left.AddChild(_stats);
        left.AddChild(new HSeparator());
        left.AddChild(_actions);

        right.AddChild(new Label { Text = "보관 중인 물건" });
        _inventory = new VBoxContainer();
        right.AddChild(_inventory);
        right.AddChild(new HSeparator());
        _log = new RichTextLabel
        {
            BbcodeEnabled = true,
            ScrollFollowing = true,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        right.AddChild(_log);
    }

    private void Refresh()
    {
        var s = _game.State;
        var materials = string.Join(", ", _data.Materials.Values.Select(m => $"{m.Name} {s.MaterialCount(m.Id)}"));
        _news.Text = MorningNews();
        var invasion = _game.Settings.Invasions
            ? $"마왕군 위협 {s.Threat:0} · 납품 방어력 {s.DeliveredDefense:0.#} · " +
              $"다음 침공까지 {_game.DaysUntilInvasion}일 (방어 확률 {_game.DefenseChance:P0})\n"
            : "";
        _status.Text =
            $"{s.Day}일째 / {GameRules.VictoryDay}일 · 남은 행동 {s.ActionsLeft} · {s.Gold} 골드\n" +
            invasion +
            $"재료: {materials}";

        var st = s.Stats;
        _stats.Text =
            $"힘 {st[StatType.Strength]:0.0} ↔ 섬세함 {st[StatType.Finesse]:0.0}\n" +
            $"화술 {st[StatType.Eloquence]:0.0} ↔ 패기 {st[StatType.Boldness]:0.0}\n" +
            $"무학 {st[StatType.Martial]:0.0} ↔ 학문 {st[StatType.Scholarship]:0.0}\n" +
            $"명성 {st.Fame:+0;-0;0}";

        Clear(_actions);
        var gather = Row(_actions, $"채집  {Hint(GameRules.GatherTraining)}");
        foreach (var m in _data.Materials.Values.Where(m => m.GatherAmount > 0))
            AddButton(gather, $"{m.Name} +{m.GatherAmount}", () => Do(() => _game.Gather(m.Id)));

        var buy = Row(_actions, $"구매  {Hint(GameRules.BuyTraining)}");
        foreach (var m in _data.Materials.Values)
            AddButton(buy, $"{m.Name} ({_game.BuyCost(m, 1)}G)", () => Do(() => _game.Buy(m.Id, 1)));

        _actions.AddChild(new Label { Text = "제작" });
        foreach (var item in _data.Items.Values)
        {
            var canCraft = _game.CanCraft(item.Id) && s.ActionsLeft > 0;
            var row = Row(_actions, $"  {item.Name}  {Hint(item.Training)}");
            AddButton(row, "만들기", () => OpenForging(item)).Disabled = !canCraft;
            if (_game.AutoCraftQuality(item.Id) is { } autoQuality)
                AddButton(row, $"자동 {autoQuality:P0}", () => Do(() => _game.CraftAuto(item.Id))).Disabled = !canCraft;
            // 필요한 재료를 "가진 수/필요한 수"로 보여주고, 모자라면 빨갛게 표시한다.
            var needs = new RichTextLabel { BbcodeEnabled = true, FitContent = true, AutowrapMode = TextServer.AutowrapMode.Off, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            needs.Text = string.Join("  ", item.Materials.Select(m =>
            {
                var have = s.MaterialCount(m.Key);
                var text = $"{_data.Materials[m.Key].Name} {have}/{m.Value}";
                return have >= m.Value ? text : $"[color=#e07070]{text}[/color]";
            }));
            row.AddChild(needs);
        }

        _actions.AddChild(new Label { Text = $"판매  {Hint(GameRules.SellTraining)}" });
        if (s.Inventory.Count == 0)
            _actions.AddChild(new Label { Text = "  팔 물건이 없습니다.", Modulate = new Color(1, 1, 1, 0.6f) });
        var sell = new HFlowContainer();
        _actions.AddChild(sell);
        for (var i = 0; i < s.Inventory.Count; i++)
        {
            var index = i;
            var crafted = s.Inventory[i];
            AddButton(sell, $"{_data.Items[crafted.ItemId].Name} {crafted.Quality:P0} → {_game.SellPrice(crafted)}G", () => Do(() => _game.Sell(index)))
                .Disabled = s.ActionsLeft <= 0;
        }

        var end = Row(_actions, "");
        AddButton(end, "하루 마치기", () => _onEndDay());
        AddButton(end, "타이틀로", () => _onTitle());

        Clear(_inventory);
        for (var i = 0; i < s.Inventory.Count; i++)
        {
            var index = i;
            var crafted = s.Inventory[i];
            var row = Row(_inventory, $"{_data.Items[crafted.ItemId].Name} (품질 {crafted.Quality:P0})");
            if (_game.Settings.Invasions && _game.DefenseValue(crafted) > 0)
                AddButton(row, $"납품 +{_game.DefenseValue(crafted):0.#}", () => Do(() => _game.Deliver(index)));
        }

        _log.Clear();
        foreach (var line in s.Log.TakeLast(30)) _log.AppendText(line + "\n");
    }

    /// <summary>제작 미니게임을 띄운다. 재료와 행동은 미니게임을 끝냈을 때만 쓴다.</summary>
    private void OpenForging(ItemDef item)
    {
        GetViewport().GuiReleaseFocus();
        AddChild(ForgingPanel.Create(
            item.Name,
            _game.State.Stats[StatType.Strength],
            quality => Do(() => _game.Craft(item.Id, quality)),
            () => { }));
    }

    /// <summary>아침 소식. 지금은 침공 예고만 있고, 의뢰나 시세 같은 사건은 나중에 붙인다.</summary>
    private string MorningNews()
    {
        if (!_game.Settings.Invasions) return $"{_game.State.Day}일째 아침. 마을은 평화롭습니다.";
        return _game.DaysUntilInvasion switch
        {
            1 => "오늘 밤 마왕군이 쳐들어옵니다! 납품을 서두르세요.",
            <= 3 => $"마왕군이 다가오고 있다는 소문이 돕니다. ({_game.DaysUntilInvasion}일 뒤 침공)",
            _ => $"{_game.State.Day}일째 아침. 다음 침공까지 {_game.DaysUntilInvasion}일 남았습니다.",
        };
    }

    /// <summary>행동이 능력치를 어떻게 바꾸는지 짧게 적는다. 예: 힘+10 섬세함-7</summary>
    private static string Hint(StatGain training) =>
        $"{UiText.StatNames[training.Stat]}+{training.Gain:0.#} {UiText.StatNames[StatPairs.Opposite(training.Stat)]}-{training.Loss:0.#}";

    private static HBoxContainer Row(Container parent, string title)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = title, CustomMinimumSize = new Vector2(300, 0) });
        parent.AddChild(row);
        return row;
    }

    private static Button AddButton(Container parent, string text, Action onPressed)
    {
        var button = new Button { Text = text };
        button.Pressed += onPressed;
        parent.AddChild(button);
        return button;
    }

    private static void Clear(Node node)
    {
        foreach (var child in node.GetChildren()) child.QueueFree();
    }
}
