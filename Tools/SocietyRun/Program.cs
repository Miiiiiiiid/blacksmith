using System.Text;
using Blacksmith.Core.Society;

// 사회 시뮬레이션을 여러 시드로 돌려 결과를 요약한다.
// dotnet run --project Tools/SocietyRun -- [시드 수] [최대 세대] [연대기를 볼 시드]
var seeds = args.Length > 0 ? int.Parse(args[0]) : 200;
var maxGenerations = args.Length > 1 ? int.Parse(args[1]) : 40;
var chronicleSeed = args.Length > 2 ? int.Parse(args[2]) : 1;

var runs = Enumerable.Range(1, seeds).Select(seed =>
{
    var sim = new SocietySimulation(seed);
    sim.Run(maxGenerations);
    return sim;
}).ToList();

var report = new StringBuilder();
var won = runs.Where(r => r.State.Won).ToList();
report.AppendLine($"시드 {seeds}개, 최대 {maxGenerations}세대");
report.AppendLine($"마왕 토벌: {won.Count}개 ({100.0 * won.Count / seeds:0}%)");
if (won.Count > 0)
{
    var gens = won.Select(r => r.State.History.Count).OrderBy(g => g).ToList();
    report.AppendLine($"토벌까지 세대: 최소 {gens.First()}, 중간 {gens[gens.Count / 2]}, 최대 {gens.Last()}");
}
report.AppendLine($"완전 몰락 횟수: 평균 {runs.Average(r => r.State.Collapses):0.0}, 최대 {runs.Max(r => r.State.Collapses)}");

var all = runs.SelectMany(r => r.State.History).ToList();
report.AppendLine();
report.AppendLine("세대 끝 침공 결과 (전체 세대)");
foreach (var group in all.GroupBy(h => h.Outcome).OrderBy(g => g.Key))
    report.AppendLine($"  {group.Key}: {group.Count()}");

report.AppendLine();
report.AppendLine("세대별 중간값 (그 세대까지 끝나지 않은 시드만)");
report.AppendLine("세대  시드  인구    단계별 비율(잔존/마을/도시/국가)  군사력  마왕군");
foreach (var group in all.GroupBy(h => h.Generation).OrderBy(g => g.Key).Take(15))
{
    var stages = string.Join("/", Enum.GetValues<Stage>().Select(s => $"{100.0 * group.Count(h => h.Stage == s) / group.Count():0}"));
    report.AppendLine($"{group.Key,4}  {group.Count(),4}  {Median(group.Select(h => (double)h.Population)),6:0}  {stages,-30}  {Median(group.Select(h => h.Military)),6:0}  {Median(group.Select(h => h.Enemy)),6:0}");
}

static double Median(IEnumerable<double> values)
{
    var v = values.OrderBy(x => x).ToList();
    return v[v.Count / 2];
}

string Spread(IEnumerable<double> values)
{
    var v = values.OrderBy(x => x).ToList();
    return v.Count == 0 ? "-" : $"하위10% {v[v.Count / 10]:0} / 중간 {v[v.Count / 2]:0} / 상위10% {v[v.Count * 9 / 10]:0}";
}
var first = all.Where(h => h.Generation == 1).ToList();
report.AppendLine();
report.AppendLine($"1세대 끝 인구: {Spread(first.Select(h => (double)h.Population))}");
report.AppendLine($"1세대 끝 군사력: {Spread(first.Select(h => h.Military))}");

report.AppendLine("완전 몰락 횟수별 시드 수: " + string.Join(", ", runs.GroupBy(r => r.State.Collapses).OrderBy(g => g.Key).Select(g => $"{g.Key}번 {g.Count()}개")));
report.AppendLine("몰락을 가장 많이 겪은 시드: " + string.Join(", ", runs.OrderByDescending(r => r.State.Collapses).Take(5).Select(r => $"{r.State.Seed}({r.State.Collapses}번)")));
var sample = runs[chronicleSeed - 1];
report.AppendLine();
report.AppendLine($"시드 {chronicleSeed}의 연대기");
foreach (var e in sample.State.Chronicle)
{
    var year = e.Turn / SocietyRules.TurnsPerYear + 1;
    report.AppendLine($"  {e.Generation}세대 {year,2}년  {e.Text}");
}
report.AppendLine();
report.AppendLine($"시드 {chronicleSeed}의 세대 요약");
foreach (var h in sample.State.History)
{
    var stats = string.Join(", ", h.Stats.Select(kv => $"{SocietySimulation.StatName(kv.Key, h.Stage)} {kv.Value:0}"));
    report.AppendLine($"  {h.Generation}세대: 인구 {h.Population} ({h.Stage}) 군사력 {h.Military:0} vs 마왕군 {h.Enemy:0} → {h.Outcome}, 남은 인구 {h.PopulationAfter} | {stats}");
}

Console.Write(report);
