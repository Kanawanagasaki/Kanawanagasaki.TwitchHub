namespace Kanawanagasaki.TwitchHub.Services;

using System.Text.Json;
using System.Threading;
using Kanawanagasaki.TwitchHub.Models;
using Microsoft.EntityFrameworkCore;

public class TwitchRewardsService : BackgroundService
{
    private readonly ILogger<TwitchRewardsService> _logger;
    private readonly TwitchEventSubService _twitchEventSub;
    private readonly TwitchAuthService _twitchAuth;
    private readonly TwitchApiService _twitchApi;
    private readonly TwitchChatMessagesService _twitchChatMessages;
    private readonly SevenTvApiService _sevenTv;
    private readonly EmotesService _emotes;
    private readonly IServiceScope _scope;
    private readonly SQLiteContext _db;

    private readonly HashSet<Guid> _connectedAuthUuids = [];
    private DateTimeOffset _lastSync = DateTimeOffset.MinValue;

    public TwitchRewardsService(ILogger<TwitchRewardsService> logger,
        TwitchEventSubService twitchEventSub,
        TwitchAuthService twitchAuth,
        TwitchApiService twitchApi,
        TwitchChatMessagesService twitchChatMessages,
        SevenTvApiService sevenTv,
        EmotesService emotes,
        IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _twitchEventSub = twitchEventSub;
        _twitchAuth = twitchAuth;
        _twitchApi = twitchApi;
        _twitchChatMessages = twitchChatMessages;
        _sevenTv = sevenTv;
        _emotes = emotes;
        _scope = serviceScopeFactory.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<SQLiteContext>();
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _twitchEventSub.OnWsNotification += (Guid authUuid, JsonElement json) =>
        {
            if (!json.TryGetProperty("payload", out var payloadProp))
                return;
            if (!payloadProp.TryGetProperty("subscription", out var subscriptionProp))
                return;
            if (!subscriptionProp.TryGetProperty("type", out var typeProp))
                return;
            if (typeProp.ValueKind != JsonValueKind.String)
                return;
            if (typeProp.GetString() != "channel.channel_points_custom_reward_redemption.add")
                return;

            Task.Run(async () =>
            {
                var auth = await _twitchAuth.GetRestoredByUuid(authUuid);
                if (auth is not null)
                {
                    _logger.LogInformation("[{AuthUsername}] Got WS Notification, processing {RedemptionType}", auth.Username, typeProp.GetString());
                    await Process(auth);
                }
            });
        };

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var localRewards = await _db.TwitchCustomRewards.Where(x => x.IsCreated).ToArrayAsync(ct);
                var authUuids = localRewards.Select(x => x.AuthUuid).ToArray();
                var auths = new TwitchAuthModel?[authUuids.Length];
                for (int i = 0; i < authUuids.Length; i++)
                    auths[i] = await _twitchAuth.GetRestoredByUuid(authUuids[i]);

                foreach (var authUuid in _connectedAuthUuids.ToArray())
                {
                    var auth = auths.FirstOrDefault(x => x?.Uuid == authUuid);
                    if (auth is null)
                    {
                        auth = await _twitchAuth.GetRestoredByUuid(authUuid);
                        if (auth is not null)
                            await _twitchEventSub.Unsubscribe(auth, ["channel.channel_points_custom_reward_redemption.add"], ct);
                        _connectedAuthUuids.Remove(authUuid);
                    }
                }

                if (TimeSpan.FromHours(2) < DateTimeOffset.UtcNow - _lastSync)
                {
                    foreach (var auth in auths)
                    {
                        if (auth is null)
                            continue;
                        if (!await Sync(auth))
                            _logger.LogWarning("[{AuthUsername}] Failed to sync custom rewards", auth.Username);
                    }
                    _lastSync = DateTimeOffset.UtcNow;
                }

                foreach (var auth in auths)
                {
                    if (auth is null)
                        continue;

                    if (!_connectedAuthUuids.Contains(auth.Uuid))
                    {
                        await _twitchEventSub.Subscribe(auth, [new()
                        {
                            Type = "channel.channel_points_custom_reward_redemption.add",
                            Version = "1",
                            Condition = new
                            {
                                broadcaster_user_id = auth.UserId
                            }
                        }], ct);
                        _connectedAuthUuids.Add(auth.Uuid);
                    }

                    await Process(auth);
                    await Task.Delay(TimeSpan.FromSeconds(1), ct);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                _logger.LogError(e.StackTrace);
            }
            finally
            {
                await Task.Delay(TimeSpan.FromMinutes(10), ct);
            }
        }
    }

    private async Task Process(TwitchAuthModel auth)
    {
        try
        {
            var localRewards = await _db.TwitchCustomRewards.Where(x => x.IsCreated && x.AuthUuid == auth.Uuid).ToArrayAsync();
            var redemptions = await GetRedemptions(auth);
            foreach (var redemption in redemptions)
            {
                var botAuth = await _db.TwitchAuth.FirstOrDefaultAsync(x => x.Uuid == redemption.bot_auth_uuid);
                if (botAuth is null)
                {
                    await Cancel(auth, redemption.reward_type, redemption.id);
                    _logger.LogError("[{AuthUsername}] Bot auth not found for {RewardType}", auth.Username, redemption.reward_type);
                    continue;
                }
                var localReward = localRewards.FirstOrDefault(x => x.RewardType == redemption.reward_type);
                if (localReward is null)
                {
                    await Cancel(auth, redemption.reward_type, redemption.id);
                    _logger.LogError("[{AuthUsername}] Local reward not found for {RewardType}", auth.Username, redemption.reward_type);
                    continue;
                }

                _logger.LogInformation("[{AuthUsername}] Processing reward of type {RewardType}", auth.Username, redemption.reward_type);

                switch (redemption.reward_type)
                {
                    case ERewardType.Add7TvEmote:
                        {
                            var res = await ProcessAdd7TvEmote(auth, redemption);
                            if (res.success)
                                await Fulfill(auth, redemption.reward_type, redemption.id);
                            else
                                await Cancel(auth, redemption.reward_type, redemption.id);

                            _twitchChatMessages.SendMessage(botAuth.Username, auth.Username, (res.success ? "" : $"@{redemption.user_name} ") + res.message);
                            break;
                        }
                    default:
                        _logger.LogWarning("[{AuthUsername}] Unknown reward type {RewardType}", auth.Username, redemption.reward_type);
                        break;
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError("An exception was throw while processing {AuthUsername}: {ExceptionMessage}", auth.Username, e.Message);
        }
    }

    private async Task<(bool success, string message)> ProcessAdd7TvEmote(TwitchAuthModel auth, Redemption redemption)
    {
        if (string.IsNullOrWhiteSpace(redemption.user_input))
            return (false, "You were supposed to enter valid link for a 7Tv emote");
        if (!Uri.TryCreate(redemption.user_input, UriKind.Absolute, out var uri))
            return (false, "You were supposed to enter valid link for a 7Tv emote");
        if (uri.Host != "7tv.app" || uri.Segments.Length < 3 || uri.Segments[1] != "emotes/")
            return (false, "Link format is incorrect. Enter a valid link for a 7Tv emote");

        var (res, message) = await _sevenTv.AddEmoteToDefaultSet(uri.Segments[2]);
        if (res)
            _emotes.ResetChannelCache(auth.UserId, auth.Username);

        return (res, message);
    }

    public async Task<bool> Sync(TwitchAuthModel auth)
    {
        var localRewards = await _db.TwitchCustomRewards.Where(x => x.AuthUuid == auth.Uuid).ToArrayAsync();
        var rewardTypes = new Dictionary<ERewardType, List<TwitchCustomRewardModel>>();
        foreach (var localReward in localRewards)
        {
            if (rewardTypes.ContainsKey(localReward.RewardType))
                rewardTypes[localReward.RewardType].Add(localReward);
            else
                rewardTypes[localReward.RewardType] = [localReward];
        }

        // Remove duplicate rewards in database
        foreach ((var type, var list) in rewardTypes)
        {
            if (1 < list.Count)
            {
                for (int i = 1; i < list.Count; i++)
                {
                    _db.TwitchCustomRewards.Remove(list[i]);
                    _logger.LogInformation("[{AuthUsername}] Removing duplicate reward from database of type {RewardType}", auth.Username, type);
                }
            }
        }

        // Create local rewards of missing types
        foreach (var type in Enum.GetValues<ERewardType>())
        {
            if (rewardTypes.ContainsKey(type))
                continue;

            var localReward = new TwitchCustomRewardModel
            {
                Uuid = Guid.NewGuid(),
                AuthUuid = auth.Uuid,
                BotAuthUuid = null,
                RewardType = type,
                IsCreated = false,
                TwitchId = null,
                Title = type switch
                {
                    ERewardType.Add7TvEmote => "Add 7Tv Emote",
                    _ => "Hello :)"
                },
                Cost = type switch
                {
                    ERewardType.Add7TvEmote => 1_000,
                    _ => 999_999_999
                },
                BackgroundColor = type switch
                {
                    ERewardType.Add7TvEmote => "#29d8f6",
                    _ => "#ffffff"
                },
                IsUserInputRequired = type switch
                {
                    ERewardType.Add7TvEmote => true,
                    _ => false
                },
                Prompt = "",
                Extra = ""
            };

            _db.TwitchCustomRewards.Add(localReward);

            _logger.LogInformation("[{AuthUsername}] Created local reward of type {RewardType}", auth.Username, type);
        }

        await _db.SaveChangesAsync();

        // Sync local and remote rewards
        var remoteRewards = await _twitchApi.GetCustomRewardsList(auth);
        if (remoteRewards is null || !remoteRewards.IsSuccessStatusCode)
        {
            _logger.LogWarning("[{AuthUsername}] Failed to get rewards list ({StatusCode})", auth.Username, remoteRewards?.StatusCode.ToString() ?? "NULL");
            return false;
        }

        foreach (var localReward in localRewards)
        {
            if (localReward.TwitchId is null && localReward.IsCreated)
                localReward.IsCreated = false;
            else if (localReward.IsCreated)
            {
                var remoteReward = remoteRewards.data.FirstOrDefault(x => x.id == localReward.TwitchId);
                if (remoteReward is null)
                {
                    var remoteRewardRes = await _twitchApi.CreateCustomReward(auth, new()
                    {
                        title = localReward.Title,
                        cost = localReward.Cost,
                        is_user_input_required = localReward.IsUserInputRequired,
                        prompt = localReward.Prompt,
                        background_color = localReward.BackgroundColor,
                        is_enabled = true,
                        should_redemptions_skip_request_queue = false
                    });
                    if (remoteRewardRes is null || !remoteRewardRes.IsSuccessStatusCode || remoteRewardRes.data.Length == 0)
                    {
                        _logger.LogWarning("[{AuthUsername}] Failed to create remote reward of type {RewardType}", auth.Username, localReward.RewardType);
                        continue;
                    }
                    localReward.TwitchId = remoteRewardRes.data[0].id;
                    _logger.LogInformation("[{AuthUsername}] Created remote reward of type {RewardType} with twitch ID {TwitchId}", auth.Username, localReward.RewardType, localReward.TwitchId);
                }
                else
                {
                    localReward.Title = remoteReward.title ?? string.Empty;
                    localReward.Cost = remoteReward.cost;
                    localReward.IsUserInputRequired = remoteReward.is_user_input_required;
                    localReward.Prompt = remoteReward.prompt;
                    localReward.BackgroundColor = remoteReward.background_color;
                    _logger.LogDebug("[{AuthUsername}] Local reward of type {RewardType} synchronized with remote counterpart", auth.Username, localReward.RewardType);
                }
            }
            else if (localReward.TwitchId is not null && remoteRewards.data.Any(x => x.id == localReward.TwitchId))
            {
                var statusCode = await _twitchApi.DeleteCustomReward(auth, localReward.TwitchId);
                if (statusCode % 100 == 2)
                    _logger.LogInformation("[{AuthUsername}] Remote reward of type {RewardType} has been deleted. Deletion reason: localReward.IsCreated == false", auth.Username, localReward.RewardType);
                else
                    _logger.LogInformation("[{AuthUsername}] Failed to delete remote reward of type {RewardType} ({StatusCode})", auth.Username, localReward.RewardType, statusCode);
            }
        }

        // Remove remote rewards that is not in local database
        foreach (var remoteReward in remoteRewards.data)
        {
            if (!localRewards.Any(x => x.TwitchId == remoteReward.id))
            {
                var statusCode = await _twitchApi.DeleteCustomReward(auth, remoteReward.id);
                if (statusCode % 100 == 2)
                    _logger.LogInformation("[{AuthUsername}] Remote reward has been deleted. Deletion reason: Unknown type", auth.Username);
                else
                    _logger.LogInformation("[{AuthUsername}] Failed to delete remote reward of unknown type ({StatusCode})", auth.Username, statusCode);
            }
            else
            {
                var req = remoteReward.ToReq();
                req.is_paused = false;
                req.is_enabled = true;

                await _twitchApi.UpdateCustomReward(auth, remoteReward.id, req);
            }
        }

        await _db.SaveChangesAsync();

        return true;
    }

    public async Task<TwitchCustomRewardModel?> Get(TwitchAuthModel auth, ERewardType type)
    {
        var localReward = await _db.TwitchCustomRewards.FirstOrDefaultAsync(x => x.RewardType == type && x.AuthUuid == auth.Uuid);
        if (localReward is null)
        {
            if (!await Sync(auth))
            {
                _logger.LogError("[{AuthUsername}] Failed to retrieve custom reward of type {CustomRewardType}", auth.Username, type);
                return null;
            }
            localReward = await _db.TwitchCustomRewards.FirstOrDefaultAsync(x => x.RewardType == type && x.AuthUuid == auth.Uuid);
        }
        return localReward;
    }

    public async Task<bool> Enable(TwitchAuthModel auth, TwitchAuthModel botAuth, ERewardType type)
    {
        var localReward = await _db.TwitchCustomRewards.FirstOrDefaultAsync(x => x.RewardType == type && x.AuthUuid == auth.Uuid);
        if (localReward is null)
        {
            if (!await Sync(auth))
            {
                _logger.LogError("[{AuthUsername}] Failed to enable custom reward of type {CustomRewardType}", auth.Username, type);
                return false;
            }
            localReward = await _db.TwitchCustomRewards.FirstOrDefaultAsync(x => x.RewardType == type && x.AuthUuid == auth.Uuid);
        }
        if (localReward is null)
        {
            _logger.LogError("[{AuthUsername}] Wth? Something strange happened while enabling custom twitch reward {CustomRewardType}", auth.Username, type);
            return false;
        }

        if (localReward.BotAuthUuid != botAuth.Uuid)
        {
            localReward.BotAuthUuid = botAuth.Uuid;
            await _db.SaveChangesAsync();
        }

        if (localReward.IsCreated)
        {
            _logger.LogError("[{AuthUsername}] Custom reward {CustomRewardType} already created", auth.Username, type);
            return false;
        }

        var remoteRewardRes = await _twitchApi.CreateCustomReward(auth, new()
        {
            title = localReward.Title,
            cost = localReward.Cost,
            is_user_input_required = localReward.IsUserInputRequired,
            prompt = localReward.Prompt,
            background_color = localReward.BackgroundColor,
            is_enabled = true,
            should_redemptions_skip_request_queue = false
        });
        if (remoteRewardRes is null || !remoteRewardRes.IsSuccessStatusCode || remoteRewardRes.data.Length == 0)
        {
            _logger.LogError("[{AuthUsername}] Failed to create custom twitch reward {CustomRewardType} - {StatusCode}\n{Response}", auth.Username, type, remoteRewardRes?.StatusCode, remoteRewardRes?.ResponseStr);
            return false;
        }

        localReward.IsCreated = true;
        localReward.TwitchId = remoteRewardRes.data[0].id;
        await _db.SaveChangesAsync();

        if (!_connectedAuthUuids.Contains(auth.Uuid))
        {
            await _twitchEventSub.Subscribe(auth, [new()
            {
                Type = "channel.channel_points_custom_reward_redemption.add",
                Version = "1",
                Condition = new
                {
                    broadcaster_user_id = auth.UserId
                }
            }], default);
            _connectedAuthUuids.Add(auth.Uuid);
        }

        return true;
    }

    public async Task<bool> Update(TwitchAuthModel auth, TwitchAuthModel botAuth, TwitchCustomRewardModel reward)
    {
        var localReward = await _db.TwitchCustomRewards.FirstOrDefaultAsync(x => x.RewardType == reward.RewardType && x.AuthUuid == auth.Uuid);
        if (localReward is null)
        {
            if (!await Sync(auth))
            {
                _logger.LogError("[{AuthUsername}] Failed to update custom reward of type {CustomRewardType}", auth.Username, reward.RewardType);
                return false;
            }
            localReward = await _db.TwitchCustomRewards.FirstOrDefaultAsync(x => x.RewardType == reward.RewardType && x.AuthUuid == auth.Uuid);
        }
        if (localReward is null)
        {
            _logger.LogError("[{AuthUsername}] Wth? Something strange happened while updating custom twitch reward {CustomRewardType}", auth.Username, reward.RewardType);
            return false;
        }

        localReward.BotAuthUuid = botAuth.Uuid;
        localReward.IsCreated = reward.IsCreated;
        localReward.TwitchId = reward.TwitchId;
        localReward.Title = reward.Title;
        localReward.Cost = reward.Cost;
        localReward.IsUserInputRequired = reward.IsUserInputRequired;
        localReward.Prompt = reward.Prompt;
        localReward.BackgroundColor = reward.BackgroundColor;
        localReward.Extra = reward.Extra;

        await _db.SaveChangesAsync();

        if (localReward.IsCreated && localReward.TwitchId is not null)
        {
            var remoteRewardRes = await _twitchApi.UpdateCustomReward(auth, localReward.TwitchId, new()
            {
                title = localReward.Title,
                cost = localReward.Cost,
                is_user_input_required = localReward.IsUserInputRequired,
                prompt = localReward.Prompt,
                background_color = localReward.BackgroundColor,
                is_enabled = true,
                should_redemptions_skip_request_queue = false
            });
            if (remoteRewardRes is null || !remoteRewardRes.IsSuccessStatusCode)
            {
                _logger.LogError("[{AuthUsername}] Failed to update custom twitch reward {CustomRewardType} - {StatusCode}", auth.Username, reward.RewardType, remoteRewardRes?.StatusCode);
                return false;
            }
        }

        return true;
    }

    public async Task<bool> Disable(TwitchAuthModel auth, TwitchAuthModel botAuth, ERewardType type)
    {
        var localReward = await _db.TwitchCustomRewards.FirstOrDefaultAsync(x => x.RewardType == type && x.AuthUuid == auth.Uuid);
        if (localReward is null)
        {
            if (!await Sync(auth))
            {
                _logger.LogError("[{AuthUsername}] Failed to disable custom reward of type {CustomRewardType}", auth.Username, type);
                return false;
            }
            localReward = await _db.TwitchCustomRewards.FirstOrDefaultAsync(x => x.RewardType == type && x.AuthUuid == auth.Uuid);
        }
        if (localReward is null)
        {
            _logger.LogError("[{AuthUsername}] Wth? Something strange happened while disabling custom twitch reward {CustomRewardType}", auth.Username, type);
            return false;
        }

        if (localReward.BotAuthUuid != botAuth.Uuid)
        {
            localReward.BotAuthUuid = botAuth.Uuid;
            await _db.SaveChangesAsync();
        }

        if (!localReward.IsCreated)
        {
            _logger.LogError("[{AuthUsername}] Custom reward {CustomRewardType} already disabled", auth.Username, type);
            return false;
        }

        if (localReward.TwitchId is not null)
            await _twitchApi.DeleteCustomReward(auth, localReward.TwitchId);

        localReward.IsCreated = false;
        localReward.TwitchId = null;
        await _db.SaveChangesAsync();

        return true;
    }

    public async Task<List<Redemption>> GetRedemptions(TwitchAuthModel auth)
    {
        var localRewards = await _db.TwitchCustomRewards.Where(x => x.AuthUuid == auth.Uuid && x.IsCreated).ToArrayAsync();
        var redemptions = new List<Redemption>();
        foreach (var localReward in localRewards)
        {
            if (localReward.TwitchId is null)
                continue;

            var res = await _twitchApi.GetFirstUnfulfilledRedemption(auth, localReward.TwitchId);
            if (res is not null && res.IsSuccessStatusCode)
            {
                foreach (var redemption in res.data)
                {
                    redemption.reward_type = localReward.RewardType;
                    redemption.bot_auth_uuid = localReward.BotAuthUuid.HasValue ? localReward.BotAuthUuid.Value : null;
                    redemption.extra = localReward.Extra;
                    redemptions.Add(redemption);
                }
            }
        }

        return redemptions;
    }

    public async Task<bool> Fulfill(TwitchAuthModel auth, ERewardType type, string redemptionId)
    {
        var localReward = await _db.TwitchCustomRewards.FirstOrDefaultAsync(x => x.AuthUuid == auth.Uuid && x.RewardType == type && x.IsCreated);
        if (localReward?.TwitchId is null)
        {
            _logger.LogError("[{AuthUsername}] Failed to fulfill custom reward of type {CustomRewardType}", auth.Username, type);
            return false;
        }

        var res = await _twitchApi.UpdateRedemptionStatus(auth, localReward.TwitchId, redemptionId, ERedemptionStatus.FULFILLED);
        return res?.IsSuccessStatusCode ?? false;
    }

    public async Task<bool> Cancel(TwitchAuthModel auth, ERewardType type, string redemptionId)
    {
        var localReward = await _db.TwitchCustomRewards.FirstOrDefaultAsync(x => x.AuthUuid == auth.Uuid && x.RewardType == type && x.IsCreated);
        if (localReward?.TwitchId is null)
        {
            _logger.LogError("[{AuthUsername}] Failed to fulfill custom reward of type {CustomRewardType}", auth.Username, type);
            return false;
        }

        var res = await _twitchApi.UpdateRedemptionStatus(auth, localReward.TwitchId, redemptionId, ERedemptionStatus.CANCELED);
        return res?.IsSuccessStatusCode ?? false;
    }

}
