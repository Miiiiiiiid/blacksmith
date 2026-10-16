namespace Blacksmith.Core;

/// <summary>여섯 능력치. 이름은 가안이며 서로 반대되는 3쌍으로 묶인다.</summary>
public enum StatType
{
    Strength,     // 힘
    Finesse,      // 섬세함
    Eloquence,    // 화술
    Boldness,     // 패기
    Martial,      // 무학
    Scholarship,  // 학문
}

public static class StatPairs
{
    public static StatType Opposite(StatType stat) => stat switch
    {
        StatType.Strength => StatType.Finesse,
        StatType.Finesse => StatType.Strength,
        StatType.Eloquence => StatType.Boldness,
        StatType.Boldness => StatType.Eloquence,
        StatType.Martial => StatType.Scholarship,
        StatType.Scholarship => StatType.Martial,
        _ => throw new ArgumentOutOfRangeException(nameof(stat)),
    };
}

/// <summary>능력치와 명성. 한쪽을 키우면 같은 쌍의 반대쪽이 퇴보한다.</summary>
public sealed class Stats
{
    public Dictionary<StatType, double> Values { get; set; } =
        Enum.GetValues<StatType>().ToDictionary(s => s, _ => 0.0);

    /// <summary>명성. +는 명예, -는 악명.</summary>
    public double Fame { get; set; }

    public double this[StatType stat] => Values[stat];

    public void Train(StatType stat, double amount, double decayRatio)
    {
        Values[stat] = Math.Min(GameRules.StatMax, Values[stat] + amount);
        var opposite = StatPairs.Opposite(stat);
        Values[opposite] = Math.Max(0, Values[opposite] - amount * decayRatio);
    }

    public void ChangeFame(double amount) =>
        Fame = Math.Clamp(Fame + amount, -GameRules.FameMax, GameRules.FameMax);
}
