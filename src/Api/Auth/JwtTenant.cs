using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace transactionalsystem.Api.Auth;

/// <summary>
/// HS256 bearer tokens. The tenant comes only from the "tenant_id" claim of a validated token, never from a
/// header such as X-Tenant-Id, and there is no default tenant.
/// </summary>
public static class JwtTenant
{
    public const string TenantClaim = "tenant_id";

    private static byte[] FromBase64Url(string s)
    {
        var padded = s.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '='));
    }

    private static string ToBase64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Tenant claim of a valid token (signature, exp, nbf checked), else null.</summary>
    public static string? TenantFrom(string? authorization, string? secret)
    {
        if (string.IsNullOrEmpty(secret) || authorization is null || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return null;
        var parts = authorization[7..].Trim().Split('.');
        if (parts.Length != 3) return null;
        try
        {
            using var header = JsonDocument.Parse(FromBase64Url(parts[0]));
            if (!header.RootElement.TryGetProperty("alg", out var alg) || alg.GetString() != "HS256") return null;
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var expected = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{parts[0]}.{parts[1]}"));
            if (!CryptographicOperations.FixedTimeEquals(expected, FromBase64Url(parts[2]))) return null;
            using var claims = JsonDocument.Parse(FromBase64Url(parts[1]));
            var root = claims.RootElement;
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (root.TryGetProperty("exp", out var exp) && exp.ValueKind == JsonValueKind.Number && now >= exp.GetInt64()) return null;
            if (root.TryGetProperty("nbf", out var nbf) && nbf.ValueKind == JsonValueKind.Number && now < nbf.GetInt64()) return null;
            return root.TryGetProperty(TenantClaim, out var tenant) && tenant.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(tenant.GetString()) ? tenant.GetString() : null;
        }
        catch (Exception e) when (e is FormatException or JsonException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>Signs an HS256 token (tests and local tooling; production tokens come from your identity provider).</summary>
    public static string Sign(IDictionary<string, object> claims, string secret)
    {
        var head = ToBase64Url(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        var body = ToBase64Url(JsonSerializer.SerializeToUtf8Bytes(claims));
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return $"{head}.{body}.{ToBase64Url(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{head}.{body}")))}";
    }
}
