using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;
using QwQ_Music.Common.Utilities;
using QwQ_Music.Models.Enums;

namespace QwQ_Music.Common.Manager;

public static class CacheManager
{
    public static Bitmap NotExist { get; } = GetBuiltInImage("没有图片哦.webp");

    public static Bitmap Loading { get; } = GetBuiltInImage("图片绘制中.webp");

    public static Bitmap Damaged { get; } = GetBuiltInImage("图片压坏了.webp");

    public static Bitmap Default { get; } = GetBuiltInImage("看我.webp");

    public static Dictionary<AudioQualityLevel, Bitmap> AudioQualityLevelLogo = new()
    {
        [AudioQualityLevel.PQ] = GetBuiltInImage("PQ.png"),
        [AudioQualityLevel.HQ] = GetBuiltInImage("HQ.png"),
        [AudioQualityLevel.SQ] = GetBuiltInImage("SQ.png"),
        [AudioQualityLevel.HR] = GetBuiltInImage("HR.png"),
    };

    public static WeakCache<string, Bitmap> ImageCache { get; } = new();

    /// <summary>
    ///     设置或更新图片到缓存
    /// </summary>
    public static void SetImage(string id, Bitmap bitmap)
    {
        ImageCache[id] = bitmap;
    }

    /// <summary>
    ///     通过图片Id删除图片
    /// </summary>
    public static void DeleteImage(string id)
    {
        ImageCache.Remove(id);
    }

    /// <summary>
    ///     通过图片Id集合批量删除图片
    /// </summary>
    public static void DeleteImages(IEnumerable<string> ids)
    {
        foreach (string id in ids)
        {
            ImageCache.Remove(id);
        }
    }

    /// <summary>
    ///     获取内置图片
    /// </summary>
    /// <returns></returns>
    /// <exception cref="FileNotFoundException">无法找到图片资源时抛出异常</exception>
    public static Bitmap GetBuiltInImage(string imageFileName)
    {
        try
        {
            var assembly = App.CurrentAssembly;

            using var stream =
                assembly.GetManifestResourceStream($"QwQ_Music.Assets.EmbeddedRes.Images.{imageFileName}")
             ?? throw new FileNotFoundException($"无法找到 {imageFileName} 资源");

            return new Bitmap(stream);
        }
        catch (Exception)
        {
            // 如果资源加载失败，返回一个空位图
            var bitmap = new RenderTargetBitmap(new PixelSize(100, 100));

            return bitmap;
        }
    }

    /// <summary>
    ///     清理引用
    /// </summary>
    public static void ClearCache()
    {
        Default.Dispose();
        Loading.Dispose();
        NotExist.Dispose();
        Damaged.Dispose();

        foreach (var bitmap in AudioQualityLevelLogo.Values)
        {
            bitmap.Dispose();
        }
    }
}
