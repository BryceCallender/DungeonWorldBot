namespace DungeonWorldBot.Gear.Homebrew;

/// <summary>
/// A homebrew masterwork bow with a single gem socket. The socketed gem awakens an Aspect.
/// </summary>
public static class GemAspectedBow
{
    public const string Name = "Gem-Aspected Bow";
    public const string OpportunistsVolley = "Opportunist's Volley";

    public static readonly CustomGear Gear = new(
        Name,
        "A masterwork bow with a single gemstone socket. A gem placed in the socket awakens an Aspect tied to its nature. Only one Aspect may be active at a time.",
        "Gem",
        [
            new("Sapphire", "Aspect of Accuracy",
                "Once per attack, reroll a single damage die and take the higher result.",
                "Thin blue lines briefly trace possible arrow paths before converging into a single trajectory.")
            {
                RerollLowestDamageDie = true,
            },
            new("Emerald", "Aspect of Connection",
                "Whenever you damage a creature, you Mark it. When a Marked creature takes damage, all other Marked creatures suffer 1 damage per " +
                "Marked creature. Marks remain until the end of combat.",
                "Emerald threads briefly connect Marked creatures.")
            {
                DamageNote = "Mark the target. When a Marked creature takes damage, every other Marked creature suffers 1 damage per Marked creature.",
                Counter = new GearCounter("Marks", "Creatures Marked by the Emerald. Cleared at the end of combat.") { ClearsAfterCombat = true },
            },
            new("Ruby", "Aspect of Evasion",
                "Whenever you successfully deal damage, gain 1 Dodge. Spend a Dodge to immediately avoid danger or damage. You can hold up to 3 Dodges.",
                "A red afterimage lingers where you stood moments before.")
            {
                Counter = new GearCounter("Dodge", "Spend one to immediately avoid danger or damage.", Max: 3, GainOnDamage: true),
            },
            new("Diamond", "Aspect of Penetration",
                "Your attacks ignore magical and physical armor and damage reduction. Magical barriers give no protection from your arrows unless " +
                "specifically designed to stop physical objects.",
                "Arrows leave behind a trail of white crystalline light.")
            {
                DamageNote = "Ignores magical and physical armor, damage reduction, and magical barriers.",
            },
            new("Amethyst", "Aspect of Revelation",
                "The first time you damage a creature, choose one: learn its highest stat, its lowest stat, one resistance, one special ability, or " +
                "its current HP range. Each further successful hit reveals another truth.",
                "Purple runes briefly appear across the target's body.")
            {
                DamageNote = "Learn one truth: its highest stat, lowest stat, one resistance, one special ability, or current HP range.",
            },
            new("Topaz", "Aspect of Opportunity",
                "Once per round, when an ally deals damage to a creature, you may immediately make an Opportunist's Volley against it: roll your " +
                "damage normally, and the target suffers half (rounded up).",
                "A golden shimmer highlights an opening in the target's defenses before the shot is loosed.")
            {
                Action = new GearAction(OpportunistsVolley, "When an ally deals damage to a creature, once per round", HalfDamage: true),
            },
        ],
        UsedFor: ["Volley", "Blood Moon", "Lunar Barrage", OpportunistsVolley])
    {
        SwapNote = "Replacing gems is an extra action you can take on your turn.",
    };
}
