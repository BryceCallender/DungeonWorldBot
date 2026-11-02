namespace DungeonWorldBot.Data.Entities;

/// <summary>
/// What changed on a character, for their timeline.
/// </summary>
public enum CharacterEventKind
{
    Created,
    LevelChanged,
    StatChanged,
    MaxHPChanged,
    ClassChanged,
    MoveLearned,
    MoveForgotten,
    DebilityTaken,
    DebilityHealed,
    BondMade,
    BondResolved,
    GoalSet,
    GoalCompleted,
    ItemGained,
    ItemLost,
    CoinsChanged,
    ShapeLearned,
    Died,
    Revived,
}
