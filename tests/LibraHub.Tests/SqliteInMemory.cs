using LibraHub.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LibraHub.Tests;

/// <summary>A private, in-memory SQLite connection kept open for the lifetime of one test.</summary>
public class SqliteInMemory : IDisposable
{
    readonly SqliteConnection _conn;
    public SqliteInMemory()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        using var ctx = NewContext();
        ctx.Database.EnsureCreated();
    }
    public AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options);
    public void Dispose() => _conn.Dispose();
}
