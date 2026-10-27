using DungeonWorldBot.Dashboard.Services.Shared;

namespace DungeonWorldBot.Dashboard.Models.Shared;

/// <summary>
/// A mistake a form post sent back in the address (see <see cref="DeskForms.Back"/>): what was wrong, and the dialog to
/// reopen and show it in, or none to show it at the top of the page. Pages read both from the query and hand this to
/// their <c>Modal</c>s, and app.js takes them back out of the address once shown.
/// </summary>
public record FormErrors(string? Dialog, string? Error)
{
    public static readonly FormErrors None = new(null, null);

    /// <summary>
    /// The mistake for the top of the page, from a form that isn't in a dialog, like the quick HP buttons.
    /// </summary>
    public string? ForPage => Dialog is null ? Error : null;

    /// <summary>
    /// The mistake for one dialog, which also opens it as the page loads.
    /// </summary>
    public string? For(string dialog) => Dialog == dialog ? Error : null;
}
