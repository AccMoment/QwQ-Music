using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using QwQ_Music.Models;
using QwQ_Music.Models.ConfigModels;

namespace QwQ_Music.Common.Services.Databases;

public class MusicListItemsRepository : IDisposable {
    public static readonly MusicListItemsRepository Instance = new(StaticConfig.DatabasePath);

    public const string NextPath = nameof(NextPath);
    public const string AddTime = nameof(AddTime);

    public const string TABLE_NAME = "playlist_items";
    private readonly DatabaseService _db;

    public MusicListItemsRepository(string dbPath) {
        _db = new DatabaseService(dbPath);
        Initialize();
    }

    private void Initialize() {
        // 创建表（如果不存在）
        _db.CreateTable(
            TABLE_NAME,
            $"""
             {nameof(MusicListModel.Name)} TEXT,
             {nameof(MusicItemModel.FilePath)} TEXT,
             {nameof(AddTime)} INTEGER,
             {nameof(NextPath)} TEXT,
             PRIMARY KEY ({nameof(MusicListModel.Name)}, {nameof(MusicItemModel.FilePath)}),
             FOREIGN KEY ({nameof(MusicListModel.Name)}) REFERENCES {MusicListRepository.TABLE_NAME}({
                 nameof(MusicListModel.Name)}) ON DELETE CASCADE,
             FOREIGN KEY ({nameof(MusicItemModel.FilePath)}) REFERENCES {MusicItemRepository.TABLE_NAME}({
                 nameof(MusicItemModel.FilePath)})
             """);
    }

    public void Dispose() {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private string GetFirst(string musicListName) {
        return (_db.Query(
                $"SELECT {NextPath} from {TABLE_NAME} " +
                $"WHERE {nameof(MusicItemModel.FilePath)} = @{nameof(MusicListModel.Name)}",
                new Dictionary<string, object> {
                    [nameof(MusicListModel.Name)] = $"QWQ_PLAYLIST_{musicListName}_HEAD"
                })[0]
            ["NextPath"] as string)!;
    }

    /// <summary>
    ///     添加歌曲到歌单
    /// </summary>
    public void InsertRange(MusicListModel musicList, IEnumerable<MusicItemModel> items) {
        string musicListName = musicList.Name;
        //  在此处将原本的第一首加到列表末尾，以便创建 data时直接 Select更新 items的尾项。
        string[] itemsArray = items.Select(item => item.FilePath).Append(GetFirst(musicListName)).ToArray();
        long time = DateTime.Now.Ticks;
        //  更新 HEAD，将首项更换为 items的首项。
        SqliteCommand cmd = _db.UpdateNonExecute(
            TABLE_NAME,
            new Dictionary<string, object?> { [NextPath] = itemsArray.First() },
            $"{nameof(MusicListModel.Name)} = @{nameof(MusicListModel.Name)}",
            new Dictionary<string, object?> { [nameof(MusicListModel.Name)] = musicList.Name });
        var data = itemsArray.Take(..^1)
                             .Select((item, index) => new Dictionary<string, object?> {
                                 [nameof(MusicListModel.Name)] = musicListName,
                                 [nameof(MusicItemModel.FilePath)] = item,
                                 [AddTime] = time,
                                 [NextPath] = itemsArray[index + 1]
                             });

        _db.InsertMultipleNonExecute(ref cmd, TABLE_NAME, data);
        _db.Execute(cmd);
    }

    /// <summary>
    ///     添加歌曲到歌单
    /// </summary>
    public void Insert(MusicListModel musicList, MusicItemModel item) { InsertRange(musicList, [item]); }

    /// <summary>
    ///     从歌单中删除指定歌曲
    /// </summary>
    public void RemoveRange(MusicListModel musicList, IEnumerable<MusicItemModel> items) {
        SqliteCommand cmd = _db.NewCommand();
        items.AsParallel()
             .ForAll(item => {
                 _db.UpdateNonExecute(
                     ref cmd,
                     TABLE_NAME,
                     new Dictionary<string, object?> { [NextPath] = null },
                     $"{nameof(MusicListModel.Name)} = @{nameof(MusicListModel.Name)} AND {NextPath} = @{NextPath}",
                     new Dictionary<string, object?> {
                         [nameof(MusicListModel.Name)] = musicList.Name, [NextPath] = item.FilePath
                     });
                 _db.DeleteNonExecute(
                     ref cmd,
                     TABLE_NAME,
                     $"{nameof(MusicListModel.Name)} = @{nameof(MusicListModel.Name)} AND {
                         nameof(MusicItemModel.FilePath)
                     } = @{nameof(MusicItemModel.FilePath)}",
                     new Dictionary<string, object> {
                         [nameof(MusicListModel.Name)] = musicList.Name,
                         [nameof(MusicItemModel.FilePath)] = item.FilePath
                     });
             });
        _db.Execute(cmd);
    }

    public void Remove(MusicListModel musicList, MusicItemModel item) { RemoveRange(musicList, [item]); }

    /// <summary>
    ///     清空整个歌单
    /// </summary>
    public void Clear(string playlistName) {
        var whereParams = new Dictionary<string, object> { [nameof(MusicListModel.Name)] = playlistName };

        _db.Delete(TABLE_NAME, $"{nameof(MusicListModel.Name)} = @{nameof(MusicListModel.Name)}", whereParams);
        CreateHead(playlistName);
    }

    private void CreateHead(string playlistName) {
        DateTime now = DateTime.Now;
        _db.Insert(
            TABLE_NAME,
            new Dictionary<string, object?> {
                [nameof(MusicListModel.Name)] = playlistName,
                [nameof(MusicItemModel.FilePath)] = $"QWQ_PLAYLIST_{playlistName}_HEAD",
                [NextPath] = null,
                [AddTime] = now
            });
    }

    /// <summary>
    ///     获取歌单中所有歌曲
    /// </summary>
    /// <returns>文件路径列表</returns>
    public IEnumerable<string> GetAll(string playlistName) {
        const string sql = $"SELECT {nameof(MusicItemModel.FilePath)} FROM {TABLE_NAME} " +
                           $"WHERE {nameof(MusicListModel.Name)} = @{nameof(MusicListModel.Name)} ";

        var parameters = new Dictionary<string, object> { [nameof(MusicListModel.Name)] = playlistName };

        FrozenDictionary<string, string?> result = _db.Query(sql, parameters)
                                                      .ToFrozenDictionary(
                                                          item => (string)item[nameof(MusicItemModel.FilePath)]!,
                                                          item => (string?)item[nameof(NextPath)]);

        return Order(result);

        IEnumerable<string> Order(IDictionary<string, string?> raw) {
            string? current = raw[$"QWQ_PLAYLIST_{playlistName}_HEAD"];
            while (current != null) {
                yield return current;
                current = raw[current];
            }
        }
    }

    /// <summary>
    ///     检查某首歌是否在歌单中
    /// </summary>
    public bool Contains(string playlistName, string filePath) {
        const string sql = $"SELECT 1 FROM {TABLE_NAME} " +
                           $"WHERE {nameof(MusicListModel.Name)} = @{nameof(MusicListModel.Name)} " +
                           $"AND {nameof(MusicItemModel.FilePath)} = @{nameof(MusicItemModel.FilePath)} " +
                           $"LIMIT 1";

        var parameters = new Dictionary<string, object> {
            [nameof(MusicItemModel.FilePath)] = filePath, [nameof(MusicListModel.Name)] = playlistName
        };

        return _db.Query(sql, parameters).Count != 0;
    }
}