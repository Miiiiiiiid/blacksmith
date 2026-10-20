namespace Blacksmith.Core.Society;

/// <summary>
/// 기여 환산표. 주인공의 행동 하나가 사회에 얼마나 닿는지 정한다.
/// 기준은 평범한 대표자(능력 10)가 한 턴에 쌓는 양, 즉 0.1 × 규모다.
/// 밸런스를 맞출 때 도구가 바꿔가며 돌려볼 수 있게 상수가 아닌 속성으로 둔다.
/// </summary>
public static class ContributionRules
{
    /// <summary>의뢰 하나를 납품하면 그 분야 수치가 바로 이만큼 × 규모 × 품질 오른다. 수치처럼 조금씩 닳는다.</summary>
    public static double RequestYield { get; set; } = 0.1;

    /// <summary>대표자 맞춤 장비 하나가 대표자 능력을 이만큼 × 품질 올린다. 대표자가 은퇴하면 함께 사라진다.</summary>
    public static double GearAbility { get; set; } = 0.025;

    /// <summary>겸직: 대장간 대신 대표자 일을 거들면 그 분야 수치가 이만큼 × 규모 × 품질 오른다.</summary>
    public static double ServeYield { get; set; } = 0.06;

    /// <summary>역작 공정 하나의 위력. 위력이 높을수록 공정이 어려워져 덜 오른다.</summary>
    public static double MasterworkGain { get; set; } = 1.6;
    public static double MasterworkSoftCap { get; set; } = 2000;

    /// <summary>마왕 연구 하나의 성과. 성과가 쌓일수록 약점이 밝혀져 마왕에게 주는 피해가 늘어난다.</summary>
    public static double ResearchGain { get; set; } = 1;
    /// <summary>연구 성과가 이만큼이면 최대 보너스의 절반을 얻는다.</summary>
    public static double ResearchHalf { get; set; } = 100;
    public static double ResearchMaxBonus { get; set; } = 0.5;

    /// <summary>주인공의 솜씨. 행동마다 조금씩 늘고, 품질에 곱해진다.</summary>
    public static double StartingSkill { get; set; } = 10;
    public static double SkillPerAction { get; set; } = 0.05;
    /// <summary>수비에 성공해 다음 세대가 이을 때 남는 솜씨의 비율.</summary>
    public static double SkillKeptOnSuccession { get; set; } = 0.7;
    /// <summary>멸망해도 살아남은 이들이 기억하는 연구 성과의 비율.</summary>
    public static double ResearchKeptOnCollapse { get; set; } = 0.5;

    /// <summary>멸망한 뒤 폐허에서 역작(유물)을 되찾을 확률. 되찾으면 위력이 그대로 다음 대장장이에게 간다.</summary>
    public static double RelicRecoverChance { get; set; } = 0.5;

    /// <summary>선제 일격은 역작의 위력이 마왕 체력의 이 배율 이상일 때만 쓴다(봇 기준).</summary>
    public static double FirstStrikeMargin { get; set; } = 1.15;
}
