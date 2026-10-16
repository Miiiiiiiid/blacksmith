namespace Blacksmith.Core;

public enum GameStatus { Playing, Victory, GameOver }

public sealed class CraftedItem
{
    public string ItemId { get; set; } = "";
    /// <summary>품질 0~1.</summary>
    public double Quality { get; set; }
}

/// <summary>품목별로 직접 만든 횟수와 최고 품질. 자동 제작에 쓴다.</summary>
public sealed class CraftRecord
{
    public int Count { get; set; }
    public double BestQuality { get; set; }
}

/// <summary>저장되는 게임의 모든 상태. 여기에만 상태를 두어야 상시 저장이 단순해진다.</summary>
public sealed class GameState
{
    public int Seed { get; set; }
    public Difficulty Difficulty { get; set; } = Difficulty.Normal;
    public int Day { get; set; } = 1;
    public int ActionsLeft { get; set; } = GameRules.ActionsPerDay;
    public int Gold { get; set; } = GameRules.StartingGold;
    public Stats Stats { get; set; } = new();
    public Dictionary<string, int> Materials { get; set; } = new();
    public List<CraftedItem> Inventory { get; set; } = new();
    public Dictionary<string, CraftRecord> CraftRecords { get; set; } = new();
    /// <summary>지금까지 만든 물건 수. 자동 제작도 센다.</summary>
    public int TotalCrafted { get; set; }

    public double Threat { get; set; } = GameRules.StartingThreat;
    /// <summary>다음 침공 전까지 왕국군에 납품한 방어력 합.</summary>
    public double DeliveredDefense { get; set; }

    public GameStatus Status { get; set; } = GameStatus.Playing;
    public List<string> Log { get; set; } = new();

    /// <summary>오늘 아침의 모습. 저녁 정산에서 비교한다.</summary>
    public DaySnapshot DayStart { get; set; } = new();
    /// <summary>가장 최근 저녁 정산. 아직 하루도 안 끝났으면 null.</summary>
    public DayReport? LastReport { get; set; }
    public List<ChronicleEntry> Chronicle { get; set; } = new();

    public int MaterialCount(string id) => Materials.GetValueOrDefault(id);

    public void AddMaterial(string id, int amount) =>
        Materials[id] = MaterialCount(id) + amount;
}
