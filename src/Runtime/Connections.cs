using Npgsql;

namespace transactionalsystem.Runtime;

/// <summary>Builds an Npgsql data source from a libpq-style URL: postgres://user:password@host:5432/database</summary>
public static class Connections
{
    public static NpgsqlDataSource FromUrl(string databaseUrl)
    {
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null
        };
        return NpgsqlDataSource.Create(builder.ConnectionString);
    }
}
