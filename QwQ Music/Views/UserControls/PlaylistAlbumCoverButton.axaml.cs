using Avalonia;
using Avalonia.Controls;

namespace QwQ_Music.Views.UserControls;

public partial class PlaylistAlbumCoverButton : Button {
    public static readonly StyledProperty<bool> ExternalPointerOverProperty =
        AvaloniaProperty.Register<PlaylistAlbumCoverButton, bool>(nameof(ExternalPointerOver));

    public PlaylistAlbumCoverButton() { InitializeComponent(); }

    public bool ExternalPointerOver {
        get => GetValue(ExternalPointerOverProperty);
        set => SetValue(ExternalPointerOverProperty, value);
    }
}