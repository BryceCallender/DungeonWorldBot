using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Moves;
using static DungeonWorldBot.ClassSheets.ClassMove;

namespace DungeonWorldBot.ClassSheets.InverseWorld;

public static class Survivor
{
    public static readonly ClassSheet Sheet = new(
        ClassType.Survivor,
        "Inverse World",
        startingMoves:
        [
            new("Hold On to What's Precious",
                "When you Defend an ally, a friend or something you cherish, gain +1 hold, even on a miss. " +
                "When something or someone you cherish is in your hand and would be taken, knocked away, moved, broken or harmed, you can take the effect yourself instead."),
            new("Reminders of the Past", "When you meet a traveller or enemy you've met before (your call), tell the GM about your last encounter; they'll tell you how they've changed. When you find a marked grave, say who they were and how you knew them."),
            Rolled("Survive", StatType.Con,
                "When you brace for expected harm",
                "Choose two:",
                "Choose one:",
                options:
                [
                    "Take half damage, rounded down",
                    "Take a debility instead of damage (not if you have all six)",
                    "Ignore every effect of the attack except damage: you aren't moved, burned, poisoned, restrained...",
                    "Take +1 ongoing against the cause until you've conquered it",
                ]),
        ],
        features:
        [
            new ClassFeature("Drive", "What drives you to survive?",
            [
                new("Drive: Inner Peace", "Mark XP when you settle a confrontation without violence."),
                new("Drive: Something to Call My Own", "Mark XP when you obtain something or somewhere that's yours alone."),
                new("Drive: To Stare Death in the Face", "Mark XP when you willingly face impossible odds for the thrill of it."),
            ]),
            new ClassFeature("Cataclysm", "What kind of cataclysm did you survive?",
            [
                new("Cataclysm: Man", "You lost everything to human cruelty. With sympathetic or guilt-ridden NPCs, your tortured past counts as leverage."),
                new("Cataclysm: Beast", "You lost everything to a legendary monster. +2 damage against beasts or monsters that hurt you or someone you cherish."),
                new("Cataclysm: God", "You lost everything to the wrath of a god (Sola or one of his captors). Little else seems threatening now: choose an extra Eternal Mark."),
            ]),
            new ClassFeature("Eternal Mark", "Which Eternal Marks did the cataclysm leave on you?",
            [
                new("A Hand Bloodied", "Your body is a weapon with the hand and forceful tags."),
                new("A Body Scarred", "+1 armor.") { Armor = 1 },
                new("A Limb Replaced", "Anything you hold counts as something you cherish."),
                new("A Mind Shattered", "+1 to Defy Danger against any kind of manipulation."),
                new("A Heart Broken", "+1 forward against anyone who insults something you've lost."),
                new("A Scar Burning", "Your scar glows and burns when you're in danger."),
            ])
            {
                Picks = 2,
                ExtraPicks = [("Cataclysm: God", 1), ("Further Marked", 1), ("More Scars Than Skin", 1)],
            },
        ],
        level2To5:
        [
            Rolled("Alone Against the World", StatType.Con,
                "When you stand alone against an approaching major threat",
                "After a glorious standoff, you drive it back, taking some damage.",
                "After a grueling struggle, you drive it back but suffer a permanent scar (physical, mental or emotional). Describe it.",
                miss: "Roll your Last Breath: after a desperate attempt, you've failed."),
            new("Dead Man Walking", "When you take damage, you can ignore it and gain that much Pain instead. At your next short rest, spend all your Pain and take that much damage, ignoring armor."),
            new("Defy Opposition", "On a 12+ Defy Danger against something trying to harm you, gain an advantage over it: knock it down, get out of reach, get on top of it, disarm it..."),
            new("Further Marked", "The next time you'd take a debility or suffer great personal harm, don't; gain an Eternal Mark instead."),
            new("Kid, Let Me Tell You About The Calamity", "When you Make Camp, tell an ally a tale from your past and pick one of your Eternal Marks. They have its benefit until you next Make Camp."),
            new("No One Shall Suffer As I Have", "Mark XP when you stop a cataclysm, lessen its consequences, or evacuate people from it."),
            new("Protector", "When someone you cherish would suffer the consequences of their own move or decision, you can take all of them instead."),
            new("Survival Instinct", "When you scavenge, you always find 1 use of rations, ammo, gear, bandages or antitoxin, if you need it right now."),
            new("Tenacity", "When you Parley by relentlessly pressing your case despite those who'd silence, ignore or harm you, roll +CON instead of CHA."),
            new("Worldly", "Take a move from a playbook no other player is using.") { Multiclass = MulticlassGrant.AnyClass() },
            new("You're Already Dead", "When you'd deal damage, you can skip rolling and gain 1 Fate instead. Spend all your Fate at any time to deal your damage that many times to one enemy you gained it from, as a single hit. You can discard Fate to spare them."),
        ],
        level6To10:
        [
            new("Defy Fate", "On a 12+ Defy Danger, you don't just avoid the danger, you negate it. If it was an enemy's move, they stop using it, knowing it's useless against you.") { Replaces = "Defy Opposition" },
            new("Got A Grave with My Name On It", "When you take your Last Breath, roll +CON.") { Requires = "Dead Man Walking" },
            new("More Scars Than Skin", "The next time you'd take a debility or suffer great personal harm, don't; gain an Eternal Mark instead.") { Requires = "Further Marked" },
            new("Otherworldly", "Take another move from a playbook no other player is using.") { Multiclass = MulticlassGrant.AnyClass(), Requires = "Worldly" },
            new("Something To Remember Me By", "Whatever you roll on Alone Against the World, leave a permanent mark on the threat: a crippling wound, a devastating loss, or a shattered ideal.") { Requires = "Alone Against the World" },
            new("The Moment We Met, Your Fate Was Sealed", "When you deal damage with You're Already Dead, deal the total to every enemy you gained Fate from.") { Requires = "You're Already Dead" },
            Rolled("Unstoppable", StatType.Con,
                "When you act in spite of a debility, condition or restraint",
                "Hold 2.",
                "Hold 1.",
                miss: "Hold 1, but after you spend it, the debility, condition or restraint gets worse.",
                note: "Spend 1 hold to ignore every debility, condition and restraint on you for a few crucial moments."),
            new("You Shall Not Pass", "Spend 2 hold from Defend to glare an approaching enemy to a halt. On a 12+ Defend, gain 5 hold."),
            Rolled("Your Weapons Cannot Harm Me", StatType.Con,
                "When you take an enemy's attack without striking back",
                "Choose three:",
                "Choose two:",
                miss: "Choose one, and take +1d4 damage from their attacks.",
                options:
                [
                    "Their weapons shatter against you",
                    "Take half damage from the attack, rounded down",
                    "Lesser enemies run from you in fear",
                    "Take +1 forward against them",
                ],
                optionsOn: OptionBands.All),
        ],
        resources:
        [
            new ClassResource("Fate", "Gained instead of rolling damage. Spend it all to deal your damage that many times in one hit.", GrantedBy: "You're Already Dead"),
            new ClassResource("Pain", "Damage you've put off with Dead Man Walking. You take it all at your next short rest.", GrantedBy: "Dead Man Walking"),
        ]);
}
