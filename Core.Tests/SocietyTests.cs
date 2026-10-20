using Blacksmith.Core.Society;

namespace Blacksmith.Core.Tests;

public class SocietyTests
{
    [Fact]
    public void Same_seed_gives_same_history()
    {
        var a = new SocietySimulation(7);
        var b = new SocietySimulation(7);
        a.Run(10);
        b.Run(10);
        Assert.Equal(a.State.History, b.State.History, new HistoryComparer());
        Assert.Equal(a.State.Chronicle.Select(e => e.Text), b.State.Chronicle.Select(e => e.Text));
    }

    [Fact]
    public void Starts_as_thirty_remnants_with_food_combat_and_a_hero()
    {
        var sim = new SocietySimulation(1);
        Assert.Equal(30, sim.State.Population);
        sim.Step();
        Assert.Equal(new[] { SocietyStat.Food, SocietyStat.Combat, SocietyStat.Strike }, sim.State.Leaders.Keys.OrderBy(k => k));
        Assert.Equal("농사꾼", SocietySimulation.Title(SocietyStat.Food, sim.CurrentStage));
    }

    [Theory]
    [InlineData(499, Stage.Remnant)]
    [InlineData(500, Stage.Town)]
    [InlineData(1000, Stage.City)]
    [InlineData(2000, Stage.Nation)]
    public void Population_sets_the_stage(double population, Stage stage) =>
        Assert.Equal(stage, SocietySimulation.StageOf(population));

    [Fact]
    public void Titles_grow_with_the_society()
    {
        Assert.Equal("힘센 사람", SocietySimulation.Title(SocietyStat.Combat, Stage.Remnant));
        Assert.Equal("방위대장", SocietySimulation.Title(SocietyStat.Combat, Stage.City));
        Assert.Equal("군대장", SocietySimulation.Title(SocietyStat.Combat, Stage.Nation));
        Assert.Equal("농업장관", SocietySimulation.Title(SocietyStat.Food, Stage.Nation));
    }

    [Fact]
    public void A_generation_is_nine_hundred_turns_and_ends_in_an_invasion()
    {
        var sim = new SocietySimulation(3);
        sim.RunGeneration();
        Assert.Equal(900, SocietyRules.TurnsPerGeneration);
        Assert.Single(sim.State.History);
        Assert.Equal(2, sim.State.Generation);
    }

    [Fact]
    public void Total_destruction_restarts_as_remnants()
    {
        var sim = new SocietySimulation(5);
        sim.State.Boss = 1e9;
        sim.RunGeneration();
        Assert.Equal(InvasionOutcome.Destroyed, sim.State.History.Single().Outcome);
        Assert.Equal(30, sim.State.Population);
        Assert.Equal(1, sim.State.Collapses);
        Assert.Empty(sim.State.Stats);
    }

    [Fact]
    public void A_repelled_boss_comes_back_weaker_by_the_strike_damage()
    {
        var sim = new SocietySimulation(4) { DefenseBonus = 1000, StrikeBonus = 0.0001 };
        sim.RunGeneration();
        var h = sim.State.History.Single();
        Assert.Equal(InvasionOutcome.Repelled, h.Outcome);
        Assert.Equal(h.Boss - h.Strike, h.BossAfter, 6);
    }

    [Fact]
    public void A_strike_that_reaches_full_health_kills_the_boss()
    {
        var sim = new SocietySimulation(4) { DefenseBonus = 1000, StrikeBonus = 1000 };
        sim.RunGeneration();
        Assert.Equal(InvasionOutcome.Victory, sim.State.History.Single().Outcome);
        Assert.True(sim.State.Won);
    }

    [Fact]
    public void A_breach_is_the_end_and_feeds_the_boss()
    {
        var sim = new SocietySimulation(5);
        sim.State.Boss = 1e6;
        sim.RunGeneration();
        var h = sim.State.History.Single();
        Assert.Equal(InvasionOutcome.Destroyed, h.Outcome);
        Assert.True(h.BossAfter > h.Boss + h.Population * SocietyRules.BossAbsorbsPopulation);
        Assert.NotEmpty(sim.State.Legacy);
    }

