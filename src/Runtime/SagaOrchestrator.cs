using Npgsql;

namespace transactionalsystem.Runtime;

/// <summary>
/// Orchestrated saga: progress is persisted after every step; when a step fails, the completed steps
/// are compensated in reverse order. Re-running a saga id resumes after its completed steps.
/// </summary>
public sealed class SagaOrchestrator
{
    public sealed record Step(string Name, Func<Task> Action, Func<Task> Compensate);

    public sealed record Status(string Value, IReadOnlyList<string> CompletedSteps);

    private readonly NpgsqlDataSource _db;
    private readonly string _s;

    public SagaOrchestrator(NpgsqlDataSource db, string? schema = null)
    {
        _db = db;
        _s = Schema.Name(schema);
    }

    public async Task<string> RunAsync(string sagaId, string tenantId, IReadOnlyList<Step> steps)
    {
        await using var conn = await _db.OpenConnectionAsync();
        await CommandService.Exec(conn, $"INSERT INTO {_s}.ghk_sagas (id, tenant_id, status) VALUES ($1, $2, 'RUNNING') ON CONFLICT (id) DO NOTHING", CancellationToken.None, sagaId, tenantId);
        var completed = new List<string>((await StatusAsync(sagaId))?.CompletedSteps ?? Array.Empty<string>());
        foreach (var step in steps)
        {
            if (completed.Contains(step.Name)) continue;
            try
            {
                await step.Action();
            }
            catch
            {
                await Save(conn, sagaId, "COMPENSATING", completed);
                for (var i = completed.Count - 1; i >= 0; i--)
                {
                    await steps.First(s => s.Name == completed[i]).Compensate();
                    completed.RemoveAt(i);
                    await Save(conn, sagaId, "COMPENSATING", completed);
                }
                await Save(conn, sagaId, "COMPENSATED", completed);
                return "COMPENSATED";
            }
            completed.Add(step.Name);
            await Save(conn, sagaId, "RUNNING", completed);
        }
        await Save(conn, sagaId, "COMPLETED", completed);
        return "COMPLETED";
    }

    public async Task<Status?> StatusAsync(string sagaId)
    {
        await using var cmd = _db.CreateCommand($"SELECT status, completed_steps FROM {_s}.ghk_sagas WHERE id = $1");
        cmd.Parameters.Add(new() { Value = sagaId });
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        var done = reader.GetString(1);
        return new Status(reader.GetString(0), done.Length == 0 ? Array.Empty<string>() : done.Split(','));
    }

    private Task Save(NpgsqlConnection conn, string sagaId, string status, List<string> completed) =>
        CommandService.Exec(conn, $"UPDATE {_s}.ghk_sagas SET status = $2, completed_steps = $3, updated_at = now() WHERE id = $1", CancellationToken.None, sagaId, status, string.Join(',', completed));
}
