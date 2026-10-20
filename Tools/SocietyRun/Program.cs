using Blacksmith.Core;
using System.Text;
using Blacksmith.Core.Society;

// 사회 시뮬레이션을 여러 시드로 돌려 난이도와 플레이 스타일별 결과를 요약한다.
// dotnet run --project Tools/SocietyRun -- [시드 수] [최대 세대] [연대기를 볼 시드] [연대기의 봇 번호(0 = 주인공 없음)]
// dotnet run --project Tools/SocietyRun -- tune [시드 수] : 기여 환산표의 값을 바꿔가며 스타일별 결과를 본다.
if (args.Length > 0 && args[0] == "tune")
{
    Tuning.Run(args.Length > 1 ? int.Parse(args[1]) : 100);
    return;
}
var seeds = args.Length > 0 ? int.Parse(args[0]) : 200;
var maxGenerations = args.Length > 1 ? int.Parse(args[1]) : 40;
var chronicleSeed = args.Length > 2 ? int.Parse(args[2]) : 1;
var chronicleBot = args.Length > 3 ? int.Parse(args[3]) : 5;

var strategies = new List<IPlayStrategy?> { null };
strategies.AddRange(Bots.All);
string NameOf(IPlayStrategy? s) => s?.Name ?? "주인공 없음";

List<SocietySimulation> RunAll(Difficulty difficulty, IPlayStrategy? strategy) =>
    Enumerable.Range(1, seeds).Select(seed =>
    {
        var sim = new SocietySimulation(seed, difficulty, strategy);
        sim.Run(maxGenerations);
        return sim;
    }).ToList();

static double Median(IEnumerable<double> values)
{
    var v = values.OrderBy(x => x).ToList();
    return v.Count == 0 ? 0 : v[v.Count / 2];
}

var report = new StringBuilder();
report.AppendLine($"시드 {seeds}개, 최대 {maxGenerations}세대");
report.AppendLine("목표: 보통은 토벌까지 중간 3세대, 최대 8세대, 한 번도 안 무너짐 약 68%. 하드는 1세대 토벌 5% 안팎.");

foreach (var difficulty in new[] { Difficulty.Normal, Difficulty.Hard })
{
    report.AppendLine();
    report.AppendLine($"{(difficulty == Difficulty.Normal ? "보통" : "하드")} (마왕 첫 체력 {SocietyRules.BossHealth * SocietySimulation.BossHealthRatio(difficulty):0})");
    report.AppendLine("스타일          토벌   1세대 토벌  토벌까지 세대(중간/최대)  완전 몰락(평균)  한 번도 안 무너짐  1세대 수비력/공격대(중간)");
    foreach (var strategy in strategies)
    {
        var sims = RunAll(difficulty, strategy);
        var won = sims.Where(r => r.State.Won).ToList();
        var gens = won.Count == 0 ? "-" : $"{Median(won.Select(r => (double)r.State.History.Count)):0}/{won.Max(r => r.State.History.Count)}";
        var firstGen = 100.0 * sims.Count(r => r.State.Won && r.State.History.Count == 1) / seeds;
        var clean = 100.0 * sims.Count(r => r.State.Collapses == 0) / seeds;
        report.AppendLine($"{NameOf(strategy),-12}  {100.0 * won.Count / seeds,4:0}%  {firstGen,9:0}%  {gens,22}  {sims.Average(r => r.State.Collapses),14:0.0}  {clean,14:0}%  {Median(sims.Select(r => r.State.History[0].Defense)),8:0}/{Median(sims.Select(r => r.State.History[0].Strike)),0:0}");
    }
}

var bot = strategies[Math.Clamp(chronicleBot, 0, strategies.Count - 1)];
var sample = new SocietySimulation(chronicleSeed, Difficulty.Normal, bot);
sample.Run(maxGenerations);
report.AppendLine();
report.AppendLine($"시드 {chronicleSeed}, 보통, {NameOf(bot)}의 연대기");
foreach (var e in sample.State.Chronicle.Take(80))
{
    var year = e.Turn / SocietyRules.TurnsPerYear + 1;
    report.AppendLine($"  {e.Generation}세대 {year,2}년  {e.Text}");
}
report.AppendLine();
report.AppendLine($"시드 {chronicleSeed}, 보통, {NameOf(bot)}의 세대 요약");
foreach (var h in sample.State.History.Take(20))
{
    var stats = string.Join(", ", h.Stats.Select(kv => $"{SocietySimulation.StatName(kv.Key, h.Stage)} {kv.Value:0}"));
    report.AppendLine($"  {h.Generation}세대: 인구 {h.Population} ({h.Stage}) 수비력 {h.Defense:0}, 공격대 {h.Strike:0} vs 마왕 {h.Boss:0} → {h.Outcome}, 남은 인구 {h.PopulationAfter}, 다음 마왕 {h.BossAfter:0} | {stats}");
}
var p = sample.State.Player;
report.AppendLine($"  마지막 주인공: 솜씨 {p.Skill:0.0}, 역작 위력 {p.MasterworkPower:0}, 연구 {p.Research:0}, 행동 {string.Join(", ", p.ActionCounts.Select(kv => $"{kv.Key} {kv.Value}"))}");

Console.Write(report);
