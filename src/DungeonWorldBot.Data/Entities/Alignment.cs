namespace DungeonWorldBot.Data.Entities;

public enum Alignment
{
    Unknown,
    LawfulGood,
    NeutralGood,
    ChaoticGood,
    LawfulNeutral,
    TrueNeutral,
    ChaoticNeutral,
    LawfulEvil,
    ChaoticEvil,
    Unaligned,

    // Appended rather than placed after LawfulEvil so the int values already stored in the database don't shift
    NeutralEvil
}