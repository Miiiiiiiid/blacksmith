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
    public void Perfect_stages_make_perfect_quality()
    {
        var hammers = Enumerable.Repeat(1.0, ForgingMinigame.HammerStrikes).ToList();
        Assert.Equal(1, ForgingMinigame.Combine(1, hammers, 1), 6);
        Assert.Equal(0, ForgingMinigame.Combine(0, new List<double>(), 0), 6);
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
