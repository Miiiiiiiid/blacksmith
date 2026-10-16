namespace Blacksmith.Core;

public readonly record struct ActionResult(bool Ok, string Message);

/// <summary>
/// 플레이어 행동을 처리하는 게임 로직. 엔진과 무관한 순수 C#이라 Godot에서 Unity로 옮겨도 그대로 쓴다.
/// </summary>
public sealed class Game
{
    public GameData Data { get; }
    public GameState State { get; }

    public Game(GameData data, GameState state)
    {
        Data = data;
        State = state;
    }

    public static Game NewGame(GameData data, int seed, Difficulty difficulty = Difficulty.Normal)
    {
        var settings = DifficultySettings.For(difficulty);
        var state = new GameState { Seed = seed, Difficulty = difficulty, Threat = settings.StartingThreat };
        foreach (var (id, amount) in GameRules.StartingMaterials)
            if (data.Materials.ContainsKey(id)) state.AddMaterial(id, amount);
        state.DayStart = DaySnapshot.Take(state);
        var game = new Game(data, state);
        game.Chronicle($"마을 변두리에 작은 대장간을 열었습니다. (난이도 {settings.Name})");
        return game;
    }

    public DifficultySettings Settings => DifficultySettings.For(State.Difficulty);

    public ActionResult Gather(string materialId)
    {
        if (Blocked() is { } blocked) return blocked;
        if (!Data.Materials.TryGetValue(materialId, out var material) || material.GatherAmount <= 0)
            return Fail("채집할 수 없는 재료입니다.");

        State.AddMaterial(material.Id, material.GatherAmount);
        return Spend(StatType.Martial, $"{material.Name} {material.GatherAmount}개를 채집했습니다.");
    }

    public ActionResult Buy(string materialId, int quantity)
    {
        if (Blocked() is { } blocked) return blocked;
        if (!Data.Materials.TryGetValue(materialId, out var material) || quantity <= 0)
            return Fail("살 수 없는 재료입니다.");

        var cost = BuyCost(material, quantity);
        if (State.Gold < cost) return Fail($"골드가 부족합니다. ({cost} 필요)");

        State.Gold -= cost;
        State.AddMaterial(material.Id, quantity);
        return Spend(StatType.Eloquence, $"{material.Name} {quantity}개를 {cost} 골드에 샀습니다.");
    }

    /// <summary>화술이 높을수록 싸게 산다. 최대 25% 할인.</summary>
    public int BuyCost(MaterialDef material, int quantity) =>
        (int)Math.Ceiling(material.BuyPrice * quantity * (1 - State.Stats[StatType.Eloquence] / GameRules.StatMax * 0.25));

    public bool CanCraft(string itemId) =>
        Data.Items.TryGetValue(itemId, out var item)
        && item.Materials.All(m => State.MaterialCount(m.Key) >= m.Value);

    /// <param name="minigameScore">제작 미니게임 점수 0~1.</param>
    public ActionResult Craft(string itemId, double minigameScore) => CraftInternal(itemId, minigameScore, manual: true);

    /// <summary>자동 제작 품질. 아직 충분히 만들어보지 않았으면 null.</summary>
    public double? AutoCraftQuality(string itemId) =>
        State.CraftRecords.TryGetValue(itemId, out var record) && record.Count >= GameRules.AutoCraftAfter
            ? record.BestQuality * GameRules.AutoCraftRatio
            : null;

    /// <summary>미니게임 없이 최고 기록의 일정 비율 품질로 만든다.</summary>
    public ActionResult CraftAuto(string itemId) =>
        AutoCraftQuality(itemId) is { } quality
            ? CraftInternal(itemId, quality, manual: false)
            : Fail($"{GameRules.AutoCraftAfter}번 이상 직접 만들어야 자동으로 만들 수 있습니다.");

    private ActionResult CraftInternal(string itemId, double score, bool manual)
    {
        if (Blocked() is { } blocked) return blocked;
        if (!Data.Items.TryGetValue(itemId, out var item)) return Fail("알 수 없는 품목입니다.");
        if (!CanCraft(itemId)) return Fail("재료가 부족합니다.");

        foreach (var (materialId, amount) in item.Materials)
            State.Materials[materialId] -= amount;

        var quality = Math.Clamp(score, 0, 1);
        State.Inventory.Add(new CraftedItem { ItemId = item.Id, Quality = quality });
        State.TotalCrafted++;

        if (manual)
        {
            if (!State.CraftRecords.TryGetValue(item.Id, out var record))
                State.CraftRecords[item.Id] = record = new CraftRecord();
            record.Count++;
            record.BestQuality = Math.Max(record.BestQuality, quality);
            if (record.Count == 1) Chronicle($"처음으로 {item.Name}을(를) 만들었습니다. 품질 {quality:P0}");
        }

        var how = manual ? "만들었습니다" : "자동으로 만들었습니다";
        return Spend(item.TrainsStat, $"{item.Name}을(를) {how}. 품질 {quality:P0}");
    }

    public int SellPrice(CraftedItem crafted) =>
        (int)Math.Round(Data.Items[crafted.ItemId].BasePrice
            * GameRules.QualityPriceMultiplier(crafted.Quality)
            * (1 + State.Stats[StatType.Eloquence] / GameRules.StatMax * 0.5));

    public ActionResult Sell(int inventoryIndex)
    {
        if (Blocked() is { } blocked) return blocked;
        if (inventoryIndex < 0 || inventoryIndex >= State.Inventory.Count) return Fail("팔 물건이 없습니다.");

        var crafted = State.Inventory[inventoryIndex];
        var price = SellPrice(crafted);
        State.Inventory.RemoveAt(inventoryIndex);
        State.Gold += price;
        return Spend(StatType.Eloquence, $"{Data.Items[crafted.ItemId].Name}을(를) {price} 골드에 팔았습니다.");
    }

