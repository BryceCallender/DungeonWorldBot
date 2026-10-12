using System.Security.Cryptography;
using d20Tek.DiceNotation.DieRoller;

namespace DungeonWorldBot.Rolling;

/// <summary>
/// Rolls each die with the OS cryptographic random number generator.
/// </summary>
/// <remarks>
/// <see cref="RandomNumberGenerator.GetInt32(int, int)"/> is uniform for every die size (no modulo bias) and is
/// thread-safe, unlike the library's CryptoDieRoller (one byte per roll, so uneven above d2 and unable to reach
/// most faces of a d1000) or RandomDieRoller (a shared, non-thread-safe <see cref="Random"/>).
/// </remarks>
public sealed class SecureDieRoller : RandomDieRollerBase
{
    protected override int GetNextRandom(int sides)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sides, 1);
        return RandomNumberGenerator.GetInt32(1, sides + 1);
    }
}
