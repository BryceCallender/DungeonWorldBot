using DungeonWorldBot.Data.Entities;

namespace DungeonWorldBot.Features.Techniques;

public interface ITechniqueService
{
    /// <remarks>
    /// Check <see cref="Rules.TechniqueRules.WhyCannotDevelop"/> and <see cref="Rules.TechniqueRules.WhyCannotHave"/> first.
    /// </remarks>
    Task<Technique> DevelopAsync(Character character, string name, TechniqueTag tags, bool signature);

    /// <summary>
    /// Renames the Technique or replaces its tags. Null leaves that part as it is.
    /// </summary>
    Task UpdateAsync(Technique technique, string? name, TechniqueTag? tags);

    Task ForgetAsync(Character character, Technique technique);

    /// <summary>
    /// Takes away Barrier armor from the last Channel, which only lasts until the next one.
    /// </summary>
    /// <returns>The armor removed.</returns>
    Task<IReadOnlyList<ArmorBonus>> DropBarrierAsync(Character character);

    /// <summary>
    /// Gives the character their Barrier armor until their next Channel.
    /// </summary>
    /// <returns>How much armor it gave.</returns>
    Task<int> RaiseBarrierAsync(Character character);
}
