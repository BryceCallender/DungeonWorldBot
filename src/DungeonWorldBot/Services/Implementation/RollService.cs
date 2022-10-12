using DungeonWorldBot.API.Models;
using DungeonWorldBot.Data.Entities;

namespace DungeonWorldBot.Services.Implementation;

public class RollService : IRollService
{
    private readonly Random _random;
    
    public RollService(Random random)
    {
        _random = random;
    }

    public Roll Roll(string roll)
    {
        return ParseRoll(roll);
    }

    public Roll RollWithStat(string roll, Stat stat)
    {
        var rollResult = ParseRoll($"{roll}+{stat.Modifier}");
        return rollResult;
    }
    
    private Roll ParseRoll(string roll)
    {
        var die = roll.ToLower().Split("+", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var rollResult = new Roll { Representation = roll };

        foreach (var dice in die)
        {
            if (dice.Contains('d'))
            {
                var parsedDie = ParseDie(dice);
                var results = Roll(parsedDie.count, parsedDie.faces);
                var sum = results.Sum();
                rollResult.Total += sum;
                rollResult.Rolls.Add(new SubRoll(dice, sum, results));
            }
            else
            {
                var result = int.TryParse(dice, out var modifier);
                
                if (!result) 
                    continue;
                
                rollResult.Total += modifier;
                rollResult.Rolls.Add(new SubRoll(modifier.ToString(), modifier, new List<int> { modifier }));
            }
        }

        return rollResult;
    }

    private static (int count, int faces) ParseDie(string die)
    {
        var values = die.ToLower().Split('d', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        
        var result = int.TryParse(values[^1], out var faces);
        if (!result)
        {
            return (0, 0);
        }

        if (values.Length == 1)
        {
            return (1, faces);
        }
        
        result = int.TryParse(values[0], out var rollCount);
        
        return result ? (rollCount, faces) : (0, 0);
    }
    
    private List<int> Roll(int rollCount, int faces)
    {
        var results = new List<int>();

        for (var i = 0; i < rollCount; i++)
        {
            results.Add(_random.Next(1, faces));
        }

        return results;
    }
}