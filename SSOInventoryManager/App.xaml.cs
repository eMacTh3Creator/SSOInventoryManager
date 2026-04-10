using System.Windows;
using System.Windows.Threading;

namespace SSOInventoryManager;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        ShowErrorDialog(e.Exception);
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            ShowErrorDialog(ex);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        Dispatcher.Invoke(() => ShowErrorDialog(e.Exception));
    }

    private static void ShowErrorDialog(Exception ex)
    {
        var message = $"{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}";
        var result = MessageBox.Show(
            $"An unexpected error occurred:\n\n{message}\n\nCopy details to clipboard?",
            "SSO Inventory Manager — Error",
            MessageBoxButton.YesNo,
            MessageBoxImage.Error);

        if (result == MessageBoxResult.Yes)
            Clipboard.SetText(message);
    }
}
