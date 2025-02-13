namespace Kanawanagasaki.TwitchHub;

using Kanawanagasaki.TwitchHub.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

public class SQLiteContext : DbContext
{
    public required DbSet<TwitchAuthModel> TwitchAuth { get; set; }
    public required DbSet<TwitchCustomRewardModel> TwitchCustomRewards { get; set; }
    public required DbSet<TextCommandModel> TextCommands { get; set; }
    public required DbSet<JsAfkCodeModel> JsAfkCodes { get; set; }
    public required DbSet<ViewerVoice> ViewerVoices { get; set; }
    public required DbSet<SettingModel> Settings { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var connectionStringBuilder = new SqliteConnectionStringBuilder { DataSource = "data.db" };
        var connection = new SqliteConnection(connectionStringBuilder.ToString());

        optionsBuilder.UseSqlite(connection);
    }
}
