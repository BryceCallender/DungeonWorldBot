using DungeonWorldBot.Data.Entities;
using static DungeonWorldBot.Features.Classes.ClassMove;

namespace DungeonWorldBot.Features.Classes.GrimWorld;

public static class Templar
{
    public static readonly ClassSheet Sheet = new(
        ClassType.Templar,
        "Grim World",
        new ClassColor("#c0620c", "#f2a052"),
        startingMoves:
        [
            new("Wrath Embodied", "You hold up to 3 Wrath. Spend an uninterrupted hour or so on your purification ritual to set your Wrath back to 3."),
            new("Penance", "Quickly punish yourself (cilice, whip, knout...) to take 1d4 damage (ignores armor) and gain 1 Wrath."),
            new("Smite", "On a 10+ Hack and Slash, you can spend 1 Wrath to deal +1d4 damage."),
            new("Divine Force", "On a 10+ Volley, you can spend 1 Wrath to knock back every enemy close to where it lands."),
            new("Blinding Light", "On a 10+ Defy Danger, you can spend 1 Wrath to blind your attacker with holy light."),
            new("Consecrated Ground", "On a 10+ Defend, you can spend 1 Wrath to consecrate the ground around you. Enemies take damage equal to your level when they first enter it."),
            Rolled("Confession", StatType.Cha,
                "When you grant absolution to someone confessing a sin or wrongdoing",
                "They heal 1d8 damage.",
                "They heal 1d8 damage, but their sin sullies your mind: perform penance and take 1d4 damage (ignores armor)."),
            Rolled("Inquisitor", StatType.Cha,
                "When you question a captive",
                "They make three statements, two true and one a lie, and the GM tells you one that's definitely true.",
                "They make three statements: two true, one a lie.",
                miss: "They won't break, they hold back, or something goes very, very wrong.",
                note: "To press further, guess which statement is the lie. It leaves serious scars, but if you're right they break and truthfully answer your questions."),
        ],
        features:
        [
            new ClassFeature("Facet of Inquisition", "Which Facet of Inquisition do you favor?",
            [
                new("Facet: Fear", "+1 to Parley when the character fears you."),
                new("Facet: Surprise", "+1 piercing against surprised enemies."),
                new("Facet: Ruthless Efficiency", "As Quartermaster on a Perilous Journey, treat a 7-9 as a 10+."),
                new("Facet: Fanatical Devotion", "Ignore the effects of CHA debilities.") { IgnoresDebility = StatType.Cha },
            ])
            {
                ExtraPicks = [("Nobody Expects", 3)],
            },
        ],
        level2To5:
        [
            new("Red Hot Wrath", "Add your current Wrath to your damage. Smite costs no Wrath on a 10+."),
            new("Armor of Wrath", "Ignore the clumsy tag on all armor. When an enemy's skin touches your armor, they take your current Wrath as damage (ignores armor)."),
            new("Clean Conscience", "When you hear someone's Confession, their next attack deals +1d4 damage."),
            new("Prophetic Dream", "After a few hours of sleep, wake with 1 Prophecy and a vague warning of danger in the next day or so. Spend a Prophecy to make a Defy Danger an automatic 10+."),
            new("Keen to Inquire", "You can roll +Wrath instead of CHA for Confession and Inquisitor."),
            new("Stench of Guilt", "You can smell guilt. Most people have at least a faint stink; those with none at all are either truly pious or truly evil, and the pious are rare."),
            new("Warming Up", "When an ally Aids you, you can gain 1 Wrath instead of taking +1."),
            new("White Knight", "Take a move from the Paladin list. If there's a Paladin in the party, you need similar beliefs or at least mutual respect.") { Multiclass = MulticlassGrant.FromClasses(ClassType.Paladin) },
            new("Cleansing Censer", "Gain a blessed censer of holy flame (melee, reach, +1 damage). When you purify your campsite with it as you Make Camp, anyone who sleeps a few hours heals to full."),
            new("Judgment", "Spend 1 Wrath to declare someone deserves punishment. The next time they deal damage, they take 1d6 damage (ignores armor). Describe how."),
            new("Holy Vengeance", "When you kill an enemy that harmed you, gain 1 Wrath."),
        ],
        level6To10:
        [
            new("Soul Fire", "Smite deals +1d8 damage instead and works on a 7+."),
            new("Nobody Expects", "Gain every Facet of Inquisition."),
            new("Friend of Sinners", "On a 7+ Confession, they heal 2d8 instead and no penance is owed. On a 10+, you also heal 1d8."),
            new("Vivid Prophecy", "Prophetic Dream gives you 2 Prophecy, and your dreams can be about anyone you have a bond with.") { Requires = "Prophetic Dream" },
            new("We Have Ways", "As Inquisitor, you can treat a 6- as a 7-9 or a 7-9 as a 10+, but the captive dies at the end."),
            Rolled("Zealotry", StatType.Cha,
                "When you use your holy authority to push a believer into something clearly dangerous",
                "They do it. Choose one option for free, and spend 1 Wrath for each other option:",
                "They do it. Spend 1 Wrath for each option you choose:",
                options:
                [
                    "They try as hard as they can without hesitating",
                    "They won't give up, even facing certain death",
                    "They spread the word and rally other believers",
                ]),
            new("Holy Crusade", "Take another move from the Paladin list. A Paladin in the party can take one of your moves when they level up.") { Multiclass = MulticlassGrant.FromClasses(ClassType.Paladin), Requires = "White Knight" },
            new("Miracle of Healing", "When you Make Camp with your Cleansing Censer, anyone who sleeps a few hours can heal to full or remove 1 debility.") { Requires = "Cleansing Censer" },
            new("Bless Ally",
                "When you Aid an ally, you can spend 1 Wrath to choose one:\n" +
                "• Their weapon deals +1d8 damage on their next attack\n• The next damage they take is halved"),
            new("Wrathful", "Divine Force, Blinding Light and Consecrated Ground work on a 7+. On a 12+, they cost no Wrath."),
        ],
        deathMove: new("Death: Angel of Wrath",
            "When you die (failing or forgoing Last Breath), holy fire pours out of you and the ground you touch is forever consecrated. Until your wrath reaches everyone nearby who deserves it, you succeed at every roll and deal max damage. The GM will tell you when your work is done."),
        resources:
        [
            new ClassResource("Wrath", "Spent on Smite, Divine Force, Blinding Light and Consecrated Ground. Penance gains 1; your purification ritual restores it to 3.", Start: 3, Max: 3),
            new ClassResource("Prophecy", "Gained from Prophetic Dreams. Spend one to make a Defy Danger an automatic 10+.", GrantedBy: "Prophetic Dream"),
        ]);
}
