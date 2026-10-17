namespace Blacksmith.Core;

/// <summary>제작 미니게임 종류. 망치질은 타이밍 맞추기, 풀무질은 스페이스 연타, 세공은 방향키 따라 누르기.</summary>
public enum Minigame { Hammer, Bellows, Arrows }

public enum Arrow { Up, Down, Left, Right }

/// <summary>
/// 제작 미니게임의 점수 규칙. 시간 흐름과 입력은 엔진이 맡고, 여기서는 위치와 점수만 계산한다.
/// 제작 한 번에 미니게임 3개를 무작위로(겹칠 수 있게) 골라 하고, 품질은 세 점수의 평균이다.
/// 한 종류를 못하는 사람도 어떤 품목이든 잘 만들 기회가 있게 하려고 품목과 상관없이 고른다.
/// </summary>
public static class ForgingMinigame
{
    public const int RoundsPerCraft = 3;
    /// <summary>망치질 표시가 한 번 왕복하는 시간(초).</summary>
    public const double HammerPeriod = 1.2;
    /// <summary>망치질 초록 구간의 가운데 위치(0~1).</summary>
    public const double HammerTarget = 0.5;
    public const int ArrowCount = 6;
    public const double BellowsSeconds = 3.0;

    /// <summary>이번 제작에서 할 미니게임들. 같은 게 여러 번 나올 수 있다.</summary>
    public static IReadOnlyList<Minigame> PickRounds(Random random) =>
        Enumerable.Range(0, RoundsPerCraft).Select(_ => (Minigame)random.Next(3)).ToArray();

    /// <summary>적정 구간의 반폭. 힘이 높을수록 넓어져서 맞추기 쉬워진다.</summary>
    public static double ZoneHalfWidth(double strength) =>
        0.05 + Ratio(strength) * 0.10;

    /// <summary>0에서 1로 갔다가 돌아오는 표시 위치. 시간 t초일 때.</summary>
    public static double MarkerPosition(double t, double periodSeconds)
    {
        var phase = (t / periodSeconds) % 1.0;
        if (phase < 0) phase += 1;
        return phase < 0.5 ? phase * 2 : 2 - phase * 2;
    }

    /// <summary>구간 안이면 1점, 구간 밖으로 반폭만큼 더 벗어나면 0점까지 줄어든다.</summary>
    public static double ScoreTiming(double position, double target, double halfWidth)
    {
        var distance = Math.Abs(position - target);
        if (distance <= halfWidth) return 1;
        return Math.Max(0, 1 - (distance - halfWidth) / halfWidth);
    }

    /// <summary>풀무질 만점에 필요한 연타 수. 힘이 높을수록 적게 눌러도 된다. 20번에서 14번까지.</summary>
    public static int BellowsTarget(double strength) =>
        (int)Math.Round(20 - 6 * Ratio(strength));

    public static double ScoreBellows(int presses, double strength) =>
        Math.Clamp((double)presses / BellowsTarget(strength), 0, 1);

    /// <summary>세공 제한 시간(초). 섬세함이 높을수록 여유롭다. 4초에서 6초까지.</summary>
    public static double ArrowSeconds(double finesse) => 4 + 2 * Ratio(finesse);

    public static IReadOnlyList<Arrow> RandomArrows(Random random) =>
        Enumerable.Range(0, ArrowCount).Select(_ => (Arrow)random.Next(4)).ToArray();

    /// <summary>맞게 누른 방향키 비율.</summary>
    public static double ScoreArrows(int correct) => Math.Clamp((double)correct / ArrowCount, 0, 1);

    /// <summary>미니게임 점수들을 합친 품질 0~1.</summary>
    public static double Combine(IReadOnlyList<double> scores) =>
        scores.Count == 0 ? 0 : Math.Clamp(scores.Average(), 0, 1);

    private static double Ratio(double stat) => Math.Clamp(stat / GameRules.StatMax, 0, 1);
}
