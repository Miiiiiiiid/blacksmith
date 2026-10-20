namespace Blacksmith.Core.Society;

/// <summary>플레이 스타일별 봇. 기여 환산표가 여러 스타일을 다 살리는지 확인하는 데 쓴다.</summary>
public static class Bots
{
    public static IReadOnlyList<IPlayStrategy> All { get; } = new IPlayStrategy[]
    {
        new RequestBot(), new GearBot(), new MasterworkBot(), new NoForgeBot(), new BalancedBot(),
    };

    /// <summary>지금 가장 급한 분야. 굶주리면 식량, 수비가 모자라면 군사나 방벽, 아니면 공격대.</summary>
    public static SocietyStat Neediest(SocietySimulation sim)
    {
        if (sim.FoodSecurity < 0.5) return SocietyStat.Food;
        var boss = sim.State.Boss;
        if (sim.Defense * sim.ResearchMultiplier < boss * 1.3)
        {
            var wallsOpen = sim.CurrentStage >= Stage.Town;
            return wallsOpen && sim.Stat(SocietyStat.Walls) < sim.Stat(SocietyStat.Combat) * 0.6 ? SocietyStat.Walls : SocietyStat.Combat;
        }
        return SocietyStat.Strike;
    }

    /// <summary>의뢰만 받아 그때그때 급한 분야를 채운다.</summary>
    public sealed class RequestBot : IPlayStrategy
    {
        public string Name => "의뢰형";
        public PlayerAction Choose(SocietySimulation sim) => new(ActionKind.Request, Neediest(sim));
    }

    /// <summary>대표자 맞춤 장비로 사람을 키운다. 느리지만 쌓인다.</summary>
    public sealed class GearBot : IPlayStrategy
    {
        public string Name => "장비형";
        public PlayerAction Choose(SocietySimulation sim) => new(ActionKind.Gear, Neediest(sim));
    }

    /// <summary>무기 하나만 판다. 역작이 충분하면 선제 일격, 아니면 용사에게 들려 공격대에 보탠다.</summary>
    public sealed class MasterworkBot : IPlayStrategy
    {
        public string Name => "역작 올인";
        public PlayerAction Choose(SocietySimulation sim) => new(ActionKind.Masterwork);
    }

    /// <summary>대장간을 건드리지 않는다. 대표자 일을 거들고 마왕을 연구한다.</summary>
    public sealed class NoForgeBot : IPlayStrategy
    {
        public string Name => "대장간 안 씀";
        public PlayerAction Choose(SocietySimulation sim) =>
            sim.State.Turn % 4 == 3 ? new(ActionKind.Research) : new(ActionKind.Serve, Neediest(sim));
    }

    /// <summary>의뢰, 장비, 연구, 역작을 돌아가며 한다.</summary>
    public sealed class BalancedBot : IPlayStrategy
    {
        public string Name => "균형형";
        public PlayerAction Choose(SocietySimulation sim) => (sim.State.Turn % 6) switch
        {
            0 or 1 => new(ActionKind.Request, Neediest(sim)),
            2 or 3 => new(ActionKind.Gear, Neediest(sim)),
            4 => new(ActionKind.Research),
            _ => new(ActionKind.Masterwork),
        };
    }
}
