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


    /// <summary>
    /// 更新歌单区域
    /// </summary>
    /// <param name="playlistName">歌单名</param>
    /// <param name="playlist">歌曲列表</param>
    /// <param name="range">更新区域</param>
    public void UpdateRange(string playlistName, IList<MusicItemModel> playlist, Range range) {
        _db.BeginTransaction();
        SqliteCommand command = _db.NewCommand();
        long now = DateTime.Now.Ticks;

        int max = playlist.Count;
        // ReSharper disable JoinDeclarationAndInitializer

        // ReSharper restore JoinDeclarationAndInitializer
        var (start, len) = range.GetOffsetAndLength(max);
        var end = start + len;
        try {
            var data = new Dictionary<string, object?>[len];

            #region FirstItem

            string? prevPath = start > 0 ? playlist[start - 1].FilePath : null;
            string path = playlist[start].FilePath;
            string? nextPath = start + 1 < max - 1 ? playlist[start + 1].FilePath : null;
            data[0] = new Dictionary<string, object?> {
                [nameof(MusicListModel.Name)] = playlistName,
                [nameof(MusicItemModel.FilePath)] = path,
                [NextPath] = nextPath,
                [AddTime] = now
            };
            if (prevPath is not null) {
                _db.UpdateNonExecute(
                    ref command,
                    TABLE_NAME,
                    new Dictionary<string, object?> { [NextPath] = path },
                    $"{nameof(MusicItemModel.FilePath)} = @{nameof(MusicItemModel.FilePath)}",
                    new Dictionary<string, object?> { [nameof(MusicItemModel.FilePath)] = prevPath });
            }

            #endregion FirstItem

            if (len == 1) {
                _db.InsertNonExecute(ref command, TABLE_NAME, data[0]);
                _db.Execute(command);
                _db.Commit();
                return;
            }


            #region LastItem

            path = playlist[end].FilePath;
            nextPath = end < max - 2 ? playlist[end + 1].FilePath : null;
            data[len - 1] = new Dictionary<string, object?> {
                [nameof(MusicListModel.Name)] = playlistName,
                [nameof(MusicItemModel.FilePath)] = path,
                [NextPath] = nextPath,
                [AddTime] = now + len - 1
            };

            #endregion LastItem

            #region OtherItems

            nextPath = playlist[start + 1].FilePath;
            for (int curr = 1; curr < len; curr++) {
                path = nextPath;
                nextPath = playlist[start + curr + 1].FilePath;
                data[curr] = new Dictionary<string, object?> {
                    [nameof(MusicListModel.Name)] = playlistName,
                    [nameof(MusicItemModel.FilePath)] = path,
                    [NextPath] = nextPath,
                    [AddTime] = now + curr
                };
            }

            #endregion OtherItems

            _db.InsertMultipleNonExecute(ref command, TABLE_NAME, data);
            _db.Execute(command);
            _db.Commit();
        } catch {
            _db.Rollback();

            throw;
        }
    }

    /// <summary>
    /// 更新歌单区域
    /// </summary>
    /// <param name="musicList">歌单</param>
    /// <param name="range">更新区域</param>
    public void UpdateRange(MusicListModel musicList, Range range) {
        UpdateRange(musicList.Name, musicList.Musics!, range);
    }


    /// <summary>
    ///     添加歌曲到歌单
    /// </summary>
    public void Insert(MusicListModel musicList, IList<MusicItemModel> items) {
        int index = musicList.Musics!.IndexOf(items.First());
        UpdateRange(musicList.Name, musicList.Musics!, index..(index + items.Count));
    }
    /// <summary>
    ///     添加歌曲到歌单
    /// </summary>
    public void Insert(MusicListModel musicList, MusicItemModel item) {
        Insert(musicList, [item]);
    }

    /// <summary>
    ///     从歌单中删除指定歌曲
    /// </summary>
    public void Remove(MusicListModel musicList, MusicItemModel item) {
        var whereParams = new Dictionary<string, object> {
            [nameof(MusicListModel.Name)] = musicList.Name, [nameof(MusicItemModel.FilePath)] = item.FilePath
        };

        _db.Delete(
            TABLE_NAME,
            $"{nameof(MusicListModel.Name)} = @{nameof(MusicListModel.Name)} AND {nameof(MusicItemModel.FilePath)} = @{
                nameof(MusicItemModel.FilePath)}",
            whereParams);
    }

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