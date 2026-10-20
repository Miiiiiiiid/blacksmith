namespace Blacksmith.Core.Society;

/// <summary>사회 시뮬레이션의 밸런스 숫자. 모두 여기서 조절한다.</summary>
public static class SocietyRules
{
    // 시간: 한 달을 상순·중순·하순으로 나눠 25년 = 900턴이 한 세대.
    public const int TurnsPerMonth = 3;
    public const int TurnsPerYear = 12 * TurnsPerMonth;
    public const int YearsPerGeneration = 25;
    public const int TurnsPerGeneration = YearsPerGeneration * TurnsPerYear;

    // 인구 단계. 넘으면 새 사회 수치가 열린다.
    public const int StartingPopulation = 30;
    public const int TownPopulation = 500;
    public const int CityPopulation = 1000;
    public const int NationPopulation = 2000;
    /// <summary>이 밑으로 떨어지면 완전히 무너진 것으로 보고 잔존세력으로 리셋한다.</summary>
    public const int CollapsePopulation = 30;

    // 식량. 한 사람이 한 턴에 1을 먹는다.
    public const double FoodPerPerson = 1.0;
    public const double BaseFoodYield = 0.93;
    public const double FoodSpoilage = 0.02;
    /// <summary>비축이 이 턴 수만큼의 식량이면 식량 사정이 넉넉함(1)이다.</summary>
    public const double FoodReserveTurns = 6;

    // 인구 증가. 식량 사정과 정치안정도가 출생률을 올린다.
    public const double BaseBirthRate = 0.0010;
    public const double BaseDeathRate = 0.0006;
    public const double StarvationDeathRate = 0.01;
    public const double StabilityGrowthScale = 40;
    /// <summary>땅이 먹여 살릴 수 있는 인구. 기술과 식량 수치가 늘려준다. 가까워질수록 출생이 줄어든다.</summary>
    public const double BaseCapacity = 2500;

    // 다른 잔존세력 흡수.
    public const double AbsorbChance = 0.03;
    public const int AbsorbMin = 5;
    public const int AbsorbMax = 40;
    /// <summary>주변을 떠도는 사람의 수. 흡수하면 줄고, 해마다 조금씩 차오른다.</summary>
    public const double StartingWanderers = 1200;
    public const double WanderersPerYear = 30;
    public const double MaxWanderers = 2000;
    /// <summary>침공으로 잃은 인구 중 죽지 않고 흩어져 떠도는 비율. 나중에 다시 흡수할 수 있다.</summary>
    public const double ScatteredRatio = 0.3;

    // 사회 수치는 대표자의 능력만큼 쌓이고 조금씩 닳는다. 대표자 능력에 상한이 없어서 수치도 무한정 자란다.
    public const double StatGainRate = 0.01;
    public const double StatDecay = 0.005;

    // 대표자.
    public const double MeanAbility = 10;
    public const double AbilitySpread = 0.35;
    /// <summary>이 능력 이상이면 이름난 인물로 연대기에 남긴다.</summary>
    public const double NotableAbility = 20;
    public const double YearlyAbilityGrowth = 1.0;
    public const int RetireAge = 60;

    // 마왕. 25년마다 깨어나 먹이를 찾아 내려오는 단일 개체.
    public const double BossHealth = 3500;
    /// <summary>뚫렸을 때 마왕의 남은 체력 1당 잃는 인구. 마왕은 먹은 만큼 체력이 는다.</summary>
    public const double PopulationLossPerBossHealth = 3500;
}
