using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using QwQ_Music.Common.Helper;
using QwQ_Music.Common.Services;
using QwQ_Music.Common.Utilities;
using QwQ_Music.Models.Enums;

namespace QwQ_Music.Common.Managers;

public static class CacheManager {
    public static readonly Dictionary<AudioQualityLevel, Bitmap> AudioQualityLevelLogo = new() {
        [AudioQualityLevel.PQ] = GetBuiltInImage("PQ.png"),
        [AudioQualityLevel.HQ] = GetBuiltInImage("HQ.png"),
        [AudioQualityLevel.SQ] = GetBuiltInImage("SQ.png"),
        [AudioQualityLevel.HR] = GetBuiltInImage("HR.png")
    };

    public static Bitmap NotExist { get; } = GetBuiltInImage("没有图片哦.webp");

    public static Bitmap Loading { get; } = GetBuiltInImage("图片绘制中.webp");

    public static Bitmap Damaged { get; } = GetBuiltInImage("图片压坏了.webp");

    public static Bitmap Default { get; } = GetBuiltInImage("看我.webp");

    public static WeakCache<string, Bitmap> ImageCache { get; } = new();

    /// <summary>
    ///     设置或更新图片到缓存
    /// </summary>
    public static void SetImage(string id, Bitmap bitmap) { ImageCache[id] = bitmap; }

    /// <summary>
    ///     通过图片Id删除图片
    /// </summary>
    public static void DeleteImage(string id) { ImageCache.Remove(id); }

    /// <summary>
    ///     通过图片Id集合批量删除图片
    /// </summary>
    public static void DeleteImages(IEnumerable<string> ids) {
        foreach (string id in ids) {
            ImageCache.Remove(id);
        }
    }

    /// <summary>
    ///     获取内置图片
    /// </summary>
    /// <returns></returns>
    /// <exception cref="FileNotFoundException">无法找到图片资源时抛出异常</exception>
    public static Bitmap GetBuiltInImage(string imageFileName) {
        try {
            var assembly = App.CurrentAssembly;

            using var stream =
                assembly.GetManifestResourceStream($"QwQ_Music.Assets.EmbeddedRes.Images.{imageFileName}") ??
                throw new FileNotFoundException($"无法找到 {imageFileName} 资源");

            return new Bitmap(stream);
        } catch (Exception) {
            // 如果资源加载失败，返回一个空位图
            var bitmap = new RenderTargetBitmap(new PixelSize(100, 100));

            return bitmap;
        }
    }

    private delegate Task<Bitmap?> LoadCacheFuncType<in T>(T source, int defaultValue = -1);

    private static Bitmap TryLoadCache<T>(
        string? id,
        string idType,
        string cacheType,
        T? source,
        int defaultValue,
        LoadCacheFuncType<T> loader,
        Action? callIfExist = null) {
        if (id is null || source is null) {
            return NotExist;
        }

        // 尝试从缓存获取图片
        if (ImageCache.TryGetValue($"{idType}:{id}", out Bitmap? image) &&
            image != null &&
            image != Default &&
            image != Loading) {
            return image;
        }


        Task.Run(async () => {
            try {
                Bitmap? bitmap = await loader(source, defaultValue).ConfigureAwait(false);

                if (bitmap != null) {
                    ImageCache[$"{idType}:{id}"] = bitmap;
                    await LoggerService.InfoAsync($"加载了{id}的{idType}").ConfigureAwait(false);
                    callIfExist?.Invoke();
                } else {
                    ImageCache[$"{idType}:{id}"] = NotExist;
                    await LoggerService.InfoAsync($"尝试加载{id}的{idType}，但不存在。").ConfigureAwait(false);
                }
            } catch (Exception e) {
                await LoggerService.ErrorAsync($"异步加载{idType}'{id}'的{cacheType}时发生错误: {e}").ConfigureAwait(false);
            }
        });

        return Default;
    }

    public static Bitmap TryLoadCacheFromWeb(
        string? id,
        string idType,
        string cacheType,
        Uri? uri,
        Action? callIfExist = null,
        int defaultValue = 128) {
        return TryLoadCache(
            id,
            idType,
            cacheType,
            uri,
            defaultValue,
            ImageHelper.LoadFromWebAndDecodeToWidthAsync,
            callIfExist);
    }

    public static Bitmap TryLoadCacheFromFile(
        string? id,
        string idType,
        string cacheType,
        string? path,
        Action? callIfExist = null,
        int defaultValue = -1) {
        return TryLoadCache(id, idType, cacheType, path, defaultValue, ImageHelper.LoadFromFileAsync, callIfExist);
    }

    /// <summary>
    ///     清理引用
    /// </summary>
    public static void ClearCache() {
        Default.Dispose();
        Loading.Dispose();
        NotExist.Dispose();
        Damaged.Dispose();

        foreach (var bitmap in AudioQualityLevelLogo.Values) {
            bitmap.Dispose();
        }
    }
}