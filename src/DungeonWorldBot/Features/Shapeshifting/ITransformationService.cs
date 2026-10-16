using DungeonWorldBot.Data.Entities;

namespace DungeonWorldBot.Features.Shapeshifting;

public interface ITransformationService
{
    Task<Transformation> LearnAsync(Character character, string name, string? description, string? moves);

    /// <summary>
    /// Changes what's known about a form. Null leaves that part as it is.
    /// </summary>
    Task UpdateAsync(Transformation form, string? description, string? moves);

    Task ForgetAsync(Character character, Transformation form);

    /// <summary>
    /// Takes these forms (merged, when there's more than one), leaving any other form.
    /// </summary>
    Task ShiftAsync(Character character, IReadOnlyList<Transformation> forms, int hold);

    /// <summary>
    /// Returns to natural form and loses any hold left.
    /// </summary>
    /// <returns>The forms the character was in.</returns>
    Task<IReadOnlyList<Transformation>> RevertAsync(Character character);
}
