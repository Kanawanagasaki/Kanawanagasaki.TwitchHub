namespace Kanawanagasaki.TwitchHub;

using Kanawanagasaki.TwitchHub.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

public class SQLiteContext : DbContext
{
    public DbSet<TwitchAuthModel> TwitchAuth { get; set; }
    public DbSet<TextCommandModel> TextCommands { get; set; }
    public DbSet<JsAfkCodeModel> JsAfkCodes { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var connectionStringBuilder = new SqliteConnectionStringBuilder { DataSource = "data.db" };
        var connection = new SqliteConnection(connectionStringBuilder.ToString());

        optionsBuilder.UseSqlite(connection);
    }
}
