using CommunityToolkit.Mvvm.ComponentModel;
using QwQ_Music.ViewModels.Bases;

namespace QwQ_Music.ViewModels;

public partial class FatalExceptionViewModel : ViewModelBase
{
    [ObservableProperty] public partial object? ExceptionObject { get; set; }

    [ObservableProperty] public partial string? ExceptionMessage { get; set; }

    [ObservableProperty] public partial string? ExceptionStackTrace { get; set; }
}
