namespace Blacksmith.Core.Society;

/// <summary>
/// 주인공 없이 사회만 굴러가는 시뮬레이션. 한 턴은 상순·중순·하순 중 하나이고, 900턴이 끝나면 마왕군이 쳐들어온다.
/// 같은 시드면 같은 역사가 나온다.
/// </summary>
public sealed class SocietySimulation
{
    private static readonly SocietyStat[] RemnantStats = { SocietyStat.Food, SocietyStat.Combat };
    private static readonly SocietyStat[] TownStats = { SocietyStat.Tech, SocietyStat.Economy };
    private static readonly SocietyStat[] CityStats = { SocietyStat.Stability, SocietyStat.Culture };

    private readonly Random _random;

    public SocietyState State { get; }

    public SocietySimulation(int seed)
    {
        _random = new Random(seed);
        State = new SocietyState { Seed = seed };
        StartAsRemnant();
        Log("잔존세력 30명이 폐허에서 다시 모였다.");
    }

    public static Stage StageOf(double population) =>
        population >= SocietyRules.NationPopulation ? Stage.Nation
        : population >= SocietyRules.CityPopulation ? Stage.City
        : population >= SocietyRules.TownPopulation ? Stage.Town
        : Stage.Remnant;

    public static IEnumerable<SocietyStat> UnlockedStats(Stage stage)
    {
        foreach (var stat in RemnantStats) yield return stat;
        if (stage >= Stage.Town) foreach (var stat in TownStats) yield return stat;
        if (stage >= Stage.City) foreach (var stat in CityStats) yield return stat;
    }

    public static string Title(SocietyStat stat, Stage stage) => stat switch
    {
        SocietyStat.Food => stage switch { Stage.Remnant => "농사꾼", Stage.Nation => "농업장관", _ => "식량 담당" },
        SocietyStat.Combat => stage switch { Stage.Remnant => "힘센 사람", Stage.Nation => "군대장", _ => "방위대장" },
        SocietyStat.Tech => stage switch { Stage.Nation => "기술장관", Stage.City => "기술 담당", _ => "장인" },
        SocietyStat.Economy => stage switch { Stage.Nation => "재무장관", Stage.City => "재무 담당", _ => "행상" },
        SocietyStat.Stability => stage == Stage.Nation ? "군주" : "지도자",
        SocietyStat.Culture => stage == Stage.Nation ? "문화장관" : "이야기꾼",
        _ => stat.ToString(),
    };

    public static string StatName(SocietyStat stat, Stage stage) => stat switch
    {
        SocietyStat.Food => "식량",
        SocietyStat.Combat => stage == Stage.Nation ? "군사력" : "전투력",
        SocietyStat.Tech => "기술",
        SocietyStat.Economy => "경제",
        SocietyStat.Stability => "정치안정도",
        SocietyStat.Culture => "문화",
        _ => stat.ToString(),
    };

    public Stage CurrentStage => StageOf(State.Population);

    public double Stat(SocietyStat stat) => State.Stats.GetValueOrDefault(stat);

    /// <summary>식량 비축이 넉넉한 정도. 0이면 굶주림, 1이면 넉넉함.</summary>
    public double FoodSecurity =>
        Math.Clamp(State.FoodStock / (State.Population * SocietyRules.FoodPerPerson * SocietyRules.FoodReserveTurns), 0, 1.5);

    /// <summary>침공을 막는 힘. 2천 명이 넘으면 군대가 생겨 1.5배가 된다.</summary>
    public double Military =>
        Stat(SocietyStat.Combat)
        * (1 + Stat(SocietyStat.Tech) / 100)
        * (1 + Stat(SocietyStat.Economy) / 400)
        * (CurrentStage == Stage.Nation ? 1.5 : 1);

    /// <summary>승리하거나 세대 상한에 닿을 때까지 돌린다.</summary>
    public void Run(int maxGenerations)
    {
        while (!State.Won && State.Generation <= maxGenerations) RunGeneration();
    }

    public void RunGeneration()
    {
        for (State.Turn = 0; State.Turn < SocietyRules.TurnsPerGeneration; State.Turn++) Step();
        Invade();
    }

