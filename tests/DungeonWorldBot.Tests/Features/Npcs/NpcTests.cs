using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Npcs;
using Microsoft.EntityFrameworkCore;

namespace DungeonWorldBot.Tests.Features.Npcs;

public sealed class NpcTests : IDisposable
{
    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private DungeonWorldContext CreateContext() => _db.CreateContext();

    private static Npc NewNpc(string name = "Bram", int armor = 1) => new()
    {
        Name = name,
        Role = "Hireling",
        CurrentHP = 8,
        MaxHP = 8,
        Armor = armor,
        Damage = "d6",
        Loyalty = 1,
        Skills = "Protector +2",
    };

    [Fact]
    public async Task Npcs_SaveAndAreFoundWhateverTheCase()
    {
        await using (var context = CreateContext())
            await new NpcService(context).AddAsync(NewNpc());

        await using (var context = CreateContext())
        {
            var npc = await new NpcService(context).FindAsync("  bRAM ");
            Assert.NotNull(npc);
            Assert.Equal("Bram", npc.Name);
            Assert.Equal("Protector +2", npc.Skills);
        }
    }

    [Fact]
    public async Task Names_AreUniqueWhateverTheCase()
    {
        await using var context = CreateContext();
        var service = new NpcService(context);
        await service.AddAsync(NewNpc("Bram"));

        await Assert.ThrowsAsync<DbUpdateException>(() => service.AddAsync(NewNpc("bram")));
    }

    [Theory]
    [InlineData(DamageType.Physical, 0, 4, 1)]
    [InlineData(DamageType.Physical, 1, 4, 0)]
    [InlineData(DamageType.Magical, 0, 4, 0)]
    [InlineData(DamageType.True, 0, 4, 0)]
    public async Task Damage_IsReducedByArmorOnlyWhenPhysical(DamageType type, int piercing, int rolled, int blocked)
    {
        await using var context = CreateContext();
        var service = new NpcService(context);
        var npc = NewNpc(armor: 1);
        await service.AddAsync(npc);

        var hit = await service.DamageAsync(npc, rolled, type, piercing);

        Assert.Equal(blocked, hit.Blocked);
        Assert.Equal(rolled - blocked, hit.Taken);
        Assert.Equal(8 - hit.Taken, npc.CurrentHP);
    }

    [Fact]
    public async Task Damage_StopsAtZeroAndNegativeRollsDontHeal()
    {
        await using var context = CreateContext();
        var service = new NpcService(context);
        var npc = NewNpc(armor: 0);
        await service.AddAsync(npc);

        await service.DamageAsync(npc, -3);
        Assert.Equal(8, npc.CurrentHP);

        await service.DamageAsync(npc, 50);
        Assert.Equal(0, npc.CurrentHP);
    }

    [Fact]
    public async Task Healing_StopsAtMaxAndBlankHealsFully()
    {
        await using var context = CreateContext();
        var service = new NpcService(context);
        var npc = NewNpc(armor: 0);
        await service.AddAsync(npc);
        await service.DamageAsync(npc, 6);

        await service.HealAsync(npc, 3);
        Assert.Equal(5, npc.CurrentHP);

        await service.HealAsync(npc, 100);
        Assert.Equal(8, npc.CurrentHP);

        await service.DamageAsync(npc, 6);
        await service.HealAsync(npc);
        Assert.Equal(8, npc.CurrentHP);
    }

    [Fact]
    public async Task Saving_KeepsCurrentHpWithinMax()
    {
        await using var context = CreateContext();
        var service = new NpcService(context);
        var npc = NewNpc();
        await service.AddAsync(npc);

        npc.MaxHP = 5;
        await service.SaveAsync(npc);

        Assert.Equal(5, npc.CurrentHP);
    }

    [Fact]
    public async Task Stats_SaveAsModifiers()
    {
        await using (var context = CreateContext())
        {
            var npc = NewNpc();
            npc.SetModifier(StatType.Str, 2);
            npc.SetModifier(StatType.Int, -1);
            await new NpcService(context).AddAsync(npc);
        }

        await using (var context = CreateContext())
        {
            var npc = (await new NpcService(context).FindAsync("Bram"))!;
            Assert.Equal(2, npc.Modifier(StatType.Str));
            Assert.Equal(-1, npc.Modifier(StatType.Int));
            Assert.Equal(0, npc.Modifier(StatType.Cha));
            Assert.Equal("STR +2 · DEX +0 · CON +0 · INT -1 · WIS +0 · CHA +0", NpcSheet.Stats(npc));
        }
    }

    [Fact]
    public void Skills_AreReadFromTheirText()
    {
        var npc = NewNpc();
        npc.Skills = "Protector +2, Tracker+1; Burglar -1, Cook";

        Assert.Equal([("Protector", 2), ("Tracker", 1), ("Burglar", -1)], NpcRolls.Skills(npc));
    }

    [Fact]
    public void RollOptions_CoverStatsLoyaltySkillsAndDamage()
    {
        var npc = NewNpc();
        npc.Dex = 1;

        var options = NpcRolls.Options(npc);

        Assert.Equal(["STR", "DEX", "CON", "INT", "WIS", "CHA", "Loyalty", "Protector", "Damage"], options.Select(o => o.Name));
        Assert.Equal("2d6+1", NpcRolls.Find(npc, "dex")!.Dice);
        Assert.Equal("2d6+1", NpcRolls.Find(npc, "loyalty")!.Dice);
        Assert.Equal("2d6+2", NpcRolls.Find(npc, "PROTECTOR")!.Dice);
        Assert.Equal(new NpcRollOption("Damage", "d6", null), NpcRolls.Find(npc, "Damage"));
        Assert.Null(NpcRolls.Find(npc, "Arcana"));
    }

    [Fact]
    public void RollOptions_LeaveOutDamageWithoutDice()
    {
        var npc = NewNpc();
        npc.Damage = null;

        Assert.DoesNotContain(NpcRolls.Options(npc), o => o.Name == NpcRolls.Damage);
    }

    [Fact]
    public void Summary_ShowsWhatMatters()
    {
        var npc = NewNpc();
        npc.CurrentHP = 0;

        var summary = NpcSheet.Summary(npc);

        Assert.Contains("0/8", summary);
        Assert.Contains("Loyalty +1", summary);
        Assert.Contains("down", summary);
    }
}
