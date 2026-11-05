using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Moves;
using static DungeonWorldBot.Features.Classes.ClassMove;

namespace DungeonWorldBot.Features.Classes.GrimWorld;

public static class Battlemaster
{
    private static ClassMove Tactic(string name, string summary) =>
        new($"Default Tactic: {name}", $"{summary} You're always in this Tactic until you Change Tactics, and can spend 1 Gambit at any time to return to it.");

    public static readonly ClassSheet Sheet = new(
        ClassType.Battlemaster,
        "Grim World",
        new ClassColor("#4c6480", "#9aafca"),
        startingMoves:
        [
            new("Gambit",
                "When you or someone you have a bond with takes damage from an outside source, gain 1 Gambit (it was all part of the plan). " +
                "Reveal your ploy at any time and spend Gambits, stacking as you like:\n" +
                "• +1 to anyone's roll (1)\n• Deal your damage to an enemy (2)\n" +
                "• Create an obstacle or distraction one enemy must deal with first (3)\n" +
                "• The GM helps you reveal a trap, ambush or plan that gives your side a surprising advantage (4)\n" +
                "Gambits reset to 0 after a battle."),
            new("Heavy Armor Training", "Ignore the clumsy tag on armor you wear."),
            new("Tactics",
                "Only one Tactic is active at a time.\n" +
                "• **Aggressive:** Hack and Slash deals your damage even on a 6-.\n" +
                "• **Cautious:** +1 to Defy Danger.\n" +
                "• **Reactive:** You can't Hack and Slash, but you can Counterattack.\n" +
                "• **Defensive:** When you Defend, a 6- counts as a 7-9.\n" +
                "• **Reckless:** Deal +1d4 damage, and take +1d4 damage."),
            Rolled("Change Tactics", StatType.Int,
                "When you draw on your training to size up a fight",
                "You form a plan and pick a new Tactic.",
                "You pick a new Tactic, but choose one:",
                options:
                [
                    "The Tactic is slow to take hold: -1 forward",
                    "An earlier plan set this up: spend 1 Gambit",
                    "It puts you in a spot or draws attention to you (the GM says how)",
                ],
                optionsOn: OptionBands.WeakHit),
            Rolled("Counterattack", StatType.Dex,
                "When you're attacked in melee while using Reactive Tactics and try to counter",
                "You parry the attack and riposte, dealing your damage.",
                "You expertly parry the attack."),
            new("War Knowledge", "+1 to Spout Lore about combat: historic battles, military figures, martial weapons and armor."),
            new("Battle of Wits", "When an enemy is watching you and you face a hard choice, the GM tells you what they expect you to do."),
        ],
        features:
        [
            new ClassFeature("Default Tactic", "Which Tactic is your Default Tactic?",
            [
                Tactic("Aggressive", "Hack and Slash deals your damage even on a 6-."),
                Tactic("Cautious", "+1 to Defy Danger."),
                Tactic("Reactive", "You can't Hack and Slash, but you can Counterattack."),
                Tactic("Defensive", "When you Defend, a 6- counts as a 7-9."),
                Tactic("Reckless", "Deal +1d4 damage, and take +1d4 damage."),
            ]),
        ],
        level2To5:
        [
            new("Block and Strike", "Add your shield's armor to your damage. With a shield in Reactive Tactics, Counterattack can roll +STR instead."),
            new("Battle Leader", "+1 to Order Hirelings. Hirelings often accept your training as payment."),
            new("Reckless Aggression", "Reckless Tactics: your melee attacks gain +2 piercing. Aggressive Tactics: roll damage twice and keep the better result."),
            new("Combat Sight", "Cautious Tactics: a 10+ on Defy Danger gains you a Gambit. Reactive Tactics: a 10+ on Counterattack lets you ask the GM one Discern Realities question."),
            new("Intelligent Defense", "You can Defend with INT instead of CON. In Defensive Tactics, spend 1 Gambit to halve an attack's damage or effect on you or what you're Defending."),
            new("Battlefield Motivation", "When you rally your allies mid-battle, spend 1 Gambit: they each ignore one debility of their choice until it gets worse or the battle ends. You can skip the Gambit, but you draw unwanted attention (the GM says how)."),
            new("Risky Gambit", "When you tell an ally to do something obviously dangerous and it puts them in a spot, gain 1 Gambit."),
            new("Misdirection", "While you hold an enemy's attention, spend 2 Gambits to let an ally make a free attack against it, as if they rolled a 10+."),
            new("Phase 2", "When you kill an enemy, you can Change Tactics for free."),
            new("Playing the Long Game", "When your Gambits would reset to 0, keep 1."),
            new("Multiclass Learner", "Take one move from another class (not Called Shot or Backstab), treating your level as one lower. Tell everyone how you learned it.") { Multiclass = MulticlassGrant.AnyClass(levelOneLower: true) with { ExcludedMoves = ["Called Shot", "Backstab"] } },
        ],
        level6To10:
        [
            new("Lethal Gambit", "Spend 3 Gambits to add +1d10 damage to anyone's attack."),
            new("Ambush Master", "When you set up a surprise attack or trap, name Strikers equal to your INT modifier (min 1, max 3): yourself, allies or trap objects. The first time each Striker deals damage, they roll twice and keep the better."),
            new("Combat Assessment", "Add \"What mistake has my enemy made that I can exploit?\" to your Discern Realities questions. On a 12+ Change Tactics, you can ask it for free."),
            new("All Part of the Plan", "Spend 2 Gambits to negate the damage of any attack."),
            new("Self Defense Training", "Spend an hour or two drilling your allies against an enemy you know. The GM picks the Defy Danger stat and you explain why it works; against that enemy, Defying Danger that way takes +1. Practice daily to keep it up."),
            new("Battlefield Desperation", "Battlefield Motivation turns the chosen debility into a +1 to that modifier instead (max +3), until it gets worse or the battle ends.") { Requires = "Battlefield Motivation" },
            new("Command Ally", "Spend 2 Gambits to let an ally make a free attack against an enemy, as if they rolled a 10+. Spend 1 more to maximize its damage.") { Replaces = "Misdirection" },
            new("Surprise Attack", "Gain Called Shot (Ranger) or Backstab (Thief). When you coordinate with a Ranger or Thief to use it together, they add your INT modifier to their first attack's damage.") { Multiclass = MulticlassGrant.FromClasses(ClassType.Ranger, ClassType.Thief) with { OnlyMoves = ["Called Shot", "Backstab"] } },
            new("Tactical Teacher", "Gain the bond \"I will teach ___ to have a tactical mind.\" That ally can choose to gain the effects of your current Tactic."),
        ],
        deathMove: new("Death: Grand Plan",
            "When you die (failing or forgoing Last Breath), your death is revealed as your final gambit. Work with the GM to reveal your master plot: it gives your allies a major advantage, sets your enemies up for destruction, or sets big world events in motion."),
        resources:
        [
            new ClassResource("Gambit", "Gained when you or a bonded ally takes outside damage. Spent on Gambit options. Resets to 0 after a battle.")
            {
                Bonuses = [new("Playing the Long Game", Reset: 1)],
            },
        ]);
}
