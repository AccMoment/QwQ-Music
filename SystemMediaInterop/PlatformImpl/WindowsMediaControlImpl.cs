#if _WIN_NT

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Windows.Media;
using Windows.Media.Playback;
using Windows.Storage.Streams;
using Microsoft.Win32;

namespace SystemMediaInterop.PlatformImpl;

public sealed class WindowsMediaControlImpl : ISystemMediaControlImpl {
    private readonly MediaPlayer _systemInterfaceProvider;
    private InMemoryRandomAccessStream? _thumbnail;

    public WindowsMediaControlImpl() {
        _systemInterfaceProvider = new MediaPlayer();
        _systemInterfaceProvider.CommandManager.IsEnabled = false;
        Provider.IsEnabled = true;
        Provider.DisplayUpdater.AppMediaId = SystemMediaControl.AppId;
        Provider.DisplayUpdater.Update();
        Provider.ButtonPressed += OnButtonPressed;
        Provider.PlaybackPositionChangeRequested += OnPlaybackPositionChangeRequested;
    }


    public async Task UpdateInfoAsync(IMediaItem model) {
        Provider.DisplayUpdater.Type = MediaPlaybackType.Music;
        Provider.DisplayUpdater.MusicProperties.Title = model.Title;
        Provider.DisplayUpdater.MusicProperties.Artist = model.Artists;
        Provider.DisplayUpdater.MusicProperties.AlbumTitle = model.Album;
        InMemoryRandomAccessStream? old = _thumbnail;
        await UpdateThumbnailAsync(model.ThumbnailStream).ConfigureAwait(false);
        if (_thumbnail is not null)
            Provider.DisplayUpdater.Thumbnail = RandomAccessStreamReference.CreateFromStream(_thumbnail);
        Duration = model.Duration;
        Provider.DisplayUpdater.Update();
        old?.Dispose();
        return;

        async Task UpdateThumbnailAsync(Stream source) {
            var stream = new InMemoryRandomAccessStream();
            await source.CopyToAsync(stream.AsStreamForWrite());
            stream.Seek(0);
            _thumbnail = stream;
        }
    }

    private SystemMediaTransportControls Provider => _systemInterfaceProvider.SystemMediaTransportControls;


    public double PlaybackSpeed {
        get => Provider.PlaybackRate;
        set => Provider.PlaybackRate = value;
    }

    public double Volume { get; set; }

    public TimeSpan Position {
        get;
        set {
            field = value;
            Provider.UpdateTimelineProperties(
                new SystemMediaTransportControlsTimelineProperties {
                    Position = value,
                    StartTime = TimeSpan.Zero,
                    EndTime = Duration,
                    MaxSeekTime = Duration,
                    MinSeekTime = TimeSpan.Zero
                });
        }
    }

    public TimeSpan Duration {
        get;
        set {
            field = value;
            Provider.UpdateTimelineProperties(
                new SystemMediaTransportControlsTimelineProperties {
                    Position = Position,
                    StartTime = TimeSpan.Zero,
                    EndTime = value,
                    MaxSeekTime = value,
                    MinSeekTime = TimeSpan.Zero
                });
        }
    }

    public MediaPlaybackStatus Status {
        get => StatusConverter.Convert(Provider.PlaybackStatus);
        set => Provider.PlaybackStatus = StatusConverter.Convert(value);
    }

    public bool ShuffleEnabled {
        get => false;
        set => throw new InvalidOperationException();
    }

    // public MediaPlaybackMode Mode { get; set; }

    public bool IsPlayEnabled {
        get => Provider.IsPlayEnabled;
        set => Provider.IsPlayEnabled = value;
    }

    public bool IsPauseEnabled {
        get => Provider.IsPauseEnabled;
        set => Provider.IsPauseEnabled = value;
    }

    public bool IsPreviousEnabled {
        get => Provider.IsPreviousEnabled;
        set => Provider.IsPreviousEnabled = value;
    }

    public bool IsNextEnabled {
        get => Provider.IsNextEnabled;
        set => Provider.IsNextEnabled = value;
    }

    public bool IsStopEnabled {
        get => Provider.IsStopEnabled;
        set => Provider.IsStopEnabled = value;
    }

    public event EventHandler? PlayRequested;
    public event EventHandler? PauseRequested;
    public event EventHandler? NextRequested;
    public event EventHandler? PreviousRequested;
    public event EventHandler? StopRequested;
    public event EventHandler<PlaybackPositionChangedEventArgs>? SeekRequested;

