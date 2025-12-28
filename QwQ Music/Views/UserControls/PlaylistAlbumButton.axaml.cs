using Avalonia;
using Avalonia.Controls;

namespace QwQ_Music.Views.UserControls;

public partial class PlaylistAlbumButton : Button {
    public static readonly StyledProperty<bool> ExternalPointerOverProperty =
        AvaloniaProperty.Register<MusicAlbumButton, bool>(nameof(ExternalPointerOver));

    public PlaylistAlbumButton() { InitializeComponent(); }

    public bool ExternalPointerOver {
        get => GetValue(ExternalPointerOverProperty);
        set => SetValue(ExternalPointerOverProperty, value);
    }
}