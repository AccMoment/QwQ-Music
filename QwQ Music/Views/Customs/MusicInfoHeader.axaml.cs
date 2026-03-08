using System;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media.Imaging;
using QwQ_Music.Common.Managers;

namespace QwQ_Music.Views.Customs;

public class MusicInfoHeader : TemplatedControl {
    public static readonly DirectProperty<MusicInfoHeader, string> TitleProperty =
        AvaloniaProperty.RegisterDirect<MusicInfoHeader, string>(nameof(Title), o => o.Title, (o, v) => o.Title = v);

    public string Title {
        get;
        set => SetAndRaise(TitleProperty, ref field, value);
    } = "";

    public static readonly DirectProperty<MusicInfoHeader, Bitmap> CoverProperty =
        AvaloniaProperty.RegisterDirect<MusicInfoHeader, Bitmap>(nameof(Cover), o => o.Cover, (o, v) => o.Cover = v);

    public Bitmap Cover {
        get;
        set => SetAndRaise(CoverProperty, ref field, value);
    } = CacheManager.Loading;

    public static readonly DirectProperty<MusicInfoHeader, string> DescriptionProperty =
        AvaloniaProperty.RegisterDirect<MusicInfoHeader, string>(
            nameof(Description),
            o => o.Description,
            (o, v) => o.Description = v);

    public string Description {
        get;
        set => SetAndRaise(DescriptionProperty, ref field, value);
    } = "";

    public static readonly DirectProperty<MusicInfoHeader, DateTime> CreateTimeProperty =
        AvaloniaProperty.RegisterDirect<MusicInfoHeader, DateTime>(
            nameof(CreateTime),
            o => o.CreateTime,
            (o, v) => o.CreateTime = v);

    public DateTime CreateTime {
        get;
        set => SetAndRaise(CreateTimeProperty, ref field, value);
    }//TODO: XAML INSERT

    public static readonly DirectProperty<MusicInfoHeader, DateTime> LatestModifyTimeProperty =
        AvaloniaProperty.RegisterDirect<MusicInfoHeader, DateTime>(
            nameof(LatestModifyTime),
            o => o.LatestModifyTime,
            (o, v) => o.LatestModifyTime = v);

    public DateTime LatestModifyTime {
        get;
        set => SetAndRaise(LatestModifyTimeProperty, ref field, value);
    }//TODO: XAML INSERT

    public static readonly DirectProperty<MusicInfoHeader, string> CreatorProperty =
        AvaloniaProperty.RegisterDirect<MusicInfoHeader, string>(
            nameof(Creator),
            o => o.Creator,
            (o, v) => o.Creator = v);

    public string Creator {
        get;
        set => SetAndRaise(CreatorProperty, ref field, value);
    } = "";//TODO: XAML INSERT

    public static readonly StyledProperty<bool> IsReadonlyProperty =
        AvaloniaProperty.Register<MusicInfoHeader, bool>(nameof(IsReadonly));

    public bool IsReadonly {
        get => GetValue(IsReadonlyProperty);
        set => SetValue(IsReadonlyProperty, value);
    }
}