    public void Step()
    {
        var stage = CurrentStage;
        if (stage > State.HighestStage)
        {
            State.HighestStage = stage;
            Log(stage switch
            {
                Stage.Town => "인구가 500명을 넘었다. 화폐가 생기고 기술과 경제를 맡을 사람이 나섰다.",
                Stage.City => "인구가 1000명을 넘었다. 지도자를 세웠다.",
                _ => "인구가 2000명을 넘었다. 군대가 생겼다.",
            });
        }

        foreach (var stat in UnlockedStats(stage))
        {
            if (!State.Leaders.ContainsKey(stat)) Appoint(stat, stage);
            Accumulate(stat, State.Leaders[stat].Ability);
        }

        Feed();
        Grow();
        Absorb();
        AgeLeaders(stage);
        State.Hardship *= 1 - 1.0 / (SocietyRules.TurnsPerYear * 10);
    }

    private double Scale(SocietyStat stat)
    {
        var pop = State.Population;
        return stat switch
        {
            SocietyStat.Combat => Math.Pow(pop / 100, 0.7),
            SocietyStat.Tech => Math.Sqrt(pop / 500),
            SocietyStat.Economy => Math.Pow(pop / 500, 0.7),
            SocietyStat.Culture => Math.Sqrt(pop / 1000),
            _ => 1,
        };
    }

    /// <summary>수치는 대표자 능력만큼 쌓이고 조금씩 닳는다. 그래서 대표자 능력 × 규모 쪽으로 다가간다.</summary>
    private void Accumulate(SocietyStat stat, double ability)
    {
        var value = Stat(stat);
        value += ability * SocietyRules.StatGainRate * Scale(stat) - value * SocietyRules.StatDecay;
        State.Stats[stat] = value;
    }

    private void Feed()
    {
        var yield = SocietyRules.BaseFoodYield + Stat(SocietyStat.Food) / 200 * (1 + Stat(SocietyStat.Tech) / 100);
        State.FoodStock += State.Population * SocietyRules.FoodPerPerson * (yield - 1);
        State.FoodStock *= 1 - SocietyRules.FoodSpoilage;
        if (State.FoodStock < 0) State.FoodStock = 0;
    }

    /// <summary>땅이 먹여 살릴 수 있는 인구.</summary>
    public double Capacity =>
        SocietyRules.BaseCapacity * (1 + Stat(SocietyStat.Tech) / 40) * (1 + Stat(SocietyStat.Food) / 100);

    private void Grow()
    {
        var security = FoodSecurity;
        var births = State.Population * SocietyRules.BaseBirthRate * security
                     * (1 + Stat(SocietyStat.Stability) / SocietyRules.StabilityGrowthScale)
                     * Math.Max(0, 1 - State.Population / Capacity);
        var deathRate = SocietyRules.BaseDeathRate + (security <= 0 ? SocietyRules.StarvationDeathRate : 0);
        State.Population += births - State.Population * deathRate;
    }

    /// <summary>주변을 떠도는 무리를 받아들인다. 떠도는 사람이 많고, 지켜줄 힘과 먹일 식량이 있을수록 잘 모인다.</summary>
    private void Absorb()
    {
        State.Wanderers = Math.Min(SocietyRules.MaxWanderers, State.Wanderers + SocietyRules.WanderersPerYear / SocietyRules.TurnsPerYear);
        var chance = SocietyRules.AbsorbChance * (0.5 + FoodSecurity)
                     * (1 + Stat(SocietyStat.Combat) / 100 + Stat(SocietyStat.Culture) / 50 + Stat(SocietyStat.Economy) / 100)
                     * State.Wanderers / SocietyRules.StartingWanderers;
        if (_random.NextDouble() >= chance) return;

        var size = Math.Min(State.Wanderers, _random.Next(SocietyRules.AbsorbMin, SocietyRules.AbsorbMax + 1) * (1 + (int)CurrentStage));
        State.Wanderers -= size;
        State.Population += size;
        if (size >= 100) Log($"떠돌던 무리 {size:0}명을 받아들였다.");
    }

    private void AgeLeaders(Stage stage)
    {
        foreach (var (stat, person) in State.Leaders.ToList())
        {
            person.Age += 1.0 / SocietyRules.TurnsPerYear;
            person.Ability += person.Growth / SocietyRules.TurnsPerYear;
            if (person.Age < SocietyRules.RetireAge) continue;
            if (_random.NextDouble() < 0.005 * (person.Age - SocietyRules.RetireAge + 1))
            {
                State.Leaders.Remove(stat);
                Appoint(stat, stage);
            }
        }
    }

