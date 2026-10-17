using System.Text.Json;
using System.Text.Json.Serialization;

namespace Blacksmith.Core;

public sealed class MaterialDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int BuyPrice { get; set; }
    /// <summary>채집 한 번에 얻는 양. 0이면 채집할 수 없다.</summary>
    public int GatherAmount { get; set; }
}

public sealed class ItemDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public Dictionary<string, int> Materials { get; set; } = new();
    public int BasePrice { get; set; }
    /// <summary>왕국군에 납품했을 때의 방어력.</summary>
    public double DefensePower { get; set; }
    /// <summary>제작할 때 능력치 변화.</summary>
    public StatGain Training { get; set; } = new(StatType.Strength, 0, 0);
}

/// <summary>재료와 품목 정의. JSON 데이터 파일에서 읽는다.</summary>
public sealed class GameData
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true,
    };

    public Dictionary<string, MaterialDef> Materials { get; }
    public Dictionary<string, ItemDef> Items { get; }

    public GameData(IEnumerable<MaterialDef> materials, IEnumerable<ItemDef> items)
    {
        Materials = materials.ToDictionary(m => m.Id);
        Items = items.ToDictionary(i => i.Id);
        foreach (var item in Items.Values)
            foreach (var materialId in item.Materials.Keys)
                if (!Materials.ContainsKey(materialId))
                    throw new InvalidDataException($"품목 '{item.Id}'의 재료 '{materialId}'가 정의되지 않았습니다.");
    }

    public static GameData FromJson(string materialsJson, string itemsJson) => new(
        JsonSerializer.Deserialize<List<MaterialDef>>(materialsJson, JsonOptions) ?? new(),
        JsonSerializer.Deserialize<List<ItemDef>>(itemsJson, JsonOptions) ?? new());
}
