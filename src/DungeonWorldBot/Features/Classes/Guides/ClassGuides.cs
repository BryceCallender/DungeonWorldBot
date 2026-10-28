using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Subclasses;
using DungeonWorldBot.Rules;

namespace DungeonWorldBot.Features.Classes.Guides;

/// <summary>
/// How to play each class with the bot's commands. Every guide runs the same way: make the character, the basic moves,
/// the class's rolled moves, then whatever is special about the class, then downtime, subclasses and levelling up. All but
/// the special steps are built from the sheet.
/// </summary>
public static class ClassGuides
{
    private static readonly Dictionary<ClassType, WrittenGuide> Written = new()
    {
        [ClassType.Ranger] = new(
            "A hunter at home in the wild, deadly at range, with an animal companion at their side.",
            [
                Step("Work with your companion",
                    "The bot doesn't track your animal companion, so **Command** goes on the roll by hand: when it attacks your " +
                    "target, put its ferocity in `extra-damage`; when it helps you track or discern realities, put its cunning in `bonus`.",
                    Cmd("/move hack-and-slash"), Cmd("/move volley"), Cmd("/move discern-realities")),
                Step("Later: a god's favor",
                    "Take **God Amidst the Wastes** and you cast cleric spells. Your cleric level is worked out for you; " +
                    "learn spells with `/spell learn` and check what you have with `/spell known`.",
                    Cmd("/spell learn"), Cmd("/spell known")),
            ]),

        [ClassType.Paladin] = new(
            "A holy warrior on a quest, healing allies and commanding foes with divine authority.",
            [
                Step("Heal at a cost",
                    "A hit on **Lay on Hands** heals with `/character heal`. On a 7-9 the hurt moves to you, so take it with " +
                    "`/character damage` on yourself.",
                    Cmd("/character heal"), Cmd("/character damage")),
                Step("Later: Divine Favor",
                    "**Divine Favor** gives you cleric spells. Learn them with `/spell learn` and check them with `/spell known`.",
                    Cmd("/spell learn"), Cmd("/spell known")),
            ]),

        [ClassType.Druid] = new(
            "A shapeshifter bound to their land. The bot tracks the forms you know, the one you're in and your hold.",
            [
                Step("Learn your forms",
                    "Add every species of your land you might turn into with `/shapeshift learn`, with the moves the GM gives it. " +
                    "**Studied Essence** adds more. `/shapeshift forms` shows what you know.",
                    Cmd("/shapeshift learn"), Cmd("/shapeshift forms"), Cmd("/shapeshift edit")),
                Step("Change shape",
                    "`/shapeshift into` rolls **Shapeshifter** (+WIS) and holds 3, 2 or 1 for you.",
                    Cmd("/shapeshift into", note: "+WIS")),
                Step("Act in your form",
                    "Spend hold on your form's moves with `/shapeshift move`. When it runs out you change back, " +
                    "or return early with `/shapeshift revert`.",
                    Cmd("/shapeshift move"), Cmd("/shapeshift revert")),
                Step("Later: Balance",
                    "With **Balance**, gain a point whenever you deal damage and spend them to heal 1d4 each.",
                    Cmd("/resource gain", "Balance"), Cmd("/resource spend", "Balance")),
            ]),

        [ClassType.Barbarian] = new(
            "A hungry outsider who fights in a frenzy, chasing their appetites wherever they lead.",
            [
                Step("Feed your appetites",
                    "While chasing an appetite you roll 1d6+1d8 instead of 2d6, which the move commands don't do. " +
                    "Roll it with `/roll stat`, picking the stat and `1d6+1d8` as the dice. If the d6 is higher, the GM adds a complication.",
                    Cmd("/roll stat")),
                Step("Cheat death",
                    "**The Upper Hand** gives +1 to Last Breath; add it to what `/move last-breath` shows.",
                    Cmd("/move last-breath")),
            ]),

        [ClassType.Bard] = new(
            "A performer whose songs heal, inspire and charm, and the party's best talker.",
            [
                Step("Heal with your art",
                    "When **Arcane Art** heals an ally, put it on them with `/character heal`.",
                    Cmd("/character heal")),
                Step("Back your friends",
                    "`/move aid` rolls with your bonds with that player, so make bonds with `/character bond`.",
                    Cmd("/character bond"), Cmd("/move aid")),
            ]),

        [ClassType.Cleric] = new(
            "A servant of their deity, granted spells to heal, protect and smite.",
            [
                Step("Commune",
                    "Each time you commune, start over with `/spell clear` and pick your spells with `/spell learn`: none above your " +
                    "level, up to your level+1 in total. Rotes are always yours. `/spell known` shows what you've got.",
                    Cmd("/spell clear"), Cmd("/spell learn"), Cmd("/spell known")),
                Step("After you cast",
                    "If your deity takes a spell back on a 7-9 **Cast a Spell**, remove it with `/ability remove`. " +
                    "Healing spells go on with `/character heal`.",
                    Cmd("/ability remove"), Cmd("/character heal")),
            ]),

        [ClassType.Fighter] = new(
            "A master of one weapon, built for the thick of the fight.",
            [
                Step("Your signature weapon",
                    "Its enhancements set your damage and armor when you make the character. If your gear changes later, " +
                    "fix them with `/character damage-dice` and `/armor set`.",
                    Cmd("/character damage-dice"), Cmd("/armor set"), Cmd("/armor show")),
            ]),

        [ClassType.Immolator] = new(
            "A fire-wielder who conjures blades of flame and bends fire to their will.",
            [
                Step("Track your brand",
                    "After you conjure your **Burning Brand**, set its uses to 3, and spend one with each attack.",
                    Cmd("/resource set", "Burning Brand Uses"), Cmd("/resource spend", "Burning Brand Uses")),
                Step("Fight fire with fire",
                    "When you take odd damage after armor, roll a d4 and add it to your brand's uses, take it forward, " +
                    "or ask the GM to take it off the damage.",
                    Cmd("/roll die"), Cmd("/resource gain", "Burning Brand Uses")),
            ]),

        [ClassType.Thief] = new(
            "A sneak who strikes from the shadows, picks every lock and knows every poison.",
            [
                Step("Backstab without rolling",
                    "**Backstab** lets you just deal your damage instead of rolling. Do that with `/roll damage`.",
                    Cmd("/roll damage")),
                Step("Keep your poison",
                    "Track your poison's doses with `/ability add` and 3 uses. `/ability use` when you coat a blade, " +
                    "and `/ability restore` when you brew more.",
                    Cmd("/ability add"), Cmd("/ability use"), Cmd("/ability restore")),
            ]),

        [ClassType.Wizard] = new(
            "A scholar of the arcane who prepares spells from their spellbook.",
            [
                Step("Prepare spells",
                    "When you study your spellbook, start over with `/spell clear` and prepare with `/spell learn`: up to your " +
                    "level+1 in total. Cantrips are always prepared. Browse with `/spell list` and read one with `/spell info`.",
                    Cmd("/spell clear"), Cmd("/spell learn"), Cmd("/spell list"), Cmd("/spell info")),
                Step("After you cast",
                    "If you forget a spell on a 7-9 **Cast a Spell**, remove it with `/ability remove`.",
                    Cmd("/spell known"), Cmd("/ability remove")),
            ]),

        [ClassType.Battlemaster] = new(
            "A tactician who plans every fight. The bot applies your active Tactic to your moves for you.",
            [
                Step("Know your Tactic",
                    "You start in your Default Tactic. `/tactic show` shows which one you're in and what it does. " +
                    "In Reactive you can't Hack and Slash, but you can **Counterattack**.",
                    Cmd("/tactic show")),
                Step("Switch Tactics",
                    "After **Change Tactics**, switch with `/tactic change`. Spending a Gambit gets you back to your default any time.",
                    Cmd("/tactic change"), Cmd("/tactic default")),
                Step("Build Gambits",
                    "Whenever you or someone you have a bond with takes outside damage, gain a Gambit. Spend them on your options, " +
                    "and reset them after the battle.",
                    Cmd("/resource gain", "Gambit"), Cmd("/resource spend", "Gambit"), Cmd("/resource reset", "Gambit")),
            ]),

        [ClassType.Channeler] = new(
            "A conduit for raw elemental magic, with an Elemental leashed to them by a stream of power.",
            [
                Step("Feel the burn",
                    "On a 7-9 **Channel** the power hurts: take the 1d4 with `/character damage`.",
                    Cmd("/character damage")),
                Step("Build Control",
                    "After **Summon Elemental**, roll the 1d4 and gain that much Control. **Elemental Attack** rolls +Control for you.",
                    Cmd("/roll die"), Cmd("/resource gain", "Control")),
                Step("Spend Control",
                    "Lose 1 on a 7-9 Elemental Attack. When you or your Elemental would take damage, spend 1 to negate it. " +
                    "At 0 it breaks free.",
                    Cmd("/resource spend", "Control"), Cmd("/resource show")),
            ]),

        [ClassType.Necromancer] = new(
            "A stitcher of corpses who raises the dead to do their bidding.",
            [
                Step("Power your corpse",
                    "After **Raise the Dead**, set your Power to what the roll gives you, usually 3.",
                    Cmd("/resource set", "Power")),
                Step("Hold it together",
                    "Lose 1 Power on a 7-9 **Attack Command**, and spend 1 when your corpse takes a big hit to keep the limb. " +
                    "At 0 Power it collapses.",
                    Cmd("/resource spend", "Power"), Cmd("/resource show")),
            ]),

        [ClassType.Slayer] = new(
            "A monster hunter driven by a bloodthirst that grows worse until it's sated by a kill.",
            [
                Step("Feel the thirst",
                    "Every 6- you roll gains you 1 Thirst, and every point brings a Withdrawal debility you don't already have.",
                    Cmd("/resource gain", "Thirst"), Cmd("/debility add")),
                Step("Strike with Thirst",
                    "**Slayer's Strike** lets Hack and Slash roll +Thirst instead of STR, which the bot doesn't do for you: put the " +
                    "difference in `bonus` on `/move hack-and-slash`.",
                    Cmd("/move hack-and-slash")),
                Step("Cathartic kill",
                    "Land the killing blow and your Thirst resets. Remove the Withdrawal debilities too.",
                    Cmd("/resource reset", "Thirst"), Cmd("/debility remove")),
            ]),

        [ClassType.Templar] = new(
            "An inquisitor of the faith who spends Wrath to turn strong hits into holy punishment.",
            [
                Step("Spend Wrath on a 10+",
                    "A 10+ on Hack and Slash, Volley, Defy Danger or Defend lets you spend 1 Wrath for Smite, Divine Force, " +
                    "Blinding Light or Consecrated Ground. Add Smite's 1d4 with `extra-damage`.",
                    Cmd("/resource spend", "Wrath"), Cmd("/move hack-and-slash")),
                Step("Get it back",
                    "**Penance** is 1d4 damage to yourself for 1 Wrath. Your purification ritual sets it back to 3.",
                    Cmd("/character damage"), Cmd("/resource gain", "Wrath"), Cmd("/resource reset", "Wrath")),
            ]),

        [ClassType.Shaman] = new(
            "A spirit-binder who keeps Worldly Spirits in totems and releases them for their power.",
            [
                Step("Bind spirits",
                    "Craft a totem and bind a Worldly Spirit with `/class learn`. You can keep three; each starts with 1 Charge.",
                    Cmd("/class learn"), Cmd("/ability list")),
                Step("Spend Charges",
                    "If a totem loses its Charge after **Spirit Talk**, use it. At 0 it shatters, so remove it.",
                    Cmd("/ability use"), Cmd("/ability remove")),
            ]),

        [ClassType.Captain] = new(
            "The captain of an airship and its crew. The bot tracks your Trade, Map, ship Stress and cannon ammo.",
            [
                Step("Hold Map",
                    "After **Chart the Course**, gain the Map it gives you. Spend it on shortcuts and safe spots; it's gone once you're through.",
                    Cmd("/resource gain", "Map"), Cmd("/resource spend", "Map"), Cmd("/resource reset", "Map")),
                Step("Fight with the ship",
                    "Each cannon shot uses ammo. A single hit of 10+ damage marks a box of Ship Stress.",
                    Cmd("/resource spend", "Cannon Ammo"), Cmd("/resource gain", "Ship Stress")),
                Step("Trade",
                    "Meet merchants in port for a Trade, and spend it somewhere else to carouse or resupply the ship.",
                    Cmd("/resource gain", "Trade"), Cmd("/resource spend", "Trade"), Cmd("/resource show")),
            ]),

        [ClassType.Collector] = new(
            "A hoarder of curios who rolls their own Lore stat, set by what they collect.",
            [
                Step("Dig through your collection",
                    "**Keeper of Curios** can turn up anything; the GM tells you the catch. Last uses get **Make It Count**'s +1.",
                    Cmd("/inventory show")),
                Step("Later: Luck",
                    "With **Lucky Charm**, spend 1 Luck to reroll a 6-, and reset it to 3 when you make camp.",
                    Cmd("/resource spend", "Luck"), Cmd("/resource reset", "Luck"), Cmd("/downtime make-camp")),
            ]),

        [ClassType.Lantern] = new(
            "The keeper of a fickle little light, a piece of the god Sola, that becomes a weapon or a guide.",
            [
                Step("Fight with your light",
                    "Your light takes a **Light Weaponry** form; attack with `/move volley` or `/move hack-and-slash` to match. " +
                    "On a 6- it reverts and sulks for a while.",
                    Cmd("/move volley"), Cmd("/move hack-and-slash")),
            ]),

        [ClassType.Mechanic] = new(
            "An inventor in a one-of-a-kind mech suit, whose Stress boxes soak up damage.",
            [
                Step("Fight in your suit",
                    "Hack and Slash in your suit rolls +INT, which the bot doesn't do for you: put INT minus STR in `bonus` on `/move hack-and-slash`.",
                    Cmd("/move hack-and-slash")),
                Step("Hold together",
                    "When you'd take damage in the suit, mark Stress instead. Unmark one when you make camp.",
                    Cmd("/resource gain", "Suit Stress"), Cmd("/resource spend", "Suit Stress"), Cmd("/downtime make-camp")),
                Step("Push the button",
                    "The **Big Red Button** is a one-use ability; repair it at camp.",
                    Cmd("/ability use"), Cmd("/ability restore")),
            ]),

        [ClassType.SkyDancer] = new(
            "A flier who dances through the sky and away from every blow.",
            [
                Step("Scout from above",
                    "**Eye in the Sky** gives +1 to Discern Realities from up high: add it as `bonus`. " +
                    "**I've Got A Bad Feeling About This** asks the GM about an area, no roll needed.",
                    Cmd("/move discern-realities")),
            ]),

        [ClassType.Survivor] = new(
            "Someone who lived through a cataclysm and refuses to fall.",
            [
                Step("Take it your way",
                    "After **Survive**, ask the GM to halve the damage you pick, or take a debility instead.",
                    Cmd("/character damage"), Cmd("/debility add")),
                Step("Later: Fate and Pain",
                    "**You're Already Dead** banks Fate instead of rolling damage; **Dead Man Walking** puts off damage as Pain until your next rest.",
                    Cmd("/resource gain", "Fate"), Cmd("/resource gain", "Pain"), Cmd("/resource reset")),
            ]),

        [ClassType.Walker] = new(
            "A wall-walker who climbs any surface and strikes from above.",
            [
                Step("Stay hidden",
                    "While you cling to a ceiling without moving, **No One Looks Up**. Ask the GM if you're above their sight line; no roll needed.",
                    Cmd("/move defy-danger")),
            ]),

        [ClassType.Golem] = new(
            "A constructed warrior with a Force stat set by what they're made of.",
            [
                Step("Follow orders",
                    "**Prime Directive** gives +1 forward to orders from someone you have a bond with: add it as `bonus`.",
                    Cmd("/character bond")),
                Step("Get repaired",
                    "Potions and bandages don't work on you, but a use of a repair kit heals 4.",
                    Cmd("/character heal")),
            ]),
    };

