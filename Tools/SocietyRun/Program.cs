using System.Text;
using Blacksmith.Core.Society;

// 사회 시뮬레이션을 여러 시드로 돌려 결과를 요약한다.
// dotnet run --project Tools/SocietyRun -- [시드 수] [최대 세대] [연대기를 볼 시드] [연대기의 보정 배율]
var seeds = args.Length > 0 ? int.Parse(args[0]) : 200;
var maxGenerations = args.Length > 1 ? int.Parse(args[1]) : 40;
var chronicleSeed = args.Length > 2 ? int.Parse(args[2]) : 1;
var chronicleBonus = args.Length > 3 ? double.Parse(args[3]) : 1;

List<SocietySimulation> RunAll(double bonus) => Enumerable.Range(1, seeds).Select(seed =>
{
    var sim = new SocietySimulation(seed) { DefenseBonus = bonus, StrikeBonus = bonus };
    sim.Run(maxGenerations);
    return sim;
}).ToList();

static double Median(IEnumerable<double> values)
{
    var v = values.OrderBy(x => x).ToList();
    return v.Count == 0 ? 0 : v[v.Count / 2];
}

static string Spread(IEnumerable<double> values)
{
    var v = values.OrderBy(x => x).ToList();
    return v.Count == 0 ? "-" : $"하위10% {v[v.Count / 10]:0} / 중간 {v[v.Count / 2]:0} / 상위10% {v[v.Count * 9 / 10]:0}";
}

var report = new StringBuilder();
report.AppendLine($"시드 {seeds}개, 최대 {maxGenerations}세대");
report.AppendLine();
report.AppendLine("주인공 보정별 결과 (수비력과 공격대에 같은 배율을 곱함. 1배 = 주인공 없음)");
report.AppendLine("배율   토벌 비율  토벌까지 세대(중간/최대)  완전 몰락(평균)  한 번도 안 무너짐");
foreach (var bonus in new[] { 0.5, 1.0, 1.5, 2, 3 })
{
    var sims = RunAll(bonus);
    var won = sims.Where(r => r.State.Won).ToList();
    var gens = won.Count == 0 ? "-" : $"{Median(won.Select(r => (double)r.State.History.Count)):0}/{won.Max(r => r.State.History.Count)}";
    var clean = 100.0 * sims.Count(r => r.State.Collapses == 0) / seeds;
    report.AppendLine($"{bonus,4}배  {100.0 * won.Count / seeds,7:0}%  {gens,22}  {sims.Average(r => r.State.Collapses),14:0.0}  {clean,14:0}%");
}

var runs = RunAll(1);
var all = runs.SelectMany(r => r.State.History).ToList();
report.AppendLine();
report.AppendLine("주인공 없음: 마왕이 깨어났을 때의 결과 (전체 세대)");
foreach (var group in all.GroupBy(h => h.Outcome).OrderBy(g => g.Key))
    report.AppendLine($"  {group.Key}: {group.Count()}");

report.AppendLine();
report.AppendLine("주인공 없음: 세대별 중간값");
report.AppendLine("세대  인구    수비력  공격대  마왕 체력");
foreach (var group in all.GroupBy(h => h.Generation).OrderBy(g => g.Key).Take(10))
    report.AppendLine($"{group.Key,4}  {Median(group.Select(h => (double)h.Population)),6:0}  {Median(group.Select(h => h.Defense)),6:0}  {Median(group.Select(h => h.Strike)),6:0}  {Median(group.Select(h => h.Boss)),8:0}");

var first = all.Where(h => h.Generation == 1).ToList();
report.AppendLine();
report.AppendLine($"1세대 끝 인구: {Spread(first.Select(h => (double)h.Population))}");
report.AppendLine($"1세대 끝 수비력: {Spread(first.Select(h => h.Defense))}");

var sample = new SocietySimulation(chronicleSeed) { DefenseBonus = chronicleBonus, StrikeBonus = chronicleBonus };
sample.Run(maxGenerations);
report.AppendLine();
report.AppendLine($"시드 {chronicleSeed}, 보정 {chronicleBonus}배의 연대기");
foreach (var e in sample.State.Chronicle.Take(80))
{
    var year = e.Turn / SocietyRules.TurnsPerYear + 1;
    report.AppendLine($"  {e.Generation}세대 {year,2}년  {e.Text}");
}
report.AppendLine();
report.AppendLine($"시드 {chronicleSeed}, 보정 {chronicleBonus}배의 세대 요약");
foreach (var h in sample.State.History.Take(20))
{
    var stats = string.Join(", ", h.Stats.Select(kv => $"{SocietySimulation.StatName(kv.Key, h.Stage)} {kv.Value:0}"));
    report.AppendLine($"  {h.Generation}세대: 인구 {h.Population} ({h.Stage}) 수비력 {h.Defense:0}, 공격대 {h.Strike:0} vs 마왕 {h.Boss:0} → {h.Outcome}, 남은 인구 {h.PopulationAfter}, 다음 마왕 {h.BossAfter:0} | {stats}");
}

Console.Write(report);
