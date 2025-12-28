using System;
using System.Collections.Generic;
using System.Linq;
using QwQ_Music.Common.Interfaces;
using QwQ_Music.Common.Managers;
using QwQ_Music.Models;
using QwQ_Music.Models.ConfigModels;

namespace QwQ_Music.Common.Services.Databases;

public class MusicListRepository : IDatabaseRepository<MusicListModel> {
    public static readonly MusicListRepository Instance = new(StaticConfig.DatabasePath);

    public const string TABLE_NAME = "playlists";
    private readonly DatabaseService _db;

    private MusicListRepository(string path) {
        _db = new DatabaseService(path);
        Initialize();
    }

    private void Initialize() {
        _db.CreateTable(
            TABLE_NAME,
            $"""
             {nameof(MusicListModel.Name)} TEXT NOT NULL PRIMARY KEY,
             {nameof(MusicListModel.Description)} TEXT,
             {nameof(MusicListModel.IsCoverExist)} INTEGER,
             {nameof(MusicListModel.CreateTime)} INTEGER,
             {nameof(MusicListModel.ModifyTime)} INTEGER,
             {nameof(MusicListModel.SortMode)} INTEGER
             """);
    }

    public void Dispose() {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    public MusicListModel? Get(string primaryKey) {
        var result = _db.Query(
            $"SELECT * FROM {TABLE_NAME} WHERE {nameof(MusicListModel.Name)} = @primaryKey",
            new Dictionary<string, object> { ["primaryKey"] = primaryKey });

        return result.Count > 0 ? MapToModel(result[0]) : null;
    }

    public IEnumerable<MusicListModel> GetAll() {
        var rows = _db.Query($"SELECT * FROM {TABLE_NAME}");

        return rows.Select(MapToModel).Where(x => x is not null).Cast<MusicListModel>();
    }

    public int Count() {
        var result = _db.Query($"SELECT COUNT(*) AS cnt FROM {TABLE_NAME}");

        return Convert.ToInt32(result[0]["cnt"]);
    }

    public void Insert(MusicListModel item) {
        var data = ModelToDictionary(item);
        _db.Insert(TABLE_NAME, data);
    }

    public void Update(MusicListModel item) {
        var data = ModelToDictionary(item);

        // 移除主键字段，因为主键不应该被更新
        data.Remove(nameof(MusicListModel.Name));

        const string whereClause = $"{nameof(MusicListModel.Name)} = @{nameof(MusicListModel.Name)}";

        _db.Update(
            TABLE_NAME,
            data,
            whereClause,
            new Dictionary<string, object?> { [nameof(MusicListModel.Name)] = item.Name });
    }

    public void Update(string primaryKey, string[] fields, string?[] values) {
        if (fields.Length != values.Length)
            throw new ArgumentException("字段和值的长度必须相同。");

        var data = new Dictionary<string, object?>();

        for (int i = 0; i < fields.Length; i++) {
            data[fields[i]] = values[i];
        }

        const string whereClause = $"{nameof(MusicListModel.Name)} = @primaryKey";

        _db.Update(TABLE_NAME, data, whereClause, new Dictionary<string, object?> { ["primaryKey"] = primaryKey });
    }

    public void Update(string primaryKey, Dictionary<string, object?> fieldValues) {
        if (fieldValues.Count == 0)
            return;

        const string whereClause = $"{nameof(MusicListModel.Name)} = @primaryKey";

        _db.Update(
            TABLE_NAME,
            fieldValues,
            whereClause,
            new Dictionary<string, object?> { ["primaryKey"] = primaryKey });
    }

    public void Delete(string primaryKey) {
        const string whereClause = $"{nameof(MusicListModel.Name)} = @primaryKey";

        _db.Delete(TABLE_NAME, whereClause, new Dictionary<string, object> { ["primaryKey"] = primaryKey });
    }

    public bool Exists(string primaryKey) {
        var result = _db.Query(
            $"SELECT 1 FROM {TABLE_NAME} WHERE {nameof(MusicListModel.Name)} = @primaryKey LIMIT 1",
            new Dictionary<string, object> { ["primaryKey"] = primaryKey });

        return result.Count > 0;
    }

    #region Helper Methods

    private static MusicListModel? MapToModel(Dictionary<string, object?> dict) {
        try {
            var model = new MusicListModel {
                Name = (string)dict[nameof(MusicListModel.Name)]!,
                Description = (string)dict[nameof(MusicListModel.Description)]!,
                
                CreateTime =
                    new DateTime(
                        ParseHelpers.TryParse<long>(dict, nameof(MusicListModel.CreateTime)) ?? DateTime.Now.Ticks),
                ModifyTime = new DateTime(
                    ParseHelpers.TryParse<long>(dict, nameof(MusicListModel.ModifyTime)) ?? DateTime.Now.Ticks),
            };
            if (ParseHelpers.TryParse<bool>(dict, nameof(MusicListModel.IsCoverExist)) is not true) {
                CacheManager.SetImage(model.Name, CacheManager.NotExist);
                LoggerService.Warning($"歌单'{model.Name}'的封面不存在。");
            }

            LoggerService.Info($"加载了歌单'{model.Name}'。");

            return model;
        } catch (Exception ex) {
            NotificationService.Warning($"检测到严重的歌单存储错误。{dict[nameof(MusicListModel.Name)]}的数据将无法恢复。");
            LoggerService.Warning($"{dict[nameof(MusicListModel.Name)]}加载失败：{ex.Message}\n{ex.StackTrace}");

            return null;
        }
    }

    private static Dictionary<string, object?> ModelToDictionary(MusicListModel model) {
        return new Dictionary<string, object?> {
            [nameof(MusicListModel.Name)] = model.Name,
            [nameof(MusicListModel.Description)] = model.Description,
            [nameof(MusicListModel.IsCoverExist)] = model.IsCoverExist,
            [nameof(MusicListModel.CreateTime)] = model.CreateTime,
            [nameof(MusicListModel.ModifyTime)] = model.ModifyTime
        };
    }

    #endregion
}