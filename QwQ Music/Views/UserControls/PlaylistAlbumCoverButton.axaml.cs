using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace QwQ_Music.Views.UserControls;

public partial class PlaylistAlbumCoverButton : TemplatedControl {
    public static readonly StyledProperty<bool> ExternalPointerOverProperty =
        AvaloniaProperty.Register<PlaylistAlbumCoverButton, bool>(nameof(ExternalPointerOver));

    public bool ExternalPointerOver {
        get => GetValue(ExternalPointerOverProperty);
        set => SetValue(ExternalPointerOverProperty, value);
    }
}