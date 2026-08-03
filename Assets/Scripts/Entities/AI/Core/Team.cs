/// <summary>
/// Which side an entity belongs to. Drives how an AI reacts when its vision picks something up:
/// hostiles get chased and attacked, friendlies get approached and interacted with, neutrals get
/// ignored.
/// </summary>
public enum Team
{
    Neutral = 0,
    Player = 1,
    Security = 2,
    Bandits = 3,
}

public enum TeamRelation
{
    Neutral,
    Friendly,
    Hostile,
}

public static class Teams
{
    /// <summary>
    /// Default relation table, deliberately rule-based rather than a hand-authored matrix: anything
    /// involving <see cref="Team.Neutral"/> is neutral, matching sides are friendly, and any two
    /// different non-neutral sides are hostile. That covers Player-vs-Security and
    /// Security-vs-Bandits without needing per-pair setup; swap this for a lookup table if a
    /// faction ever needs an asymmetric or non-obvious stance.
    /// </summary>
    public static TeamRelation Relation(Team self, Team other)
    {
        if (self == Team.Neutral || other == Team.Neutral)
            return TeamRelation.Neutral;

        return self == other ? TeamRelation.Friendly : TeamRelation.Hostile;
    }

    public static bool IsHostile(Team self, Team other) => Relation(self, other) == TeamRelation.Hostile;
    public static bool IsFriendly(Team self, Team other) => Relation(self, other) == TeamRelation.Friendly;
}
