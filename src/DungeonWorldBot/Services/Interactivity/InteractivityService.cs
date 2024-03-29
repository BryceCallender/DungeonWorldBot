﻿//
//  InteractivityService.cs
//
//  Author:
//       Jarl Gullberg <jarl.gullberg@gmail.com>
//
//  Copyright (c) 2017 Jarl Gullberg
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU Affero General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU Affero General Public License for more details.
//
//  You should have received a copy of the GNU Affero General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.
//

using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Rest.Core;
using Remora.Results;

namespace DungeonWorldBot.Services.Interactivity;

/// <summary>
/// Acts as a Discord plugin for interactive messages.
/// </summary>
public class InteractivityService
{
    /// <summary>
    /// Holds the Discord channel API.
    /// </summary>
    private readonly IDiscordRestChannelAPI _channelAPI;

    public InteractivityService(
        IDiscordRestChannelAPI channelAPI)
    {
        _channelAPI = channelAPI;
    }

    /// <summary>
    /// Gets the next message sent in the given channel.
    /// </summary>
    /// <param name="channelID">The channel to watch.</param>
    /// <param name="timeout">The timeout, after which the method gives up.</param>
    /// <param name="ct">The cancellation token for this operation.</param>
    /// <returns>The message, or null if no message was sent within the given timespan.</returns>
    public async Task<Result<IMessage?>> GetNextMessageAsync
    (
        Snowflake channelID,
        Snowflake lastMessageID,
        TimeSpan timeout,
        CancellationToken ct = default
    )
    {
        var now = DateTimeOffset.UtcNow;
        var timeoutTime = now + timeout;

        while (now <= timeoutTime)
        {
            now = DateTimeOffset.UtcNow;
            var getMessage = await _channelAPI.GetChannelMessagesAsync
            (
                channelID,
                after: lastMessageID,
                limit: 1,
                ct: ct
            );

            if (!getMessage.IsSuccess)
            {
                return Result<IMessage?>.FromError(getMessage);
            }

            var message = getMessage.Entity.FirstOrDefault();
            if (message is not null)
            {
                return Result<IMessage?>.FromSuccess(message);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
        }

        return Result<IMessage?>.FromSuccess(null);
    }
}