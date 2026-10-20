namespace Blacksmith.Core.Society;

/// <summary>주인공이 한 턴에 하는 행동의 종류.</summary>
public enum ActionKind
{
    /// <summary>분야 하나의 의뢰를 받아 납품한다. 그 분야 수치가 바로 오른다.</summary>
    Request,
    /// <summary>분야 대표자에게 맞춤 장비를 만들어 준다. 대표자 능력이 올라 오래 쌓인다.</summary>
    Gear,
    /// <summary>역작의 공정 하나를 진행한다.</summary>
    Masterwork,
    /// <summary>마왕을 연구한다.</summary>
    Research,
    /// <summary>대장간 대신 대표자 일을 거든다(겸직).</summary>
    Serve,
}

public readonly record struct PlayerAction(ActionKind Kind, SocietyStat Stat = SocietyStat.Combat);

/// <summary>주인공. 세대가 바뀌면 다음 대장장이가 잇는다.</summary>
public sealed class Smith
{
    public double Skill { get; set; } = ContributionRules.StartingSkill;
    /// <summary>역작의 위력. 0이면 역작이 없음.</summary>
    public double MasterworkPower { get; set; }
    /// <summary>마왕 연구 성과.</summary>
    public double Research { get; set; }
    public Dictionary<ActionKind, int> ActionCounts { get; } = new();
}

/// <summary>주인공 대신 행동을 고르는 봇.</summary>
public interface IPlayStrategy
{
    string Name { get; }
    PlayerAction Choose(SocietySimulation sim);

    /// <summary>역작으로 선제 일격을 할지. 기본은 한 방에 죽일 수 있을 만큼 여유가 있을 때만.</summary>
    bool UseFirstStrike(SocietySimulation sim) =>
        sim.State.Player.MasterworkPower * sim.ResearchMultiplier >= sim.State.Boss * ContributionRules.FirstStrikeMargin;
}
