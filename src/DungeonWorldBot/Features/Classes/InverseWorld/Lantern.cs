using DungeonWorldBot.Data.Entities;
using static DungeonWorldBot.Features.Classes.ClassMove;

namespace DungeonWorldBot.Features.Classes.InverseWorld;

public static class Lantern
{
    public static readonly ClassSheet Sheet = new(
        ClassType.Lantern,
        "Inverse World",
        new ClassColor("#c0365e", "#f27a98"),
        startingMoves:
        [
            new("Your Little Light",
                "You carry a piece of the god Sola: a glowing ball of light that floats where it likes but stays near you and rests in your lantern. " +
                "It usually obeys, though it's fickle. It can't speak, but it signals with colors and beams, always gives you light, can't touch anything, and can't be harmed. Give it a name."),
            new("Light Weaponry",
                "Command your little light to take one of these forms; it keeps it until it decides you need another. On a 6- while it's a weapon, it reverts to its harmless form and sulks for a while.\n" +
                "• **Arrows of Light:** near, mystical, piercing 2. Instead of spending ammo, lose 1 piercing until you next Make Camp.\n" +
                "• **Shield of Light:** +1 armor, which you can lend to anyone within reach.\n" +
                "*Mystical: Hack and Slash or Volley with it using +INT instead of STR or DEX.*"),
            Rolled("Bend Light", StatType.Cha,
                "When you convince natural light to obey you, choosing an effect",
                "It works perfectly.",
                "It works, but the light is fickle and it won't last long, so hurry.",
                miss: "The light is sick of being bossed around: the GM picks an effect and uses it against you.",
                options:
                [
                    "Command it to attack: briefly blind or stun a group within near range",
                    "Calm it until it's still: a wall of light blocks one passage",
                    "Praise it until it comes close: fill an area with light",
                    "Terrify it until it flees: shroud an area in darkness",
                ]),
            Rolled("Reveal the Way", StatType.Cha,
                "When you show a non-hostile NPC their best course of action",
                "They take it, in whatever way benefits them most.",
                "They aren't sure, but they're listening: you gain leverage over them.",
                note: "When a player asks your advice, tell them their best course of action. If they follow it, they take +1 forward. At session end, mark XP if someone who followed your advice benefited."),
        ],
        features:
        [
            new ClassFeature("Drive", "What drives you to bring light to the darkness?",
            [
                new("Drive: Emissary of Light", "Mark XP when you ease the suffering of a person or place."),
                new("Drive: Enemy of Darkness", "Mark XP when you take steps to destroy a place or creature of the shadows."),
                new("Drive: Revealer of Truth", "Mark XP when you discover a hidden truth and reveal it."),
            ]),
            new ClassFeature("Source of Light", "How did you get your little light?",
            [
                new("Gift from God", "Sola gave you the light himself. Arrows of Light have piercing 3, and the Shield of Light still protects you while lent out."),
                new("Hand-Me-Down", "Your mentor passed the light to you. When you Reveal the Way to your little light, you always take the 10+ result."),
                new("Stolen Property", "You ripped the light from Sola himself, and it learned from you: it can lift things up to 1 weight and often moves things around."),
            ]),
        ],
        level2To5:
        [
            new("Beacon of Inspiration", "On a 12+ Reveal the Way, the NPC does exactly what you said, trusting you completely, and thanks you as best they can if it works out."),
            new("Fist of Dawn",
                "Add a Light Weaponry form. **Fist of Dawn:** hand, no damage, mystical, forceful. Instead of dealing damage, send the target flying to a spot within near range. " +
                "You can also Volley with anything you can lift, giving it the near and thrown tags."),
            Rolled("Healing Light", StatType.Int,
                "When you stitch sunlight into a wound",
                "They heal 1d8 HP and are filled with energy: +1 forward.",
                "They heal 1d8 HP.",
                miss: "The sunlight won't cooperate: they take -1 forward from the searing pain."),
            new("Illuminated Warrior", "Your little light can hold two Light Weaponry forms at once. Lend one to an ally or wield both."),
            new("Light of Revelation",
                "Add to Bend Light:\n• Commit the light to the truth: reveal what's behind illusions, enchantments and invisible things here\n• Ask the light to show you something: reveal a secret in this area"),
            new("Magical Dabbler", "Take a non-multiclass move from any class, treating your level as one lower unless it's magic-based.") { Multiclass = MulticlassGrant.AnyClass(levelOneLower: true) },
            new("Rainbow Road", "Add to Bend Light:\n• Convince the light to let you through: create a bridge to somewhere within near range"),
            new("Truth to Power", "On a 7+ Spout Lore, take +1 forward when acting on what you learned."),
            new("Twilight Blade",
                "Add a Light Weaponry form. **Twilight Blade:** close, ignores armor, mystical. It cuts through anything. When you deal damage with it, you can remove something from the target: something worn, something held, or a small part of them."),
            Rolled("Will O' The Wisp", StatType.Cha,
                "When you use your little light as bait",
                "You lure a single creature within near range, and the light can string it along as far as you like. It doesn't bring friends, and nobody knows where it went.",
                "You lure a single creature within near range, and the light can string it along as far as you like."),
        ],
        level6To10:
        [
            new("A Light in the Darkness", "When you stand firm against an approaching threat, you and every ally who backs you up take +1 forward."),
            new("Archon", "While you can see your little light, you can Hack and Slash with your Light Weaponry as if you stood where it is. If it takes damage doing so, it reverts to its harmless form for a while."),
            new("Dawn's Cage", "After moving a target with Fist of Dawn, you can trap them in an inescapable cage of light. Your little light is the cage and can't do anything else until you free them.") { Requires = "Fist of Dawn" },
            new("Evanescence", "Add to Bend Light:\n• Warn the light away from something or someone, making it invisible"),
            Rolled("Light of Rebirth", StatType.Int,
                "When you stitch sunlight into a soul",
                "Remove a debility or condition, or replace a missing limb with solid light, and they heal 1d8 HP.",
                "Remove a debility or condition, or replace a missing limb with solid light.",
                miss: "The sun's brilliant energy burns their soul.") with { Requires = "Healing Light" },
            new("Magical Initiate", "Take another non-multiclass move from any class, treating your level as one lower unless it's magic-based.") { Multiclass = MulticlassGrant.AnyClass(levelOneLower: true), Requires = "Magical Dabbler" },
            new("Mirror Shield", "When your Shield of Light blocks damage, deal 1d6 damage (ignores armor) to its source."),
            new("Sola's Speaker", "On a 12+ Bend Light, the lights take a liking to you: take the 10+ result on Bend Light until you leave the area or upset them."),
            new("Solar Aura",
                "Add a Light Weaponry form. **Solar Aura:** an aura seals everything within reach of you; nothing outside can get in by any means. If anyone inside leaves or attacks out of it, the light reverts to its harmless form."),
            new("Twilight Reckoning", "When you damage a surprised, defenseless or wounded enemy with your Twilight Blade, you can sever anything from them instead of dealing damage: their life, a limb, a title, a relationship, a prized possession, a belief.") { Requires = "Twilight Blade" },
        ]);
}
