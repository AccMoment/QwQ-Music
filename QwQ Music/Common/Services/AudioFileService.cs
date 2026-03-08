using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using QwQ_Music.Models;
using MusicItemsManager = QwQ_Music.Common.Managers.MusicItemsManager;

namespace QwQ_Music.Common.Services;

public static class AudioFileService {
    /// <summary>
    ///     处理存储项目并导入音乐文件
    /// </summary>
    public static async Task ProcessStorageItemsAsync(IReadOnlyList<IStorageItem> items) {
        var paths = FileOperationService.ConvertStorageItemsToPathStrings(items);

        if (paths.Count == 0) {
            NotificationService.Info("提示", "获取的文件数量为 0 ！");

            return;
        }

        NotificationService.Info("提示", "开始导入中，请稍等....！");

        var allFilePaths = FileOperationService.GetAllFilePaths(paths);
        var musicItems = ImportMusicFilesAsync(allFilePaths);

        await MusicItemsManager.All.AddAsync(musicItems).ConfigureAwait(false);
    }

    /// <summary>
    ///     导入音乐文件
    /// </summary>
    /// <param name="filePaths">要导入的文件路径列表</param>
    /// <returns>导入的音乐文件信息</returns>
    private static async IAsyncEnumerable<MusicItemModel> ImportMusicFilesAsync(IReadOnlyList<string> filePaths) {
        // 过滤出音频文件
        string[] audioFilePaths = AudioFileValidator.FilterAudioFiles(filePaths).ToArray();

        if (audioFilePaths.Length == 0) {
            NotificationService.Info("提示", "没有找到可导入的音频文件！");

            yield break;
        }

        // 过滤掉已存在的路径
        var existingFilePaths =
            audioFilePaths.Where(path => MusicItemsManager.All.MusicItems.ContainsKey(path)).ToList();
        var newFilePaths = audioFilePaths.Except(existingFilePaths);

        // 如果有已存在的文件，显示提示
        if (existingFilePaths.Count > 0) {
            string existingTitles = string.Join(
                "、",
                existingFilePaths.Select(path => $"《{Path.GetFileNameWithoutExtension(path)}》"));

            NotificationService.Info($"歌曲{existingTitles}已存在于音乐库中！");
        }

        foreach (MusicItemModel model in newFilePaths.Select(path => new MusicItemModel { FilePath = path })) {
            await model.UpdateMetaDataAsync().ConfigureAwait(false);
            yield return model;
        }
    }
}