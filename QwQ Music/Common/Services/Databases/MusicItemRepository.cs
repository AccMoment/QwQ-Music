using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using QwQ_Music.Common.Interfaces;
using QwQ_Music.Models;
using QwQ_Music.Models.ConfigModels;
using QwQ_Music.Models.Enums;

namespace QwQ_Music.Common.Services.Databases;

public class MusicItemRepository : IDatabaseRepository<MusicItemModel> {
    public static readonly MusicItemRepository Instance = new(StaticConfig.DatabasePath);
    public const string TABLE_NAME = "music";
    private readonly DatabaseService _db;

    private MusicItemRepository(string dbPath) {
        _db = new DatabaseService(dbPath);
        Initialize();
    }

    private void Initialize() {
        _db.CreateTable(
            TABLE_NAME,
            $"""
             {nameof(MusicItemModel.Title)} TEXT NOT NULL,
             {nameof(MusicItemModel.Artists)} TEXT,
             {nameof(MusicItemModel.Composer)} TEXT,
             {nameof(MusicItemModel.Album)} TEXT,
             {nameof(MusicItemModel.AlbumArtist)} TEXT,
             {nameof(MusicItemModel.CoverId)} TEXT,
             {nameof(MusicItemModel.FilePath)} TEXT NOT NULL PRIMARY KEY,
             {nameof(MusicItemModel.FileSize)} TEXT NOT NULL,
             {nameof(MusicItemModel.Record)} INTEGER NOT NULL,
             {nameof(MusicItemModel.Duration)} INTEGER NOT NULL,
             {nameof(MusicItemModel.CoverColors)} TEXT,
             {nameof(MusicItemModel.Gain)} TEXT NOT NULL,
             {nameof(MusicItemModel.SampleRate)} TEXT NOT NULL,
             {nameof(MusicItemModel.Channels)} INTEGER NOT NULL,
             {nameof(MusicItemModel.EncodingFormat)} TEXT NOT NULL,
             {nameof(MusicItemModel.Comment)} TEXT,
             {nameof(MusicItemModel.AudioQualityLevel)} INTEGER,
             {nameof(MusicItemModel.Remarks)} TEXT,
             {nameof(MusicItemModel.LyricOffset)} TEXT,
             {nameof(MusicItemModel.InsertTime)} INTEGER,
             {nameof(MusicItemModel.ModificationTime)} INTEGER
             """);
    }

    public void Rebuild() {
        _db.DropTable(TABLE_NAME);
        Initialize();
    }

