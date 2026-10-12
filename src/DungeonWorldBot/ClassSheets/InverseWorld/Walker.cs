using DungeonWorldBot.Data.Entities;
using static DungeonWorldBot.ClassSheets.ClassMove;

namespace DungeonWorldBot.ClassSheets.InverseWorld;

public static class Walker
{
    public static readonly ClassSheet Sheet = new(
        ClassType.Walker,
        "Inverse World",
        startingMoves:
        [
            new("Wall Walking", "Climb walls and ceilings of any texture as fast as you walk or run. While climbing you only have one hand free. You can carry one passenger, but then you can't use your hands at all."),
            Rolled("Death From Above", StatType.Str,
                "When you drop in on someone from above",
                "Choose two:",
                "Choose one:",
                options:
                [
                    "Deal your damage with a melee weapon",
                    "Kidnap them: retreat somewhere nearby, taking them with you",
                    "No one notices you drop in, and your target doesn't make a sound",
                ]),
            new("No One Looks Up", "While you cling to a ceiling without talking, moving or attacking, NPCs never notice you (unless you've been spotted). If you're above their line of sight, you can move too; ask the GM if you are and they'll answer truthfully."),
            Rolled("On a Mirror's Edge", StatType.Str,
                "When you start free running",
                "Hold 3.",
                "Hold 2.",
                miss: "Hold 1, but the GM adds a complication when you spend it.",
                options:
                [
                    "Avoid or knock aside an enemy attack and keep moving",
                    "Leap around, over or through an obstacle or enemy in your path",
                    "Get somewhere out of reach or out of sight",
                ],
                note: "Spend 1 hold for an option. You lose all hold the moment you stop moving."),
        ],
        features:
        [
            new ClassFeature("Drive", "What drives you to take the risks you do?",
            [
                new("Drive: Competitive Instincts", "Mark XP when you beat someone in a contest of skill or strength."),
                new("Drive: Power Over Others", "Mark XP when you drop someone from a great height because you can."),
                new("Drive: Defy The Rules", "Mark XP when you go where you aren't allowed and take something to show for it."),
            ]),
            new ClassFeature("Training", "Which society taught you to wall walk?",
            [
                new("The Cloud Seas", "You can Wall Walk on the surface of any water: lakes, rivers, waterfalls, even clouds."),
                new("The Great Nations", "You use an advanced climbing harness of your own design. On walls and ceilings you keep both hands free, even with a passenger."),
                new("The Hidden Cities", "No One Looks Up changes: while you climb along a ceiling without talking or attacking, NPCs never notice you (unless you've been spotted)."),
                new("The Worldcrust", "You learned to move erratically on the world's most dangerous walls. +1 armor while Wall Walking."),
            ]),
        ],
        level2To5:
        [
            Rolled("Brutal Strike", StatType.Str,
                "When you attack a surprised or defenseless enemy in melee, instead of just dealing your damage",
                "Choose two:",
                "Choose one:",
                options:
                [
                    "Knock them down or toss them aside",
                    "Deal your damage +1d6",
                    "Maim them: a limb or sense of your choice stops working",
                    "Terrify them: they'll do anything to get away from you",
                ]),
            new("Competitive Streak", "When you win a contest, challenge or bet, take +1 forward."),
            new("Dropping In and Checking Out", "Add to Death From Above:\n• Knock them unconscious with one swift, decisive blow"),
            new("Hangman's Noose", "A rope or grappling hook you throw at something within near range always catches exactly where you want, and never comes loose unless you let it or it's cut."),
            new("Mighty Dabbler", "Take a non-multiclass move from any class, treating your level as one lower unless it rolls +STR or boosts your physical abilities."),
            Rolled("No Trespassing", StatType.Dex,
                "When a foe enters an area you had time to prepare",
                "Hold 2.",
                "Hold 2, and the GM holds 1.",
                miss: "Hold 1, and the GM holds 2.",
                options:
                [
                    "A trap pins them in place for a moment",
                    "Deal your damage with a deadly trap or surprise attack",
                    "Suddenly appear anywhere in the area",
                ],
                note: "When someone moves through the area, spend 1 hold for an option."),
            new("Respectable Member of Society", "When you make the Outstanding Warrants move, you can apply the result to another player instead of yourself."),
            new("Spider's Grip", "You never lose your grip while Wall Walking. When you fall or are sent flying, you can catch yourself on any wall or ceiling you pass."),
            new("Talk the Talk", "With people who value strength or physical skill, impressive athletic feats always count as leverage for Parley."),
            new("Walk the Walk", "When you draw attention to yourself before a daring athletic stunt, take +1 forward to it."),
        ],
        level6To10:
        [
            new("Always Pack Spare Rope", "When you Make Camp, restock 3 uses of adventuring gear."),
            new("Cutthroat", "When you deal damage intending to kill, deal +1d6 damage."),
            new("Evasion", "On a 12+ Defy Danger, you transcend the danger: the GM offers a better outcome, true beauty, or a moment of grace."),
            new("Into Thin Air", "When you try to hide from someone who's spotted you and you break line of sight, you always find a way to hide or slip away."),
            new("Mighty Initiate", "Take another non-multiclass move from any class, treating your level as one lower unless it rolls +STR or boosts your physical abilities.") { Requires = "Mighty Dabbler" },
            new("Mighty Master", "Take another non-multiclass move from any class, treating your level as one lower unless it rolls +STR or boosts your physical abilities.") { Requires = "Mighty Initiate" },
            new("Shadow Step", "Step into a dark, shadowy place and you can step out of any other within near range."),
            new("Spider's Leap", "Leap anywhere within near range with ease. With room to leap around, you can use Death From Above anywhere, any time.") { Requires = "Spider's Grip" },
            new("Walking On Sunshine", "You can Wall Walk on rays and beams of light, like those from a window, a mirror or a Lantern's little light."),
            new("Wire Fu",
                "Knotted rope in your hands is a weapon (reach, forceful); metal wire is a weapon (reach, messy). When you deal damage with either, choose one:\n" +
                "• Knock something out of their hands, sending it flying\n• Push them anywhere within reach of you\n• Trip or distract them: an ally gets +1 forward against them") { Requires = "Hangman's Noose" },
        ]);
}
