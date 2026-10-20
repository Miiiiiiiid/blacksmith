namespace Blacksmith.Core.Society;

/// <summary>구성원 이름을 음절 두 개로 지어낸다.</summary>
public static class Names
{
    private static readonly string[] First = { "가", "나", "다", "라", "마", "바", "사", "아", "자", "하", "도", "로", "소", "우", "주", "혜", "윤", "서", "태", "민" };
    private static readonly string[] Second = { "람", "온", "린", "율", "솔", "결", "빈", "울", "란", "현", "든", "슬", "별", "담", "훈", "노", "이", "겸", "새", "한" };

    public static string Next(Random random) => First[random.Next(First.Length)] + Second[random.Next(Second.Length)];
}
