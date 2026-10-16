namespace Blacksmith.Core;

/// <summary>밸런스 숫자. 프로토타입 출발점이며 플레이하면서 조절한다.</summary>
public static class GameRules
{
    public const int ActionsPerDay = 4;
    public const double DecayRatio = 0.6;
    public const double TrainPerAction = 1.0;
    public const double StatMax = 100;
    public const double FameMax = 100;

    public const int StartingGold = 50;
    /// <summary>시작 재료. 첫날 바로 단검 하나는 만들어볼 수 있게 한다.</summary>
    public static readonly IReadOnlyDictionary<string, int> StartingMaterials = new Dictionary<string, int>
    {
        ["iron_ore"] = 2, ["coal"] = 2, ["wood"] = 2,
    };
    public const int InvasionInterval = 7;
    public const int VictoryDay = 30;

    public const double StartingThreat = 10;
    public const double ThreatPerDay = 3;
    /// <summary>침공을 막으면 위협이 이만큼 비율로 줄어든다.</summary>
    public const double ThreatReliefOnDefense = 0.5;

    /// <summary>같은 품목을 이만큼 직접 만들면 미니게임을 건너뛸 수 있다.</summary>
    public const int AutoCraftAfter = 3;
    /// <summary>자동 제작 품질 = 최고 기록 × 이 비율.</summary>
    public const double AutoCraftRatio = 0.8;

    /// <summary>제작 품질(0~1)이 가격에 주는 배율. 품질 0이면 0.5배, 1이면 2배.</summary>
    public static double QualityPriceMultiplier(double quality) => 0.5 + 1.5 * quality;
}
