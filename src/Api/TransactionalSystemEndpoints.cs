using System.Text.Json;
using transactionalsystem.Api.Auth;
using transactionalsystem.Domain.Kernel;
using transactionalsystem.Runtime;

namespace transactionalsystem.Api;

/// <summary>POST /api/v1/transactional-system/{id}/{command} (process_transactional_system).</summary>
// ghk:implements TransactionalSystem
public static class TransactionalSystemEndpoints
{
    private static readonly Dictionary<string, int> StatusCodes = new() { ["created"] = 202, ["replayed"] = 200, ["in-progress"] = 409 };

    public static IEndpointRouteBuilder MapTransactionalSystemEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/transactional-system/{id}/{command}", async (string id, string command, HttpRequest request, CommandService commands, IConfiguration config) =>
        {
            var tenant = JwtTenant.TenantFrom(request.Headers.Authorization.ToString(), config["Jwt:Secret"] ?? config["JWT_SECRET"]);
            if (tenant is null) return Results.Json(new { detail = "A valid bearer token with a tenant is required" }, statusCode: 401);
            Dictionary<string, object?>? payload = null;
            if (request.ContentLength is > 0) payload = await request.ReadFromJsonAsync<Dictionary<string, object?>>();
            try
            {
                var result = await commands.HandleAsync(new CommandService.Request(
                    tenant, id, command, payload,
                    request.Headers.TryGetValue("Idempotency-Key", out var key) ? key.ToString() : null,
                    request.Headers.TryGetValue("traceparent", out var tp) ? tp.ToString() : null,
                    "TransactionalSystem"));
                return Results.Json(result, statusCode: StatusCodes.GetValueOrDefault(result.Status, 200));
            }
            catch (DomainValidationException e)
            {
                return Results.Json(new { detail = e.Message }, statusCode: 422);
            }
        });
        return app;
    }
}
