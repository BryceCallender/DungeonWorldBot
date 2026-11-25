namespace DungeonWorldBot.Dashboard.Services.Party;

public static class PartyServices
{
    /// <summary>
    /// Reading the party's characters and their timelines, the two things players change about them (their journals and
    /// pictures), and the moves they roll from their sheets.
    /// </summary>
    public static IServiceCollection AddParty(this IServiceCollection services) =>
        services
            .AddScoped<PartyReader>()
            .AddScoped<CharacterHistory>()
            .AddScoped<SheetRolls>()
            .AddSingleton<CharacterJournal>()
            .AddSingleton<PortraitStore>();
}
