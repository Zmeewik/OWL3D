using System.Collections.Generic;
using UnityEngine;

/// <summary>What an <see cref="EnemyCommand"/> is telling its recipients about.</summary>
public enum EnemyCommandType
{
    /// <summary>Someone spotted a hostile. <see cref="EnemyCommand.target"/> is who.</summary>
    TargetSpotted,

    /// <summary>A previously spotted hostile was lost. Recipients should stand down.</summary>
    TargetLost,

    /// <summary>Go to <see cref="EnemyCommand.position"/>.</summary>
    MoveTo,

    /// <summary>Take up positions around <see cref="EnemyCommand.position"/> at the command's radius.</summary>
    SurroundPoint,

    /// <summary>Stay within the command's radius of <see cref="EnemyCommand.target"/> and engage anything hostile.</summary>
    Defend,

    /// <summary>Drop everything and idle where you are.</summary>
    HoldPosition,

    /// <summary>An alarm post was triggered. Rally to <see cref="EnemyCommand.position"/> combat-ready.</summary>
    AlarmRaised,
}

/// <summary>How the manager decides who receives a command.</summary>
public enum EnemyCommandScope
{
    /// <summary>Everyone registered, no filtering.</summary>
    All,

    /// <summary>Only entities within <see cref="EnemyCommand.radius"/> of <see cref="EnemyCommand.origin"/>.</summary>
    Radius,

    /// <summary>Only entities whose TeamMember.radioFrequency matches, at any distance.</summary>
    RadioFrequency,
}

/// <summary>
/// One event broadcast from <see cref="EnemyManager"/> to enemy strategies. Deliberately a plain
/// data struct rather than a method call per event type: strategies parse it themselves (see
/// <see cref="EnemyStrategy.HandleCommand"/>), so adding a new command type doesn't force every
/// existing strategy to grow a new method it doesn't care about.
///
/// Built through the static factories rather than a constructor so each call site reads as the
/// event it represents and can't silently leave a field that type depends on unset.
/// </summary>
public readonly struct EnemyCommand
{
    public readonly EnemyCommandType type;
    public readonly EnemyCommandScope scope;

    /// <summary>Who raised the event. May be null for commands issued by game logic rather than an entity.</summary>
    public readonly Transform source;

    /// <summary>Subject of the command -- the spotted hostile, or the thing to defend.</summary>
    public readonly TeamMember target;

    /// <summary>Where the command refers to. For TargetSpotted this is the target's last known position.</summary>
    public readonly Vector3 position;

    /// <summary>Meaning depends on scope (delivery range) and type (how far to surround / defend from).</summary>
    public readonly float radius;

    public readonly int radioFrequency;

    /// <summary>Which teams this command is meant for. Null means every recipient the scope selects.</summary>
    public readonly IReadOnlyList<Team> affectedTeams;

    /// <summary>Point the command is measured from for <see cref="EnemyCommandScope.Radius"/> delivery.</summary>
    public Vector3 origin => source != null ? source.position : position;

    private EnemyCommand(
        EnemyCommandType type,
        EnemyCommandScope scope,
        Transform source,
        TeamMember target,
        Vector3 position,
        float radius,
        int radioFrequency,
        IReadOnlyList<Team> affectedTeams = null)
    {
        this.type = type;
        this.scope = scope;
        this.source = source;
        this.target = target;
        this.position = position;
        this.radius = radius;
        this.radioFrequency = radioFrequency;
        this.affectedTeams = affectedTeams;
    }

    /// <summary>"I see a hostile" -- shouted to whoever is close enough to hear it.</summary>
    public static EnemyCommand TargetSpotted(Transform source, TeamMember target, float hearingRadius)
        => new(EnemyCommandType.TargetSpotted, EnemyCommandScope.Radius, source, target,
               target != null ? target.transform.position : Vector3.zero, hearingRadius, 0);

    /// <summary>"I see a hostile" -- relayed over the radio to everyone on the channel.</summary>
    public static EnemyCommand TargetSpottedOnRadio(Transform source, TeamMember target, int frequency)
        => new(EnemyCommandType.TargetSpotted, EnemyCommandScope.RadioFrequency, source, target,
               target != null ? target.transform.position : Vector3.zero, 0f, frequency);

    public static EnemyCommand TargetLost(Transform source, TeamMember target, int frequency)
        => new(EnemyCommandType.TargetLost, EnemyCommandScope.RadioFrequency, source, target,
               target != null ? target.transform.position : Vector3.zero, 0f, frequency);

    public static EnemyCommand MoveTo(Vector3 position, int frequency)
        => new(EnemyCommandType.MoveTo, EnemyCommandScope.RadioFrequency, null, null, position, 0f, frequency);

    public static EnemyCommand SurroundPoint(Vector3 position, float surroundRadius, int frequency)
        => new(EnemyCommandType.SurroundPoint, EnemyCommandScope.RadioFrequency, null, null, position,
               surroundRadius, frequency);

    public static EnemyCommand Defend(TeamMember target, float guardRadius, int frequency)
        => new(EnemyCommandType.Defend, EnemyCommandScope.RadioFrequency, null, target,
               target != null ? target.transform.position : Vector3.zero, guardRadius, frequency);

    public static EnemyCommand HoldPosition(int frequency)
        => new(EnemyCommandType.HoldPosition, EnemyCommandScope.RadioFrequency, null, null, Vector3.zero, 0f, frequency);

    /// <summary>
    /// "The alarm's up" -- level-wide, filtered to whichever teams the post that raised it affects.
    /// <see cref="radius"/> doubles as how wide a ring recipients rally in around <see cref="position"/>.
    /// </summary>
    public static EnemyCommand AlarmRaised(Vector3 postPosition, IReadOnlyList<Team> affectedTeams, float rallyRadius)
        => new(EnemyCommandType.AlarmRaised, EnemyCommandScope.All, null, null, postPosition, rallyRadius, 0, affectedTeams);
}
