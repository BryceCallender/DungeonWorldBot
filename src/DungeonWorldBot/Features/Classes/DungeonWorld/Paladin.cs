using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Spells;
using static DungeonWorldBot.Features.Classes.ClassMove;

namespace DungeonWorldBot.Features.Classes.DungeonWorld;

/// <summary>
/// Summarised from the Dungeon World SRD by Sage LaTorra and Adam Koebel (CC BY 3.0).
/// </summary>
public static class Paladin
{
    private const string ClericLevel = "Cleric Level";

    public static readonly ClassSheet Sheet = new(
        ClassType.Paladin,
        "Dungeon World",
        startingMoves:
        [
            new("Human", "When you pray for guidance, even for a moment, and ask \"What here is evil?\", the GM tells you honestly."),
            Rolled("Lay on Hands", StatType.Cha,
                "When you touch someone skin to skin and pray for their well-being",
                "Heal 1d8 damage or remove one disease.",
                "They're healed, but the damage or disease is transferred to you."),
            new("Armored", "Ignore the clumsy tag on armor you wear."),
            Rolled("I Am the Law", StatType.Cha,
                "When you give an NPC an order based on your divine authority",
                "They choose one, and you take +1 forward against them:",
                "They choose one:",
                miss: "They do as they please, and you take -1 forward against them.",
                options:
                [
                    "Do what you say",
                    "Back away cautiously, then flee",
                    "Attack you",
                ]),
            new("Quest",
                "When you dedicate yourself to a mission through prayer and ritual cleansing, declare it: slay a great blight, defend someone from the iniquities that beset them, or discover a truth. " +
                "Choose up to two boons:\n" +
                "• An unwavering sense of direction to ___\n• Invulnerability to ___ (edged weapons, fire, enchantment...)\n• A mark of divine authority\n" +
                "• Senses that pierce lies\n• A voice that transcends language\n• Freedom from hunger, thirst and sleep\n" +
                "The GM then sets your vows: Honor, Temperance, Piety, Valor, Truth or Hospitality."),
        ],
        features: [],
        level2To5:
        [
            new("Divine Favor", "Dedicate yourself to a deity. Gain the cleric's Commune and Cast a Spell moves as a level 1 cleric, gaining an effective cleric level each time you level up.") { GrantsSpells = new(SpellList.Cleric, ClericLevel) },
            new("Bloody Aegis", "When you take damage, you can grit your teeth and take a debility of your choice instead. You can't if you have all six."),
            new("Smite", "While on a quest, deal +1d4 damage."),
            new("Exterminatus", "When you vow aloud to defeat an enemy, deal +2d4 damage against them and -4 against everyone else until they're defeated. If you fail or give up, the effect lasts until you redeem yourself."),
            new("Charge!", "When you lead the charge into combat, those you lead take +1 forward."),
            new("Staunch Defender", "When you Defend, you always get +1 hold, even on a 6-."),
            new("Setup Strike", "When you Hack and Slash, choose an ally: their next attack on your target deals +1d4 damage."),
            new("Holy Protection", "+1 armor while on a quest."),
            new("Voice of Authority", "+1 to Order Hirelings."),
            new("Hospitaller", "When you heal an ally, heal +1d8 damage."),
        ],
        level6To10:
        [
            new("Evidence of Faith", "When you see divine magic happen, ask the GM which deity granted it and what it does. Take +1 acting on the answers.") { Requires = "Divine Favor" },
            new("Holy Smite", "While on a quest, deal +1d8 damage.") { Replaces = "Smite" },
            new("Ever Onward", "When you lead the charge into combat, those you lead take +1 forward and +2 armor forward.") { Replaces = "Charge!" },
            new("Impervious Defender", "When you Defend, you always get +1 hold, even on a 6-. On a 12+, instead of hold, the nearest attacker is stymied, giving you a clear advantage (the GM describes it).") { Replaces = "Staunch Defender" },
            new("Tandem Strike", "When you Hack and Slash, choose an ally: their next attack on your target deals +1d4 damage and they take +1 forward against it.") { Replaces = "Setup Strike" },
            new("Divine Protection", "+2 armor while on a quest.") { Replaces = "Holy Protection" },
            new("Divine Authority", "+1 to Order Hirelings. On a 12+, the hireling overcomes fear and doubt and carries out your order especially well.") { Replaces = "Voice of Authority" },
            new("Perfect Hospitaller", "When you heal an ally, heal +2d8 damage.") { Replaces = "Hospitaller" },
            new("Indomitable", "When you suffer a debility (even through Bloody Aegis), take +1 forward against whatever caused it."),
            new("Perfect Knight", "When you Quest, choose three boons instead of two."),
        ],
        resources:
        [
            new ClassResource(ClericLevel, "Your effective cleric level for spells: 1 when you take Divine Favor, +1 each level after. Learn spells with /spell learn.", Start: 1, GrantedBy: "Divine Favor")
            {
                ResetWithAll = false,
                GrowsWithLevel = true,
            },
        ]);
}