    /// <summary>
    /// 대표자는 무작위로 나오지만, 문화와 안정이 높거나 막 몰락을 겪은 난세일수록 뛰어난 사람이 나올 확률이 높다.
    /// </summary>
    private void Appoint(SocietyStat stat, Stage stage)
    {
        var luck = Math.Exp(Gaussian() * SocietyRules.AbilitySpread);
        var climate = 1 + Stat(SocietyStat.Culture) / 200 + Stat(SocietyStat.Stability) / 400 + State.Hardship * 0.5;
        var person = new Person
        {
            Name = Names.Next(_random),
            Role = stat,
            Ability = SocietyRules.MeanAbility * luck * climate,
            Growth = SocietyRules.YearlyAbilityGrowth * (0.5 + _random.NextDouble()),
            Age = 20 + _random.Next(21),
        };
        State.Leaders[stat] = person;
        if (person.Ability >= SocietyRules.NotableAbility)
            Log($"이름난 인물 {person.Name}이(가) {Title(stat, stage)}이(가) 되었다. (능력 {person.Ability:0})");
    }

    private void Invade()
    {
        var enemy = State.Enemy;
        var military = Military * (0.85 + 0.3 * _random.NextDouble());
        var before = State.Population;
        var snapshot = new Dictionary<SocietyStat, double>(State.Stats);
        var stage = CurrentStage;
        InvasionOutcome outcome;

        if (military >= enemy)
        {
            var survivors = military - enemy * 0.5;
            var reserve = enemy * SocietyRules.FinalBattleReserve * (0.8 + 0.4 * _random.NextDouble());
            if (_random.NextDouble() < survivors / (survivors + reserve))
            {
                outcome = InvasionOutcome.Victory;
                State.Won = true;
                Log($"마왕군 {enemy:0}을(를) 막고 쫓아가 최후의 결전에서 이겼다. 마왕이 쓰러졌다.");
            }
            else
            {
                outcome = InvasionOutcome.DefendedButLost;
                State.Population *= 0.9;
                State.Stats[SocietyStat.Combat] = Stat(SocietyStat.Combat) * 0.7;
                Log($"마왕군 {enemy:0}을(를) 막아냈지만 최후의 결전에서 졌다. 마왕은 물러나 힘을 기른다.");
            }
        }
        else
        {
            var remaining = enemy - military;
            var ratio = remaining / enemy;
            var lost = Math.Min(State.Population, remaining * SocietyRules.PopulationLossPerEnemyPower);
            State.Population -= lost;
            State.Wanderers += lost * SocietyRules.ScatteredRatio;
            if (State.Population < SocietyRules.CollapsePopulation)
            {
                outcome = InvasionOutcome.Destroyed;
                State.Collapses++;
                Log($"마왕군 {enemy:0}에 군사력 {military:0}으로 맞섰다가 완전히 무너졌다. 살아남은 이들이 피난을 떠났다.");
                StartAsRemnant();
            }
            else
            {
                outcome = InvasionOutcome.Damaged;
                foreach (var stat in State.Stats.Keys.ToList()) State.Stats[stat] *= 1 - ratio;
                State.FoodStock *= 1 - ratio;
                foreach (var stat in State.Leaders.Keys.ToList())
                    if (_random.NextDouble() < ratio) State.Leaders.Remove(stat);
                State.Hardship = Math.Max(State.Hardship, 0.5);
                Log($"마왕군 {enemy:0}을(를) 막지 못했다. 인구 {before:0}명 중 {before - State.Population:0}명을 잃었지만 터전은 지켰다.");
            }
        }

        State.Enemy *= outcome switch
        {
            InvasionOutcome.DefendedButLost => SocietyRules.EnemyAfterRepelled,
            InvasionOutcome.Damaged => SocietyRules.EnemyAfterDamaged,
            InvasionOutcome.Destroyed => SocietyRules.EnemyAfterDestroyed,
            _ => 1,
        };
        State.History.Add(new GenerationSummary(
            State.Generation, (int)before, stage, military, enemy, outcome, (int)State.Population, snapshot));
        State.Generation++;
    }

    private void StartAsRemnant()
    {
        State.Population = SocietyRules.StartingPopulation;
        State.FoodStock = State.Population * SocietyRules.FoodReserveTurns;
        State.Stats.Clear();
        State.Leaders.Clear();
        State.HighestStage = Stage.Remnant;
        State.Hardship = 1;
    }

    private void Log(string text) => State.Chronicle.Add(new SocietyEvent(State.Generation, State.Turn, text));

    private double Gaussian()
    {
        var u1 = 1 - _random.NextDouble();
        var u2 = _random.NextDouble();
        return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
    }
}
