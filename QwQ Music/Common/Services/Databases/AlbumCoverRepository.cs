using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Microsoft.Data.Sqlite;
using QwQ_Music.Common.Helpers;
using QwQ_Music.Common.Interfaces;
using QwQ_Music.Common.Managers;
using QwQ_Music.Models;
using QwQ_Music.Models.ConfigModels;

namespace QwQ_Music.Common.Services.Databases;

public class AlbumCoverRepository : IAsyncDatabaseRepository<string, MusicItemModel, Bitmap?> {
    public static readonly AlbumCoverRepository Instance = new(StaticConfig.CachePath);

    public const string TABLE_NAME = "album_cover";
    private readonly AsyncDatabaseService _db;

    private AlbumCoverRepository(string dbPath) {
        _db = new AsyncDatabaseService(dbPath);
        InitializeAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        _ = AlbumThumbnailRepository.Instance; // Pre-initialize Database.
    }

    private async Task InitializeAsync() {
        await LoggerService.DebugAsync("正在初始化专辑封面仓库").ConfigureAwait(false);

        await _db.CreateTableAsync(
                     TABLE_NAME,
                     $"""
                      {nameof(AlbumModel.Name)} TEXT PRIMARY KEY,
                      {nameof(AlbumModel.Cover)} BLOB
                      """)
                 .ConfigureAwait(false);
    }

    public async Task RebuildAsync() {
        await LoggerService.DebugAsync("正在重建专辑封面仓库").ConfigureAwait(false);

        await _db.DropTableAsync(TABLE_NAME).ConfigureAwait(false);
        await InitializeAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync() {
        await _db.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    public async Task<Bitmap?> SingleAsync(string id) {
        await LoggerService.DebugAsync($"正在获取专辑'{id}'的封面").ConfigureAwait(false);

        var result = await _db.SingleAsync(
                                  $"SELECT * FROM {TABLE_NAME} WHERE {nameof(AlbumModel.Name)} = @{
                                      nameof(AlbumModel.Name)}",
                                  new Dictionary<string, object> { [nameof(AlbumModel.Name)] = id })
                              .ConfigureAwait(false);
        return ParseHelper.ParseToBitmap(result?[nameof(AlbumModel.Cover)]);
    }

    public async Task<IEnumerable<Bitmap?>> GetAsync() {
        await LoggerService.DebugAsync("正在获取所有专辑封面。警告：可能发生长时磁盘IO，应尽可能避免该操作").ConfigureAwait(false);

        var result = await _db.QueryAsync($"SELECT * FROM {TABLE_NAME}").ConfigureAwait(false);
        return result.Select(item => ParseHelper.ParseToBitmap(item[nameof(AlbumModel.Cover)]) ?? CacheManager.Damaged);
    }

    public async Task<int> CountAsync() { return await _db.CountAsync(TABLE_NAME).ConfigureAwait(false); }

    public async Task InsertAsync(MusicItemModel item, InsertExist onInsertExist = InsertExist.REPLACE) {
        await InsertAsync(item, null, onInsertExist).ConfigureAwait(false);
    }

    public async Task InsertAsync(MusicItemModel item, byte[]? image, InsertExist onInsertExist = InsertExist.REPLACE) {
        var value = ToDictionary(item, image);
        if (value is null)
            return;
        await LoggerService.DebugAsync($"正在更新专辑'{value[nameof(AlbumModel.Name)]}'的如下字段：{string.Join(',', value.Keys)}。")
                           .ConfigureAwait(false);
        if (!value.Remove(nameof(AlbumModel.Thumbnail), out var thumbnail)) {
            await _db.InsertAsync(TABLE_NAME, value, onInsertExist).ConfigureAwait(false);
            return;
        }

        SqliteCommand command = _db.InsertNonExecute(TABLE_NAME, value, onInsertExist);
        value.Remove(nameof(AlbumModel.Cover));
        value[nameof(AlbumModel.Thumbnail)] = thumbnail;
        _db.InsertNonExecute(ref command, AlbumThumbnailRepository.TABLE_NAME, value, onInsertExist);
        await AsyncDatabaseService.ExecuteAsync(command).ConfigureAwait(false);
    }


    public async Task UpdateAsync(MusicItemModel item) {
        var value = ToDictionary(item);
        if (value is not null)
            await UpdateAsync(
                    (value[nameof(AlbumModel.Name)] as string)!,
                    new Dictionary<string, object?> { [nameof(AlbumModel.Cover)] = value[nameof(AlbumModel.Cover)] })
                .ConfigureAwait(false);
    }


    public async Task UpdateAsync(string name, Dictionary<string, object?> fieldValues) {
        await LoggerService.DebugAsync($"正在更新专辑'{name}'的如下字段：{string.Join(',', fieldValues.Keys)}。")
                           .ConfigureAwait(false);
        if (fieldValues.Count == 0)
            return;
        var whereClause = $"{nameof(AlbumModel.Name)} = @{nameof(AlbumModel.Name)}";
        var whereParams = new Dictionary<string, object?> { [nameof(AlbumModel.Name)] = name };

        if (!fieldValues.Remove(nameof(AlbumModel.Thumbnail), out var thumbnail)) {
            await _db.UpdateAsync(TABLE_NAME, fieldValues, whereClause, whereParams).ConfigureAwait(false);
            return;
        }

        SqliteCommand command = _db.UpdateNonExecute(TABLE_NAME, fieldValues, whereClause, whereParams);
        fieldValues.Remove(nameof(AlbumModel.Cover));
        fieldValues[nameof(AlbumModel.Thumbnail)] = thumbnail;
        _db.UpdateNonExecute(ref command, AlbumThumbnailRepository.TABLE_NAME, fieldValues, whereClause, whereParams);
        await AsyncDatabaseService.ExecuteAsync(command).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string id) {
        await LoggerService.DebugAsync($"正在删除专辑{id}的封面。").ConfigureAwait(false);
        await _db.DeleteAsync(
                     TABLE_NAME,
                     $"{nameof(AlbumModel.Name)} = @{nameof(AlbumModel.Name)}",
                     new Dictionary<string, object> { [nameof(AlbumModel.Name)] = id })
                 .ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(string id) {
        await LoggerService.DebugAsync($"正在检测是否存在专辑'{id}'").ConfigureAwait(false);
        var result = await _db.SingleAsync(
                                  $"SELECT 1 FROM {TABLE_NAME} WHERE {nameof(AlbumModel.Name)} = @{
                                      nameof(AlbumModel.Name)}",
                                  new Dictionary<string, object> { [nameof(AlbumModel.Name)] = id })
                              .ConfigureAwait(false);
        return result is not null;
    }

    private static Dictionary<string, object?>? ToDictionary(MusicItemModel model, Bitmap? bitmap = null) {
        Bitmap cover = bitmap ?? model.Cover;
        if (!CacheManager.IsValid(cover))
            return null;
        return new Dictionary<string, object?> {
            [nameof(AlbumModel.Name)] = string.IsNullOrEmpty(model.Album) ? model.AlbumId : model.Album,
            [nameof(AlbumModel.Artists)] = model.AlbumArtists,
            [nameof(AlbumModel.Cover)] = DumpHelper.BitmapToBytes(cover),
            [nameof(AlbumModel.Thumbnail)] = DumpHelper.BitmapToBytes(model.Thumbnail)
        };
    }

    private static Dictionary<string, object?>? ToDictionary(MusicItemModel model, byte[]? image) {
        if (model is { AlbumId: null })
            return null;
        return new Dictionary<string, object?> {
            [nameof(AlbumModel.Name)] = model.AlbumId,
            [nameof(AlbumModel.Cover)] = image ?? DumpHelper.BitmapToBytes(model.Cover),
            [nameof(AlbumModel.Thumbnail)] = DumpHelper.BitmapToBytes(model.Thumbnail)
        };
    }
}

public class AlbumThumbnailRepository : IAsyncReadonlyDatabaseRepository<string, Bitmap?> {
    public static readonly AlbumThumbnailRepository Instance = new(StaticConfig.CachePath);

