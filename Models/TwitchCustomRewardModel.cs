namespace Kanawanagasaki.TwitchHub.Models;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

[Table("twitch_custom_reward"), Index(nameof(AuthUuid)), Index(nameof(TwitchId))]
public class TwitchCustomRewardModel
{
    [Key]
    public required Guid Uuid { get; init; }
    public required Guid AuthUuid { get; init; }
    public required ERewardType RewardType { get; set; }
    public bool IsCreated { get; set; } = false;

    public Guid? BotAuthUuid { get; set; }
    public string? TwitchId { get; set; }

    public required string Title { get; set; }
    public required int Cost { get; set; }
    public bool IsUserInputRequired { get; set; } = false;
    public string? Prompt { get; set; }
    public string? BackgroundColor { get; set; }

    public string? Extra { get; set; } = null;
}

public enum ERewardType
{
    Add7TvEmote
}
