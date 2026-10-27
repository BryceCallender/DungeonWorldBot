using DungeonWorldBot.Dashboard.Models.Shared;

namespace DungeonWorldBot.Dashboard.Services.Shared;

/// <summary>
/// Where the DM's plain form posts go once they're done: back to their page, with the mistake (and the dialog to show
/// it in) if there was one. app.js reopens the dialog and takes the mistake back out of the address.
/// </summary>
public static class DeskForms
{
    /// <param name="page">The page the form was on, e.g. "/npcs".</param>
    /// <param name="dialog">The dialog to reopen with the mistake, or null to show it at the top of the page.</param>
    /// <param name="anchor">The part of the page to come back to, e.g. "npc-3".</param>
    public static IResult Back(string page, string? error, string? dialog = null, string? anchor = null)
    {
        var query = error is null ? "" : $"?{(dialog is null ? "" : $"dialog={dialog}&")}error={Uri.EscapeDataString(error)}";
        return Results.LocalRedirect($"{page}{query}{(anchor is null ? "" : $"#{anchor}")}");
    }

    /// <summary>
    /// Which way a quick HP button moves someone's health, from its value.
    /// </summary>
    public static HpChange? Change(string? value) =>
        Enum.TryParse<HpChange>(value, ignoreCase: true, out var change) ? change : null;
}
