using System;
using System.Linq;
using Blacksmith.Core;
using Godot;

namespace Blacksmith;

/// <summary>저녁 정산 화면. 오늘 바뀐 것과, 침공일이면 그 결과를 보여준다.</summary>
public partial class EveningPanel : Control
{
    private DayReport _report = null!;
    private int _gold;
    private Action _onClose = () => { };

    public static EveningPanel Create(DayReport report, int goldNow, Action onClose) =>
        new() { _report = report, _gold = goldNow, _onClose = onClose };

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        var box = UiText.CenteredPanel(this, 520, dim: true);

        box.AddChild(UiText.Title($"{_report.Day}일째 저녁", 24));

        var lines = new System.Collections.Generic.List<string>
        {
            $"골드 {UiText.Signed(_report.GoldChange)} (지금 {_gold}G)",
            $"만든 물건 {_report.Crafted}개",
        };
        if (_report.StatChanges.Count > 0)
            lines.Add("능력치 " + string.Join(", ", _report.StatChanges
                .OrderByDescending(c => c.Value)
                .Select(c => $"{UiText.StatNames[c.Key]} {UiText.Signed(c.Value)}")));
        if (Math.Abs(_report.FameChange) > 1e-9)
            lines.Add($"명성 {UiText.Signed(_report.FameChange)}");
        box.AddChild(new Label { Text = string.Join("\n", lines) });

        if (_report.Invasion is { } invasion)
        {
            box.AddChild(new HSeparator());
            var result = UiText.Title(invasion.Defended ? "마왕군의 침공을 막아냈다!" : "마을이 함락되었다", 22);
            result.AddThemeColorOverride("font_color", invasion.Defended ? new Color(0.5f, 0.85f, 0.5f) : new Color(0.9f, 0.4f, 0.4f));
            box.AddChild(result);
            box.AddChild(new Label
            {
                Text = $"방어 확률 {invasion.Chance:P0}",
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }

        var ended = _report.Invasion is { Defended: false } || _report.Day >= GameRules.VictoryDay;
        var button = UiText.Button(box, ended ? "결말 보기" : "잠자리에 든다", Close);
        button.CustomMinimumSize = new Vector2(0, 48);
        button.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void Close()
    {
        _onClose();
        QueueFree();
    }
}
