using CommunityToolkit.Mvvm.ComponentModel;
using QwQ_Music.Models.Enums;

namespace QwQ_Music.Models.ConfigModels;

public partial class SystemConfig : ObservableObject {
    [ObservableProperty]
    public partial bool KeepSystemAwake { get; set; } = true;

    [ObservableProperty]
    public partial bool KeepDisplay { get; set; } = false;

    [ObservableProperty]
    public partial string Language { get; set; } = "zh_CN";

    [ObservableProperty]
    public partial bool IsDebugMode { get; set; }

    [ObservableProperty]
    public partial ClosingBehavior ClosingBehavior { get; set; } = ClosingBehavior.Ask;
}