using DungeonWorldBot.Features.Classes.Guides;

namespace DungeonWorldBot.Dashboard.Models.Bridge;

/// <summary>
/// The Play panel's commands for a character, in the order they're reached for at the table.
/// </summary>
/// <param name="First">Their class and subclass moves, what their class's mechanics do, their resources, and the basic
/// moves.</param>
/// <param name="Rest">Everything else the guide lists for them, like reading their sheet or making camp.</param>
public record PanelSections(IReadOnlyList<GuideStep> First, IReadOnlyList<GuideStep> Rest);