    /// <returns>The guide for a class, or null if it hasn't been written yet.</returns>
    public static ClassGuide? For(ClassSheet sheet)
    {
        if (!Written.TryGetValue(sheet.Type, out var written))
            return null;

        return new ClassGuide(
            sheet.Type,
            written.Pitch,
            [GettingStarted(sheet), Basics, ClassMoves(sheet), .. written.Steps, Downtime, Subclasses(sheet), LevellingUp(sheet)]);
    }

    /// <summary>
    /// Every rolled move a new character could have that's made with /move class. Shapeshifter has its own command.
    /// </summary>
    public static IEnumerable<ClassMove> RolledMoves(ClassSheet sheet) =>
        sheet.GrantedMoves
            .Concat(sheet.Features.SelectMany(f => f.Options))
            .Where(m => m.Roll is not null && m.Name != ShapeshiftRules.Shapeshifter);

    /// <summary>
    /// What a move adds to its roll, e.g. "+WIS" or "+Control".
    /// </summary>
    public static string RollsWith(ClassMove move) =>
        "+" + (move.RollsWith ?? move.Roll?.Stat?.ToString().ToUpper() ?? "nothing");

    private static GuideStep GettingStarted(ClassSheet sheet)
    {
        var choices = sheet.Features.Select(f => f.Name.ToLowerInvariant()).ToList();
        var text = "Run `/character create`. It walks you through stats, race and alignment" +
                   (choices.Count > 0 ? $", then your {Join(choices)}." : ".") +
                   " Already have a character? `/class setup` adds any starting moves they're missing.";

        return Step("Make your character", text, Cmd("/character create"), Cmd("/class setup"), Cmd("/class sheet"));
    }