    private void OnButtonPressed(
        SystemMediaTransportControls sender,
        SystemMediaTransportControlsButtonPressedEventArgs args) {
        switch (args.Button) {
            case SystemMediaTransportControlsButton.Play:
                PlayRequested?.Invoke(this, EventArgs.Empty);
                break;
            case SystemMediaTransportControlsButton.Pause:
                PauseRequested?.Invoke(this, EventArgs.Empty);
                break;
            case SystemMediaTransportControlsButton.Next:
                NextRequested?.Invoke(this, EventArgs.Empty);
                break;
            case SystemMediaTransportControlsButton.Previous:
                PreviousRequested?.Invoke(this, EventArgs.Empty);
                break;
            case SystemMediaTransportControlsButton.Stop:
                StopRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    private void OnPlaybackPositionChangeRequested(
        SystemMediaTransportControls sender,
        PlaybackPositionChangeRequestedEventArgs args) {
        SeekRequested?.Invoke(this, new PlaybackPositionChangedEventArgs(args.RequestedPlaybackPosition));
    }

    public void Dispose() {
        Provider.ButtonPressed -= OnButtonPressed;
        Provider.PlaybackPositionChangeRequested -= OnPlaybackPositionChangeRequested;
        PlayRequested = null;
        PauseRequested = null;
        NextRequested = null;
        PreviousRequested = null;
        StopRequested = null;
        SeekRequested = null;
        _systemInterfaceProvider.Dispose();
        _thumbnail?.Dispose();
        _thumbnail = null!;
        GC.SuppressFinalize(this);
    }

    ~WindowsMediaControlImpl() { Dispose(); }

    private static void TryRegisterAppUserModelId(string appUserModelId, string displayName, string iconPath) {
        string subKey = $@"Software\Classes\AppUserModelId\{appUserModelId}";
        try {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(subKey)) {
                key.SetValue("DisplayName", displayName, RegistryValueKind.String);
                key.SetValue("IconUri", iconPath, RegistryValueKind.String);
            }

            Console.WriteLine("应用身份信息已注册到当前用户。");
        } catch (Exception ex) {
            Console.WriteLine($"写入注册表失败: {ex.Message}");
        }
    }

    [RequiresUnreferencedCode("Create start menu shortcut requires native COM interops.")]
    public static void SetProcessInfoId() {
        TryRegisterAppUserModelId(
            SystemMediaControl.AppId,
            "QwQ Music",
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "QwQ Music.ico"));
        SetCurrentProcessExplicitAppUserModelID(SystemMediaControl.AppId);
        TryCreateStartMenuShortcut(SystemMediaControl.AppId);
        return;

        [DllImport("shell32.dll", SetLastError = true)]
        // ReSharper disable once InconsistentNaming
        static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);
    }

    [RequiresUnreferencedCode("Uses COM interop types that must be preserved under trimming.")]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ShellLink))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(IShellLinkW))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(IPropertyStore))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(IPersistFile))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(PropVariant))]
    private static void TryCreateStartMenuShortcut(string appUserModelId) {
        string exePath = Environment.ProcessPath ?? throw new InvalidOperationException("无法获取当前进程路径。");

        string startMenuFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            "Programs");
        Directory.CreateDirectory(startMenuFolder);

        string shortcutPath = Path.Combine(startMenuFolder, "QwQ Music.lnk");
        File.Delete(shortcutPath);
        string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "QwQ Music.ico");
        try {
            // ReSharper disable once SuspiciousTypeConversion.Global
            var link = (IShellLinkW)new ShellLink();
            link.SetPath(exePath);
            link.SetWorkingDirectory(AppDomain.CurrentDomain.BaseDirectory);
            link.SetDescription("QwQ Music");
            if (File.Exists(iconPath))
                link.SetIconLocation(iconPath, 0);

            // ReSharper disable once SuspiciousTypeConversion.Global
            IPropertyStore propertyStore = (IPropertyStore)link;
            PROPERTYKEY appIdKey = PROPERTYKEY.AppUserModelId;
            using (PropVariant value = PropVariant.FromString(appUserModelId)) {
                propertyStore.SetValue(ref appIdKey, value);
                propertyStore.Commit();
            }

            // ReSharper disable once SuspiciousTypeConversion.Global
            ((IPersistFile)link).Save(shortcutPath, true);
        } catch (Exception ex) {
            var err = Marshal.GetLastWin32Error();
            ex.Data.Add("LastWin32Error", err);
            throw;
        }
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, int fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    private interface IPropertyStore {
        uint GetCount(out uint propertyCount);
        uint GetAt(uint propertyIndex, out PROPERTYKEY key);
        uint GetValue(ref PROPERTYKEY key, out PropVariant pv);
        uint SetValue(ref PROPERTYKEY key, ref PropVariant pv);
        uint Commit();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    // ReSharper disable once InconsistentNaming
    private struct PROPERTYKEY {
        public Guid fmtid;
        public uint pid;

        public static PROPERTYKEY AppUserModelId =>
            new() { fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), pid = 5 };
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PropVariant : IDisposable {
        [FieldOffset(0)]
        private ushort vt;

        [FieldOffset(8)]
        private IntPtr pointerValue;

        public static PropVariant FromString(string value) {
            var pv = new PropVariant { vt = 31 };
            pv.pointerValue = Marshal.StringToCoTaskMemUni(value);
            return pv;
        }

        public void Dispose() { PropVariantClear(ref this); }

        [DllImport("ole32.dll")]
        private static extern int PropVariantClear(ref PropVariant pvar);
    }
}

public static class StatusConverter {
    public static MediaPlaybackStatus Convert(global::Windows.Media.MediaPlaybackStatus status) {
        return status switch {
            global::Windows.Media.MediaPlaybackStatus.Changing => MediaPlaybackStatus.Changing,
            global::Windows.Media.MediaPlaybackStatus.Playing  => MediaPlaybackStatus.Playing,
            global::Windows.Media.MediaPlaybackStatus.Paused   => MediaPlaybackStatus.Paused,
            global::Windows.Media.MediaPlaybackStatus.Stopped or global::Windows.Media.MediaPlaybackStatus.Closed =>
                MediaPlaybackStatus.Stopped,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }

    public static global::Windows.Media.MediaPlaybackStatus Convert(MediaPlaybackStatus status) {
        return status switch {
            MediaPlaybackStatus.Changing => global::Windows.Media.MediaPlaybackStatus.Changing,
            MediaPlaybackStatus.Playing  => global::Windows.Media.MediaPlaybackStatus.Playing,
            MediaPlaybackStatus.Paused   => global::Windows.Media.MediaPlaybackStatus.Paused,
            MediaPlaybackStatus.Stopped  => global::Windows.Media.MediaPlaybackStatus.Stopped,
            _                            => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }
}

#endif