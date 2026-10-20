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
    public void Starts_as_thirty_remnants_with_food_and_combat_only()
    {
        var sim = new SocietySimulation(1);
        Assert.Equal(30, sim.State.Population);
        sim.Step();
        Assert.Equal(new[] { SocietyStat.Food, SocietyStat.Combat }, sim.State.Leaders.Keys.OrderBy(k => k));
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
        sim.State.Enemy = 1e9;
        sim.RunGeneration();
        Assert.Equal(InvasionOutcome.Destroyed, sim.State.History.Single().Outcome);
        Assert.Equal(30, sim.State.Population);
        Assert.Equal(1, sim.State.Collapses);
        Assert.Empty(sim.State.Stats);
    }

    private sealed class HistoryComparer : IEqualityComparer<GenerationSummary>
    {
        public bool Equals(GenerationSummary? x, GenerationSummary? y) =>
            x!.Generation == y!.Generation && x.Population == y.Population && x.Outcome == y.Outcome && x.Military == y.Military;

        public int GetHashCode(GenerationSummary obj) => obj.Generation;
    }
}
