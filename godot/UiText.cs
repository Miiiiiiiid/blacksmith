using System.Collections.Generic;
using Blacksmith.Core;
using Godot;

namespace Blacksmith;

/// <summary>화면 여러 곳에서 쓰는 글자와 작은 UI 도우미.</summary>
public static class UiText
{
    public static readonly Dictionary<StatType, string> StatNames = new()
    {
        [StatType.Strength] = "힘",
        [StatType.Finesse] = "섬세함",
        [StatType.Eloquence] = "화술",
        [StatType.Boldness] = "패기",
        [StatType.Martial] = "무학",
        [StatType.Scholarship] = "학문",
    };

    public static string Signed(double value) => value.ToString("+0.#;-0.#;0");

    public static Label Title(string text, int size = 28)
    {
        var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", size);
        return label;
    }

    public static Button Button(Container parent, string text, System.Action onPressed)
    {
        var button = new Button { Text = text };
        button.Pressed += onPressed;
        parent.AddChild(button);
        return button;
    }

    /// <summary>화면 가운데 놓이는 패널과 그 안의 세로 상자.</summary>
    public static VBoxContainer CenteredPanel(Control parent, float width, bool dim)
    {
        if (dim)
        {
            var backdrop = new ColorRect { Color = new Color(0, 0, 0, 0.6f) };
            parent.AddChild(backdrop);
            backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        }
        var center = new CenterContainer();
        parent.AddChild(center);
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(width, 0) };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.17f, 0.17f, 0.19f),
            BorderColor = new Color(0.35f, 0.33f, 0.3f),
            BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2,
            CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
        });
        center.AddChild(panel);
        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{side}", 24);
        panel.AddChild(margin);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 14);
        margin.AddChild(box);
        return box;
    }
}
