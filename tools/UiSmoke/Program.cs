using System.Windows;
using System.Windows.Threading;
using BalanceBoard.App;
using BalanceBoard.App.Services;

// Loads MainWindow on an STA thread with the same merged theme dictionaries as App.xaml.
Environment.SetEnvironmentVariable("BALANCEBOARD_DEV", "1");

const string AppAssembly = "BalanceBoardApp";

var exitCode = 0;
var started = new ManualResetEventSlim(false);

var thread = new Thread(() =>
{
    try
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(LoadTheme("Themes/Colors.xaml"));
        app.Resources.MergedDictionaries.Add(LoadTheme("Themes/Controls.xaml"));

        app.Dispatcher.Invoke(() =>
        {
            var options = StartupOptions.Parse(["--dev", "--no-cleanup", "--allow-multiple"]);
            var window = new MainWindow(options);
            Console.WriteLine("UI smoke: MainWindow constructed and XAML loaded.");
            window.Close();
        });
        app.Dispatcher.InvokeShutdown();
    }
    catch (Exception ex)
    {
        exitCode = 1;
        Console.Error.WriteLine("UI smoke FAILED: MainWindow could not load.");
        Console.Error.WriteLine(ex.ToString());
    }
    finally
    {
        started.Set();
    }
});

thread.SetApartmentState(ApartmentState.STA);
thread.Start();
started.Wait(TimeSpan.FromSeconds(30));

if (!thread.Join(TimeSpan.FromSeconds(5)))
{
    Console.Error.WriteLine("UI smoke FAILED: timed out.");
    exitCode = 1;
}

return exitCode;

static ResourceDictionary LoadTheme(string relativePath) =>
    new()
    {
        Source = new Uri($"pack://application:,,,/{AppAssembly};component/{relativePath}", UriKind.Absolute),
    };