    public double DefenseValue(CraftedItem crafted) =>
        Data.Items[crafted.ItemId].DefensePower * (0.5 + crafted.Quality);

    /// <summary>왕국군에 납품한다. 돈은 받지 않지만 다음 침공의 방어력이 오르고 명성이 오른다.</summary>
    public ActionResult Deliver(int inventoryIndex)
    {
        if (Blocked() is { } blocked) return blocked;
        if (inventoryIndex < 0 || inventoryIndex >= State.Inventory.Count) return Fail("납품할 물건이 없습니다.");

        var crafted = State.Inventory[inventoryIndex];
        var defense = DefenseValue(crafted);
        if (defense <= 0) return Fail("전투에 쓸 수 없는 물건입니다.");

        State.Inventory.RemoveAt(inventoryIndex);
        State.DeliveredDefense += defense;
        State.Stats.ChangeFame(1);
        return Spend(null, $"{Data.Items[crafted.ItemId].Name}을(를) 왕국군에 납품했습니다. 방어력 +{defense:0.#}");
    }

    public int DaysUntilInvasion => GameRules.InvasionInterval - (State.Day - 1) % GameRules.InvasionInterval;

    /// <summary>오늘 밤이 침공일인지.</summary>
    public bool InvasionTonight => Settings.Invasions && State.Day % GameRules.InvasionInterval == 0;

    /// <summary>다음 침공을 막을 확률. 납품한 방어력과 위협의 비율로 정한다.</summary>
    public double DefenseChance =>
        State.DeliveredDefense + State.Threat <= 0 ? 1 : State.DeliveredDefense / (State.DeliveredDefense + State.Threat);

    /// <summary>하루를 마친다. 저녁 정산을 만들고, 침공일이면 판정한 뒤 다음 날 아침으로 넘어간다.</summary>
    public ActionResult EndDay()
    {
        if (State.Status != GameStatus.Playing) return Fail("게임이 끝났습니다.");

        var report = BuildReport();
        State.LastReport = report;

        if (InvasionTonight)
        {
            var chance = DefenseChance;
            var defended = DeterministicRandom.Roll(State.Seed, State.Day, "invasion") < chance;
            report.Invasion = new InvasionResult { Chance = chance, Defended = defended };
            if (!defended)
            {
                State.Status = GameStatus.GameOver;
                Chronicle($"마왕군이 마을을 함락했습니다. (방어 확률 {chance:P0})");
                return Log(new ActionResult(true,
                    $"{State.Day}일째 밤, 마왕군이 마을을 함락했습니다. (방어 확률 {chance:P0}) 게임오버."));
            }
            Chronicle($"마왕군의 침공을 막아냈습니다. (방어 확률 {chance:P0})");
            State.Threat *= GameRules.ThreatReliefOnDefense;
            State.DeliveredDefense = 0;
            State.Stats.ChangeFame(5);
            report.FameChange += 5;
        }

        State.Day++;
        State.ActionsLeft = GameRules.ActionsPerDay;
        State.Threat += Settings.ThreatPerDay;
        State.DayStart = DaySnapshot.Take(State);

        if (State.Day > GameRules.VictoryDay)
        {
            State.Status = GameStatus.Victory;
            Chronicle(Settings.Invasions
                ? $"{GameRules.VictoryDay}일 동안 마을을 지켜냈습니다."
                : $"{GameRules.VictoryDay}일 동안 대장간을 꾸려냈습니다.");
            return Log(new ActionResult(true, $"{GameRules.VictoryDay}일을 버텨냈습니다. 승리!"));
        }

        var defendedText = report.Invasion is { } inv ? $"마왕군의 침공을 막아냈습니다! (방어 확률 {inv.Chance:P0}) " : "";
        return Log(new ActionResult(true, $"{defendedText}{State.Day}일째 아침이 밝았습니다."));
    }

    private DayReport BuildReport()
    {
        var start = State.DayStart;
        return new DayReport
        {
            Day = State.Day,
            GoldChange = State.Gold - start.Gold,
            StatChanges = State.Stats.Values
                .Select(kv => (kv.Key, Change: kv.Value - start.Stats.GetValueOrDefault(kv.Key)))
                .Where(c => Math.Abs(c.Change) > 1e-9)
                .ToDictionary(c => c.Key, c => c.Change),
            FameChange = State.Stats.Fame - start.Fame,
            Crafted = State.TotalCrafted - start.Crafted,
        };
    }

    private void Chronicle(string text) => State.Chronicle.Add(new ChronicleEntry { Day = State.Day, Text = text });

    private ActionResult? Blocked()
    {
        if (State.Status != GameStatus.Playing) return Fail("게임이 끝났습니다.");
        if (State.ActionsLeft <= 0) return Fail("오늘은 더 행동할 수 없습니다. 하루를 마치세요.");
        return null;
    }

    private ActionResult Spend(StatType? trained, string message)
    {
        State.ActionsLeft--;
        if (trained is { } stat)
            State.Stats.Train(stat, GameRules.TrainPerAction, GameRules.DecayRatio);
        return Log(new ActionResult(true, message));
    }

    private ActionResult Fail(string message) => new(false, message);

    private ActionResult Log(ActionResult result)
    {
        State.Log.Add($"[{State.Day}일] {result.Message}");
        if (State.Log.Count > 200) State.Log.RemoveAt(0);
        return result;
    }
}
