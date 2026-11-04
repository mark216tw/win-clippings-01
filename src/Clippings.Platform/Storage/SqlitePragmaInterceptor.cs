using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Clippings.Platform.Storage;

public sealed class SqlitePragmaInterceptor : DbConnectionInterceptor
{
    private static readonly string[] Pragmas =
    [
        "PRAGMA synchronous=NORMAL;",
        "PRAGMA temp_store=MEMORY;",
        "PRAGMA cache_size=-8192;",
    ];

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        base.ConnectionOpened(connection, eventData);
        ApplyPragmas(connection);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken).ConfigureAwait(false);
        foreach (var pragma in Pragmas)
        {
            using var command = connection.CreateCommand();
            command.CommandText = pragma;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static void ApplyPragmas(DbConnection connection)
    {
        foreach (var pragma in Pragmas)
        {
            using var command = connection.CreateCommand();
            command.CommandText = pragma;
            command.ExecuteNonQuery();
        }
    }
}
