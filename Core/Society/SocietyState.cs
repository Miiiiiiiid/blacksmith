namespace Blacksmith.Core.Society;

/// <summary>사회 수치. 인구 단계가 오를수록 하나씩 열린다.</summary>
public enum SocietyStat { Food, Combat, Strike, Tech, Economy, Walls, Stability, Culture }

/// <summary>인구로 정해지는 사회 단계.</summary>
public enum Stage { Remnant, Town, City, Nation }

/// <summary>마왕이 깨어났을 때의 결과.</summary>
public enum InvasionOutcome
{
    /// <summary>막아내고, 물러나는 마왕을 공격대가 쓰러뜨렸다.</summary>
    Victory,
    /// <summary>막아냈지만 공격대가 마왕을 죽이지 못했다. 입힌 피해만큼 다음 마왕이 약해진다.</summary>
    Repelled,
    /// <summary>뚫렸지만 터전은 남았다. 마왕은 먹은 만큼 강해진다.</summary>
    Breached,
    /// <summary>완전히 무너졌다. 잔존세력으로 다시 시작한다.</summary>
    Destroyed,
}

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
    int Generation, int Population, Stage Stage, double Defense, double Strike, double Boss,
    InvasionOutcome Outcome, int PopulationAfter, double BossAfter, IReadOnlyDictionary<SocietyStat, double> Stats);

public sealed class SocietyState
{
    public int Seed { get; set; }
    public int Generation { get; set; } = 1;
    public int Turn { get; set; }
    public double Population { get; set; } = SocietyRules.StartingPopulation;
    public double FoodStock { get; set; }
    /// <summary>마왕의 체력. 25년마다 깨어난다.</summary>
    public double Boss { get; set; } = SocietyRules.BossHealth;
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