    public void Dispose() {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    public MusicItemModel? Get(string primaryKey) {
        var result = _db.Query(
            $"SELECT * FROM {TABLE_NAME} WHERE {nameof(MusicItemModel.FilePath)} = @primaryKey",
            new Dictionary<string, object> { ["primaryKey"] = primaryKey });

        return result.Count > 0 ? Parse(result[0]) : null;
    }

    public IEnumerable<MusicItemModel> GetAll() {
        List<Dictionary<string, object?>> rows = _db.Query($"SELECT * FROM {TABLE_NAME}");

        return rows.AsParallel().Select(Parse).Where(item => item is not null).Cast<MusicItemModel>();
    }

    public int Count() {
        var result = _db.Query($"SELECT COUNT(*) AS cnt FROM {TABLE_NAME}");

        return Convert.ToInt32(result[0]["cnt"]);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Insert(MusicItemModel item) {
        _db.Insert(TABLE_NAME, ToDictionary(item));
    }

    public void Update(MusicItemModel item) {
        var data = ToDictionary(item);
        data.Remove(nameof(MusicItemModel.FilePath));

        const string whereClause = $"{nameof(MusicItemModel.FilePath)} = @{nameof(MusicItemModel.FilePath)}";

        _db.Update(
            TABLE_NAME,
            data,
            whereClause,
            new Dictionary<string, object?> { [nameof(MusicItemModel.FilePath)] = item.FilePath });
    }

    public void Update(string primaryKey, string[] fields, string?[] values) {
        if (fields.Length != values.Length)
            throw new ArgumentException("字段和值的长度必须相同。");

        var data = new Dictionary<string, object?>();

        for (int i = 0; i < fields.Length; i++) {
            data[fields[i]] = values[i];
        }

        const string whereClause = $"{nameof(MusicItemModel.FilePath)} = @primaryKey";

        _db.Update(TABLE_NAME, data, whereClause, new Dictionary<string, object?> { ["primaryKey"] = primaryKey });
    }

    public void Update(string primaryKey, Dictionary<string, object?> fieldValues) {
        if (fieldValues.Count == 0)
            return;

        const string whereClause = $"{nameof(MusicItemModel.FilePath)} = @primaryKey";

        _db.Update(
            TABLE_NAME,
            fieldValues,
            whereClause,
            new Dictionary<string, object?> { ["primaryKey"] = primaryKey });
    }

    public void Delete(string primaryKey) {
        const string whereClause = $"{nameof(MusicItemModel.FilePath)} = @primaryKey";

        _db.Delete(TABLE_NAME, whereClause, new Dictionary<string, object> { ["primaryKey"] = primaryKey });
    }

    public bool Exists(string primaryKey) {
        var result = _db.Query(
            $"SELECT 1 FROM {TABLE_NAME} WHERE {nameof(MusicItemModel.FilePath)} = @primaryKey LIMIT 1",
            new Dictionary<string, object> { ["primaryKey"] = primaryKey });

        return result.Count > 0;
    }

    #region Helper Methods

    private static MusicItemModel? Parse(Dictionary<string, object?> dict) {
        int errors = 0;
        bool critical = false;
        try {
            var model = new MusicItemModel {
                FilePath = ParseHelpers.TryParse(dict, nameof(MusicItemModel.FilePath)) ?? CriticalError(""),
                Title = ParseHelpers.TryParse(dict, nameof(MusicItemModel.Title)) ?? Error(""),
                Artists = ParseHelpers.TryParse(dict, nameof(MusicItemModel.Artists)) ?? Error(""),
                Album = ParseHelpers.TryParse(dict, nameof(MusicItemModel.Album)) ?? Error(""),
                AlbumArtist = ParseHelpers.TryParse(dict, nameof(MusicItemModel.AlbumArtist)) ?? Error(""),
                Composer = ParseHelpers.TryParse(dict, nameof(MusicItemModel.Composer)),
                CoverId = ParseHelpers.TryParse(dict, nameof(MusicItemModel.CoverId)),
                FileSize = ParseHelpers.TryParse(dict, nameof(MusicItemModel.FileSize)) ?? Error("未知"),
                Record =
                    TimeSpan.FromTicks(ParseHelpers.TryParse<long>(dict, nameof(MusicItemModel.Record)) ?? Error(0)),
                Duration =
                    TimeSpan.FromTicks(ParseHelpers.TryParse<long>(dict, nameof(MusicItemModel.Duration)) ?? Error(0)),
                SampleRate = ParseHelpers.TryParse<int>(dict, nameof(MusicItemModel.SampleRate)) ?? Error(44100),
                Channels = ParseHelpers.TryParse<int>(dict, nameof(MusicItemModel.Channels)) ?? Error(2),
                CoverColors = (ParseHelpers.TryParse(dict, nameof(MusicItemModel.CoverColors)))?.Split("、"),
                Gain = ParseHelpers.TryParse<double>(dict, nameof(MusicItemModel.Gain)) ?? Error(0),
                EncodingFormat = ParseHelpers.TryParse(dict, nameof(MusicItemModel.EncodingFormat)) ?? Error("Unknown"),
                Comment = ParseHelpers.TryParse(dict, nameof(MusicItemModel.Comment)) ?? Error(""),
                AudioQualityLevel =
                    (AudioQualityLevel?)ParseHelpers.TryParse<int>(dict, nameof(MusicItemModel.AudioQualityLevel)) ??
                    Error(AudioQualityLevel.Unknown),
                Remarks = ParseHelpers.TryParse(dict, nameof(MusicItemModel.Remarks)) ?? Error(""),
                LyricOffset = ParseHelpers.TryParse<double>(dict, nameof(MusicItemModel.LyricOffset)) ?? Error(0),
                InsertTime =
                    new DateTime(ParseHelpers.TryParse<long>(dict, nameof(MusicItemModel.InsertTime)) ?? Error(0)),
                ModificationTime = new DateTime(
                    ParseHelpers.TryParse<long>(dict, nameof(MusicItemModel.ModificationTime)) ??
                    Error(DateTime.Now.Ticks))
            };

            if (critical) {
                NotificationService.Warning($"检测到严重的音频存储错误。{dict[nameof(MusicItemModel.Title)]}的信息将无法恢复。");
                return null;
            }

            if (errors > 0) {
                NotificationService.Warning(
                    $"检测到对{dict[nameof(MusicItemModel.Title)]}的信息存储存在{errors}处错误。请在设置>播放中点击[强制刷新标签]按钮以修复。");
            }

            return model;
        } catch (Exception) {
            NotificationService.Warning("检测到严重的音频存储错误。可能是由于大版本更新导致的数据缺失。请在设置>播放中点击[强制刷新标签]按钮以尝试修复。");
            return null;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        T Error<T>(T value) {
            errors++;
            return value;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        T CriticalError<T>(T value) {
            critical = true;
            return value;
        }
    }

    private static Dictionary<string, object?> ToDictionary(MusicItemModel model) {
        var dict = new Dictionary<string, object?> {
            [nameof(MusicItemModel.Title)] = model.Title,
            [nameof(MusicItemModel.Artists)] = model.Artists,
            [nameof(MusicItemModel.Composer)] = model.Composer,
            [nameof(MusicItemModel.Album)] = model.Album,
            [nameof(MusicItemModel.AlbumArtist)] = model.AlbumArtist,
            [nameof(MusicItemModel.CoverId)] = model.CoverId,
            [nameof(MusicItemModel.FilePath)] = model.FilePath,
            [nameof(MusicItemModel.FileSize)] = model.FileSize,
            [nameof(MusicItemModel.Record)] = model.Record.Ticks,
            [nameof(MusicItemModel.Duration)] = model.Duration.Ticks,
            [nameof(MusicItemModel.CoverColors)] =
                model.CoverColors != null ? string.Join("、", model.CoverColors) : null,
            [nameof(MusicItemModel.Gain)] = model.Gain,
            [nameof(MusicItemModel.SampleRate)] = model.SampleRate,
            [nameof(MusicItemModel.Channels)] = model.Channels,
            [nameof(MusicItemModel.EncodingFormat)] = model.EncodingFormat,
            [nameof(MusicItemModel.Comment)] = model.Comment,
            [nameof(MusicItemModel.AudioQualityLevel)] = (int)model.AudioQualityLevel,
            [nameof(MusicItemModel.Remarks)] = model.Remarks,
            [nameof(MusicItemModel.LyricOffset)] = model.LyricOffset,
            [nameof(MusicItemModel.InsertTime)] = model.InsertTime.Ticks,
            [nameof(MusicItemModel.ModificationTime)] = model.ModificationTime.Ticks
        };

        return dict;
    }

    #endregion
}