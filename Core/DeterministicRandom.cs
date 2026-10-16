namespace Blacksmith.Core;

/// <summary>
/// 시드, 날짜, 사건 이름으로 결과가 정해지는 난수.
/// 같은 상황을 다시 불러와도 같은 결과가 나와서 세이브-로드로 운을 바꿀 수 없다.
/// </summary>
public static class DeterministicRandom
{
    public static double Roll(int seed, int day, string eventKey)
    {
        // FNV-1a 해시. string.GetHashCode는 실행마다 달라져서 쓰지 않는다.
        unchecked
        {
            uint hash = 2166136261;
            void Mix(int value)
            {
                for (var i = 0; i < 4; i++)
                {
                    hash ^= (byte)(value >> (8 * i));
                    hash *= 16777619;
                }
            }
            Mix(seed);
            Mix(day);
            foreach (var c in eventKey) Mix(c);
            return new Random((int)hash).NextDouble();
        }
    }
}
