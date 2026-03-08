using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace QwQ_Music.Common.Services.Databases;

[AttributeUsage(AttributeTargets.ReturnValue | AttributeTargets.Parameter)]
public class NotNullIf(string parameterName, bool condition) : Attribute {
    public string ParameterName { get; } = parameterName;
    public bool Condition { get; } = condition;
}

public enum InsertExist {
    FAIL, IGNORE, REPLACE
}

/// <summary>
///     提供基于 Sqlite 的数据库操作服务，包括建表、删表、增删改查等常用功能。
/// </summary>
public class AsyncDatabaseService : IAsyncDisposable {
    private readonly SqliteConnection _connection;

    /// <summary>
    ///     初始化数据库服务。
    /// </summary>
    /// <param name="dbPath">数据库文件路径</param>
    public AsyncDatabaseService(string dbPath) {
        if (string.IsNullOrWhiteSpace(dbPath))
            throw new ArgumentException("数据库路径不能为空。", nameof(dbPath));

        string? directory = Path.GetDirectoryName(dbPath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        string connectionString = $"Data Source={dbPath};Foreign Keys=True;";
        _connection = new SqliteConnection(connectionString);
        _connection.Open(); // 构造时自动打开连接
    }

    /// <summary>
    ///     释放数据库连接资源。
    /// </summary>
    public async ValueTask DisposeAsync() {
        await _connection.CloseAsync().ConfigureAwait(false);
        await _connection.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    #region 查询操作

    /// <summary>
    ///     执行查询，返回结果集。
    /// </summary>
    /// <param name="sql">SQL 查询语句</param>
    /// <param name="parameters">参数字典</param>
    /// <returns>结果集，每行是一个字典</returns>
    public async Task<List<Dictionary<string, object?>>> QueryAsync(
        string sql,
        Dictionary<string, object>? parameters = null) {
        if (string.IsNullOrWhiteSpace(sql))
            throw new ArgumentException("SQL 语句不能为空。", nameof(sql));

        await using var cmd = NewCommand();
        cmd.CommandText = sql;

        if (parameters != null) {
            foreach ((string key, object value) in parameters) {
                cmd.Parameters.AddWithValue($"@{key}", value);
            }
        }

        var result = new List<Dictionary<string, object?>>();
        await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);

        while (await reader.ReadAsync().ConfigureAwait(false)) {
            var row = new Dictionary<string, object?>();

            for (int i = 0; i < reader.FieldCount; i++) {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }

            result.Add(row);
        }

        return result;
    }

    public async Task<Dictionary<string, object?>?> SingleAsync(
        string sql,
        Dictionary<string, object>? parameters = null) {
        var data = await QueryAsync(sql + " LIMIT 1", parameters).ConfigureAwait(false);
        return data.FirstOrDefault();
    }

    public async Task<int> CountAsync(string tableName) {
        var result = await QueryAsync($"SELECT COUNT(*) AS cnt FROM {tableName}").ConfigureAwait(false);
        return Convert.ToInt32(result[0]["cnt"]);
    }

    #endregion

    #region 工具方法

    /// <summary>
    ///     转义标识符以防止关键字冲突。
    /// </summary>
    /// <param name="identifier">标识符名称</param>
    /// <returns>转义后的标识符</returns>
    private static string EscapeIdentifier(string identifier) { return $"\"{identifier.Replace("\"", "\"\"")}\""; }

    #endregion

    #region 表结构操作

    public async Task CreateTriggerAsync(string triggerName, string action) {
        if (string.IsNullOrWhiteSpace(triggerName))
            throw new ArgumentException("触发器名不能为空。", nameof(triggerName));
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("操作不能为空。", nameof(action));
        await using var cmd = NewCommand();
        cmd.CommandText = $"CREATE TRIGGER IF NOT EXISTS {EscapeIdentifier(triggerName)} {action};";
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        
    }

    /// <summary>
    ///     创建表（如果不存在）。
    /// </summary>
    public async Task CreateTableAsync(string tableName, string columnsDefinition) {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("表名不能为空。", nameof(tableName));

        if (string.IsNullOrWhiteSpace(columnsDefinition))
            throw new ArgumentException("列定义不能为空。", nameof(columnsDefinition));

        await using var cmd = NewCommand();
        cmd.CommandText = $"CREATE TABLE IF NOT EXISTS {EscapeIdentifier(tableName)} ({columnsDefinition});";
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     删除表（如果存在）。
    /// </summary>
    public async Task DropTableAsync(string tableName) {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("表名不能为空。", nameof(tableName));

        await using var cmd = NewCommand();
        cmd.CommandText = $"DROP TABLE IF EXISTS {EscapeIdentifier(tableName)};";
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    #endregion


    public SqliteCommand NewCommand() { return _connection.CreateCommand(); }


    #region 数据操作拓展-Insert

    /// <summary>
    ///     插入多条数据。不写入。
    /// </summary>
    public void InsertMultipleNonExecute(
        List<SqliteCommand> commands,
        string tableName,
        in IEnumerable<Dictionary<string, object?>> dataArray,
        InsertExist onInsertExist) {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("表名不能为空。", nameof(tableName));

        commands.AddRange(dataArray.Select(data => InsertNonExecute(tableName, data, onInsertExist)));

        // ReSharper disable once ConvertIfStatementToReturnStatement
        if (commands.Count == 0)
            throw new ArgumentException("插入数据不能为空。", nameof(dataArray));
    }

    /// <summary>
    ///     插入多条数据。不写入。
    /// </summary>
    public List<SqliteCommand> InsertMultipleNonExecute(
        string tableName,
        in IEnumerable<Dictionary<string, object?>> dataArray,
        InsertExist onInsertExist) {
        List<SqliteCommand> commands = [];
        InsertMultipleNonExecute(commands, tableName, dataArray, onInsertExist);
        return commands;
    }

    /// <summary>
    ///     插入一条数据。不写入。
    /// </summary>
    public void InsertNonExecute(
        ref SqliteCommand command,
        string tableName,
        Dictionary<string, object?> data,
        InsertExist onInsertExist) {
        string action = onInsertExist switch {
            InsertExist.FAIL    => "INSERT",
            InsertExist.IGNORE  => "INSERT OR IGNORE",
            InsertExist.REPLACE => "INSERT OR REPLACE",
            _                   => throw new ArgumentOutOfRangeException(nameof(onInsertExist), onInsertExist, null)
        };
        string columns = string.Join(", ", data.Keys.Select(EscapeIdentifier));
        string paramNames = string.Join(", ", data.Keys.Select(key => $"@{key}"));
        foreach ((string key, object? value) in data) {
            command.Parameters.AddWithValue($"@{key}", value ?? DBNull.Value);
        }

        command.CommandText = $"{action} INTO {EscapeIdentifier(tableName)} ({columns}) VALUES ({paramNames});";
    }

    /// <summary>
    ///     插入一条数据。不写入。
    /// </summary>
    public SqliteCommand InsertNonExecute(
        string tableName,
        Dictionary<string, object?> data,
        InsertExist onInsertExist) {
        SqliteCommand command = NewCommand();
        InsertNonExecute(ref command, tableName, data, onInsertExist);
        return command;
    }

    /// <summary>
    ///     插入一条数据并立即写入。
    /// </summary>
    public async Task InsertAsync(string tableName, Dictionary<string, object?> data, InsertExist onInsertExist) {
        await ExecuteAsync(InsertNonExecute(tableName, data, onInsertExist)).ConfigureAwait(false);
    }

    /// <summary>
    ///     插入多条数据并立即写入。
    /// </summary>
    public async Task InsertMultipleAsync(
        string tableName,
        IEnumerable<Dictionary<string, object?>> dataArray,
        InsertExist onInsertExist) {
        await ExecuteAsync(InsertMultipleNonExecute(tableName, dataArray, onInsertExist)).ConfigureAwait(false);
    }

    #endregion

    #region 数据操作拓展-Update

    /// <summary>
    ///     更新多条数据。不写入。
    /// </summary>
    public void UpdateMultipleNonExecute(
        List<SqliteCommand> commands,
        string tableName,
        in IEnumerable<Dictionary<string, object?>> dataArray,
        string whereClause,
        in IEnumerable<Dictionary<string, object?>> whereParamsArray) {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("表名不能为空。", nameof(tableName));
        commands.AddRange(
            dataArray.Zip(whereParamsArray)
                     .Select(item => UpdateNonExecute(tableName, item.First, whereClause, item.Second)));
    }

    /// <summary>
    ///     更新多条数据。不写入。
    /// </summary>
    public List<SqliteCommand> UpdateMultipleNonExecute(
        string tableName,
        in IEnumerable<Dictionary<string, object?>> dataArray,
        string whereClause,
        in IEnumerable<Dictionary<string, object?>> whereParamsArray) {
        List<SqliteCommand> commands = [];
        UpdateMultipleNonExecute(commands, tableName, dataArray, whereClause, whereParamsArray);
        return commands;
    }

    /// <summary>
    ///     更新一条数据。不写入。
    /// </summary>
    public void UpdateNonExecute(
        ref SqliteCommand command,
        string tableName,
        Dictionary<string, object?> data,
        string whereClause,
        Dictionary<string, object?> whereParams) {
        if (string.IsNullOrWhiteSpace(whereClause))
            throw new ArgumentException("WHERE 条件不能为空。", nameof(whereClause));

        string setClause = string.Join(", ", data.Keys.Select(key => $"{EscapeIdentifier(key)} = @{key}"));

        foreach ((string key, object? value) in data) {
            command.Parameters.AddWithValue($"@{key}", value ?? DBNull.Value);
        }

        foreach ((string key, object? value) in whereParams) {
            command.Parameters.AddWithValue($"@{key}", value ?? DBNull.Value);
        }

        command.CommandText = $"UPDATE {EscapeIdentifier(tableName)} SET {setClause} WHERE {whereClause};";
    }

    /// <summary>
    ///     更新一条数据。不写入。
    /// </summary>
    public SqliteCommand UpdateNonExecute(
        string tableName,
        Dictionary<string, object?> data,
        string whereClause,
        Dictionary<string, object?> whereParams) {
        SqliteCommand command = NewCommand();
        UpdateNonExecute(ref command, tableName, data, whereClause, whereParams);
        return command;
    }

    /// <summary>
    ///     更新一条数据并立即写入。
    /// </summary>
    public async Task UpdateAsync(
        string tableName,
        Dictionary<string, object?> data,
        string whereClause,
        Dictionary<string, object?> whereParams) {
        await ExecuteAsync(UpdateNonExecute(tableName, data, whereClause, whereParams)).ConfigureAwait(false);
    }

    /// <summary>
    ///     更新多条数据并立即写入。
    /// </summary>
    public async Task UpdateMultipleAsync(
        string tableName,
        IEnumerable<Dictionary<string, object?>> dataArray,
        string whereClause,
        IEnumerable<Dictionary<string, object?>> whereParamsArray) {
        await ExecuteAsync(UpdateMultipleNonExecute(tableName, dataArray, whereClause, whereParamsArray))
            .ConfigureAwait(false);
    }

    #endregion

    #region 数据操作拓展-Delete

    /// <summary>
    ///     删除多条数据。不写入。
    /// </summary>
    public void DeleteMultipleNonExecute(
        List<SqliteCommand> commands,
        string tableName,
        string whereClause,
        IEnumerable<Dictionary<string, object>> whereParamsArray) {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("表名不能为空。", nameof(tableName));

        if (string.IsNullOrWhiteSpace(whereClause))
            throw new ArgumentException("WHERE 条件不能为空。", nameof(whereClause));

        commands.AddRange(
            whereParamsArray.Select(whereParams => DeleteNonExecute(tableName, whereClause, whereParams)));
    }

    /// <summary>
    ///     删除多条数据。不写入。
    /// </summary>
    public List<SqliteCommand> DeleteMultipleNonExecute(
        string tableName,
        string whereClause,
        IEnumerable<Dictionary<string, object>> whereParamsArray) {
        List<SqliteCommand> commands = [];
        DeleteMultipleNonExecute(commands, tableName, whereClause, whereParamsArray);
        return commands;
    }

    /// <summary>
    ///     删除一条数据。不写入。
    /// </summary>
    public void DeleteNonExecute(
        ref SqliteCommand command,
        string tableName,
        string whereClause,
        Dictionary<string, object> whereParams) {
        foreach ((string key, object value) in whereParams) {
            command.Parameters.AddWithValue($"@{key}", value);
        }

        command.CommandText = $"DELETE FROM {EscapeIdentifier(tableName)} WHERE {whereClause};";
    }

    /// <summary>
    ///     删除一条数据。不写入。
    /// </summary>
    public SqliteCommand DeleteNonExecute(
        string tableName,
        string whereClause,
        Dictionary<string, object> whereParams) {
        SqliteCommand command = NewCommand();
        DeleteNonExecute(ref command, tableName, whereClause, whereParams);
        return command;
    }

    /// <summary>
    ///     删除一条数据并立即写入。
    /// </summary>
    public async Task DeleteAsync(string tableName, string whereClause, Dictionary<string, object> whereParams) {
        await ExecuteAsync(DeleteNonExecute(tableName, whereClause, whereParams)).ConfigureAwait(false);
    }

    /// <summary>
    ///     删除多条数据并立即写入。
    /// </summary>
    public async Task DeleteMultipleAsync(
        string tableName,
        string whereClause,
        IEnumerable<Dictionary<string, object>> whereParamsArray) {
        await ExecuteAsync(DeleteMultipleNonExecute(tableName, whereClause, whereParamsArray)).ConfigureAwait(false);
    }

    #endregion

    #region 事务支持

    /// <summary>
    ///     开始事务
    /// </summary>
    public SqliteTransaction BeginTransaction() { return _connection.BeginTransaction(); }

    /// <summary>
    ///     提交事务
    /// </summary>
    public static async Task CommitAsync(SqliteTransaction transaction) {
        try {
            await transaction.CommitAsync().ConfigureAwait(false);
        } catch (SqliteException) {
            await transaction.RollbackAsync().ConfigureAwait(false);
            throw;
        } finally {
            await transaction.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     执行 SQL 命令
    /// </summary>
    public static async Task ExecuteAsync(params List<SqliteCommand> commands) {
        foreach (SqliteCommand command in commands) {
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            await command.DisposeAsync().ConfigureAwait(false);
        }
    }

    #endregion
}