    public const string TABLE_NAME = "album_thumbnail";
    private readonly AsyncDatabaseService _db;


    private AlbumThumbnailRepository(string dbPath) {
        _db = new AsyncDatabaseService(dbPath);
        InitializeAsync().ConfigureAwait(false).GetAwaiter().GetResult();
    }

    private async Task InitializeAsync() {
        await LoggerService.DebugAsync("正在初始化专辑缩略图数据库。").ConfigureAwait(false);
        await _db.CreateTableAsync(
                     TABLE_NAME,
                     $"""
                      {nameof(AlbumModel.Name)} TEXT PRIMARY KEY,
                      {nameof(AlbumModel.Thumbnail)} BLOB
                      """)
                 .ConfigureAwait(false);
    }

    public async Task RebuildAsync() {
        await LoggerService.DebugAsync("正在重建专辑缩略图数据库。").ConfigureAwait(false);
        await _db.DropTableAsync(TABLE_NAME).ConfigureAwait(false);
        await InitializeAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync() {
        await _db.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    public async Task<Bitmap?> SingleAsync(string id) {
        await LoggerService.DebugAsync($"正在获取专辑'{id}'的缩略图").ConfigureAwait(false);
        var result = await _db.SingleAsync(
                                  $"SELECT * FROM {TABLE_NAME} WHERE {nameof(AlbumModel.Name)} = @{
                                      nameof(AlbumModel.Name)}",
                                  new Dictionary<string, object> { [nameof(AlbumModel.Name)] = id })
                              .ConfigureAwait(false);
        return ParseHelper.ParseToBitmap(result?[nameof(AlbumModel.Thumbnail)]);
    }

    public async Task<IEnumerable<Bitmap?>> GetAsync() {
        await LoggerService.DebugAsync("正在获取所有专辑缩略图。警告：可能发生长时磁盘IO，应尽可能避免该操作").ConfigureAwait(false);

        var result = await _db.QueryAsync($"SELECT * FROM {TABLE_NAME}").ConfigureAwait(false);
        return result.Select(item => ParseHelper.ParseToBitmap(item[nameof(AlbumModel.Thumbnail)]) ??
                                     CacheManager.Damaged);
    }

    public async Task<int> CountAsync() {
        await LoggerService.DebugAsync("正在计算专辑缩略图数量").ConfigureAwait(false);
        return await _db.CountAsync(TABLE_NAME).ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(string id) {
        await LoggerService.DebugAsync($"正在检测是否存在专辑{id}的缩略图").ConfigureAwait(false);

        var result = await _db.SingleAsync(
                                  $"SELECT 1 FROM {TABLE_NAME} WHERE {nameof(AlbumModel.Name)} = @{
                                      nameof(AlbumModel.Name)}",
                                  new Dictionary<string, object> { [nameof(AlbumModel.Name)] = id })
                              .ConfigureAwait(false);
        return result is not null;
    }
}