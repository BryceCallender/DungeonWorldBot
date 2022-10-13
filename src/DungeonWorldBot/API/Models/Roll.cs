using System.Text;

namespace DungeonWorldBot.API.Models;

public class Roll
{
    public string Representation { get; set; } = null!;
    
    public int Total { get; set; }

    public List<SubRoll> Rolls { get; set; } = new();

    public override string ToString()
    {
        var result = new StringBuilder();

        for(var i = 0; i < Rolls.Count; i++)
        {
            var subRoll = Rolls[i];
            result.Append($"({string.Join('+', subRoll.Values)})");
            
            if (i < Rolls.Count - 1)
                result.Append('+');
        }
        
        return result.ToString();
    }
}

public record SubRoll(string Representation, int Total, List<int> Values);