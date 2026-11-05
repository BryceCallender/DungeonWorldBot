using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using DungeonWorldBot.Infrastructure;
using Remora.Commands.Attributes;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DungeonWorldBot.Features.GameMaster;

public partial class DmCommand
{
    [Group("diag")]
    [RequireGameMaster]
    public partial class DiagnosticCommands(ICommandContext context, FeedbackService feedbackService)
        : DungeonWorldCommandBase(context, feedbackService)
    {
        // The Raspberry Pi's own tool, which prints e.g. "temp=48.3'C"
        private const string VcGenCmd = "/usr/bin/vcgencmd";

        [GeneratedRegex(@"temp=(\d+(?:\.\d+)?)")]
        private static partial Regex Temperature();

        [Command("temp")]
        [Description("Show the Raspberry Pi's temperature")]
        public async Task<IResult> GetPiTemperature()
        {
            if (!File.Exists(VcGenCmd))
                return await ReplyWithErrorAsync("The bot isn't running on a Raspberry Pi, so there's no temperature to read.");

            using var process = Process.Start(new ProcessStartInfo(VcGenCmd, "measure_temp")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (process is null)
                return await ReplyWithErrorAsync("Couldn't run vcgencmd.");

            // Read before waiting, or a full output buffer would leave both sides waiting on each other
            var output = await process.StandardOutput.ReadToEndAsync(CancellationToken);
            await process.WaitForExitAsync(CancellationToken);

            var match = Temperature().Match(output);
            if (!match.Success)
                return await ReplyWithErrorAsync($"Couldn't read the temperature from \"{output.Trim()}\".");

            var celsius = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var fahrenheit = celsius * 9 / 5 + 32;

            return await ReplyWithEmbedAsync(new Embed(
                Title: "Temperature",
                Description: "Current temp of raspberry pi",
                Fields: new List<IEmbedField>
                {
                    new EmbedField("°C", Math.Truncate(celsius).ToString(CultureInfo.InvariantCulture), true),
                    new EmbedField("°F", Math.Truncate(fahrenheit).ToString(CultureInfo.InvariantCulture), true),
                },
                Colour: _feedbackService.Theme.Secondary));
        }
    }
}
