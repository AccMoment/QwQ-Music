#if _LINUX

using Tmds.DBus;

namespace SystemSleepInhibitor.SystemSleep;

/// <summary>
/// 通过 xdg-desktop-portal 的 Inhibit 接口阻止 Linux 系统休眠、屏保或锁屏。
/// 使用现代 org.freedesktop.portal.Inhibit 接口（推荐替代旧的 PowerManagement 接口）。
/// </summary>
public sealed class LinuxSleepHelperImpl : ISystemSleepHelperImpl {
    public async Task InhibitAsync(bool keepDisplay, string reason) {
        await RestoreAsync().ConfigureAwait(false);
        InhibitFlags flags = keepDisplay ?
            InhibitFlags.InhibitIdle | InhibitFlags.InhibitScreensaver :
            InhibitFlags.InhibitIdle;
        _sessionHandle = await InhibitSleepAsync("", (uint)flags, reason).ConfigureAwait(false);
    }

    public async Task RestoreAsync() {
        if (_sessionHandle is null)
            return;
        await _sessionHandle.DisposeAsync().ConfigureAwait(false);
    }


    // 释放标志
    // 保存从 portal 获得的会话句柄，用于释放抑制锁
    private IAsyncDisposable? _sessionHandle;

    /// <summary>
    /// 通过 D-Bus 调用 org.freedesktop.portal.Inhibit.Inhibit 方法。
    /// </summary>
    /// <returns>返回一个代表会话句柄的 IDisposable 对象，Dispose 时会自动释放抑制锁</returns>
    private async Task<IAsyncDisposable> InhibitSleepAsync(string parentWindow, uint flags, string reason) {
        // 建立 Session Bus 连接（用户会话总线）
        Connection connection = Connection.Session;

        // 创建代理对象，指向 xdg-desktop-portal 提供的 Inhibit 接口
        // 服务名: org.freedesktop.portal.Desktop
        // 对象路径: /org/freedesktop/portal/desktop
        var proxy = connection.CreateProxy<IInhibitPortal>(
            "org.freedesktop.portal.Desktop",
            "/org/freedesktop/portal/desktop");


        // 调用 D-Bus 方法，返回一个 ObjectPath，指向一个 Session 对象
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        ObjectPath sessionPath = await proxy.InhibitAsync(parentWindow, flags, reason, null).ConfigureAwait(false);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.

        // 返回一个包装了该会话路径的句柄，其 Dispose 方法会调用 Session.Close()
        return new PortalSessionHandle(connection, sessionPath);
    }

    /// <summary>
    /// 释放抑制锁，允许系统恢复正常的电源管理和屏保行为。
    /// </summary>
    public async ValueTask DisposeAsync() {
        if (_sessionHandle is null)
            return;
        await _sessionHandle.DisposeAsync().ConfigureAwait(false);
        _sessionHandle = null;
    }

    /// <summary>
    /// 内部包装类：表示一个 portal 会话，在释放时调用 Close 方法。
    /// </summary>
    private readonly record struct PortalSessionHandle(Connection Connection, ObjectPath SessionPath)
        : IAsyncDisposable {
        public async ValueTask DisposeAsync() {
            // 创建指向该会话对象的代理，并调用 Close 方法
            var sessionProxy = Connection.CreateProxy<ISessionPortal>("org.freedesktop.portal.Desktop", SessionPath);
            // 同步等待关闭完成（可改用 await 但 Dispose 不应异步）
            await sessionProxy.CloseAsync().ConfigureAwait(false);
        }
    }


    /// <summary>
    /// org.freedesktop.portal.Inhibit 接口
    /// </summary>
    [DBusInterface("org.freedesktop.portal.Inhibit")]
    private interface IInhibitPortal : IDBusObject {
        /// <summary>
        /// 发送抑制请求。
        /// </summary>
        /// <param name="parent_window">父窗口标识符（可为空字符串）</param>
        /// <param name="flags">抑制标志位掩码</param>
        /// <param name="reason">可读原因字符串</param>
        /// <param name="options">额外选项（通常为空 VariantDict）</param>
        /// <returns>返回一个会话对象路径，需要调用其 Close 方法释放抑制</returns>
        // ReSharper disable once InconsistentNaming
        Task<ObjectPath> InhibitAsync(string parent_window, uint flags, string reason, object options);
    }

    /// <summary>
    /// org.freedesktop.portal.Session 接口
    /// 每个 inhibit 请求会返回一个实现了该接口的对象，调用 Close 即可释放抑制。
    /// </summary>
    [DBusInterface("org.freedesktop.portal.Session")]
    private interface ISessionPortal : IDBusObject {
        /// <summary>关闭会话，撤销抑制效果</summary>
        Task CloseAsync();
    }

    /// <summary>
    /// 定义 Inhibit 接口中 flags 参数的常用值。
    /// 这些值来自 xdg-desktop-portal 规范。
    /// </summary>
    [Flags]
    private enum InhibitFlags : uint {
        /// <summary>阻止系统空闲后进入休眠或自动关闭屏幕（阻止 idle）</summary>
        InhibitIdle = 1,

        /// <summary>阻止屏幕保护程序启动（包括锁屏）</summary>
        InhibitScreensaver = 4
    }
}

#endif