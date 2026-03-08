using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using QwQ_Music.Common.Helpers;
using QwQ_Music.Common.Interfaces;
using QwQ_Music.Common.Managers;
using QwQ_Music.Models;
using QwQ_Music.Models.ConfigModels;

namespace QwQ_Music.Common.Services.Databases;

public class AlbumRepository : IAsyncDatabaseRepository<AlbumModel> {
    public static readonly AlbumRepository Instance = new(StaticConfig.DatabasePath);

    public const string TABLE_NAME = "albums";
    private readonly AsyncDatabaseService _db;

    protected AlbumRepository(string path) {
        _db = new AsyncDatabaseService(path);
        InitializeAsync().ConfigureAwait(false).GetAwaiter().GetResult();
    }

    private async Task InitializeAsync() {
        await LoggerService.DebugAsync("正在初始化专辑数据库").ConfigureAwait(false);

        await _db.CreateTableAsync(
                     TABLE_NAME,
                     $"""
                      {nameof(AlbumModel.Name)} TEXT NOT NULL,
                      {nameof(AlbumModel.Artists)} TEXT NOT NULL,
                      {nameof(AlbumModel.Description)} TEXT,
                      {nameof(AlbumModel.PublishTime)} INTEGER,
                      {nameof(AlbumModel.Company)} TEXT,
                      PRIMARY KEY({nameof(AlbumModel.Name)},{nameof(AlbumModel.Artists)})
                      """)
                 .ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync() {
        await _db.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    public async Task<AlbumModel?> SingleAsync(string id) {
        await LoggerService.DebugAsync($"正在获取专辑'{id}'的信息。").ConfigureAwait(false);
        var result = await _db.SingleAsync(
                                  $"SELECT * FROM {TABLE_NAME} WHERE {nameof(AlbumModel.Name)} = @primaryKey",
                                  new Dictionary<string, object> { ["primaryKey"] = id })
                              .ConfigureAwait(false);

        return result is null ? null : MapToModel(result);
    }

    public async Task<IEnumerable<AlbumModel>> GetAsync() {
        await LoggerService.DebugAsync("正在获取所有专辑信息。警告：可能发生长时磁盘IO，应尽可能避免该操作").ConfigureAwait(false);

        var rows = await _db.QueryAsync($"SELECT * FROM {TABLE_NAME}").ConfigureAwait(false);
        return rows.Select(MapToModel).Where(x => x is not null).Cast<AlbumModel>();
    }

    public async Task<int> CountAsync() {
        await LoggerService.DebugAsync("正在获取专辑数量").ConfigureAwait(false);

        var result = await _db.QueryAsync($"SELECT COUNT(*) AS cnt FROM {TABLE_NAME}").ConfigureAwait(false);

        return Convert.ToInt32(result[0]["cnt"]);
    }

    public async Task InsertAsync(AlbumModel item, InsertExist onInsertExist = InsertExist.FAIL) {
        var data = ModelToDictionary(item);
        await _db.InsertAsync(TABLE_NAME, data, onInsertExist).ConfigureAwait(false);
    }

    public async Task UpdateAsync(AlbumModel item) {
        var data = ModelToDictionary(item);

        // 移除主键字段，因为主键不应该被更新
        data.Remove(nameof(AlbumModel.Name));
        await UpdateAsync(item.Name, data).ConfigureAwait(false);
    }


    public async Task UpdateAsync(string name, Dictionary<string, object?> fieldValues) {
        if (fieldValues.Count == 0)
            return;
        await LoggerService.DebugAsync($"正在更新专辑'{name}'的如下字段：{string.Join(',', fieldValues.Keys)}")
                           .ConfigureAwait(false);

        const string whereClause = $"{nameof(AlbumModel.Name)} = @primaryKey";

        await _db.UpdateAsync(
                     TABLE_NAME,
                     fieldValues,
                     whereClause,
                     new Dictionary<string, object?> { ["primaryKey"] = name })
                 .ConfigureAwait(false);
    }

    public async Task DeleteAsync(string id) {
        await LoggerService.DebugAsync($"正在删除专辑'{id}'").ConfigureAwait(false);

        const string whereClause = $"{nameof(AlbumModel.Name)} = @primaryKey";

        await _db.DeleteAsync(TABLE_NAME, whereClause, new Dictionary<string, object> { ["primaryKey"] = id })
                 .ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(string id) {
        await LoggerService.DebugAsync($"正在检测是否存在专辑'{id}'").ConfigureAwait(false);

        var result = await _db.SingleAsync(
                                  $"SELECT 1 FROM {TABLE_NAME} WHERE {nameof(AlbumModel.Name)} = @primaryKey",
                                  new Dictionary<string, object> { ["primaryKey"] = id })
                              .ConfigureAwait(false);

        return result is not null;
    }

    #region Helper Methods

    private static AlbumModel? MapToModel(Dictionary<string, object?> dict) {
        try {
            long? publishTime = ParseHelper.TryParse<long>(dict, nameof(AlbumModel.PublishTime));
            var model = new AlbumModel {
                Name = ParseHelper.TryParse(dict, nameof(AlbumModel.Name))!,
                Artists = ParseHelper.TryParse(dict, nameof(AlbumModel.Artists))!,
                Description = (string)dict[nameof(AlbumModel.Description)]!,
                PublishTime = publishTime is null ? null : new DateTime(publishTime.Value)
            };
            LoggerService.Info($"加载了专辑'{model.Name}'。");

            return model;
        } catch (Exception ex) {
            NotificationService.Warning($"检测到严重的专辑存储错误。{dict[nameof(AlbumModel.Name)]}的数据将无法恢复。");
            LoggerService.Warning($"{dict[nameof(AlbumModel.Name)]}加载失败：{ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }

    private static Dictionary<string, object?> ModelToDictionary(AlbumModel model) {
        return new Dictionary<string, object?> {
            [nameof(AlbumModel.Name)] = model.Name,
            [nameof(AlbumModel.Artists)] = model.Artists,
            [nameof(AlbumModel.Description)] = model.Description,
            [nameof(AlbumModel.PublishTime)] = model.PublishTime?.Ticks,
            [nameof(AlbumModel.Company)] = model.Company,
        };
    }

    #endregion
}