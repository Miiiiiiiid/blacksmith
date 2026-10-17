using Blacksmith.Core;

namespace Blacksmith.Core.Tests;

public class ForgingTests
{
    [Fact]
    public void Marker_goes_up_then_comes_back()
    {
        Assert.Equal(0, ForgingMinigame.MarkerPosition(0, 2), 6);
        Assert.Equal(0.5, ForgingMinigame.MarkerPosition(0.5, 2), 6);
        Assert.Equal(1, ForgingMinigame.MarkerPosition(1, 2), 6);
        Assert.Equal(0.5, ForgingMinigame.MarkerPosition(1.5, 2), 6);
        Assert.Equal(0, ForgingMinigame.MarkerPosition(2, 2), 6);
    }

    [Fact]
    public void Inside_the_zone_scores_full_and_falls_off_outside()
    {
        Assert.Equal(1, ForgingMinigame.ScoreTiming(0.72, 0.75, 0.05));
        Assert.Equal(0.5, ForgingMinigame.ScoreTiming(0.675, 0.75, 0.05), 6);
        Assert.Equal(0, ForgingMinigame.ScoreTiming(0.2, 0.75, 0.05));
    }

    [Fact]
    public void Strength_widens_the_zone()
    {
        Assert.True(ForgingMinigame.ZoneHalfWidth(50) > ForgingMinigame.ZoneHalfWidth(0));
        Assert.Equal(ForgingMinigame.ZoneHalfWidth(100), ForgingMinigame.ZoneHalfWidth(500));
    }

    [Fact]
    public void Quality_is_the_average_of_the_rounds()
    {
        Assert.Equal(1, ForgingMinigame.Combine(new[] { 1.0, 1.0, 1.0 }), 6);
        Assert.Equal(0.5, ForgingMinigame.Combine(new[] { 1.0, 0.5, 0.0 }), 6);
        Assert.Equal(0, ForgingMinigame.Combine(Array.Empty<double>()), 6);
    }

    [Fact]
    public void Each_craft_picks_three_rounds_and_can_repeat()
    {
        var random = new Random(3);
        var picks = Enumerable.Range(0, 200).Select(_ => ForgingMinigame.PickRounds(random)).ToList();
        Assert.All(picks, p => Assert.Equal(ForgingMinigame.RoundsPerCraft, p.Count));
        Assert.Contains(picks, p => p.Distinct().Count() == 1);
        Assert.All(Enum.GetValues<Minigame>(), m => Assert.Contains(picks, p => p.Contains(m)));
    }

    [Fact]
    public void Stats_ease_the_new_minigames()
    {
        Assert.True(ForgingMinigame.FurnaceWobble(100) < ForgingMinigame.FurnaceWobble(0));
        Assert.Equal(18, ForgingMinigame.WhetstoneTarget(0));
        Assert.Equal(12, ForgingMinigame.WhetstoneTarget(100));
        Assert.True(ForgingMinigame.ReleaseHalfWidth(100) > ForgingMinigame.ReleaseHalfWidth(0));
        Assert.Equal(5, ForgingMinigame.MemoryShowSeconds(0), 6);
        Assert.Equal(7, ForgingMinigame.MemoryShowSeconds(100), 6);
        Assert.Equal(1, ForgingMinigame.ScoreFurnace(ForgingMinigame.FurnaceSeconds * 0.7), 6);
        Assert.Equal(0.6, ForgingMinigame.ScoreMemory(3), 6);
        Assert.True(ForgingMinigame.InFurnaceZone(ForgingMinigame.FurnaceTarget));
        Assert.False(ForgingMinigame.InFurnaceZone(0));
    }

    [Fact]
    public void Strength_lowers_the_bellows_target()
    {
        Assert.Equal(20, ForgingMinigame.BellowsTarget(0));
        Assert.Equal(14, ForgingMinigame.BellowsTarget(100));
        Assert.Equal(1, ForgingMinigame.ScoreBellows(14, 100));
        Assert.Equal(0.5, ForgingMinigame.ScoreBellows(10, 0), 6);
    }

    [Fact]
    public void Finesse_gives_more_time_for_arrows()
    {
        Assert.Equal(4, ForgingMinigame.ArrowSeconds(0));
        Assert.Equal(6, ForgingMinigame.ArrowSeconds(100));
        Assert.Equal(ForgingMinigame.ArrowCount, ForgingMinigame.RandomArrows(new Random(1)).Count);
        Assert.Equal(0.5, ForgingMinigame.ScoreArrows(3), 6);
    }
}

public class AutoCraftTests
{
    private static Game NewGame()
    {
        var dataDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "godot", "data");
        var data = GameData.FromJson(
            File.ReadAllText(Path.Combine(dataDir, "materials.json")),
            File.ReadAllText(Path.Combine(dataDir, "items.json")));
        var game = Game.NewGame(data, 1);
        game.State.Gold = 10_000;
        game.State.AddMaterial("iron_ore", 50);
        game.State.AddMaterial("coal", 50);
        game.State.AddMaterial("wood", 50);
        return game;
    }

    private static void CraftDagger(Game game, double score)
    {
        if (game.State.ActionsLeft == 0) game.EndDay();
        Assert.True(game.Craft("dagger", score).Ok);
    }

    [Fact]
    public void Auto_craft_unlocks_after_three_manual_crafts()
    {
        var game = NewGame();
        CraftDagger(game, 0.5);
        CraftDagger(game, 0.9);
        Assert.Null(game.AutoCraftQuality("dagger"));
        Assert.False(game.CraftAuto("dagger").Ok);

        CraftDagger(game, 0.6);
        Assert.Equal(0.9 * GameRules.AutoCraftRatio, game.AutoCraftQuality("dagger")!.Value, 6);
    }

    [Fact]
    public void Auto_craft_uses_best_quality_and_does_not_raise_it()
    {
        var game = NewGame();
        for (var i = 0; i < GameRules.AutoCraftAfter; i++) CraftDagger(game, 1.0);
        game.EndDay();

        Assert.True(game.CraftAuto("dagger").Ok);
        Assert.Equal(GameRules.AutoCraftRatio, game.State.Inventory[^1].Quality, 6);
        Assert.Equal(1.0, game.State.CraftRecords["dagger"].BestQuality, 6);
        Assert.Equal(GameRules.AutoCraftAfter, game.State.CraftRecords["dagger"].Count);
    }

    [Fact]
    public void Craft_records_survive_save_and_load()
    {
        var game = NewGame();
        CraftDagger(game, 0.7);
        var loaded = SaveSystem.Deserialize(SaveSystem.Serialize(game.State));
        Assert.Equal(1, loaded.CraftRecords["dagger"].Count);
        Assert.Equal(0.7, loaded.CraftRecords["dagger"].BestQuality, 6);
    }
}
