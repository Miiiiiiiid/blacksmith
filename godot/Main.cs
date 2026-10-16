using System;
using System.Linq;
using Blacksmith.Core;
using Godot;

namespace Blacksmith;

/// <summary>
/// 프로토타입 화면. 게임 규칙은 전부 Core에 있고, 여기서는 보여주고 입력만 받는다.
/// 버튼만 있는 임시 UI이며 도트 아트는 나중에 입힌다.
/// </summary>
public partial class Main : Control
{
    private const string SavePath = "user://save.json";

    private GameData _data = null!;
    private Game _game = null!;

    private Label _status = null!;
    private Label _stats = null!;
    private VBoxContainer _actions = null!;
    private VBoxContainer _inventory = null!;
    private RichTextLabel _log = null!;

    public override void _Ready()
    {
        _data = GameData.FromJson(
            Godot.FileAccess.GetFileAsString("res://data/materials.json"),
            Godot.FileAccess.GetFileAsString("res://data/items.json"));

        BuildLayout();
        if (!TryLoad()) StartNewGame();
        Refresh();
    }

    private void StartNewGame()
    {
        _game = Game.NewGame(_data, (int)GD.Randi());
        Save();
    }

    private bool TryLoad()
    {
        if (!Godot.FileAccess.FileExists(SavePath)) return false;
        try
        {
            _game = new Game(_data, SaveSystem.Deserialize(Godot.FileAccess.GetFileAsString(SavePath)));
            return true;
        }
        catch (Exception e)
        {
            GD.PushWarning($"저장 파일을 읽지 못해 새 게임을 시작합니다: {e.Message}");
            return false;
        }
    }

    /// <summary>상시 저장. 행동할 때마다 덮어쓴다.</summary>
    private void Save()
    {
        using var file = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Write);
        file.StoreString(SaveSystem.Serialize(_game.State));
    }

    private void Do(Func<ActionResult> action)
    {
        var result = action();
        if (!result.Ok) _log.AppendText($"[color=#d66]{result.Message}[/color]\n");
        Save();
        Refresh();
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
        _status.Text =
            $"{s.Day}일째 · 남은 행동 {s.ActionsLeft} · {s.Gold} 골드\n" +
            $"마왕군 위협 {s.Threat:0} · 납품 방어력 {s.DeliveredDefense:0.#} · " +
            $"다음 침공까지 {_game.DaysUntilInvasion}일 (방어 확률 {_game.DefenseChance:P0})\n" +
            $"재료: {materials}";

        var st = s.Stats;
        _stats.Text =
            $"힘 {st[StatType.Strength]:0.0} ↔ 섬세함 {st[StatType.Finesse]:0.0}\n" +
            $"화술 {st[StatType.Eloquence]:0.0} ↔ 패기 {st[StatType.Boldness]:0.0}\n" +
            $"무학 {st[StatType.Martial]:0.0} ↔ 학문 {st[StatType.Scholarship]:0.0}\n" +
            $"명성 {st.Fame:+0;-0;0}";

        Clear(_actions);
        if (s.Status != GameStatus.Playing)
        {
            _actions.AddChild(new Label { Text = s.Status == GameStatus.Victory ? "승리!" : "게임오버" });
            AddButton(_actions, "새 게임", () => { StartNewGame(); _log.Clear(); Refresh(); });
        }
        else
        {
            var gather = Row(_actions, "채집");
            foreach (var m in _data.Materials.Values.Where(m => m.GatherAmount > 0))
                AddButton(gather, $"{m.Name} +{m.GatherAmount}", () => Do(() => _game.Gather(m.Id)));

            var buy = Row(_actions, "구매");
            foreach (var m in _data.Materials.Values)
                AddButton(buy, $"{m.Name} ({_game.BuyCost(m, 1)}G)", () => Do(() => _game.Buy(m.Id, 1)));

            var craft = Row(_actions, "제작");
            foreach (var item in _data.Items.Values)
            {
                var canCraft = _game.CanCraft(item.Id) && s.ActionsLeft > 0;
                var button = AddButton(craft, item.Name, () => OpenForging(item));
                button.Disabled = !canCraft;
                button.TooltipText = string.Join(", ", item.Materials.Select(m => $"{_data.Materials[m.Key].Name} {m.Value}"));
                if (_game.AutoCraftQuality(item.Id) is { } autoQuality)
                {
                    var auto = AddButton(craft, $"자동 {autoQuality:P0}", () => Do(() => _game.CraftAuto(item.Id)));
                    auto.Disabled = !canCraft;
                }
            }

            AddButton(_actions, "하루 마치기", () => Do(_game.EndDay));
        }

        Clear(_inventory);
        for (var i = 0; i < s.Inventory.Count; i++)
        {
            var index = i;
            var crafted = s.Inventory[i];
            var row = Row(_inventory, $"{_data.Items[crafted.ItemId].Name} (품질 {crafted.Quality:P0})");
            AddButton(row, $"판매 {_game.SellPrice(crafted)}G", () => Do(() => _game.Sell(index)));
            if (_game.DefenseValue(crafted) > 0)
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

    private static HBoxContainer Row(Container parent, string title)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = title, CustomMinimumSize = new Vector2(220, 0) });
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
