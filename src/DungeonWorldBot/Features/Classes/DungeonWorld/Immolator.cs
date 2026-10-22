using DungeonWorldBot.Data.Entities;
using static DungeonWorldBot.Features.Classes.ClassMove;

namespace DungeonWorldBot.Features.Classes.DungeonWorld;

/// <summary>
/// Summarised from the Dungeon World playbooks (version 2.4) by Sage LaTorra and Adam Koebel.
/// </summary>
public static class Immolator
{
    public static readonly ClassSheet Sheet = new(
        ClassType.Immolator,
        "Dungeon World",
        new ClassColor("#d4480f", "#ff8a4c"),
        startingMoves:
        [
            new("Hand Crafted", "You can use your hands in place of tools and fire to craft metal objects: mundane weapons, armor and jewellery from their raw components. You can unmake them too, though without time and safety you may need to Defy Danger first."),
            Rolled("Zuko Style", StatType.Wis,
                "When you bend a flame to your will",
                "It does as you command, taking the shape and movement you desire for as long as it has fuel to burn.",
                "The effect is short-lived, lasting only a moment."),
            new("Give Me Fuel, Give Me Fire", "When you gaze intensely into someone's eyes, ask their player \"What fuels the flames of your desire?\" They answer truthfully, even if their character doesn't know or would hide it."),
            new("Fighting Fire with Fire", "When you take damage and it's odd (after armor), roll 1d4 and either add that many uses to your active burning brand, take it forward to summon your burning brand, or reduce the damage by that much."),
            Rolled("Burning Brand", StatType.Con,
                "When you conjure a weapon of pure flame",
                "Choose two tags:",
                "Choose one tag:",
                options:
                [
                    "hand",
                    "thrown, near",
                    "+1 damage",
                    "Remove the dangerous tag",
                ],
                note: "The weapon always has the fiery, touch, dangerous and 3 uses tags, and each attack uses one. You can use INT in place of STR or DEX to attack with it."),
        ],
        features:
        [
            new ClassFeature("Race Move", "Which race move do you have?",
            [
                new("Race: Salamander", "Non-magical heat and fire can't harm you."),
                new("Race: Human", "When you Make Camp next to a large, open flame, regain all your HP."),
            ]),
        ],
        level2To5:
        [
            new("Ogdru Jahad", "Gain the Wizard move Ritual. The GM always tells you what you must sacrifice for the effect you want.") { Multiclass = MulticlassGrant.FromClasses(ClassType.Wizard) with { OnlyMoves = ["Ritual"] } },
            Rolled("Moth to the Flame", StatType.Wis,
                "When you tempt a weak mind with your inner fire",
                "Their will is suppressed: they follow you and do as you desire, so long as nothing startles or surprises them.",
                "The effect is only strong enough to distract or confuse them.",
                miss: "They become agitated and upset, your fire having sparked their hidden desires."),
            new("Burning Bridges", "When you would take your Last Breath, don't. Instead erase one of your Bonds permanently, lowering your total Bonds forever. You live with 1d6 HP. With no Bonds left, take your Last Breath as normal."),
            Rolled("The Enkindler", StatType.Cha,
                "When you bolster the courage of others",
                "They shake off all fear and doubt, becoming brave in an instant.",
                "The effect is fleeting: they see through it and resort to cowardice after a moment or two.",
                miss: "They're cowed or terrified by your presence."),
            Rolled("Sick Burn", StatType.Cha,
                "When you insult an NPC",
                "You leave them no room to react: they bear your insult and the scorn of all who hear it.",
                "You cross a line. They'll have their revenge, someday.",
                miss: "You've gone too far. They blow up here and now."),
            Rolled("Lore of Flame", StatType.Wis,
                "When you stare into a source of fire looking for answers",
                "The GM tells you something new and interesting about the current situation, in good detail.",
                "The GM gives you an impression of something new and interesting about the current situation."),
            new("Burns Twice as Bright", "When you channel the flames of fate, treat a missed roll as a 7-9, or a 7-9 as a 10+ (yours or another character's). Tell the GM something you've lost: an emotion, a memory, a piece of your being. You can't use this again until you've used Burns Half as Long."),
            new("This Killing Fire", "Add messy, forceful, reach, near and far to your options for Burning Brand."),
            Rolled("Firebrand", StatType.Cha,
                "When you introduce a new idea to an NPC",
                "They believe the idea is their own and take to it with fervour.",
                "Their passion fades after a day or two.",
                miss: "They respond negatively, speaking out against the idea."),
        ],
        level6To10:
        [
            new("Fanning the Flames", "You can apply Firebrand to a group of people (a dozen or so) all at once.") { Requires = "Firebrand" },
            Rolled("Watch the World Burn", StatType.Wis,
                "When you open a channel to the burning planes, tell the GM what you're sacrificing, and call a firestorm",
                "You can extinguish the storm with a little effort.",
                "The fires rage out of your control, spreading wherever wind and weather carry them.",
                miss: "Something cruel, intelligent and hungry comes with the storm.",
                note: "Fire rains over an area about the size of a small village, damaging everyone and everything in it."),
            new("From Hell's Heart", "When you summon fire with any of your moves, you can make it the black fire of hell: it doesn't burn with heat, ignores armor and scorches the soul. Soulless creatures can't be harmed by it."),
            Rolled("Burning Ring of Fire", StatType.Cha,
                "When you fuse a willing person's soul to yours",
                "You're bound together: you sense each other at any distance and share your emotional state.",
                "You're bound, but the connection is unstable: when either of you takes a debility, so does the other.",
                miss: "The branding is rejected and you both erase your Bonds to each other (write new ones at End of Session as usual).",
                note: "The fusion can't be undone by mortal means."),
        ],
        resources:
        [
            new ClassResource("Burning Brand Uses", "Uses left on your burning brand. It starts with 3 when conjured, and each attack uses one."),
        ],
        extras:
        [
            new("Burns Half as Long", "When you sacrifice a victory to the flames of fate, treat a roll of 10+ as a miss. You gain this with Burns Twice as Bright.") { Requires = "Burns Twice as Bright" },
        ]);
}
