using System;
using System.Linq;
using Blacksmith.Core;
using Godot;

namespace Blacksmith;

/// <summary>
/// 게임 흐름을 잇는다. 타이틀 → 난이도 → 인트로 → 하루 루프(행동 → 저녁 정산) → 엔딩·연대기 → 타이틀.
/// 화면마다 지금 화면을 지우고 새로 만든다.
/// </summary>
public partial class Main : Control
{
    private const string SavePath = "user://save.json";

    private GameData _data = null!;
    private Game? _game;

    public override void _Ready()
    {
        RenderingServer.SetDefaultClearColor(new Color(0.11f, 0.11f, 0.12f));
        _data = GameData.FromJson(
            Godot.FileAccess.GetFileAsString("res://data/materials.json"),
            Godot.FileAccess.GetFileAsString("res://data/items.json"));
        ShowTitle();
    }

    public override void _Input(InputEvent e) => UiText.NoteInput(e);

    private Game? LoadSave()
    {
        if (!Godot.FileAccess.FileExists(SavePath)) return null;
        try
        {
            return new Game(_data, SaveSystem.Deserialize(Godot.FileAccess.GetFileAsString(SavePath)));
        }
        catch (Exception e)
        {
            GD.PushWarning($"저장 파일을 읽지 못했습니다: {e.Message}");
            return null;
        }
    }

    /// <summary>상시 저장. 행동할 때마다 덮어쓴다.</summary>
    private void Save()
    {
        if (_game == null) return;
        using var file = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Write);
        file.StoreString(SaveSystem.Serialize(_game.State));
    }

    private void Clear()
    {
        foreach (var child in GetChildren()) child.QueueFree();
    }

    private void ShowTitle()
    {
        Clear();
        var saved = LoadSave();
        var canContinue = saved?.State.Status == GameStatus.Playing;

        var box = UiText.CenteredPanel(this, 420, dim: false);
        box.AddChild(UiText.Title("대장장이 (가제)", 36));
        box.AddChild(new Label { Text = "프로토타입", HorizontalAlignment = HorizontalAlignment.Center });
        box.AddChild(new HSeparator());

        if (canContinue)
        {
            var state = saved!.State;
            var resume = UiText.Button(box, $"이어하기 ({state.Day}일째 아침)", () =>
            {
                _game = saved;
                ShowPlay();
            });
            resume.CallDeferred(Control.MethodName.GrabFocus);
        }

        // 진행 중인 게임이 있으면 실수로 지우지 않게 한 번 더 누르게 한다.
        var armed = !canContinue;
        Button newGame = null!;
        newGame = UiText.Button(box, "새 게임", () =>
        {
            if (!armed)
            {
                armed = true;
                newGame.Text = "새 게임 (한 번 더 누르면 기존 저장을 지웁니다)";
                return;
            }
            ShowDifficulty();
        });
        if (!canContinue) newGame.CallDeferred(Control.MethodName.GrabFocus);

        UiText.Button(box, "끝내기", () => GetTree().Quit());
    }

    private void ShowDifficulty()
    {
        Clear();
        var box = UiText.CenteredPanel(this, 520, dim: false);
        box.AddChild(UiText.Title("난이도", 28));
        foreach (var difficulty in Enum.GetValues<Difficulty>())
        {
            var settings = DifficultySettings.For(difficulty);
            var row = new HBoxContainer();
            var button = new Button { Text = settings.Name, CustomMinimumSize = new Vector2(120, 0) };
            button.Pressed += () => ShowIntro(difficulty);
            row.AddChild(button);
            row.AddChild(new Label { Text = settings.Description, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill });
            box.AddChild(row);
            if (difficulty == Difficulty.Normal) button.CallDeferred(Control.MethodName.GrabFocus);
        }
        UiText.Button(box, "돌아가기", ShowTitle);
    }

    private void ShowIntro(Difficulty difficulty)
    {
        _game = Game.NewGame(_data, (int)GD.Randi(), difficulty);
        Save();

        Clear();
        var box = UiText.CenteredPanel(this, 600, dim: false);
        box.AddChild(UiText.Title("변두리의 작은 대장간", 26));
        var story = _game.Settings.Invasions
            ? "북쪽에서 마왕군이 내려온다는 소식이 돈 지 한 달. 당신은 마을 변두리에 작은 대장간을 열었습니다.\n\n" +
              $"마왕군은 {GameRules.InvasionInterval}일마다 마을을 덮칩니다. 무기를 만들어 왕국군에 납품하면 마을을 지킬 수 있고, " +
              "팔면 돈이 됩니다. 무엇을 만들고 무엇을 익힐지에 따라 당신이 걷는 길이 달라집니다.\n\n" +
              $"{GameRules.VictoryDay}일을 버텨내세요."
            : "전쟁 소문도 없는 조용한 마을. 당신은 마을 변두리에 작은 대장간을 열었습니다.\n\n" +
              "무엇을 만들고 무엇을 익힐지에 따라 당신이 걷는 길이 달라집니다.\n\n" +
              $"{GameRules.VictoryDay}일 동안 대장간을 꾸려보세요.";
        box.AddChild(new Label { Text = story, AutowrapMode = TextServer.AutowrapMode.WordSmart });
        UiText.Button(box, "첫날 아침", ShowPlay).CallDeferred(Control.MethodName.GrabFocus);
    }

    private void ShowPlay()
    {
        Clear();
        AddChild(PlayView.Create(_game!, Save, EndDay, ShowTitle));
    }

    /// <summary>하루를 마치고 저녁 정산을 띄운다. 정산을 닫으면 다음 날 아침이나 엔딩으로 간다.</summary>
    private void EndDay()
    {
        var game = _game!;
        game.EndDay();
        Save();
        AddChild(EveningPanel.Create(game.State.LastReport!, game.State.Gold, () =>
        {
            if (game.State.Status == GameStatus.Playing) ShowPlay();
            else ShowEnding();
        }));
    }

    private void ShowEnding()
    {
        Clear();
        var state = _game!.State;
        var won = state.Status == GameStatus.Victory;

        var box = UiText.CenteredPanel(this, 720, dim: false);
        var title = UiText.Title(won ? "버텨냈다" : "게임오버", 32);
        title.AddThemeColorOverride("font_color", won ? new Color(0.95f, 0.8f, 0.45f) : new Color(0.9f, 0.4f, 0.4f));
        box.AddChild(title);

        var days = won ? GameRules.VictoryDay : state.Day;
        var top = state.Stats.Values.OrderByDescending(kv => kv.Value).First();
        box.AddChild(new Label
        {
            Text = $"{days}일 · {state.Gold} 골드 · 만든 물건 {state.TotalCrafted}개 · 명성 {UiText.Signed(state.Stats.Fame)}\n" +
                   "능력치 " + string.Join(", ", state.Stats.Values.Select(kv => $"{UiText.StatNames[kv.Key]} {kv.Value:0.#}")) + "\n" +
                   $"가장 갈고닦은 것: {UiText.StatNames[top.Key]}",
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        box.AddChild(new HSeparator());
        box.AddChild(new Label { Text = "연대기" });
        var chronicle = new RichTextLabel { BbcodeEnabled = true, CustomMinimumSize = new Vector2(0, 280) };
        foreach (var entry in state.Chronicle)
            chronicle.AppendText($"[color=#aaa]{entry.Day}일[/color]  {entry.Text}\n");
        box.AddChild(chronicle);

        UiText.Button(box, "타이틀로", ShowTitle).CallDeferred(Control.MethodName.GrabFocus);
    }
}
