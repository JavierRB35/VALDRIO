namespace Valdrio.Application;

public sealed record ConfirmedCommitRefreshResult(
    bool RefreshSucceeded,
    Exception? RefreshFailure);

public static class ConfirmedCommitRefresh
{
    public static async Task<ConfirmedCommitRefreshResult> ApplyThenRefreshAsync<T>(
        T confirmed,
        Action<T> applyConfirmed,
        Func<Task> refresh)
    {
        ArgumentNullException.ThrowIfNull(applyConfirmed);
        ArgumentNullException.ThrowIfNull(refresh);

        applyConfirmed(confirmed);

        try
        {
            await refresh();
            return new(true, null);
        }
        catch (Exception exception)
        {
            return new(false, exception);
        }
    }
}
