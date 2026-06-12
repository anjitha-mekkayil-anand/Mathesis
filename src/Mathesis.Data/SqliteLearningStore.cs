using Mathesis.Core;
using Microsoft.Data.Sqlite;

namespace Mathesis.Data;

/// <summary>
/// SQLite-backed store for agent outputs and manager decisions. Microsoft.Data.Sqlite
/// (not EF Core) keeps the dependency surface small — the write path is a handful of
/// inserts per analysis. A single connection is held open for the process lifetime
/// and guarded by a semaphore.
/// </summary>
public sealed class SqliteLearningStore : ILearningStore, IApprovalQueue, IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SqliteLearningStore(string connectionString)
    {
        _connection = new SqliteConnection(connectionString);
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await _connection.OpenAsync(ct);

        var cmd = _connection.CreateCommand();
        cmd.CommandText =
            """
            CREATE TABLE IF NOT EXISTS study_plans (
                id               INTEGER PRIMARY KEY AUTOINCREMENT,
                learner_id       TEXT    NOT NULL,
                certification_id TEXT    NOT NULL,
                plan             TEXT    NOT NULL,
                rationale        TEXT    NOT NULL,
                created_at       TEXT    NOT NULL,
                status           TEXT    NOT NULL DEFAULT 'pending',
                notes            TEXT,
                decided_at       TEXT
            );

            CREATE TABLE IF NOT EXISTS assessments (
                id               INTEGER PRIMARY KEY AUTOINCREMENT,
                learner_id       TEXT    NOT NULL,
                certification_id TEXT    NOT NULL,
                questions        TEXT    NOT NULL,
                evaluation       TEXT    NOT NULL,
                created_at       TEXT    NOT NULL
            );

            CREATE TABLE IF NOT EXISTS next_steps (
                id               INTEGER PRIMARY KEY AUTOINCREMENT,
                learner_id       TEXT    NOT NULL,
                recommendation   TEXT    NOT NULL,
                readiness_score  REAL    NOT NULL,
                created_at       TEXT    NOT NULL
            );

            PRAGMA journal_mode=WAL;
            """;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task RecordStudyPlanAsync(
        string learnerId, string certificationId, string plan, string rationale, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var cmd = _connection.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO study_plans (learner_id, certification_id, plan, rationale, created_at)
                VALUES ($learner, $cert, $plan, $rationale, $ts);
                """;
            cmd.Parameters.AddWithValue("$learner", learnerId);
            cmd.Parameters.AddWithValue("$cert", certificationId);
            cmd.Parameters.AddWithValue("$plan", plan);
            cmd.Parameters.AddWithValue("$rationale", rationale);
            cmd.Parameters.AddWithValue("$ts", DateTimeOffset.UtcNow.ToString("O"));
            await cmd.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RecordAssessmentAsync(
        string learnerId, string certificationId, string questions, string evaluation, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var cmd = _connection.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO assessments (learner_id, certification_id, questions, evaluation, created_at)
                VALUES ($learner, $cert, $questions, $evaluation, $ts);
                """;
            cmd.Parameters.AddWithValue("$learner", learnerId);
            cmd.Parameters.AddWithValue("$cert", certificationId);
            cmd.Parameters.AddWithValue("$questions", questions);
            cmd.Parameters.AddWithValue("$evaluation", evaluation);
            cmd.Parameters.AddWithValue("$ts", DateTimeOffset.UtcNow.ToString("O"));
            await cmd.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RecordNextStepAsync(
        string learnerId, string recommendation, double readinessScore, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var cmd = _connection.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO next_steps (learner_id, recommendation, readiness_score, created_at)
                VALUES ($learner, $rec, $score, $ts);
                """;
            cmd.Parameters.AddWithValue("$learner", learnerId);
            cmd.Parameters.AddWithValue("$rec", recommendation);
            cmd.Parameters.AddWithValue("$score", readinessScore);
            cmd.Parameters.AddWithValue("$ts", DateTimeOffset.UtcNow.ToString("O"));
            await cmd.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<PlanReviewItem>> GetPendingPlansAsync(CancellationToken ct = default)
        => await QueryPlansAsync("WHERE sp.status = 'pending' ORDER BY sp.created_at DESC", ct);

    public async Task<IReadOnlyList<PlanReviewItem>> GetRecentPlansAsync(int limit = 20, CancellationToken ct = default)
        => await QueryPlansAsync($"ORDER BY sp.created_at DESC LIMIT {limit}", ct);

    private async Task<IReadOnlyList<PlanReviewItem>> QueryPlansAsync(string clause, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var cmd = _connection.CreateCommand();
            cmd.CommandText =
                $"""
                SELECT sp.id, sp.learner_id, sp.certification_id, sp.plan, sp.rationale,
                       sp.created_at, sp.status, ns.recommendation, sp.notes, sp.decided_at
                FROM study_plans sp
                LEFT JOIN next_steps ns ON ns.learner_id = sp.learner_id
                    AND ns.created_at = (
                        SELECT MAX(created_at) FROM next_steps
                        WHERE learner_id = sp.learner_id
                    )
                {clause};
                """;

            var results = new List<PlanReviewItem>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                results.Add(new PlanReviewItem(
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    DateTimeOffset.Parse(reader.GetString(5)),
                    reader.GetString(6),
                    reader.IsDBNull(7) ? null : reader.GetString(7),
                    reader.IsDBNull(8) ? null : reader.GetString(8),
                    reader.IsDBNull(9) ? null : DateTimeOffset.Parse(reader.GetString(9))));
            }
            return results;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpdatePlanDecisionAsync(long id, string decision, string? notes, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var cmd = _connection.CreateCommand();
            cmd.CommandText =
                """
                UPDATE study_plans
                SET status = $status, notes = $notes, decided_at = $ts
                WHERE id = $id;
                """;
            cmd.Parameters.AddWithValue("$status", decision);
            cmd.Parameters.AddWithValue("$notes", notes ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("$ts", DateTimeOffset.UtcNow.ToString("O"));
            cmd.Parameters.AddWithValue("$id", id);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        _gate.Dispose();
    }
}
