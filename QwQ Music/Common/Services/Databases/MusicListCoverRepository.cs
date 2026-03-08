using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Microsoft.Data.Sqlite;
using QwQ_Music.Common.Helpers;
using QwQ_Music.Common.Interfaces;
using QwQ_Music.Models;
using QwQ_Music.Models.ConfigModels;

namespace QwQ_Music.Common.Services.Databases;

public class
    MusicListCoverRepository : IAsyncDatabaseRepository<(string Name, string Creator), MusicListModel, Bitmap?> {
    public static readonly MusicListCoverRepository Instance = new(StaticConfig.CachePath);

    public const string TABLE_NAME = "music_list_cover";

    private readonly AsyncDatabaseService _db;

    private MusicListCoverRepository(string path) {
        _db = new AsyncDatabaseService(path);
        InitializeAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        _ = MusicListThumbnailRepository.Instance; // Pre-initialize Database.
    }

    private async Task InitializeAsync() {
        await LoggerService.DebugAsync("正在初始化歌单封面数据库").ConfigureAwait(false);
        await _db.CreateTableAsync(
                     TABLE_NAME,
                     $"""
                        {nameof(MusicListModel.Name)} TEXT,
                        {nameof(MusicListModel.Creator)} TEXT, 
                        {nameof(MusicListModel.Cover)} BLOB,
                        PRIMARY KEY({nameof(MusicListModel.Name)},{nameof(MusicListModel.Creator)}),
                        FOREIGN KEY({nameof(MusicListModel.Name)},{nameof(MusicListModel.Creator)}) REFERENCES {
                            MusicListRepository.TABLE_NAME}({nameof(MusicListModel.Name)},{
                                nameof(MusicListModel.Creator)
                            }) ON UPDATE CASCADE ON DELETE CASCADE
                      """)
                 .ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync() {
        await _db.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    public async Task<Bitmap?> SingleAsync((string Name, string Creator) key) {
        await LoggerService.DebugAsync($"正在获取歌单'{key.Name} - {key.Creator}'的封面").ConfigureAwait(false);
        var result = await _db.SingleAsync(
                                  $"SELECT * FROM {TABLE_NAME} WHERE {nameof(MusicListModel.Name)} = @{
                                      nameof(MusicListModel.Name)} AND {
                                          nameof(MusicListModel.Creator)} = @{nameof(MusicListModel.Creator)}",
                                  new Dictionary<string, object> {
                                      [nameof(MusicListModel.Name)] = key.Name,
                                      [nameof(MusicListModel.Creator)] = key.Creator
                                  })
                              .ConfigureAwait(false);
        return result?[nameof(MusicListModel.Cover)] as Bitmap;
    }

    public async Task<IEnumerable<Bitmap?>> GetAsync() {
        await LoggerService.DebugAsync("正在获取所有歌单封面。警告：可能发生长时磁盘IO，应尽可能避免该操作").ConfigureAwait(false);
        var results = await _db.QueryAsync($"SELECT * FROM {TABLE_NAME}").ConfigureAwait(false);
        return results.Select(item => ParseHelper.ParseToBitmap(item[nameof(MusicListModel.Cover)]));
    }

    public async Task<int> CountAsync() {
        await LoggerService.DebugAsync("正在获取歌单封面数量").ConfigureAwait(false);
        return await _db.CountAsync(TABLE_NAME).ConfigureAwait(false);
    }

    public async Task InsertAsync(MusicListModel item, InsertExist onInsertExist = InsertExist.REPLACE) {
        await LoggerService.DebugAsync($"正在添加歌单'{item.Name} - {item.Creator}'的封面，模式{onInsertExist}")
                           .ConfigureAwait(false);

        var data = ToDictionary(item);
        var thumbnail = data[nameof(MusicListModel.Thumbnail)];
        data.Remove(nameof(MusicListModel.Thumbnail));
        var command = _db.InsertNonExecute(TABLE_NAME, data, onInsertExist);
        data.Remove(nameof(MusicListModel.Cover));
        data[nameof(MusicListModel.Thumbnail)] = thumbnail;
        _db.InsertNonExecute(ref command, MusicListThumbnailRepository.TABLE_NAME, data, onInsertExist);
        await AsyncDatabaseService.ExecuteAsync(command).ConfigureAwait(false);
    }

    public async Task UpdateAsync(MusicListModel item) {
        await UpdateAsync((item.Name, item.Creator), ToRawDictionary(item)).ConfigureAwait(false);
    }

    public async Task UpdateAsync((string Name, string Creator) key, Dictionary<string, object?> fieldValues) {
        if (string.IsNullOrWhiteSpace(key.Creator))
            throw new InvalidOperationException("There must have at least one creator for a music list.");
        await LoggerService.DebugAsync($"正在更新歌单'{key.Name} - {key.Creator}'的如下字段：{string.Join(',', fieldValues.Keys)}")
                           .ConfigureAwait(false);

        var whereClause = $"{nameof(MusicListModel.Name)} = @{nameof(MusicListModel.Name)} AND {
            nameof(MusicListModel.Creator)} = @{nameof(MusicListModel.Creator)}";
        var whereParams = new Dictionary<string, object?> {
            [nameof(MusicListModel.Name)] = key.Name, [nameof(MusicListModel.Creator)] = key.Creator
        };
        if (!fieldValues.Remove(nameof(MusicListModel.Thumbnail), out var thumbnail)) {
            await _db.UpdateAsync(TABLE_NAME, fieldValues, whereClause, whereParams).ConfigureAwait(false);
            return;
        }

        SqliteCommand command = _db.UpdateNonExecute(TABLE_NAME, fieldValues, whereClause, whereParams);
        fieldValues.Remove(nameof(MusicListModel.Cover));
        fieldValues[nameof(MusicListModel.Thumbnail)] = thumbnail;
        _db.UpdateNonExecute(
            ref command,
            MusicListThumbnailRepository.TABLE_NAME,
            fieldValues,
            whereClause,
            whereParams);
        await AsyncDatabaseService.ExecuteAsync(command).ConfigureAwait(false);
    }

    public async Task DeleteAsync((string Name, string Creator) key) {
        await LoggerService.DebugAsync($"正在删除歌单'{key.Name} - {key.Creator}'的封面").ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(key.Creator))
            throw new InvalidOperationException("There must have at least one creator for a music list.");

        await _db.DeleteAsync(
                     TABLE_NAME,
                     $"{nameof(MusicListModel.Name)} = @{nameof(MusicListModel.Name)} AND {
                         nameof(MusicListModel.Creator)} = @{
                             nameof(MusicListModel.Creator)}",
                     new Dictionary<string, object> {
                         [nameof(MusicListModel.Name)] = key.Name, [nameof(MusicListModel.Creator)] = key.Creator
                     })
                 .ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync((string Name, string Creator) key) {
        await LoggerService.DebugAsync($"正在检测歌单'{key.Name} - {key.Creator}'是否存在封面").ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(key.Creator))
            throw new InvalidOperationException("There must have at least one creator for a music list.");
        var result = await _db.SingleAsync(
                                  $"SELECT 1 FROM {TABLE_NAME} WHERE {nameof(MusicListModel.Name)} = @{
                                      nameof(MusicListModel.Name)} AND {
                                          nameof(MusicListModel.Creator)} = @{nameof(MusicListModel.Creator)}",
                                  new Dictionary<string, object> {
                                      [nameof(MusicListModel.Name)] = key.Name,
                                      [nameof(MusicListModel.Creator)] = key.Creator
                                  })
                              .ConfigureAwait(false);
        return result is not null;
    }

    private static Dictionary<string, object?> ToDictionary(MusicListModel model) {
        return new Dictionary<string, object?> {
            [nameof(MusicListModel.Name)] = model.Name,
            [nameof(MusicListModel.Creator)] = model.Creator,
            [nameof(MusicListModel.Cover)] = DumpHelper.BitmapToBytes(model.Cover),
            [nameof(MusicListModel.Thumbnail)] = DumpHelper.BitmapToBytes(model.Thumbnail)
        };
    }

    private static Dictionary<string, object?> ToRawDictionary(MusicListModel model) {
        return new Dictionary<string, object?> {
            [nameof(MusicListModel.Name)] = model.Name,
            [nameof(MusicListModel.Creator)] = model.Creator,
            [nameof(MusicListModel.Cover)] = model.Cover
        };
    }
}

