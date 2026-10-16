namespace Blacksmith.Core;

public enum Difficulty { Peaceful, Easy, Normal, Hard }

/// <summary>난이도별 마왕군 숫자. 평화는 침공이 없다.</summary>
public sealed record DifficultySettings(string Name, string Description, bool Invasions, double StartingThreat, double ThreatPerDay)
{
    public static DifficultySettings For(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Peaceful => new("평화", "마왕군이 오지 않습니다. 대장간 운영만 즐깁니다.", false, 0, 0),
        Difficulty.Easy => new("쉬움", "마왕군이 약하게 시작하고 천천히 강해집니다.", true, 6, 2),
        Difficulty.Normal => new("보통", "기본 난이도입니다.", true, GameRules.StartingThreat, GameRules.ThreatPerDay),
        Difficulty.Hard => new("어려움", "마왕군이 강하게 시작하고 빠르게 강해집니다.", true, 14, 4),
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty)),
    };
}
