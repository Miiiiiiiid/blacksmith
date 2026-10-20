namespace Blacksmith.Core.Society;

/// <summary>
/// 주인공 없이 사회만 굴러가는 시뮬레이션. 한 턴은 상순·중순·하순 중 하나이고, 900턴이 끝나면 잠든 마왕이 깨어나 내려온다.
/// 같은 시드면 같은 역사가 나온다.
/// </summary>
public sealed class SocietySimulation
{
    private static readonly SocietyStat[] RemnantStats = { SocietyStat.Food, SocietyStat.Combat, SocietyStat.Strike };
    private static readonly SocietyStat[] TownStats = { SocietyStat.Tech, SocietyStat.Economy, SocietyStat.Walls };
    private static readonly SocietyStat[] CityStats = { SocietyStat.Stability, SocietyStat.Culture };

    private readonly Random _random;

    public SocietyState State { get; }

    /// <summary>주인공 대신 넣는 보정. 대장장이의 무기가 군대와 공격대를 얼마나 세게 만드는지 가늠할 때 쓴다.</summary>
    public double DefenseBonus { get; init; } = 1;
    public double StrikeBonus { get; init; } = 1;

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
        SocietyStat.Strike => stage switch { Stage.Nation => "용병단장", Stage.City => "용사 파티", _ => "용사" },
        SocietyStat.Tech => stage switch { Stage.Nation => "기술장관", Stage.City => "기술 담당", _ => "장인" },
        SocietyStat.Walls => stage switch { Stage.Nation => "축성장관", Stage.City => "축성 담당", _ => "석공" },
        SocietyStat.Economy => stage switch { Stage.Nation => "재무장관", Stage.City => "재무 담당", _ => "행상" },
        SocietyStat.Stability => stage == Stage.Nation ? "군주" : "지도자",
        SocietyStat.Culture => stage == Stage.Nation ? "문화장관" : "이야기꾼",
        _ => stat.ToString(),
    };

    public static string StatName(SocietyStat stat, Stage stage) => stat switch
    {
        SocietyStat.Food => "식량",
        SocietyStat.Combat => stage == Stage.Nation ? "군사력" : "전투력",
        SocietyStat.Strike => stage switch { Stage.Nation => "용병단", Stage.City => "용사 파티", _ => "용사" },
        SocietyStat.Tech => "기술",
        SocietyStat.Economy => "경제",
        SocietyStat.Walls => "방벽",
        SocietyStat.Stability => "정치안정도",
        SocietyStat.Culture => "문화",
        _ => stat.ToString(),
    };

    public Stage CurrentStage => StageOf(State.Population);

    public double Stat(SocietyStat stat) => State.Stats.GetValueOrDefault(stat);

    /// <summary>식량 비축이 넉넉한 정도. 0이면 굶주림, 1이면 넉넉함.</summary>
    public double FoodSecurity =>
        Math.Clamp(State.FoodStock / (State.Population * SocietyRules.FoodPerPerson * SocietyRules.FoodReserveTurns), 0, 1.5);

    /// <summary>군대의 힘. 2천 명이 넘으면 군대가 생겨 1.5배가 된다.</summary>
    public double Military =>
        Stat(SocietyStat.Combat)
        * (1 + Stat(SocietyStat.Tech) / 100)
        * (1 + Stat(SocietyStat.Economy) / 400)
        * (CurrentStage == Stage.Nation ? 1.5 : 1);

    /// <summary>수비력. 군대와 방벽이 마왕의 체력과 1:1로 맞바뀐다.</summary>
    public double Defense => (Military + Stat(SocietyStat.Walls) * (1 + Stat(SocietyStat.Tech) / 100)) * DefenseBonus;

    /// <summary>물러나는 마왕을 쫓는 공격대의 힘. 용병단은 경제가 받쳐준다.</summary>
    public double StrikePower =>
        Stat(SocietyStat.Strike)
        * (1 + Stat(SocietyStat.Tech) / 100)
        * (CurrentStage == Stage.Nation ? 1 + Stat(SocietyStat.Economy) / 400 : 1)
        * StrikeBonus;

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

    private double Scale(SocietyStat stat) => Scale(stat, State.Population);

    private static double Scale(SocietyStat stat, double pop)
    {
        return stat switch
        {
            SocietyStat.Combat => Math.Pow(pop / 100, 0.7),
            SocietyStat.Tech => Math.Sqrt(pop / 500),
            SocietyStat.Economy => Math.Pow(pop / 500, 0.7),
            SocietyStat.Walls => Math.Pow(pop / 100, 0.7) * 0.5,
            // 용사 한 명 → 용사 파티 → 용병단으로 커진다.
            SocietyStat.Strike => new[] { 1, 1.5, 2.5, 4 }[(int)StageOf(pop)],
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
        SocietyRules.BaseCapacity * (1 + Stat(SocietyStat.Tech) / 100) * (1 + Stat(SocietyStat.Food) / 100);

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
    /// 대표자는 무작위로 나오지만, 문화와 안정이 높거나, 막 몰락을 겪은 난세이거나,
    /// 멸망한 앞 사회에서 그 수치가 높았을수록 뛰어난 사람이 나올 확률이 높다.
    /// </summary>
    private void Appoint(SocietyStat stat, Stage stage)
    {
        var luck = Math.Exp(Gaussian() * SocietyRules.AbilitySpread);
        var climate = 1 + Stat(SocietyStat.Culture) / 200 + Stat(SocietyStat.Stability) / 400 + State.Hardship * 0.5
                      + State.Legacy.GetValueOrDefault(stat);
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
        var boss = State.Boss;
        var defense = Defense * Swing();
        var strike = 0.0;
        var before = State.Population;
        var snapshot = new Dictionary<SocietyStat, double>(State.Stats);
        var stage = CurrentStage;
        InvasionOutcome outcome;

        if (defense >= boss)
        {
            // 군대와 방벽은 마왕에게 준 피해만큼 깎인다.
            var spent = boss / defense;
            State.Stats[SocietyStat.Combat] = Stat(SocietyStat.Combat) * (1 - spent);
            State.Stats[SocietyStat.Walls] = Stat(SocietyStat.Walls) * (1 - spent);

            // 공격대는 원래 체력의 마왕을 쫓는다.
            strike = StrikePower * Swing();
            if (strike >= boss)
            {
                outcome = InvasionOutcome.Victory;
                State.Won = true;
                Log($"깨어난 마왕(체력 {boss:0})을 수비력 {defense:0}으로 막고, 공격대가 물러나는 마왕을 쫓아가 쓰러뜨렸다.");
            }
            else
            {
                outcome = InvasionOutcome.Repelled;
                State.Boss -= strike;
                Log($"깨어난 마왕(체력 {boss:0})을 막아냈다. 공격대가 {strike:0}의 상처를 입혔지만 놓쳤다. 마왕은 다시 잠들었다.");
            }
        }
        else
        {
            // 뚫리면 멸망. 마왕은 군대 전부와 인구 일부를 먹고 강해진다.
            var army = Military * DefenseBonus;
            var eaten = State.Population * SocietyRules.BossAbsorbsPopulation;
            State.Boss += army + eaten;
            State.Wanderers += State.Population * SocietyRules.ScatteredRatio;
            outcome = InvasionOutcome.Destroyed;
            State.Collapses++;
            LeaveLegacy(before);
            Log($"깨어난 마왕(체력 {boss:0})에 수비력 {defense:0}으로 맞섰다가 뚫려 멸망했다. 마왕은 군대와 {eaten:0}명을 삼키고 체력이 {State.Boss:0}이 되었다. 살아남은 이들이 피난을 떠났다.");
            StartAsRemnant();
        }

        State.History.Add(new GenerationSummary(
            State.Generation, (int)before, stage, defense, strike, boss, outcome, (int)State.Population, State.Boss, snapshot));
        State.Generation++;
    }

    /// <summary>싸움마다 ±15%의 운.</summary>
    private double Swing() => 0.85 + 0.3 * _random.NextDouble();

    /// <summary>멸망한 사회의 수치를 같은 규모의 평범한 사회와 비교해 유산으로 남긴다.</summary>
    private void LeaveLegacy(double population)
    {
        foreach (var (stat, value) in State.Stats)
        {
            var ordinary = 2 * SocietyRules.MeanAbility * Scale(stat, SocietyRules.LegacyReferencePopulation);
            var legacy = SocietyRules.LegacyWeight * value / ordinary;
            State.Legacy[stat] = Math.Max(State.Legacy.GetValueOrDefault(stat) * SocietyRules.LegacyCarry, legacy);
        }
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
