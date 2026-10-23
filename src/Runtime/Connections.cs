using Npgsql;

namespace transactionalsystem.Runtime;

/// <summary>Builds an Npgsql data source from a libpq-style URL: postgres://user:password@host:5432/database[?sslmode=require]</summary>
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
        // libpq TLS parameters: sslmode (disable ... verify-full; require encrypts without verifying, as in libpq) and sslrootcert.
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            var value = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : "";
            if (kv[0] == "sslmode") builder.SslMode = Enum.Parse<SslMode>(value.Replace("-", ""), ignoreCase: true);
            else if (kv[0] == "sslrootcert") builder.RootCertificate = value;
        }
        return NpgsqlDataSource.Create(builder.ConnectionString);
    }
}
