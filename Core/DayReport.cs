namespace Blacksmith.Core;

/// <summary>하루를 시작할 때의 모습. 저녁 정산에서 오늘 무엇이 변했는지 비교하는 데 쓴다.</summary>
public sealed class DaySnapshot
{
    public int Gold { get; set; }
    public Dictionary<StatType, double> Stats { get; set; } = new();
    public double Fame { get; set; }
    public int Crafted { get; set; }

    public static DaySnapshot Take(GameState state) => new()
    {
        Gold = state.Gold,
        Stats = new Dictionary<StatType, double>(state.Stats.Values),
        Fame = state.Stats.Fame,
        Crafted = state.TotalCrafted,
    };
}

/// <summary>침공 판정 결과.</summary>
public sealed class InvasionResult
{
    public double Chance { get; set; }
    public bool Defended { get; set; }
}

/// <summary>저녁 정산. 하루 동안 바뀐 것과 그날 밤 침공 결과.</summary>
public sealed class DayReport
{
    public int Day { get; set; }
    public int GoldChange { get; set; }
    public Dictionary<StatType, double> StatChanges { get; set; } = new();
    public double FameChange { get; set; }
    public int Crafted { get; set; }
    public InvasionResult? Invasion { get; set; }
}

/// <summary>연대기 한 줄. 엔딩에서 내가 걸어온 길을 보여준다.</summary>
public sealed class ChronicleEntry
{
    public int Day { get; set; }
    public string Text { get; set; } = "";
}
