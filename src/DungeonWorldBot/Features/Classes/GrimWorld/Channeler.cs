using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Moves;
using static DungeonWorldBot.Features.Classes.ClassMove;

namespace DungeonWorldBot.Features.Classes.GrimWorld;

public static class Channeler
{
    private static ClassMove Element(string name) => new($"Primary Element: {name}", $"Your magic is {name.ToLower()}.");
    private static ClassMove Emanation(string name) => new($"Emanation Point: {name}", $"Magic leaves your body through your {name.ToLower()}.");

    public static readonly ClassSheet Sheet = new(
        ClassType.Channeler,
        "Grim World",
        new ClassColor("#0b8299", "#48c4dc"),
        startingMoves:
        [
            new("Trickle", "Emit a little of your Primary Element for mundane tasks or to impress someone, without Channeling."),
            Rolled("Channel", StatType.Con,
                "When you open your Conduit wide and channel magic through your body",
                "Your Primary Element flows out of your Emanation Point. Use one of your Techniques.",
                "Use a Technique, but the power is too much. Dam it with your body and take 1d4 damage (ignores armor), or let it through and the GM picks complications, like:",
                options:
                [
                    "It leaves a different part of your body, or comes out as a different element",
                    "It has an unintended effect on the environment",
                    "It causes a blast wave that throws you backwards",
                ],
                optionsOn: OptionBands.WeakHit),
            new("Techniques",
                "You know up to 3 Techniques, each with a name and 2 tags. Spend several hours to develop a new one or replace one.\n" +
                "• **Projectile:** 1d6 damage if near\n• **Barrier:** +1 armor until your next Channel\n• **Blast:** 1d8 damage if close\n" +
                "• **Hazard:** an environmental effect\n• **2 Piercing**\n• **Forceful**"),
            Rolled("Summon Elemental", StatType.Con,
                "When you call an Elemental out of yourself, choosing a Technique tag for it to embody",
                "It flows out, leashed to your Emanation Point by a stream of magic. Gain 1d4 Control.",
                "It emerges and you gain 1d4 Control, but it's moody; choose one:",
                options:
                [
                    "You must spend 1 Control right away",
                    "It embodies a different tag (the GM says which)",
                    "It resents you and will be hostile later",
                ],
                note: "While it's leashed you have Elemental Protection and Elemental Attack. At 0 Control it breaks free, using its tag at will with HP equal to twice your level.",
                optionsOn: OptionBands.WeakHit),
            new("Elemental Protection", "When you or your leashed Elemental takes damage, lose 1 Control and the damage is negated."),
            Rolled("Elemental Attack", null,
                "When you command your leashed Elemental to use its tag",
                "It succeeds.",
                "It succeeds, but you lose 1 Control.",
                rollsWith: "Control"),
        ],
        features:
        [
            new ClassFeature("Primary Element", "What is your Primary Element?",
                [Element("Flame"), Element("Frost"), Element("Shock"), Element("Shadow"), Element("Wind"), Element("Water")])
            {
                ShowOnCard = true,
            },
            new ClassFeature("Emanation Point", "Where does magic leave your body?",
                [Emanation("Forehead"), Emanation("Eyes"), Emanation("Mouth"), Emanation("Chest"), Emanation("Fingers"), Emanation("Palms")]),
        ],
        level2To5:
        [
            new("Thread the Needle", "When a Technique deals damage, you can add 1d4 bonus damage. If the bonus die shows 1 or 2, it hurts you too (ignores armor)."),
            new("Callus", "You have 5 armor against outside damage of your Primary Element."),
            Rolled("Bender of the Elements", StatType.Con,
                "When you manipulate something nearby made of your Primary Element",
                "Hold 3.",
                "Hold 1.",
                options:
                [
                    "It deals 1d6 damage to something",
                    "It gains the Forceful tag",
                    "It moves somewhere you can see",
                    "It holds a shape, even after you stop concentrating",
                ],
                note: "While you do nothing but concentrate on it, spend hold 1 for 1 on the options."),
            new("Signature Technique", "Gain an extra Technique slot. This Technique can have 3 tags."),
            new("Dual Conduits", "Choose a second element. With a grunt or a snap, swap which one is your Primary Element. Your Techniques keep their tags."),
            new("My Body is Ready", "After a few minutes of uninterrupted concentration, you can end it by unleashing a Channel that automatically succeeds."),
            new("Teleport", "Gain the Teleport Technique tag: you instantly move somewhere near."),
            new("Conduit Warrior", "When you Hack and Slash and roll a 10+, add one Technique tag to the attack."),
            Rolled("Reabsorb Elemental", StatType.Con,
                "When you reabsorb your unleashed Elemental",
                "It's drawn back through your Conduit.",
                "You absorb it, but take 1d4 damage (ignores armor)."),
        ],
        level6To10:
        [
            new("Fissure", "Thread the Needle can roll 1d4, 2d4 or 3d4 bonus damage. Each die showing 1 or 2 hurts you too (ignores armor).") { Requires = "Thread the Needle" },
            new("Area Burst", "Gain the Burst Technique tag: it affects up to 3 targets near each other, rolling any damage once for all of them."),
            new("Armor of the Elements", "Your Barrier tag gives +2 armor until your next Channel."),
            new("Devastation Wave", "Your Signature Technique can take a temporary fourth tag, but you take 1d4 damage (ignores armor) when you channel it.") { Requires = "Signature Technique" },
            new("Osmosis", "On a 10+ Reabsorb Elemental, heal 1d6 damage.") { Requires = "Reabsorb Elemental" },
            new("Fallout", "When you kill an enemy with Channel, its body becomes a Hazard."),
            new("Corporeal Trauma", "When you deal damage, take a debility of your choice to reroll any or all of your damage dice. You can't use this with all six debilities."),
            new("Affliction", "On a 12+ Channel, choose something on your target (an item, a limb, a weapon). Your element ruins it: it melts, freezes, corrupts or is encased."),
            new("Don't Blink", "Your Teleport Techniques can move other targets.") { Requires = "Teleport" },
            new("Conduit Champion", "When you Hack and Slash and roll a 10+, add one Technique tag and the Barrier tag to the attack. The Barrier lasts until your next Channel or Hack and Slash.") { Replaces = "Conduit Warrior" },
            new("Reckless Summoner", "On a 10+ Summon Elemental, you can take a 7-9 option to give the Elemental 2 tags."),
        ],
        deathMove: new("Death: Rupture",
            "When you die (failing or forgoing Last Breath), the dam breaks. Your body bursts as raw magic pours out, engulfing or hurling everyone nearby. Your conduit hangs in the air as a permanent gateway to the elemental planes, and the area is forever soaked in your element."),
        resources:
        [
            new ClassResource("Control", "Gained from Summon Elemental (1d4). Lost to block damage or on a 7-9 Elemental Attack. At 0, your Elemental breaks free."),
        ]);
}
