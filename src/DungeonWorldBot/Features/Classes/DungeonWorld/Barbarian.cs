using DungeonWorldBot.Data.Entities;
using static DungeonWorldBot.Features.Classes.ClassMove;

namespace DungeonWorldBot.Features.Classes.DungeonWorld;

/// <summary>
/// Summarized from the Dungeon World playbooks (version 2.4) by Sage LaTorra and Adam Koebel.
/// </summary>
public static class Barbarian
{
    private static ClassMove Appetite(string name) =>
        new($"Appetite: {name}", $"You hunger for {name.ToLower()}. While pursuing it, roll 1d6+1d8 instead of 2d6 for moves. If the d6 is higher, the GM adds a complication from your heedlessness.");

    public static readonly ClassSheet Sheet = new(
        ClassType.Barbarian,
        "Dungeon World",
        new ClassColor("#9c3d1c", "#e07a52"),
        startingMoves:
        [
            new("Outsider", "You and your people aren't from around here. At the start of each session, the GM asks about your homeland, why you left, or what you left behind. Answer and mark XP."),
            new("Musclebound", "While you wield a weapon, it gains the forceful and messy tags."),
            new("Herculean Appetites", "Choose two appetites. While pursuing one, roll 1d6+1d8 instead of 2d6 for moves. If the d6 is the higher die, the GM also introduces a complication or danger from your heedless pursuits."),
            Rolled("What Are You Waiting For?", StatType.Con,
                "When you cry out a challenge to your enemies",
                "They treat you as the most obvious threat and ignore your companions. Take +2 damage ongoing against them.",
                "Only a few (the weakest or most foolhardy) fall prey to your taunting."),
            new("The Upper Hand", "Take +1 ongoing to Last Breath rolls. On a 7-9 Last Breath, you make Death an offer for your life. If he accepts, you live; if not, you die."),
        ],
        features:
        [
            new ClassFeature("Armor Style", "Which do you start with?",
            [
                new("Unencumbered, Unharmed", "While you're below your Load and wear no armor or shield, take +1 armor."),
                new("Full Plate and Packing Steel", "Ignore the clumsy tag on armor you wear."),
            ]),
            new ClassFeature("Appetite", "Which two appetites drive you?",
            [
                Appetite("Pure Destruction"), Appetite("Power Over Others"), Appetite("Mortal Pleasures"),
                Appetite("Conquest"), Appetite("Riches and Property"), Appetite("Fame and Glory"),
            ])
            {
                Picks = 2,
                ExtraPicks = [("Still Hungry", 1)],
            },
        ],
        level2To5:
        [
            new("Still Hungry", "Choose an additional appetite."),
            new("Appetite for Destruction", "Take a move from the Fighter, Bard or Thief class list. You can't take their multiclass moves.") { Multiclass = MulticlassGrant.FromClasses(ClassType.Fighter, ClassType.Bard, ClassType.Thief) },
            new("My Love for You Is Like a Truck", "When you perform a feat of strength, name someone present whom you've impressed and take +1 forward to Parley with them."),
            new("What Is Best in Life", "At the end of a session, if you crushed your enemies, saw them driven before you, or heard the lamentations of their kinfolk, mark XP."),
            new("Wide-Wanderer", "When you arrive someplace, ask the GM about any important traditions, rituals and so on. They tell you what you need to know."),
            new("Usurper", "When you prove yourself superior to a person in power, take +1 forward with their followers, underlings and hangers-on."),
            new("Khan of Khans", "Your hirelings always accept the gratuitous fulfillment of one of your appetites as payment."),
            new("Samson", "Take a debility to immediately break free of any physical or mental restraint."),
            new("Smash!", "When you Hack and Slash, on a 12+ deal your damage and choose something physical your target has (a weapon, their position, a limb): they lose it."),
            new("Indestructible Hunger", "When you take damage, you can take -1 ongoing until you sate one of your appetites instead. You can't if you already have this penalty."),
            new("Eye for Weakness", "When you Discern Realities, add \"What here is weak or vulnerable?\" to the questions you can ask."),
            new("On the Move", "When you Defy Danger caused by movement (falling off a narrow bridge, rushing past an armed guard), take +1."),
        ],
        level6To10:
        [
            new("More! Always More!", "When you satisfy an appetite to the extreme (destroying something unique and significant, enormous fame, riches, power...), you can resolve it: cross it off, mark XP, and choose a new appetite."),
            new("The One Who Knocks", "When you Defy Danger, on a 12+ you turn the danger back on itself (the GM describes how)."),
            new("Healthy Distrust", "Whenever the unclean magic of mortal men makes you Defy Danger, treat a 6- as a 7-9."),
            Rolled("For the Blood God", StatType.Wis,
                "When you sacrifice what your gods value (gold, blood, bones...) as per your rites and rituals",
                "The GM grants you insight into your current trouble or a boon to help you.",
                "The sacrifice isn't enough and your gods take of your flesh too, but you still get some insight or boon.",
                miss: "You earn the ire of the fickle spirits."),
            new("A Good Day to Die", "While your current HP is below your CON (or 1, whichever is higher), take +1 ongoing."),
            new("Kill 'Em All", "Take another move from the Fighter, Bard or Thief class list. You can't take their multiclass moves.")
            {
                Requires = "Appetite for Destruction",
                Multiclass = MulticlassGrant.FromClasses(ClassType.Fighter, ClassType.Bard, ClassType.Thief),
            },
            Rolled("War Cry", StatType.Cha,
                "When you enter battle with a show of force",
                "Both:",
                "One or the other:",
                options:
                [
                    "Your allies are rallied and take +1 forward",
                    "Your enemies feel fear and act accordingly (avoiding you, hiding, attacking with fear-driven abandon)",
                ]),
            new("Mark of Might", "When you take this move and spend uninterrupted time reflecting on past glories, mark yourself with a symbol of your power. Any intelligent mortal who sees it knows you're a force to be reckoned with."),
        ]);
}
