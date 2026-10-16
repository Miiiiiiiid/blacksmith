using System.Text.Json;

namespace Blacksmith.Core;

/// <summary>게임 상태를 JSON 문자열로 바꾼다. 파일 쓰기는 엔진 쪽이 맡는다.</summary>
public static class SaveSystem
{
    public static string Serialize(GameState state) =>
        JsonSerializer.Serialize(state, GameData.JsonOptions);

    public static GameState Deserialize(string json) =>
        JsonSerializer.Deserialize<GameState>(json, GameData.JsonOptions)
        ?? throw new InvalidDataException("저장 파일을 읽을 수 없습니다.");
}
