namespace BalanceBoard.Core.Services;

/// <summary>
/// Runs work on an STA thread — required for Win32 Bluetooth pairing APIs.
/// </summary>
public static class StaThread
{
    public static T Run<T>(Func<T> func)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            return func();
        }

        T? result = default;
        Exception? error = null;
        using var done = new ManualResetEventSlim(false);

        var thread = new Thread(() =>
        {
            try
            {
                result = func();
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                done.Set();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        done.Wait();

        if (error is not null)
        {
            throw error;
        }

        return result!;
    }

    public static void Run(Action action) => Run(() =>
    {
        action();
        return true;
    });
}
