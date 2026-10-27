using DungeonWorldBot.Data.Entities;

namespace DungeonWorldBot.Features.Classes.Guides;

/// <summary>
/// A slash command to run, and what to pick in it, e.g. "/move class" with "Channel".
/// </summary>
/// <param name="Pick">The option to choose in it, like a move or resource name. Null when there's nothing to pick.</param>
/// <param name="Note">A word on what it does, like the stat a move rolls ("+WIS").</param>
public record GuideCommand(string Command, string? Pick = null, string? Note = null);

/// <summary>
/// One step of playing a class: what happens at the table, and the commands that do it on Discord.
/// </summary>
/// <param name="Text">Discord-style markdown: **bold**, *italic* and `code`.</param>
public record GuideStep(string Title, string Text, IReadOnlyList<GuideCommand> Commands);

/// <summary>
/// How to play a class through the bot, from making the character to levelling up.
/// </summary>
/// <param name="Pitch">The class in a sentence or two: what it does and what the bot tracks for it.</param>
public record ClassGuide(ClassType Class, string Pitch, IReadOnlyList<GuideStep> Steps);
