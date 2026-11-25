using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Storage;

namespace Quizware.Infrastructure.Persistence;

/// <summary>P15-04: tells "the database cannot be reached right now" apart
/// from every other database error. The first is worth retrying in a moment
/// (503); a constraint violation, for one, is not, so SQL errors are judged
/// by number (connection, network and timeout failures) and severity.</summary>
public static class DatabaseFailures
{
    // -2 timeout; -1, 2, 53, 64, 121, 233, 10053/4, 10060/1, 11001 connection
    // and network failures; 4060 database not available (still starting);
    // 40xxx/49xxx Azure SQL "service busy / unavailable".
    private static readonly HashSet<int> UnavailableNumbers =
        [-2, -1, 2, 20, 53, 64, 121, 233, 4060, 10053, 10054, 10060, 10061, 11001, 40143, 40197, 40501, 40613, 49918, 49919, 49920];

    public static bool IsUnavailable(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is RetryLimitExceededException or TimeoutException
                || (current is SqlException sql && (UnavailableNumbers.Contains(sql.Number) || sql.Class >= 20)))
            {
                return true;
            }
        }

        return false;
    }
}
