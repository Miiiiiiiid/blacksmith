using Blacksmith.Core;
using Blacksmith.Core.Society;

/// <summary>기여 환산표와 하드 난이도의 값을 바꿔가며 스타일별 결과를 본다.</summary>
static class Tuning
{
    public static void Run(int seeds)
    {
        // 환산표 전체에 같은 배율을 곱해 본다. 1배가 지금 값이다.
        var request = ContributionRules.RequestYield;
        var gear = ContributionRules.GearAbility;
        var serve = ContributionRules.ServeYield;
        var masterwork = ContributionRules.MasterworkGain;
        var research = ContributionRules.ResearchGain;
        foreach (var k in new[] { 0.6, 0.8, 1.0, 1.2, 1.5 })
        {
            ContributionRules.RequestYield = request * k;
            ContributionRules.GearAbility = gear * k;
            ContributionRules.ServeYield = serve * k;
            ContributionRules.MasterworkGain = masterwork * k;
            ContributionRules.ResearchGain = research * k;
            Console.WriteLine($"환산표 {k}배");
            foreach (var d in new[] { Difficulty.Normal, Difficulty.Hard })
                foreach (var bot in Bots.All)
                    Console.WriteLine($"  {(d == Difficulty.Normal ? "보통" : "하드")} {bot.Name,-10} {Summary(seeds, d, bot)}");
        }
        ContributionRules.RequestYield = request;
        ContributionRules.GearAbility = gear;
        ContributionRules.ServeYield = serve;
        ContributionRules.MasterworkGain = masterwork;
        ContributionRules.ResearchGain = research;

        foreach (var ratio in new[] { 1.1, 1.2, 1.3, 1.4 })
            foreach (var absorb in new[] { 1.0, 1.5 })
            {
                SocietyRules.HardBossHealthRatio = ratio;
                SocietyRules.HardBossAbsorbRatio = absorb;
                Console.WriteLine($"하드: 마왕 체력 {ratio}배, 뚫렸을 때 흡수 {absorb}배");
                foreach (var bot in Bots.All)
                    Console.WriteLine($"  {bot.Name,-10} {Summary(seeds, Difficulty.Hard, bot)}");
            }
    }

    public static string Summary(int seeds, Difficulty difficulty, IPlayStrategy? bot)
    {
        var sims = new SocietySimulation[seeds];
        Parallel.For(0, seeds, i =>
        {
            var sim = new SocietySimulation(i + 1, difficulty, bot);
            sim.Run(40);
            sims[i] = sim;
        });
        var won = sims.Where(r => r.State.Won).Select(r => r.State.History.Count).OrderBy(x => x).ToList();
        var gens = won.Count == 0 ? "-" : $"{won[won.Count / 2]}/{won.Max()}";
        var first = 100.0 * sims.Count(r => r.State.Won && r.State.History.Count == 1) / seeds;
        var clean = 100.0 * sims.Count(r => r.State.Collapses == 0) / seeds;
        return $"토벌 {100.0 * won.Count / seeds,3:0}%  1세대 {first,3:0}%  세대 {gens,6}  안 무너짐 {clean,3:0}%";
    }
}
