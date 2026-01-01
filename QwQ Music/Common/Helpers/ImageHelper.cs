using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using QwQ_Music.Common.Services;
using QwQ_Music.Common.Utilities;

namespace QwQ_Music.Common.Helpers;

public static class ImageHelper {
    /// <summary>
    ///     从Avalonia资源加载图片
    /// </summary>
    /// <param name="resourceUri"></param>
    /// <returns></returns>
    public static Bitmap LoadFromResource(Uri resourceUri) { return new Bitmap(AssetLoader.Open(resourceUri)); }

    /// <summary>
    ///     从web加载图片
    /// </summary>
    /// <param name="url">图片直连</param>
    /// <returns></returns>
    public static async Task<Bitmap?> LoadFromWeb(Uri url) {
        using var httpClient = new HttpClient();

        try {
            var response = await httpClient.GetAsync(url).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            byte[] data = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

            return new Bitmap(new MemoryStream(data));
        } catch (HttpRequestException ex) {
            await LoggerService.ErrorAsync($"An error occurred while downloading image '{url}' : {ex.Message}")
                               .ConfigureAwait(false);

            return null;
        }
    }

    /// <summary>
    ///     从网络加载图片并压缩到指定大小以内
    /// </summary>
    /// <param name="url">图片URL</param>
    /// <param name="maxSizeInBytes">最大文件大小（字节）</param>
    /// <returns>压缩后的位图</returns>
    public static async Task<Bitmap?> LoadFromWebAndCompress(Uri url, long maxSizeInBytes) {
        using var httpClient = new HttpClient();

        try {
            var response = await httpClient.GetAsync(url).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            byte[] data = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

            // 压缩图片
            var originalBitmap = new Bitmap(new MemoryStream(data));

            return await BitmapCompression.CompressBitmapAsync(originalBitmap, maxSizeInBytes).ConfigureAwait(false);
        } catch (HttpRequestException ex) {
            await LoggerService.ErrorAsync($"An error occurred while downloading image '{url}' : {ex.Message}")
                               .ConfigureAwait(false);

            return null;
        }
    }

    /// <summary>
    ///     从网络加载图片并压缩到指定大小以内
    /// </summary>
    /// <param name="url">图片URL</param>
    /// <param name="width">最大尺寸（<see cref="width" /> * <see cref="width" />）</param>
    /// <returns>压缩后的位图</returns>
    public static async Task<Bitmap?> LoadFromWebAndDecodeToWidthAsync(Uri url, int width = 128) {
        using var httpClient = new HttpClient();

        try {
            var response = await httpClient.GetAsync(url).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            byte[] data = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

            // 压缩图片
            return await Task.Run(() => Bitmap.DecodeToWidth(new MemoryStream(data), width)).ConfigureAwait(false);
        } catch (HttpRequestException ex) {
            await LoggerService.ErrorAsync($"An error occurred while downloading image '{url}' : {ex.Message}")
                               .ConfigureAwait(false);

            return null;
        }
    }

    /// <summary>
    ///     从文件系统中加载位图。
    /// </summary>
    /// <param name="coverPath">图片路径。</param>
    /// <param name="size">缩放后宽度。如果设置为-1则不缩放</param>
    /// <returns>所需的位图。</returns>
    public static async Task<Bitmap?> LoadFromFileAsync(string coverPath, int size = -1) {
        // 获取文件流

        if (!File.Exists(coverPath)) {
            await LoggerService.WarningAsync($"无法打开{coverPath}: 文件不存在").ConfigureAwait(false);
            return null;
        }

        try {
            await using var fs = new FileStream(coverPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (size == -1) {
                await LoggerService.InfoAsync($"加载了{coverPath}的原始图像。").ConfigureAwait(false);
                return new Bitmap(fs);
            }

            await LoggerService.InfoAsync($"加载{coverPath}并缩放到{size}px宽。").ConfigureAwait(false);
            return Bitmap.DecodeToWidth(fs, size);
        } catch (Exception ex) {
            await LoggerService.ErrorAsync($"加载图片错误({ex.GetType()}): {coverPath}\n" + $"{ex.Message}\n{ex.StackTrace}")
                               .ConfigureAwait(false);
            return null;
        }
    }
}