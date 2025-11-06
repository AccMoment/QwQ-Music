using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using QwQ_Music.ViewModels;

namespace QwQ_Music.Views.Windows;

public partial class DesktopPlayControlWindow : Window
{
    public DesktopPlayControlWindow()
    {
        InitializeComponent();
        DataContext = MusicPlayerViewModel.Default;
    }

    public static readonly StyledProperty<bool> StartMovingOutProperty = AvaloniaProperty.Register<DesktopPlayControlWindow, bool>(
        nameof(StartMovingOut));

    public bool StartMovingOut
    {
        get => GetValue(StartMovingOutProperty);
        set => SetValue(StartMovingOutProperty, value);
    }

    public static readonly StyledProperty<TimeSpan> RemoveAnimationDurationProperty = AvaloniaProperty.Register<DesktopPlayControlWindow, TimeSpan>(
        nameof(RemoveAnimationDuration));

    public TimeSpan RemoveAnimationDuration
    {
        get => GetValue(RemoveAnimationDurationProperty);
        set => SetValue(RemoveAnimationDurationProperty, value);
    }

    // 监听 StartMovingOut 属性变化
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsVisibleProperty)
        {
            object? newValue = change.NewValue;

            if (newValue is true)
            {
                StartMovingOut = false;
            }
        }

        if (change.Property == StartMovingOutProperty)
        {
            object? newValue = change.NewValue;

            if (newValue is true)
            {
                _ = HandleStartMovingOutAsync();
            }
        }
        
    }

    private async Task HandleStartMovingOutAsync()
    {
        // 等待指定的动画时长
        await Task.Delay(RemoveAnimationDuration);

        // 在UI线程上隐藏窗口
        await Dispatcher.UIThread.InvokeAsync(Hide);
    }
}
