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
    public const int InvasionInterval = 7;
    public const int VictoryDay = 30;

    public const double StartingThreat = 10;
    public const double ThreatPerDay = 3;
    /// <summary>침공을 막으면 위협이 이만큼 비율로 줄어든다.</summary>
    public const double ThreatReliefOnDefense = 0.5;

    /// <summary>제작 품질(0~1)이 가격에 주는 배율. 품질 0이면 0.5배, 1이면 2배.</summary>
    public static double QualityPriceMultiplier(double quality) => 0.5 + 1.5 * quality;
}
