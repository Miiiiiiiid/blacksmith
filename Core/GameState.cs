namespace Blacksmith.Core;

public enum GameStatus { Playing, Victory, GameOver }

public sealed class CraftedItem
{
    public string ItemId { get; set; } = "";
    /// <summary>품질 0~1.</summary>
    public double Quality { get; set; }
}

/// <summary>저장되는 게임의 모든 상태. 여기에만 상태를 두어야 상시 저장이 단순해진다.</summary>
public sealed class GameState
{
    public int Seed { get; set; }
    public int Day { get; set; } = 1;
    public int ActionsLeft { get; set; } = GameRules.ActionsPerDay;
    public int Gold { get; set; } = GameRules.StartingGold;
    public Stats Stats { get; set; } = new();
    public Dictionary<string, int> Materials { get; set; } = new();
    public List<CraftedItem> Inventory { get; set; } = new();

    public double Threat { get; set; } = GameRules.StartingThreat;
    /// <summary>다음 침공 전까지 왕국군에 납품한 방어력 합.</summary>
    public double DeliveredDefense { get; set; }

    public GameStatus Status { get; set; } = GameStatus.Playing;
    public List<string> Log { get; set; } = new();

    public int MaterialCount(string id) => Materials.GetValueOrDefault(id);

    public void AddMaterial(string id, int amount) =>
        Materials[id] = MaterialCount(id) + amount;
}
