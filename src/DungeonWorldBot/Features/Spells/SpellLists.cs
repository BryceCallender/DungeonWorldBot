using System.Text.RegularExpressions;

namespace DungeonWorldBot.Features.Spells;

/// <summary>
/// Spell lists from the Dungeon World SRD by Sage LaTorra and Adam Koebel (CC BY 3.0).
/// </summary>
public static partial class SpellLists
{
    private const string Ongoing = "Ongoing";
    private const string Divination = "Divination";
    private const string Enchantment = "Enchantment";
    private const string Evocation = "Evocation";
    private const string Illusion = "Illusion";
    private const string Summoning = "Summoning";

    private const string CastPenalty = " While this spell is ongoing you take -1 to cast a spell.";

    private static Spell Cleric(string name, int level, string description, params string[] tags) => new(name, SpellList.Cleric, level, description, tags);
    private static Spell Wizard(string name, int level, string description, params string[] tags) => new(name, SpellList.Wizard, level, description, tags);

    public static readonly IReadOnlyList<Spell> All =
    [
        // Cleric rotes
        Cleric("Light", 0, "An item you touch glows with divine light, about as bright as a torch. It gives off no heat or sound and needs no fuel, but is otherwise like a mundane torch. You control the color of the flame. It lasts as long as it's in your presence."),
        Cleric("Sanctify", 0, "Food or water you hold while casting is consecrated by your deity. As well as becoming holy or unholy, it's purified of any mundane spoilage."),
        Cleric("Guidance", 0, "Your deity's symbol appears before you, gestures toward the direction or course of action your deity would have you take, then disappears. It communicates through gesture only."),

        // Cleric level 1
        Cleric("Bless", 1, "Your deity smiles on a combatant of your choice. They take +1 ongoing so long as battle continues and they stand and fight." + CastPenalty, Ongoing),
        Cleric("Cure Light Wounds", 1, "At your touch wounds scab and bones stop aching. Heal an ally you touch of 1d8 damage."),
        Cleric("Detect Alignment", 1, "Choose an alignment: Good, Evil, Lawful or Chaotic. One of your senses briefly detects it, and the GM tells you what here is of that alignment."),
        Cleric("Cause Fear", 1, "Choose a target you can see and a nearby object. The target is afraid of the object while you maintain the spell; how they react (flee, panic, beg, fight) is up to them. It can't target things with less than animal intelligence (constructs, undead, automatons and the like)." + CastPenalty, Ongoing),
        Cleric("Magic Weapon", 1, "The weapon you hold while casting does +1d4 damage until you dismiss this spell." + CastPenalty, Ongoing),
        Cleric("Sanctuary", 1, "Walk the perimeter of an area as you cast, consecrating it to your deity. While you stay inside, you're alerted whenever someone acts with malice within it (including entering with harmful intent). Anyone healed within the sanctuary heals +1d4 HP."),
        Cleric("Speak With Dead", 1, "A corpse converses with you briefly, answering any three questions you pose as best it can, with what it knew in life and learned in death."),

        // Cleric level 3
        Cleric("Animate Dead", 3,
            "A hungry spirit possesses a recently dead body to serve you as a zombie that follows your orders as best it can. It's a character with only the basic moves, +1 to all stats and 1 HP, plus 1d4 traits of your choice:\n" +
            "• Talented: one stat is +2\n• Durable: +2 HP per level you have\n• It has a working brain and can do complex tasks\n• It doesn't look obviously dead, for a day or two\n" +
            "It lasts until destroyed or you end the spell." + CastPenalty, Ongoing),
        Cleric("Cure Moderate Wounds", 3, "You staunch bleeding and set bones through magic. Heal an ally you touch of 2d8 damage."),
        Cleric("Darkness", 3, "Choose an area you can see: it's filled with supernatural darkness and shadow." + CastPenalty, Ongoing),
        Cleric("Resurrection", 3,
            "Tell the GM you'd like to resurrect a corpse whose soul hasn't fully left this world. It's always possible, but the GM gives you one or more conditions: it takes days, weeks or months; you need help from someone; it costs a lot of money; you must sacrifice something. " +
            "The GM may let it happen now on the understanding the conditions must be met for it to last, or require them first."),
        Cleric("Hold Person", 3, "Choose a person you can see. Until you cast a spell or leave their presence, they can't act except to speak. It ends immediately if they take damage from any source."),

        // Cleric level 5
        Cleric("Revelation", 5, "Your deity answers with a moment of perfect understanding: the GM sheds light on the current situation. Take +1 forward when acting on it."),
        Cleric("Cure Critical Wounds", 5, "Heal an ally you touch of 3d8 damage."),
        Cleric("Divination", 5, "Name a person, place or thing you want to learn about. Your deity grants you visions of it, as clear as if you were there."),
        Cleric("Contagion", 5, "Choose a creature you can see. Until you end the spell, it suffers a disease of your choice." + CastPenalty, Ongoing),
        Cleric("Words of the Unspeaking", 5, "With a touch you speak to the spirits within things. The non-living object you touch answers three questions as best it can."),
        Cleric("True Seeing", 5, "Your vision opens to the true nature of everything you look at, piercing illusions and seeing hidden things. The GM describes the area ignoring any illusions and falsehoods, magical or otherwise." + CastPenalty, Ongoing),
        Cleric("Trap Soul", 5, "You trap the soul of a dying creature in a gem. It's aware of its imprisonment but can still be affected by spells, parley and other effects; all moves against it are at +1. You can free it any time, but it can never be recaptured."),

        // Cleric level 7
        Cleric("Word of Recall", 7, "Choose a word. The first time you speak it after casting, you and any allies who touched you during the casting return to the exact spot where you cast it. Only one location at a time; casting again replaces it."),
        Cleric("Heal", 7, "Touch an ally and heal their damage by up to your maximum HP."),
        Cleric("Harm", 7, "Touch an enemy and strike them with divine wrath: 2d8 damage to them and 1d6 to you. This damage ignores armor."),
        Cleric("Sever", 7, "Choose an appendage on the target (an arm, tentacle, wing). It's magically severed, causing no damage but considerable pain, which may stop a winged creature flying or a bull goring you." + CastPenalty, Ongoing),
        Cleric("Mark of Death", 7, "Choose a creature whose true name you know. You create permanent runes on a surface that will kill that creature if it reads them."),
        Cleric("Control Weather", 7, "Pray for rain, sun, wind or snow. Within a day or so your god answers: the weather changes as you wish and lasts a handful of days."),

        // Cleric level 9
        Cleric("Storm of Vengeance", 9, "Your deity brings unnatural weather of your choice: rain of blood or acid, clouds of souls, wind that carries away buildings, anything you can imagine."),
        Cleric("Repair", 9, "Choose one event in the target's past. All its effects (damage, poison, disease, magic) are ended and repaired: HP and diseases healed, poisons neutralized, magical effects ended."),
        Cleric("Divine Presence", 9, "Every creature must ask your leave to enter your presence, and you must grant it aloud. Any creature without your leave takes an extra 1d10 damage whenever it takes damage in your presence." + CastPenalty, Ongoing),
        Cleric("Consume Unlife", 9, "The mindless undead you touch is destroyed and you steal its death energy to heal yourself or the next ally you touch, by the HP it had left."),
        Cleric("Plague", 9, "Name a city, town, encampment or other place people live. While the spell lasts it's beset by a plague fitting your deity's domain (locusts, death of the firstborn...)." + CastPenalty, Ongoing),

        // Wizard cantrips
        Wizard("Light", 0, "An item you touch glows with arcane light, about as bright as a torch. It gives off no heat or sound and needs no fuel, but is otherwise like a mundane torch. You control the color of the flame. It lasts as long as it's in your presence."),
        Wizard("Unseen Servant", 0, "You conjure an invisible construct that can only carry items. It has Load 3 and carries whatever you hand it (it can't pick things up itself); they float a few paces behind you. It's dispelled if it takes damage or leaves your presence, dropping everything; otherwise it lasts until you end the spell.", Ongoing),
        Wizard("Prestidigitation", 0, "Minor tricks of true magic. Touching an item, you can make cosmetic changes: clean, soil, cool, warm, flavor or recolor it. Without touching one, you can make crude illusions no bigger than yourself that won't fool anyone, but might entertain them."),

        // Wizard level 1
        Wizard("Contact Spirits", 1, "Name the spirit you wish to contact (or leave it to the GM). You pull it through the planes just close enough to speak. It must answer any one question as best it can.", Summoning),
        Wizard("Detect Magic", 1, "One of your senses is briefly attuned to magic. The GM tells you what here is magical.", Divination),
        Wizard("Telepathy", 1, "You form a telepathic bond with one person you touch, letting you converse through thoughts. Only one bond at a time.", Divination, Ongoing),
        Wizard("Charm Person", 1, "The person (not beast or monster) you touch while casting counts you as a friend until they take damage or you prove otherwise.", Enchantment, Ongoing),
        Wizard("Invisibility", 1, "Touch an ally: nobody can see them until they attack or you dismiss it. While it's ongoing you can't cast a spell.", Illusion, Ongoing),
        Wizard("Magic Missile", 1, "Projectiles of pure magic spring from your fingers. Deal 2d4 damage to one target.", Evocation),
        Wizard("Alarm", 1, "Walk a wide circle as you cast. Until you next prepare spells, your magic alerts you if a creature crosses it, waking you if you're asleep."),

        // Wizard level 3
        Wizard("Dispel Magic", 3, "Choose a spell or magic effect in your presence: this spell rips it apart. Lesser spells end; powerful magic is reduced or dampened while you're nearby."),
        Wizard("Visions Through Time", 3, "Gaze into a reflective surface to see into the depths of time. The GM reveals a grim portent, a bleak event that will happen without your intervention, and something useful about how to interfere with it.", Divination),
        Wizard("Fireball", 3, "A mighty ball of flame envelops your target and everyone nearby: 2d6 damage that ignores armor.", Evocation),
        Wizard("Mimic", 3, "You take the form of someone you touch while casting. You match them physically, though not in behavior, until you take damage or choose to change back. While it's ongoing you lose access to all your wizard moves.", Ongoing),
        Wizard("Mirror Image", 3, "You create an illusory image of yourself. When attacked, roll a d6: on 4-6 the attack hits the illusion, which dissipates, ending the spell.", Illusion),
        Wizard("Sleep", 3, "1d4 enemies you can see, of the GM's choice, fall asleep. Only creatures that sleep are affected. They wake as normal: loud noises, jolts, pain.", Enchantment),

        // Wizard level 5
        Wizard("Cage", 5, "The target is held in a cage of magical force that nothing can get into or out of, until you cast another spell or dismiss it. While it lasts, the caged creature can hear your thoughts and you can't leave sight of the cage.", Evocation, Ongoing),
        Wizard("Contact Other Plane", 5, "Send a request to another plane, naming who or what to contact by location, type, name or title. You open two-way communication that either of you can cut off at any time.", Divination),
        Wizard("Polymorph", 5, "Your touch reshapes a creature entirely until you cast a spell. Describe the new form, including stat changes, adaptations and weaknesses. The GM tells you one or more: the form is unstable and temporary; their mind is altered too; the form has an unintended benefit or weakness.", Enchantment),
        Wizard("Summon Monster", 5,
            "A monster appears and aids you as best it can. It's a character with only the basic moves, +1 to all stats, 1 HP and your damage dice, plus 1d6 traits of your choice:\n" +
            "• +2 instead of +1 to one stat\n• It's not reckless\n• It does 1d8 damage\n• Strong bond to this plane: +2 HP per level you have\n• A useful adaptation\n" +
            "The GM says what monster it is from the traits. It stays until it dies or you dismiss it." + CastPenalty, Summoning, Ongoing),

        // Wizard level 7
        Wizard("Dominate", 7,
            "Your touch pushes your mind into someone else's. Gain 1d4 hold; spend 1 to make them:\n" +
            "• Speak a few words of your choice\n• Give you something they hold\n• Make a concerted attack on a target of your choice\n• Truthfully answer one question\n" +
            "The spell ends when hold runs out; you lose 1 hold if they take damage. While it's ongoing you can't cast a spell.", Enchantment, Ongoing),
        Wizard("True Seeing", 7, "You see all things as they truly are, until you tell a lie or dismiss the spell." + CastPenalty, Divination, Ongoing),
        Wizard("Shadow Walk", 7, "Shadows you target become a portal for you and your allies. Describe a location in a number of words up to your level; stepping through takes you and allies present at the casting there. Each ally can use it once.", Illusion),
        Wizard("Contingency", 7, "Choose a 5th level or lower spell you know and describe a trigger in a number of words equal to your level. The spell is held until you unleash it or the trigger happens, taking effect without a roll. Only one held spell at a time; casting again replaces it.", Evocation),
        Wizard("Cloudkill", 7, "A fog from beyond the Black Gates of Death fills the area. Whenever a creature in it takes damage, it takes a separate 1d6 damage that ignores armor. It lasts while you can see the area, or until you dismiss it.", Summoning, Ongoing),

        // Wizard level 9
        Wizard("Antipathy", 9, "Choose a target and describe a type of creature or an alignment. Such creatures can't come within sight of the target, and flee if they do, until you leave its presence or dismiss the spell." + CastPenalty, Enchantment, Ongoing),
        Wizard("Alert", 9, "Describe an event. The GM tells you when it happens, however far away, and you can view it as if you were there. Only one Alert at a time.", Divination),
        Wizard("Soul Gem", 9, "You trap the soul of a dying creature in a gem. It's aware of its imprisonment but can still be affected by spells, parley and other effects; all moves against it are at +1. You can free it any time, but it can never be recaptured."),
        Wizard("Shelter", 9, "You create a structure of pure magic, as large as a castle or as small as a hut, impervious to all non-magical damage. It lasts until you leave it or end the spell.", Evocation, Ongoing),
        Wizard("Perfect Summons", 9, "Teleport a creature to you. Name a specific creature and it appears, or describe a type and one of that type appears.", Summoning),
    ];

    // "Light (Cleric)", the form autocomplete uses for names on more than one list
    [GeneratedRegex(@"^(?<name>.+?)\s*\((?<list>[^()]+)\)\s*$")]
    private static partial Regex QualifiedName();

    public static IEnumerable<Spell> For(SpellList list) => All.Where(s => s.List == list);

    /// <summary>
    /// Spells matching a name, optionally written as "Name (List)".
    /// </summary>
    public static IReadOnlyList<Spell> Find(string input)
    {
        var name = input.Trim();
        SpellList? list = null;

        if (QualifiedName().Match(name) is { Success: true } match && Enum.TryParse<SpellList>(match.Groups["list"].Value, ignoreCase: true, out var parsed))
        {
            name = match.Groups["name"].Value;
            list = parsed;
        }

        return All.Where(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && (list is null || s.List == list)).ToList();
    }
}
