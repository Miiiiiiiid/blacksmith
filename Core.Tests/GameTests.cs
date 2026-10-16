using Blacksmith.Core;

namespace Blacksmith.Core.Tests;

public class GameTests
{
    private static readonly string DataDir =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "godot", "data");

    private static GameData LoadData() => GameData.FromJson(
        File.ReadAllText(Path.Combine(DataDir, "materials.json")),
        File.ReadAllText(Path.Combine(DataDir, "items.json")));

    private static Game NewGame(int seed = 1) => Game.NewGame(LoadData(), seed);

    [Fact]
    public void Training_a_stat_decays_its_opposite()
    {
        var stats = new Stats();
        stats.Values[StatType.Finesse] = 5;

        stats.Train(StatType.Strength, 1, 0.6);

        Assert.Equal(1, stats[StatType.Strength]);
        Assert.Equal(4.4, stats[StatType.Finesse], 6);
        Assert.Equal(0, stats[StatType.Eloquence]);
    }

    [Fact]
    public void Decay_never_goes_below_zero()
    {
        var stats = new Stats();
        stats.Train(StatType.Eloquence, 1, 0.6);
        Assert.Equal(0, stats[StatType.Boldness]);
    }

    [Fact]
    public void Actions_run_out_until_the_day_ends()
    {
        var game = NewGame();
        for (var i = 0; i < GameRules.ActionsPerDay; i++)
            Assert.True(game.Gather("iron_ore").Ok);

        Assert.False(game.Gather("iron_ore").Ok);
        game.EndDay();
        Assert.Equal(2, game.State.Day);
        Assert.Equal(GameRules.ActionsPerDay, game.State.ActionsLeft);
    }

    [Fact]
    public void Gathering_adds_materials_and_trains_martial()
    {
        var game = NewGame();
        var before = game.State.MaterialCount("iron_ore");
        game.Gather("iron_ore");
        Assert.Equal(before + 2, game.State.MaterialCount("iron_ore"));
        Assert.Equal(1, game.State.Stats[StatType.Martial]);
    }

    [Fact]
    public void Leather_cannot_be_gathered_but_can_be_bought()
    {
        var game = NewGame();
        Assert.False(game.Gather("leather").Ok);
        Assert.True(game.Buy("leather", 1).Ok);
        Assert.Equal(GameRules.StartingGold - 6, game.State.Gold);
    }

    [Fact]
    public void Crafting_consumes_materials_and_selling_pays_by_quality()
    {
        var game = NewGame();
        game.Buy("iron_ore", 1);
        game.Buy("coal", 1);
        game.Buy("wood", 1);
        var ironBefore = game.State.MaterialCount("iron_ore");

        Assert.True(game.Craft("dagger", 1.0).Ok);
        Assert.Equal(ironBefore - 1, game.State.MaterialCount("iron_ore"));
        Assert.Single(game.State.Inventory);

        game.EndDay(); // 구매 3번 + 제작 1번으로 행동을 다 썼다
        var goldBefore = game.State.Gold;
        var price = game.SellPrice(game.State.Inventory[0]);
        Assert.True(game.Sell(0).Ok);
        Assert.Equal(goldBefore + price, game.State.Gold);
        Assert.Empty(game.State.Inventory);
    }

    [Fact]
    public void New_game_starts_with_enough_for_a_dagger()
    {
        Assert.True(NewGame().CanCraft("dagger"));
    }

    [Fact]
    public void Crafting_without_materials_fails_without_spending_an_action()
    {
        var game = NewGame();
        Assert.False(game.Craft("longsword", 1.0).Ok);
        Assert.Equal(GameRules.ActionsPerDay, game.State.ActionsLeft);
    }

    [Fact]
    public void Undefended_village_falls_at_the_first_invasion()
    {
        var game = NewGame();
        for (var day = 1; day < GameRules.InvasionInterval; day++) game.EndDay();
        Assert.Equal(GameStatus.Playing, game.State.Status);

        game.EndDay();
        Assert.Equal(GameStatus.GameOver, game.State.Status);
    }

    [Fact]
    public void Reloading_a_save_gives_the_same_invasion_result()
    {
        var game = NewGame(seed: 42);
        for (var day = 1; day < GameRules.InvasionInterval; day++) game.EndDay();
        game.State.DeliveredDefense = game.State.Threat; // 방어 확률 50%

        var saved = SaveSystem.Serialize(game.State);
        game.EndDay();
        var first = game.State.Status;

        for (var i = 0; i < 5; i++)
        {
            var reloaded = new Game(LoadData(), SaveSystem.Deserialize(saved));
            reloaded.EndDay();
            Assert.Equal(first, reloaded.State.Status);
        }
    }

    [Fact]
    public void Save_round_trip_keeps_state()
    {
        var game = NewGame(seed: 7);
        var coal = game.State.MaterialCount("coal");
        game.Gather("coal");
        game.State.Stats.ChangeFame(-3);

        var loaded = SaveSystem.Deserialize(SaveSystem.Serialize(game.State));

        Assert.Equal(7, loaded.Seed);
        Assert.Equal(coal + 3, loaded.MaterialCount("coal"));
        Assert.Equal(1, loaded.Stats[StatType.Martial]);
        Assert.Equal(-3, loaded.Stats.Fame);
        Assert.Equal(GameRules.ActionsPerDay - 1, loaded.ActionsLeft);
    }

    [Fact]
    public void Defending_every_invasion_wins_on_day_thirty()
    {
        var game = NewGame();
        while (game.State.Status == GameStatus.Playing)
        {
            game.State.DeliveredDefense = 1e9; // 확실히 막는다
            game.EndDay();
        }
        Assert.Equal(GameStatus.Victory, game.State.Status);
        Assert.Equal(GameRules.VictoryDay + 1, game.State.Day);
    }
}

