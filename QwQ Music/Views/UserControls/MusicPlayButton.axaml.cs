using Avalonia;
using Avalonia.Controls.Primitives;

namespace QwQ_Music.Views.UserControls;

public partial class MusicPlayButton : TemplatedControl {
    public static readonly StyledProperty<double> AngleProperty =
        AvaloniaProperty.Register<MusicPlayButton, double>(nameof(Angle));

    public double Angle {
        get => GetValue(AngleProperty);
        set => SetValue(AngleProperty, value);
    }
}