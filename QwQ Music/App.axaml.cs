using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using QwQ_Music.Common;
using QwQ_Music.Common.Services;
using QwQ_Music.ViewModels;
using QwQ_Music.Views;
using QwQ_Music.Windows;
using Ursa.Controls;

namespace QwQ_Music;

public class App : Application {
    public static MainWindow? TopLevel { get; private set; }

    public static Assembly CurrentAssembly { get; } = Assembly.GetExecutingAssembly();

    public override void Initialize() {
        AvaloniaXamlLoader.Load(this);

#if DEBUG
        this.AttachDeveloperTools();
#endif

        AppResources.Default.Initialize();
        DataContext = new ApplicationViewModel();
    }

    public override void OnFrameworkInitializationCompleted() {
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_OnUnhandledException;
        Dispatcher.UIThread.UnhandledException += UIThread_OnUnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_OnUnobservedTaskException;

        AppDomain.CurrentDomain.ProcessExit += CurrentDomain_OnProcessExit;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit.
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            /*DisableAvaloniaDataAnnotationValidation();*/

            desktop.MainWindow = TopLevel = new MainWindow { DataContext = new MainWindowViewModel() };

            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void CurrentDomain_OnProcessExit(object? sender, EventArgs e) {
        AppDomain.CurrentDomain.ProcessExit -= CurrentDomain_OnProcessExit;

        AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_OnUnhandledException;
        Dispatcher.UIThread.UnhandledException -= UIThread_OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= TaskScheduler_OnUnobservedTaskException;
    }

    private static volatile int _errors;

    private static void ShowExceptionOverlay(
        string message,
        string title = "异常",
        MessageBoxIcon icon = MessageBoxIcon.Error,
        MessageBoxButton button = MessageBoxButton.OK) {
        if (Interlocked.Increment(ref _errors) == 11) {
            throw new InvalidOperationException();
        }

        MessageBox.ShowOverlayAsync(message, title, icon: icon, button: button)
                  .ContinueWith(_ => Interlocked.Decrement(ref _errors))
                  .ConfigureAwait(false);
    }

    private static void TaskScheduler_OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e) {
        HandleException($"后台任务出现异常: {e.Exception.Message}", e.Exception);
    }

    private static void UIThread_OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e) {
        HandleException($"应用程序出现异常: {e.Exception.Message}", e.Exception);
        e.Handled = true;
    }

    private static void CurrentDomain_OnUnhandledException(object sender, UnhandledExceptionEventArgs e) {
        // LoggerService.Error("应用域错误: ", (e.ExceptionObject as Exception)!);
    }

    private static void HandleException(string message, Exception? exception = null) {
        string fullMessage = exception != null ? $"{message}\n\n详细信息:\n{exception}" : message;

        if (Dispatcher.UIThread.CheckAccess())
            ShowExceptionOverlay(fullMessage);
        else
            Dispatcher.UIThread.Post(() => ShowExceptionOverlay(fullMessage));

        LoggerService.Error(fullMessage);
    }

    /*
    private static void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove = BindingPlugins
            .DataValidators.OfType<DataAnnotationsValidationPlugin>()
            .ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
    */
}