    internal static readonly GuideStep Basics = Step("Basic moves",
        "Everyone has these. Pick your target on Hack and Slash and Volley so a hit takes your damage off it. " +
        "Defy Danger asks which stat fits how you do it, and Aid rolls with your bonds. Last Breath shows up at 0 HP.",
        Cmd("/move hack-and-slash", note: "+STR"),
        Cmd("/move volley", note: "+DEX"),
        Cmd("/move defy-danger"),
        Cmd("/move defend", note: "+CON"),
        Cmd("/move spout-lore", note: "+INT"),
        Cmd("/move discern-realities", note: "+WIS"),
        Cmd("/move parley", note: "+CHA"),
        Cmd("/move aid", note: "+bonds"),
        Cmd("/move last-breath"));

    private static GuideStep ClassMoves(ClassSheet sheet)
    {
        var rolled = RolledMoves(sheet).ToList();
        if (rolled.Count == 0)
        {
            return Step("Class moves",
                "None of your starting moves are rolled: they change what you can do or what your other moves give you. " +
                "Read them any time with `/class info`.",
                Cmd("/class info"), Cmd("/class sheet"));
        }

        var text = "Roll these with `/move class` and pick the move; the bot adds the right stat and shows what each result means. " +
                   "The rest of your moves aren't rolled, so read them with `/class info`.";
        if (sheet.Features.Any(f => f.Options.Any(o => o.Roll is not null)))
            text += " You only have the ones you chose.";

        return new GuideStep("Class moves", text, rolled.Select(m => Cmd("/move class", m.Name, RollsWith(m))).ToList());
    }

