using DungeonWorldBot.Data.Entities;
using static DungeonWorldBot.ClassSheets.ClassMove;

namespace DungeonWorldBot.ClassSheets.DungeonWorld;

/// <summary>
/// Summarised from the Dungeon World SRD by Sage LaTorra and Adam Koebel (CC BY 3.0).
/// </summary>
public static class Druid
{
    private static ClassMove Land(string name) =>
        new($"Land: {name}", $"You're attuned to {name}: you can shapeshift into any animal that might live there.");

    public static readonly ClassSheet Sheet = new(
        ClassType.Druid,
        "Dungeon World",
        startingMoves:
        [
            new("Born of the Soil", "The spirits of your land have marked you as their own and let you take their shape wherever you go. Choose a tell (antlers, leopard's spots, hair like leaves...) that stays in every shape."),
            new("By Nature Sustained", "You don't need to eat or drink. Ignore moves that tell you to mark off a ration."),
            new("Spirit Tongue", "You understand the calls of any animal native to your land, or akin to one whose essence you've studied."),
            Rolled("Shapeshifter", StatType.Wis,
                "When you call on the spirits to change your shape",
                "Hold 3.",
                "Hold 2.",
                miss: "Hold 1, plus whatever the GM says.",
                note: "Become any species from your land or whose essence you've studied, with its natural abilities and weaknesses but your own stats. " +
                      "The GM gives you moves for the form; spend 1 hold to make one. When hold runs out, or you spend it all at once, you return to your natural form."),
            new("Studied Essence", "When you spend time contemplating an animal spirit, add its species to the shapes you can take."),
        ],
        features:
        [
            new ClassFeature("Race Move", "Which race move do you have?",
            [
                new("Race: Elf", "The sap of the elder trees flows in you: the Great Forest always counts as your land, alongside any other."),
                new("Race: Human", "You're bound to farm and field: you can always take the shape of any domesticated animal."),
                new("Race: Halfling", "You sing the healing songs of spring and brook: when you Make Camp, you and your allies heal +1d6."),
            ]),
            new ClassFeature("Land", "Which land are you attuned to?",
            [
                Land("The Great Forests"), Land("The Whispering Plains"), Land("The Vast Desert"), Land("The Stinking Mire"),
                Land("The River Delta"), Land("The Depths of the Earth"), Land("The Sapphire Islands"), Land("The Open Sea"),
                Land("The Towering Mountains"), Land("The Frozen North"), Land("The Blasted Wasteland"),
            ])
            {
                ExtraPicks = [("The Druid Sleep", 1)],
            },
        ],
        level2To5:
        [
            new("Hunter's Brother", "Choose one move from the Ranger class list."),
            new("Red of Tooth and Claw", "In a dangerous animal form, your damage is d8."),
            Rolled("Communion of Whispers", StatType.Wis,
                "When you spend time in a place, noting its spirits and calling on the spirits of the land",
                "You're granted a clear, helpful vision of significance to you, your allies and the spirits around you.",
                "You're granted a vision, but its meaning is murky.",
                miss: "The vision is upsetting, frightening or traumatizing (the GM describes it). Take -1 forward."),
            new("Barkskin", "+1 armor while your feet touch the ground."),
            new("Eyes of the Tiger", "Mark an animal with mud, dirt or blood to see through its eyes at any distance. One animal at a time."),
            new("Shed", "When you take damage while shapeshifted, you can revert to your natural form to negate it."),
            new("Thing-Talker", "Spirit Tongue, Shapeshifter and Studied Essence also work on inanimate natural things (plants, rocks) and creatures made of them, as exact copies or mobile, vaguely humanoid shapes."),
            new("Formcrafter", "When you shapeshift, pick a stat to get +1 ongoing while shifted. The GM picks one for -1 ongoing."),
            Rolled("Elemental Mastery", StatType.Wis,
                "When you call on the primal spirits of fire, water, earth or air to do a task for you",
                "Choose two:",
                "Choose one:",
                miss: "Some catastrophe results from your calling.",
                options:
                [
                    "The effect you want happens",
                    "You avoid paying nature's price",
                    "You keep control",
                ]),
            new("Balance", "When you deal damage, take 1 Balance. When you touch someone and channel the spirits of life, spend Balance to heal 1d4 HP each."),
        ],
        level6To10:
        [
            new("Embracing No Form", "When you shapeshift, roll 1d4 and add it to your hold."),
            new("Doppelgänger's Dance", "You can study the essence of specific people to take their exact form. Hiding your tell gives -1 ongoing until you return to your own form."),
            new("Blood and Thunder", "In a dangerous animal form, your damage is d10.") { Replaces = "Red of Tooth and Claw" },
            new("The Druid Sleep", "The next time you have safety and time somewhere fitting, attune yourself to a second land (the GM sets the time and cost). You're born of the soil of both."),
            new("World-Talker", "Spirit Tongue, Shapeshifter and Studied Essence also work on the pure elements: fire, water, air and earth.") { Requires = "Thing-Talker" },
            new("Stalker's Sister", "Choose one move from the Ranger class list."),
            new("Formshaper", "In an animal form, gain +1 armor or +1d4 damage, chosen when you shapeshift.") { Requires = "Formcrafter" },
            new("Chimera", "When you shapeshift, you can merge up to three shapes, each feature granting its own move."),
            new("Weather Weaver", "When the sun rises and you're under open skies, the GM asks what the weather will be today. Whatever you say comes to pass."),
        ],
        resources:
        [
            new ClassResource("Shapeshift Hold", "Spent on your form's moves. When it runs out, you return to your natural form."),
            new ClassResource("Balance", "Gained when you deal damage. Spend it to heal 1d4 HP per point.", GrantedBy: "Balance"),
        ]);
}
