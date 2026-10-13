using System.Net;
using System.Net.Http.Json;
using transactionalsystem.Api.Auth;
using transactionalsystem.Runtime;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Xunit;

namespace transactionalsystem.Tests.Api;

/// <summary>HTTP contract of the application layer against PostgreSQL (needs DATABASE_URL).</summary>
[Trait("Category", "Integration")]
// One collection: the HTTP tests of every feature migrate the same (public) schema, so they run one at a time.
[Collection("HttpApplicationLayer")]
public sealed class TransactionalSystemHttpTests : IAsyncLifetime
{
    private const string Secret = "integration-test-secret-with-32-chars!";
    private readonly WebApplicationFactory<Program> _factory = new WebApplicationFactory<Program>()
        // The relay is off here: its publish activities would mix with the runtime tests' trace assertions.
        .WithWebHostBuilder(b => b.UseSetting("Jwt:Secret", Secret).UseSetting("Outbox:RelayEnabled", "false"));
    private NpgsqlDataSource _db = null!;

    public async Task InitializeAsync()
    {
        _db = Connections.FromUrl(Environment.GetEnvironmentVariable("DATABASE_URL")!);
        await Schema.MigrateAsync(_db);
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _db.DisposeAsync();
    }

    private static string Bearer(string? tenant, string secret = Secret) =>
        "Bearer " + JwtTenant.Sign(tenant is null ? new Dictionary<string, object> { ["sub"] = "u1" } : new Dictionary<string, object> { [JwtTenant.TenantClaim] = tenant }, secret);

    private async Task<HttpResponseMessage> Post(string path, params (string Name, string Value)[] headers)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(new Dictionary<string, object>()) };
        foreach (var (name, value) in headers) request.Headers.TryAddWithoutValidation(name, value);
        return await _factory.CreateClient().SendAsync(request);
    }

    [Fact]
    public async Task Rejects_requests_without_a_valid_token_and_never_trusts_X_Tenant_Id()
    {
        var path = $"/api/v1/transactional-system/{Guid.NewGuid()}/process_transactional_system";
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(path, ("X-Tenant-Id", "acme"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(path, ("Authorization", Bearer(null)))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(path, ("Authorization", Bearer("acme", "wrong-secret")))).StatusCode);
    }

    [Fact]
    public async Task Runs_a_command_into_the_outbox_for_the_token_tenant_idempotently()
    {
        var tenant = $"tenant-{Guid.NewGuid()}";
        var id = Guid.NewGuid().ToString();
        var path = $"/api/v1/transactional-system/{id}/process_transactional_system";
        var headers = new[] { ("Authorization", Bearer(tenant)), ("Idempotency-Key", Guid.NewGuid().ToString()), ("X-Tenant-Id", "someone-else") };
        Assert.Equal(HttpStatusCode.Accepted, (await Post(path, headers)).StatusCode);
        var replay = await Post(path, headers);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Contains("replayed", await replay.Content.ReadAsStringAsync());

        await using var cmd = _db.CreateCommand("SELECT tenant_id FROM ghk_outbox WHERE aggregate_id = $1");
        cmd.Parameters.AddWithValue(id);
        Assert.Equal(tenant, await cmd.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Maps_domain_validation_errors_to_422()
    {
        var response = await Post($"/api/v1/transactional-system/{Guid.NewGuid()}/no_such_command", ("Authorization", Bearer("acme")));
        Assert.Equal((HttpStatusCode)422, response.StatusCode);
    }
}