public class MusicListThumbnailRepository : IAsyncReadonlyDatabaseRepository<(string Name, string Creator), Bitmap?> {
    public static readonly MusicListThumbnailRepository Instance;

    static MusicListThumbnailRepository() { Instance = new MusicListThumbnailRepository(StaticConfig.CachePath); }

    public const string TABLE_NAME = "music_list_thumbnail";

    private readonly AsyncDatabaseService _db;

    private MusicListThumbnailRepository(string path) {
        _db = new AsyncDatabaseService(path);
        InitializeAsync().ConfigureAwait(false).GetAwaiter().GetResult();
    }

    private async Task InitializeAsync() {
        await LoggerService.DebugAsync($"正在初始化歌单缩略图").ConfigureAwait(false);

        await _db.CreateTableAsync(
                     TABLE_NAME,
                     $"""
                        {nameof(MusicListModel.Name)} string,
                        {nameof(MusicListModel.Creator)} string, 
                        {nameof(MusicListModel.Thumbnail)} BLOB,
                        PRIMARY KEY({nameof(MusicListModel.Name)},{nameof(MusicListModel.Creator)}),
                        FOREIGN KEY({nameof(MusicListModel.Name)},{nameof(MusicListModel.Creator)}) REFERENCES {
                            MusicListCoverRepository.TABLE_NAME}({nameof(MusicListModel.Name)},{
                                nameof(MusicListModel.Creator)
                            }) ON UPDATE CASCADE ON DELETE CASCADE
                      """)
                 .ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync() {
        await _db.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    public async Task<Bitmap?> SingleAsync((string Name, string Creator) key) {
        await LoggerService.DebugAsync($"正在获取歌单'{key.Name} - {key.Creator}'的缩略图").ConfigureAwait(false);

        var result = await _db.SingleAsync(
                                  $"SELECT * FROM {TABLE_NAME} WHERE {nameof(MusicListModel.Name)} = @{
                                      nameof(MusicListModel.Name)} AND {
                                          nameof(MusicListModel.Creator)} = @{nameof(MusicListModel.Creator)}",
                                  new Dictionary<string, object> {
                                      [nameof(MusicListModel.Name)] = key.Name,
                                      [nameof(MusicListModel.Creator)] = key.Creator
                                  })
                              .ConfigureAwait(false);
        return ParseHelper.ParseToBitmap(result?[nameof(MusicListModel.Thumbnail)]);
    }

    public async Task<IEnumerable<Bitmap?>> GetAsync() {
        await LoggerService.DebugAsync($"正在获取所有歌单缩略图。警告：可能发生长时磁盘IO，应尽可能避免该操作").ConfigureAwait(false);

        var results = await _db.QueryAsync($"SELECT * FROM {TABLE_NAME}").ConfigureAwait(false);
        return results.Select(item => ParseHelper.ParseToBitmap(item[nameof(MusicListModel.Thumbnail)]));
    }

    public async Task<int> CountAsync() {
        await LoggerService.DebugAsync("正在获取歌单缩略图数量").ConfigureAwait(false);
        return await _db.CountAsync(TABLE_NAME).ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync((string Name, string Creator) key) {
        if (string.IsNullOrWhiteSpace(key.Creator))
            throw new InvalidOperationException("There must have at least one creator for a music list.");

        await LoggerService.DebugAsync($"正在检测歌单'{key.Name} - {key.Creator}'是否存在缩略图").ConfigureAwait(false);
        var result = await _db.SingleAsync(
                                  $"SELECT 1 FROM {TABLE_NAME} WHERE {nameof(MusicListModel.Name)} = @{
                                      nameof(MusicListModel.Name)} AND {
                                          nameof(MusicListModel.Creator)} = @{nameof(MusicListModel.Creator)}",
                                  new Dictionary<string, object> {
                                      [nameof(MusicListModel.Name)] = key.Name,
                                      [nameof(MusicListModel.Creator)] = key.Creator
                                  })
                              .ConfigureAwait(false);
        return result is not null;
    }
}