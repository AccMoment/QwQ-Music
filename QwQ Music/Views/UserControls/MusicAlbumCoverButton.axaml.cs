using Avalonia;
using Avalonia.Controls;

namespace QwQ_Music.Views.UserControls;

public partial class MusicAlbumCoverButton : Button {
    public static readonly StyledProperty<bool> ExternalPointerOverProperty =
        AvaloniaProperty.Register<MusicAlbumCoverButton, bool>(nameof(ExternalPointerOver));

    public MusicAlbumCoverButton() { InitializeComponent(); }

    public bool ExternalPointerOver {
        get => GetValue(ExternalPointerOverProperty);
        set => SetValue(ExternalPointerOverProperty, value);
    }
}