    internal static readonly GuideStep Downtime = Step("Downtime",
        "Between fights, `/downtime make-camp` eats a ration and heals half your max HP if you sleep, and `/downtime recover` " +
        "heals you fully and clears debilities over days of rest. On the road, whoever's on watch or doing a job on the journey rolls it. " +
        "In town, `/downtime bolster` banks Preparation: spend it for +1 by setting `prepared` on any roll.",
        Cmd("/downtime make-camp"),
        Cmd("/downtime take-watch", note: "+WIS"),
        Cmd("/downtime perilous-journey", note: "+WIS"),
        Cmd("/downtime recover"),
        Cmd("/downtime bolster"),
        Cmd("/downtime supply", note: "+CHA"),
        Cmd("/downtime carouse"),
        Cmd("/downtime outstanding-warrants", note: "+CHA"));

    private static GuideStep Subclasses(ClassSheet sheet)
    {
        var text = "Take a subclass with `/subclass join`; its moves unlock as you level. `/subclass list` shows the ones you can take, " +
                   "and `/subclass info` reads one.";

        // The ones open to every class are in /subclass list; only call out the ones made for this class
        foreach (var subclass in SubclassLibrary.All.Where(s => s.Classes.Contains(sheet.Type)))
            text += $" **{subclass.Name}** is made for the {sheet.Type}." + (subclass.CommandHint is { } hint ? $" {hint}" : string.Empty);

        return Step("Subclass", text, Cmd("/subclass list"), Cmd("/subclass info"), Cmd("/subclass join"));
    }

    private static GuideStep LevellingUp(ClassSheet sheet)
    {
        var text = "When you level up, `/class moves` lists the advanced moves you can take and `/class learn` takes one. " +
                   "Spend the stat points it gives you with `/character level allocate`.";
        if (sheet.Features.Any(f => f.LaterPicksAreAdvances))
            text += " Choices you passed on at the start can be learned later as advances too.";

        return Step("Level up", text, Cmd("/class moves"), Cmd("/class learn"), Cmd("/character level allocate"));
    }

    private static string Join(IReadOnlyList<string> items) => items.Count switch
    {
        1 => items[0],
        _ => string.Join(", ", items.Take(items.Count - 1)) + " and " + items[^1],
    };

    /// <summary>
    /// The part of a guide written for the class: what's special about playing it.
    /// </summary>
    private record WrittenGuide(string Pitch, GuideStep[] Steps);

    private static GuideStep Step(string title, string text, params GuideCommand[] commands) => new(title, text, commands);

    private static GuideCommand Cmd(string command, string? pick = null, string? note = null) => new(command, pick, note);
}
