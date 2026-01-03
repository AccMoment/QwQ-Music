using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml.MarkupExtensions;

namespace QwQ_Music.Views.UserControls;

public partial class MusicAlbumCoverButton : TemplatedControl {
    public static readonly DirectProperty<MusicAlbumCoverButton, ICommand?> CommandProperty =
        AvaloniaProperty.RegisterDirect<MusicAlbumCoverButton, ICommand?>(
            nameof(Command),
            o => o.Command,
            (o, v) => o.Command = v);

    public ICommand? Command {
        get;
        set => SetAndRaise(CommandProperty, ref field, value);
    }

    public static readonly DirectProperty<MusicAlbumCoverButton, CompiledBindingExtension?> CommandParameterProperty =
        AvaloniaProperty.RegisterDirect<MusicAlbumCoverButton, CompiledBindingExtension?>(
            nameof(CommandParameter),
            o => o.CommandParameter,
            (o, v) => o.CommandParameter = v);

    public CompiledBindingExtension? CommandParameter {
        get;
        set => SetAndRaise(CommandParameterProperty, ref field, value);
    }

    public static readonly StyledProperty<bool> ExternalPointerOverProperty =
        AvaloniaProperty.Register<MusicAlbumCoverButton, bool>(nameof(ExternalPointerOver));

    public bool ExternalPointerOver {
        get => GetValue(ExternalPointerOverProperty);
        set => SetValue(ExternalPointerOverProperty, value);
    }
}