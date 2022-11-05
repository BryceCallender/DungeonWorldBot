using System.Diagnostics;
using System.Globalization;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DungeonWorldBot.Commands;

[Group("diag")]
public class DiagnosticCommands : CommandGroup
{
    private FeedbackService _feedbackService;

    public DiagnosticCommands(
        FeedbackService feedbackService)
    {
        _feedbackService = feedbackService;
    }
    
    [Command("temp")]
    public async Task<IResult> GetPiTemperature()
    {
        var tempProcess = new ProcessStartInfo
        {
            FileName = "/usr/bin/vcgencmd",
            Arguments = "measure_temp",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(tempProcess);

        if (process is null) 
            return Result.FromError<string>("Failed to create process");
        
        await process.WaitForExitAsync();

        var output = await process.StandardOutput.ReadToEndAsync();
        Console.WriteLine(output);

        var result = double.TryParse(output[5..9], out var tempC);

        if (!result)
            return Result.FromError<string>("Failed to parse temp");

        var tempF = (tempC * (9 / 5.0)) + 32;
        
        return await _feedbackService.SendContextualEmbedAsync(
            new Embed(
                Title: $"Temperature",
                Description: "Current temp of raspberry pi",
                Fields: new List<IEmbedField>
                {
                    new EmbedField("°C", Math.Truncate(tempC).ToString(CultureInfo.InvariantCulture), true),
                    new EmbedField("°F", Math.Truncate(tempF).ToString(CultureInfo.InvariantCulture), true)
                },
                Colour: _feedbackService.Theme.Secondary
            ),
            ct: CancellationToken
        );
    }
}