namespace DungeonWorldBot.Data.Entities;

/// <summary>
/// How hurt an enemy looks, without giving away its HP.
/// </summary>
public enum EnemyCondition
{
    Unhurt,
    Hurt,
    Bloodied,
    NearDeath,
    Defeated,
}

public static class EnemyConditions
{
    public static EnemyCondition Of(int currentHP, int maxHP)
    {
        if (currentHP <= 0)
            return EnemyCondition.Defeated;
        if (currentHP >= maxHP)
            return EnemyCondition.Unhurt;

        var percent = currentHP * 100 / Math.Max(1, maxHP);
        return percent switch
        {
            <= 25 => EnemyCondition.NearDeath,
            <= 50 => EnemyCondition.Bloodied,
            _ => EnemyCondition.Hurt,
        };
    }

    public static string Describe(EnemyCondition condition) => condition switch
    {
        EnemyCondition.NearDeath => "Near death",
        _ => condition.ToString(),
    };
}