public class FlowTests
{
    private static Game NewGame(Difficulty difficulty = Difficulty.Normal)
    {
        var dataDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "godot", "data");
        var data = GameData.FromJson(
            File.ReadAllText(Path.Combine(dataDir, "materials.json")),
            File.ReadAllText(Path.Combine(dataDir, "items.json")));
        return Game.NewGame(data, 1, difficulty);
    }

    [Fact]
    public void Evening_report_shows_what_changed_today()
    {
        var game = NewGame();
        game.Gather("coal");
        game.Craft("dagger", 0.8);
        game.EndDay();

        var report = game.State.LastReport!;
        Assert.Equal(1, report.Day);
        Assert.Equal(1, report.Crafted);
        Assert.Equal(1, report.StatChanges[StatType.Martial], 6);
        Assert.Equal(1, report.StatChanges[StatType.Strength], 6);
        Assert.Null(report.Invasion);
    }

    [Fact]
    public void Invasion_night_report_carries_the_result()
    {
        var game = NewGame();
        for (var day = 1; day < GameRules.InvasionInterval; day++) game.EndDay();
        game.State.DeliveredDefense = 1e9;
        game.EndDay();

        Assert.True(game.State.LastReport!.Invasion!.Defended);
        Assert.Equal(GameStatus.Playing, game.State.Status);
        Assert.Contains(game.State.Chronicle, e => e.Text.Contains("막아냈습니다"));
    }

    [Fact]
    public void Peaceful_has_no_invasions_and_ends_on_day_thirty()
    {
        var game = NewGame(Difficulty.Peaceful);
        while (game.State.Status == GameStatus.Playing) game.EndDay();
        Assert.Equal(GameStatus.Victory, game.State.Status);
        Assert.All(game.State.Chronicle, e => Assert.DoesNotContain("마왕군", e.Text));
    }

    [Fact]
    public void Harder_difficulty_starts_with_more_threat()
    {
        Assert.True(NewGame(Difficulty.Hard).State.Threat > NewGame(Difficulty.Easy).State.Threat);
    }

    [Fact]
    public void Chronicle_records_first_craft_once()
    {
        var game = NewGame();
        game.State.AddMaterial("iron_ore", 5);
        game.State.AddMaterial("coal", 5);
        game.State.AddMaterial("wood", 5);
        game.Craft("dagger", 0.5);
        game.Craft("dagger", 0.5);
        Assert.Single(game.State.Chronicle, e => e.Text.Contains("단검"));
    }
}
