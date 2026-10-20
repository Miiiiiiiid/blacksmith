namespace Blacksmith.Core.Society;

/// <summary>사회 수치. 인구 단계가 오를수록 하나씩 열린다.</summary>
public enum SocietyStat { Food, Combat, Tech, Economy, Stability, Culture }

/// <summary>인구로 정해지는 사회 단계.</summary>
public enum Stage { Remnant, Town, City, Nation }

public enum InvasionOutcome { Victory, DefendedButLost, Damaged, Destroyed }

/// <summary>사회 수치 하나를 맡은 대표자.</summary>
public sealed class Person
{
    public string Name { get; set; } = "";
    public SocietyStat Role { get; set; }
    public double Ability { get; set; }
    /// <summary>한 해에 능력이 자라는 정도. 사람마다 다르다.</summary>
    public double Growth { get; set; }
    public double Age { get; set; }
}

public sealed record SocietyEvent(int Generation, int Turn, string Text);

/// <summary>한 세대가 끝날 때의 요약. 비교와 보고용.</summary>
public sealed record GenerationSummary(
    int Generation, int Population, Stage Stage, double Military, double Enemy,
    InvasionOutcome Outcome, int PopulationAfter, IReadOnlyDictionary<SocietyStat, double> Stats);

public sealed class SocietyState
{
    public int Seed { get; set; }
    public int Generation { get; set; } = 1;
    public int Turn { get; set; }
    public double Population { get; set; } = SocietyRules.StartingPopulation;
    public double FoodStock { get; set; }
    public double Enemy { get; set; } = SocietyRules.BaseEnemyStrength;
    public double Wanderers { get; set; } = SocietyRules.StartingWanderers;
    public Dictionary<SocietyStat, double> Stats { get; set; } = new();
    public Dictionary<SocietyStat, Person> Leaders { get; set; } = new();
    public int Collapses { get; set; }
    /// <summary>최근 몰락의 여파. 몰락하면 차오르고 세월이 지나며 빠진다. 난세에 인물이 난다.</summary>
    public double Hardship { get; set; }
    public Stage HighestStage { get; set; }
    public bool Won { get; set; }
    public List<SocietyEvent> Chronicle { get; set; } = new();
    public List<GenerationSummary> History { get; set; } = new();
}
