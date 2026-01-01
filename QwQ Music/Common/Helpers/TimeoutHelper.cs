using System;
using System.Threading.Tasks;
using System.Timers;

namespace QwQ_Music.Common.Helpers;

public static class TimeoutHelper {
    public static void Timeout(int timeoutMillisecond, Action action, Action callback) {
        Timer timer = new() { Interval = timeoutMillisecond, AutoReset = false };
        timer.Elapsed += (_, _) => {
            timer.Dispose();
            callback();
            throw new TimeoutException();
        };
        timer.Start();
        action();
    }
}