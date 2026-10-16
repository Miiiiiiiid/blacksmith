namespace Blacksmith.Core;

public enum ForgingStage { Heat, Hammer, Quench }

/// <summary>
/// 무기 제작 미니게임의 점수 규칙. 움직이는 표시를 적정 구간에서 멈추는 타이밍 게임이 세 번 이어진다.
/// 시간 흐름과 입력은 엔진이 맡고, 여기서는 위치와 점수만 계산한다.
/// </summary>
public static class ForgingMinigame
{
    public const int HammerStrikes = 5;

    /// <summary>단계별 목표 위치(0~1)와 표시가 한 번 왕복하는 시간(초).</summary>
    public static (double Target, double PeriodSeconds) StageSetup(ForgingStage stage) => stage switch
    {
        ForgingStage.Heat => (0.75, 2.4),
        ForgingStage.Hammer => (0.5, 1.2),
        ForgingStage.Quench => (0.35, 1.8),
        _ => throw new ArgumentOutOfRangeException(nameof(stage)),
    };

    /// <summary>적정 구간의 반폭. 힘이 높을수록 넓어져서 맞추기 쉬워진다.</summary>
    public static double ZoneHalfWidth(double strength) =>
        0.05 + Math.Clamp(strength / GameRules.StatMax, 0, 1) * 0.10;

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

    /// <summary>세 단계를 합친 품질 0~1.</summary>
    public static double Combine(double heat, IReadOnlyList<double> hammers, double quench)
    {
        var hammer = hammers.Count == 0 ? 0 : hammers.Average();
        return Math.Clamp(0.3 * heat + 0.45 * hammer + 0.25 * quench, 0, 1);
    }
}