    [Fact]
    public void A_first_strike_that_reaches_full_health_ends_it_in_one_blow()
    {
        var sim = new SocietySimulation(6) { FirstStrikePower = 1e9 };
        sim.RunGeneration();
        Assert.True(sim.State.Won);
        Assert.Contains(sim.State.Chronicle, e => e.Text.Contains("한 방에"));
    }

    [Fact]
    public void A_first_strike_that_fails_enrages_the_boss()
    {
        var sim = new SocietySimulation(6) { FirstStrikePower = 1, DefenseBonus = 1e6, StrikeBonus = 1e6 };
        sim.RunGeneration();
        Assert.Contains(sim.State.Chronicle, e => e.Text.Contains("격노"));
        // 막아내도 공격대는 쫓아가지 못하고, 격노는 그 공세로 끝난다.
        var h = sim.State.History.Single();
        Assert.Equal(InvasionOutcome.Repelled, h.Outcome);
        Assert.Equal(0, h.Strike);
        Assert.Equal(SocietyRules.BossHealth, sim.State.Boss);
    }

    [Fact]
    public void A_request_raises_its_field_right_away()
    {
        var alone = new SocietySimulation(8);
        var helped = new SocietySimulation(8, strategy: new Always(new PlayerAction(ActionKind.Request, SocietyStat.Combat)));
        alone.Step();
        helped.Step();
        Assert.True(helped.Stat(SocietyStat.Combat) > alone.Stat(SocietyStat.Combat));
        Assert.Equal(1, helped.State.Player.ActionCounts[ActionKind.Request]);
    }

    [Fact]
    public void Custom_gear_makes_the_representative_better()
    {
        var alone = new SocietySimulation(8);
        var helped = new SocietySimulation(8, strategy: new Always(new PlayerAction(ActionKind.Gear, SocietyStat.Strike)));
        alone.Step();
        helped.Step();
        Assert.True(helped.State.Leaders[SocietyStat.Strike].Ability > alone.State.Leaders[SocietyStat.Strike].Ability);
    }

    [Fact]
    public void Research_makes_every_blow_hit_harder()
    {
        var sim = new SocietySimulation(8, strategy: new Always(new PlayerAction(ActionKind.Research)));
        Assert.Equal(1, sim.ResearchMultiplier);
        for (var i = 0; i < 100; i++) sim.Step();
        Assert.True(sim.ResearchMultiplier > 1);
    }

    [Fact]
    public void A_strong_enough_masterwork_is_used_as_the_first_strike()
    {
        var sim = new SocietySimulation(8, strategy: new Bots.MasterworkBot());
        sim.State.Player.MasterworkPower = 1e9;
        sim.RunGeneration();
        Assert.True(sim.State.Won);
        Assert.Contains(sim.State.Chronicle, e => e.Text.Contains("한 방에"));
    }

    [Fact]
    public void The_smith_dies_with_the_society()
    {
        var sim = new SocietySimulation(5, strategy: new Bots.RequestBot());
        sim.State.Boss = 1e9;
        sim.RunGeneration();
        Assert.Equal(ContributionRules.StartingSkill, sim.State.Player.Skill);
    }

    [Fact]
    public void Hard_starts_with_a_tougher_boss()
    {
        Assert.Equal(SocietyRules.BossHealth, new SocietySimulation(1).State.Boss);
        Assert.Equal(SocietyRules.BossHealth * SocietyRules.HardBossHealthRatio, new SocietySimulation(1, Difficulty.Hard).State.Boss);
    }

    private sealed class Always(PlayerAction action) : IPlayStrategy
    {
        public string Name => "고정";
        public PlayerAction Choose(SocietySimulation sim) => action;
    }

    private sealed class HistoryComparer : IEqualityComparer<GenerationSummary>
    {
        public bool Equals(GenerationSummary? x, GenerationSummary? y) =>
            x!.Generation == y!.Generation && x.Population == y.Population && x.Outcome == y.Outcome && x.Defense == y.Defense;

        public int GetHashCode(GenerationSummary obj) => obj.Generation;
    }
}
