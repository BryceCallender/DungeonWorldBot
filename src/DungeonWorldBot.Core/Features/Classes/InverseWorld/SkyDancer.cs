using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Moves;
using static DungeonWorldBot.Features.Classes.ClassMove;

namespace DungeonWorldBot.Features.Classes.InverseWorld;

public static class SkyDancer
{
    public static readonly ClassSheet Sheet = new(
        ClassType.SkyDancer,
        "Inverse World",
        new ClassColor("#1f86c9", "#6cbcf2"),
        startingMoves:
        [
            Rolled("Take to the Sky", StatType.Dex,
                "You can fly as often and as far as you like (long trips may need Undertake a Perilous Journey). When you fly somewhere otherwise out of reach",
                "You get there, no problem.",
                "You get there, but choose one:",
                options:
                [
                    "A threat is waiting when you arrive",
                    "The winds won't cooperate and it takes longer than expected",
                    "Your landing is better described as a crash",
                ],
                optionsOn: OptionBands.WeakHit),
            Rolled("Leaf on the Wind", StatType.Dex,
                "When you dance away from an opponent's attack",
                "Choose two:",
                "Choose one:",
                options:
                [
                    "You avoid the attack",
                    "You slip away somewhere they can't follow",
                    "You taunt them, drawing their full attention",
                ]),
            new("Eye in the Sky", "When you look around from high above, take +1 to Undertake a Perilous Journey, Take Watch and Discern Realities."),
            new("I've Got A Bad Feeling About This",
                "When you take a moment to survey an area, ask the GM two of these, and they answer truthfully:\n" +
                "• Where's my best way in, out, or through?\n• Is there a trap or ambush here, and where?\n• What here is the biggest threat to me?\n• What can I see that no one else can?"),
        ],
        features:
        [
            new ClassFeature("Drive", "What drives you to take to the skies?",
            [
                new("Drive: Explore the World", "Mark XP when you go somewhere you've never been and check it out."),
                new("Drive: Fear of Harm", "Mark XP when you discover a threat, ambush or trap and deal with it before it hurts you."),
                new("Drive: Personal Freedom", "Mark XP when you avoid or escape trouble without resolving it."),
            ]),
            new ClassFeature("Means of Flight", "How do you fly?",
            [
                new("Flight: Magical", "You fly with a magical artifact like a wand, totem or broomstick. When you Take to the Sky, you can move a willing ally instead of yourself."),
                new("Flight: Mechanical", "You fly with your own invention (jetpack, rocket boots, propeller suit) covered in dangerous bits. On a 10+ Leaf on the Wind against a melee attack, also deal your damage to the attacker."),
                new("Flight: Natural", "You fly on your own wings, natural, mutated or grafted, and you're stronger for it: your damage is d8 instead of d6.") { DamageDie = "d8" },
            ]),
        ],
        level2To5:
        [
            new("Cloud Dancer", "Move and reshape clouds by hand however you like: fog cover, platforms, messages in the sky, rain, anything."),
            new("Ever Watchful", "Discern Realities asks one extra question, even on a 6-."),
            new("Fluid Motion", "On a 7+ Hack and Slash, or Leaf on the Wind against a melee attack, move your opponent anywhere within reach of you."),
            new("Forecast", "When you Make Camp, the GM tells you one hazard, enemy or obstacle you'll likely face tomorrow. Take +1 forward against it."),
            Rolled("Howling Winds", StatType.Dex,
                "When you conjure slicing winds to attack your foes",
                "Deal your damage to an enemy within near range.",
                "Deal your damage to an enemy within near range, but the winds cause collateral damage (the GM says how).",
                note: "Your Howling Winds have near and piercing 2."),
            new("Mobile Dabbler", "Take a non-multiclass move from any class, treating your level as one lower unless it rolls +DEX or improves your mobility.") { Multiclass = MulticlassGrant.AnyClass(levelOneLower: true) },
            new("Right Where I Want You", "On a 7+ Volley or other ranged attack, the wind pushes your target anywhere within reach of where they were."),
            Rolled("Rocket Man", StatType.Dex,
                "When you need to get somewhere fast",
                "You arrive the moment before you're needed.",
                "You arrive the moment before you're needed, but draw unwanted attention on the way."),
            new("Sky's Grasp", "Hold out your hand and an unattended object within near range flies to it. Toss something of 1 weight or less and it floats to exactly where you want within near range."),
            new("Water Dancer", "Take to the Sky works through or on top of water too. You can't breathe underwater, but can hold your breath unusually long."),
            Rolled("Weathermantic Empowerment", StatType.Wis,
                "When you command the weather to aid you and your allies",
                "Choose one:",
                "Choose one, but you or an ally get hindered or blown around by strange weather (the GM says what).",
                options:
                [
                    "Cloud of Fog: fill the area with dense fog",
                    "Bolt of Lightning: an ally gets +1d4 damage forward",
                    "Shield of Ice: an ally gets +1 armor forward",
                ]),
        ],
        level6To10:
        [
            new("Angelic Warrior", "When you Hack and Slash while flying, deal +1d6 damage."),
            Rolled("Escape Route", StatType.Dex,
                "When you're in too deep and need a way out, naming your escape route",
                "You're gone.",
                "You can stay or go. If you go, it costs you: leave something behind or take something with you (the GM says what)."),
            new("Ever Thoughtful", "When you Discern Realities, up to two of your questions can be anything, not just from the list.") { Requires = "Ever Watchful" },
            new("Impossible Grace", "Leaf on the Wind gives one extra option, even on a 6-."),
            new("Mobile Initiate", "Take another non-multiclass move from any class, treating your level as one lower unless it rolls +DEX or improves your mobility.") { Multiclass = MulticlassGrant.AnyClass(levelOneLower: true), Requires = "Mobile Dabbler" },
            new("Turbulence",
                "When you whip up a sudden gust, choose one:\n• Knock someone within reach off their feet and send them flying\n• Deflect incoming arrows and other small projectiles\n• Change direction mid-air to outmaneuver a pursuer"),
            Rolled("Weathermancy", StatType.Wis,
                "When you try to change the weather, choosing an effect",
                "It happens.",
                "The weather destabilizes: the GM adds a second effect and both happen.",
                miss: "You lose control: the GM picks the effect.",
                options:
                [
                    "Control Wind: anything from gale force to dead calm",
                    "Control Precipitation: start or stop rain, snow or hail",
                    "Control Cataclysm: start or stop extreme weather like a tornado or lightning storm",
                ]),
            Rolled("Weather's Rage", StatType.Dex,
                "When you conjure foul weather to attack your foes, choosing an effect",
                "Deal your damage to an enemy within near range, with the chosen effect.",
                "Deal your damage with the chosen effect, but cause collateral damage (the GM says how).",
                options:
                [
                    "Dancing Lightning: it arcs to a second or third target",
                    "Dousing Rain: forceful, area, and everything gets wet",
                    "Freezing Snow: bury the target in ice and snow",
                    "Howling Winds: piercing 3, messy, forceful",
                ]) with { Replaces = "Howling Winds" },
            new("World Dancer", "Take to the Sky to somewhere you've been before always counts as a 10+. Traveling alone there, you never need Undertake a Perilous Journey, and it takes half the time."),
        ]);
}
