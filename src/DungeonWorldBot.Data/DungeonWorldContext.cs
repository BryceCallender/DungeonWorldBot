using DungeonWorldBot.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Remora.Rest.Core;

namespace DungeonWorldBot.Data;

public class DungeonWorldContext : DbContext
{
    public DungeonWorldContext(DbContextOptions<DungeonWorldContext> options) : base(options)
    {
        Database.EnsureCreated();
    }

    public DbSet<Character> Characters => Set<Character>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        base.ConfigureConventions(builder);

        builder.Properties<Snowflake>().HaveConversion(typeof(SnowflakeConverter));
        builder.Properties<Snowflake?>().HaveConversion(typeof(NullableSnowflakeConverter));
    }

    private void SeedDatabase()
    {
        Console.WriteLine("Seeding...");
        
        var zephyr = new Snowflake(888601433103011930);
        var ren = new Snowflake(203845967810658304);
        var ziedrich = new Snowflake(479322864907190277);
        var arthur = new Snowflake(226053129337044992);
        var steve = new Snowflake(289281614767194113);
        
        var characters = new List<Character>
        {
            new()
            { 
                ID = zephyr, 
                Name = "Zephyr",
                Class = new Class
                {
                    CharacterID = zephyr,
                    Type = ClassType.Ranger
                },
                Health = new Health
                {
                    CharacterID = zephyr,
                    CurrentHP = 21,
                    MaxHP = 21
                },
                Inventory = new Inventory
                {
                    CharacterID = zephyr,
                    Items = new List<Item>
                    {
                        new()
                        {
                            Name = "Dungeon Rations",
                            Amount = 5
                        },
                        new Armor
                        {
                            Name = "Leather Armor",
                            ArmorRating = 1,
                            Type = ArmorType.Regular
                        },
                        new()
                        {
                            Name = "Bundle of Arrows",
                            Amount = 5
                        },
                        new()
                        {
                            Name = "Adventuring Gear",
                            Amount = 5
                        },
                        new()
                        {
                            Name = "Coin",
                            Amount = 9
                        },
                        new()
                        {
                            Name = "Ico",
                            Amount = 1
                        }
                    }
                },
                ArmorRating = 1,
                Level = 2,
                Stats = new List<Stat>
                {
                    new()
                    {
                        CharacterID = zephyr,
                        StatType = StatType.Str,
                        Value = 12
                    },
                    new()
                    {
                        CharacterID = zephyr,
                        StatType = StatType.Dex,
                        Value = 16
                    },
                    new()
                    {
                        CharacterID = zephyr,
                        StatType = StatType.Con,
                        Value = 13
                    },
                    new()
                    {
                        CharacterID = zephyr,
                        StatType = StatType.Int,
                        Value = 8
                    },
                    new()
                    {
                        CharacterID = zephyr,
                        StatType = StatType.Wis,
                        Value = 15
                    },
                    new()
                    {
                        CharacterID = zephyr,
                        StatType = StatType.Cha,
                        Value = 9
                    }
                },
                Status = Status.Alive | Status.Resting,
                Alignment = Alignment.LawfulGood,
                Race = Race.Elves
            },
            new()
            {
                ID = ren,
                Name = "Ren Sow",
                Class = new Class
                {
                    CharacterID = ren,
                    Type = ClassType.Slayer
                },
                Health = new Health
                {
                    CharacterID = ren,
                    CurrentHP = 23,
                    MaxHP = 23
                },
                Inventory = new Inventory
                {
                    CharacterID = ren,
                    Items = new List<Item>
                    {
                        new()
                        {
                            Name = "Dungeon Ration",
                            Amount = 5
                        },
                        new Armor
                        {
                            Name = "Leather Armor",
                            Type = ArmorType.Regular,
                            Amount = 1,
                            ArmorRating = 1
                        },
                        new()
                        {
                            Name = "Slayer's Arsenal",
                            Amount = 1
                        },
                        new Weapon
                        {
                            Name = "Dual Battle Axes",
                            Damage = 1,
                            Amount = 1
                        },
                        new Weapon
                        {
                            Name = "Crossbow",
                            Damage = 1,
                            Amount = 1
                        },
                        new()
                        {
                            Name = "Coin",
                            Amount = 6
                        }
                    }
                },
                ArmorRating = 1,
                Level = 2,
                Stats = new List<Stat>
                {
                    new()
                    {
                        CharacterID = ren,
                        StatType = StatType.Str,
                        Value = 16
                    },
                    new()
                    {
                        CharacterID = ren,
                        StatType = StatType.Dex,
                        Value = 13
                    },
                    new()
                    {
                        CharacterID = ren,
                        StatType = StatType.Con,
                        Value = 15
                    },
                    new()
                    {
                        CharacterID = ren,
                        StatType = StatType.Int,
                        Value = 9
                    },
                    new()
                    {
                        CharacterID = ren,
                        StatType = StatType.Wis,
                        Value = 8
                    },
                    new()
                    {
                        CharacterID = ren,
                        StatType = StatType.Cha,
                        Value = 12
                    }
                },
                Status = Status.Alive | Status.Resting,
                Alignment = Alignment.NeutralGood,
                Race = Race.Human
            },
            new()
            {
                ID = ziedrich,
                Name = "Ziedrich Floran",
                Class = new Class
                {
                    CharacterID = ziedrich,
                    Type = ClassType.Channeler
                },
                Health = new Health
                {
                    CharacterID = ziedrich,
                    CurrentHP = 17,
                    MaxHP = 26
                },
                Inventory = new Inventory
                {
                    CharacterID = ziedrich,
                    Items = new List<Item>
                    {
                        new()
                        {
                            Name = "Dungeon Rations",
                            Amount = 5
                        },
                        new Armor
                        {
                            Name = "Leather Armor",
                            Type = ArmorType.Regular,
                            ArmorRating = 1
                        },
                        new()
                        {
                            Name = "Southing Balm",
                            Amount = 1
                        },
                        new Weapon
                        {
                            Name = "Bowstaff",
                            Damage = 0,
                            Amount = 1
                        },
                        new()
                        {
                            Name = "Coin",
                            Amount = 11
                        }
                    }
                },
                ArmorRating = 1,
                Level = 2,
                Stats = new List<Stat>
                {
                    new()
                    {
                        CharacterID = ziedrich,
                        StatType = StatType.Str,
                        Value = 15
                    },
                    new()
                    {
                        CharacterID = ziedrich,
                        StatType = StatType.Dex,
                        Value = 12
                    },
                    new()
                    {
                        CharacterID = ziedrich,
                        StatType = StatType.Con,
                        Value = 16
                    },
                    new()
                    {
                        CharacterID = ziedrich,
                        StatType = StatType.Int,
                        Value = 9
                    },
                    new()
                    {
                        CharacterID = ziedrich,
                        StatType = StatType.Wis,
                        Value = 8
                    },
                    new()
                    {
                        CharacterID = ziedrich,
                        StatType = StatType.Cha,
                        Value = 13
                    }
                },
                Status = Status.Alive | Status.Resting,
                Alignment = Alignment.NeutralGood,
                Race = Race.Elves
            },
            new()
            {
                ID = arthur,
                Name = "Arthur",
                Class = new Class
                {
                    CharacterID = arthur,
                    Type = ClassType.Paladin,
                },
                Health = new Health
                {
                    CharacterID = arthur,
                    CurrentHP = 21,
                    MaxHP = 23
                },
                Inventory = new Inventory
                {
                    CharacterID = arthur,
                    Items = new List<Item>
                    {
                        new()
                        {
                            Name = "Dungeon Rations",
                            Amount = 10
                        },
                        new Armor
                        {
                            Name = "Scale Armor",
                            Amount = 1,
                            ArmorRating = 2,
                            Type = ArmorType.Regular
                        },
                        new()
                        {
                            Name = "Necklace",
                            Amount = 1
                        },
                        new Weapon
                        {
                            Name = "Long Sword",
                            Amount = 1,
                            Damage = 1
                        },
                        new Armor
                        {
                            Name = "Shield",
                            Type = ArmorType.Regular,
                            ArmorRating = 1
                        },
                        new()
                        {
                            Name = "Coin",
                            Amount = 13
                        }
                    }
                },
                ArmorRating = 3,
                Level = 2,
                Stats = new List<Stat>
                {
                    new()
                    {
                        CharacterID = arthur,
                        StatType = StatType.Str,
                        Value = 16
                    },
                    new()
                    {
                        CharacterID = arthur,
                        StatType = StatType.Dex,
                        Value = 8
                    },
                    new()
                    {
                        CharacterID = arthur,
                        StatType = StatType.Con,
                        Value = 13
                    },
                    new()
                    {
                        CharacterID = arthur,
                        StatType = StatType.Int,
                        Value = 9
                    },
                    new()
                    {
                        CharacterID = arthur,
                        StatType = StatType.Wis,
                        Value = 15
                    },
                    new()
                    {
                        CharacterID = arthur,
                        StatType = StatType.Cha,
                        Value = 12
                    }
                },
                Status = Status.Alive | Status.Resting,
                Alignment = Alignment.NeutralGood,
                Race = Race.Human
            },
            new()
            {
                ID = steve,
                Name = "Steve",
                Class = new Class
                {
                    CharacterID = steve,
                    Type = ClassType.Necromancer
                },
                Health = new Health
                {
                    CharacterID = steve,
                    CurrentHP = 19,
                    MaxHP = 19
                },
                Inventory = new Inventory
                {
                    CharacterID = steve,
                    Items = new List<Item>
                    {
                        new()
                        {
                            Name = "Healing Potion",
                            Amount = 2
                        },
                        new Weapon
                        {
                            Name = "Bone Dagger",
                            Damage = 1,
                            Amount = 1
                        },
                        new()
                        {
                            Name = "Someone you used to know",
                            Amount = 1
                        },
                        new()
                        {
                            Name = "Adventuring Gear",
                            Amount = 5
                        },
                        new()
                        {
                            Name = "Coin",
                            Amount = 6
                        },
                        new()
                        {
                            Name = "Tree Bark",
                            Amount = 1
                        },
                        new()
                        {
                            Name = "Necropic Jar w/ Master",
                            Amount = 1
                        },
                        new()
                        {
                            Name = "Necropic Jar w/ white & brown snakes",
                            Amount = 1
                        }
                    }
                },
                ArmorRating = 0,
                Level = 2,
                Stats = new List<Stat>
                {
                    new()
                    {
                        CharacterID = steve,
                        StatType = StatType.Str,
                        Value = 9
                    },
                    new()
                    {
                        CharacterID = steve,
                        StatType = StatType.Dex,
                        Value = 13
                    },
                    new()
                    {
                        CharacterID = steve,
                        StatType = StatType.Con,
                        Value = 15
                    },
                    new()
                    {
                        CharacterID = steve,
                        StatType = StatType.Int,
                        Value = 16
                    },
                    new()
                    {
                        CharacterID = steve,
                        StatType = StatType.Wis,
                        Value = 12
                    },
                    new()
                    {
                        CharacterID = steve,
                        StatType = StatType.Cha,
                        Value = 8
                    }
                },
                Status = Status.Alive | Status.Resting,
                Alignment = Alignment.NeutralGood,
                Race = Race.Human
            }
        };
        
        Characters.AddRange(characters);
        
        SaveChanges();
    }
}