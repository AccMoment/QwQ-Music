using Avalonia;
using Avalonia.Controls;
using QwQ_Music.Models;

namespace QwQ_Music.Views.UserControls;

public partial class MusicAlbumButton : Button
{
    public static readonly StyledProperty<bool> ExternalMouseTouchProperty = AvaloniaProperty.Register<
        MusicAlbumButton,
        bool
    >(nameof(ExternalMouseTouch));

    public static readonly StyledProperty<MusicItemModel> CurrentMusicItemProperty = AvaloniaProperty.Register<
        MusicAlbumButton,
        MusicItemModel
    >(nameof(CurrentMusicItem));

    public static readonly StyledProperty<bool> IsPlayingProperty = AvaloniaProperty.Register<MusicAlbumButton, bool>(
        nameof(IsPlaying)
    );

    public MusicAlbumButton()
    {
        InitializeComponent();
    }

    public bool ExternalMouseTouch
    {
        get => GetValue(ExternalMouseTouchProperty);
        set => SetValue(ExternalMouseTouchProperty, value);
    }

    public MusicItemModel CurrentMusicItem
    {
        get => GetValue(CurrentMusicItemProperty);
        set => SetValue(CurrentMusicItemProperty, value);
    }

    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }
}
