using Remora.Commands.Conditions;
using Remora.Results;

namespace DungeonWorldBot.Features.GameMaster;

/// <summary>
/// Only someone with the DM role can run the commands of a group (or a single command) marked with this. Anyone else
/// is told so by <see cref="Infrastructure.CommandErrorReplies"/>.
/// </summary>
/// <param name="hint">Said after the refusal, like what players can do instead.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireGameMasterAttribute(string? hint = null) : ConditionAttribute
{
    public string? Hint { get; } = hint;
}

public class RequireGameMasterCondition(IGameMasterService gameMaster) : ICondition<RequireGameMasterAttribute>
{
    public async ValueTask<Result> CheckAsync(RequireGameMasterAttribute attribute, CancellationToken ct = default)
    {
        if (await gameMaster.IsGameMasterAsync(ct))
            return Result.Success;

        var message = $"Only someone with the {gameMaster.RoleName} role can do that.";
        return new NotGameMasterError(attribute.Hint is null ? message : $"{message} {attribute.Hint}");
    }
}

/// <summary>
/// Someone without the DM role ran a DM command. The message is meant for them.
/// </summary>
public record NotGameMasterError(string Message) : ResultError(Message);
