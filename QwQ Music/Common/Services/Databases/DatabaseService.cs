using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace QwQ_Music.Common.Services.Databases;

[AttributeUsage(AttributeTargets.ReturnValue | AttributeTargets.Parameter)]
public class NotNullIf(string parameterName, bool condition) : Attribute {
    public string ParameterName { get; } = parameterName;
    public bool Condition { get; } = condition;
}

/// <summary>
///     提供基于 Sqlite 的数据库操作服务，包括建表、删表、增删改查等常用功能。
/// </summary>
public class DatabaseService : IDisposable {
    private readonly SqliteConnection _connection;

    /// <summary>
    ///     初始化数据库服务。
    /// </summary>
    /// <param name="dbPath">数据库文件路径</param>
    public DatabaseService(string dbPath) {
        if (string.IsNullOrWhiteSpace(dbPath))
            throw new ArgumentException("数据库路径不能为空。", nameof(dbPath));

        string? directory = Path.GetDirectoryName(dbPath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        string connectionString = $"Data Source={dbPath}";
        _connection = new SqliteConnection(connectionString);
        _connection.Open(); // 构造时自动打开连接
    }

    /// <summary>
    ///     释放数据库连接资源。
    /// </summary>
    public void Dispose() {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    #region 查询操作

    /// <summary>
    ///     执行查询，返回结果集。
    /// </summary>
    /// <param name="sql">SQL 查询语句</param>
    /// <param name="parameters">参数字典</param>
    /// <returns>结果集，每行是一个字典</returns>
    public List<Dictionary<string, object?>> Query(string sql, Dictionary<string, object>? parameters = null) {
        if (string.IsNullOrWhiteSpace(sql))
            throw new ArgumentException("SQL 语句不能为空。", nameof(sql));

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;

        if (parameters != null) {
            foreach ((string key, object value) in parameters) {
                cmd.Parameters.AddWithValue($"@{key}", value);
            }
        }

        var result = new List<Dictionary<string, object?>>();
        using var reader = cmd.ExecuteReader();

        while (reader.Read()) {
            var row = new Dictionary<string, object?>();

            for (int i = 0; i < reader.FieldCount; i++) {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }

            result.Add(row);
        }

        return result;
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

    /// <summary>
    ///     创建表（如果不存在）。
    /// </summary>
    public void CreateTable(string tableName, string columnsDefinition) {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("表名不能为空。", nameof(tableName));

        if (string.IsNullOrWhiteSpace(columnsDefinition))
            throw new ArgumentException("列定义不能为空。", nameof(columnsDefinition));

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"CREATE TABLE IF NOT EXISTS {EscapeIdentifier(tableName)} ({columnsDefinition});";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    ///     删除表（如果存在）。
    /// </summary>
    public void DropTable(string tableName) {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("表名不能为空。", nameof(tableName));

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"DROP TABLE IF EXISTS {EscapeIdentifier(tableName)};";
        cmd.ExecuteNonQuery();
    }

    #endregion


    public SqliteCommand NewCommand() { return _connection.CreateCommand(); }


    #region 数据操作拓展-Insert

    /// <summary>
    ///     插入多条数据。不写入。
    /// </summary>
    public void InsertMultipleNonExecute(
        ref SqliteCommand command,
        string tableName,
        in IEnumerable<Dictionary<string, object?>> dataArray) {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("表名不能为空。", nameof(tableName));

        int count = 0;
        int alreadyExistedParamsCount = command.Parameters.Count;
        foreach (Dictionary<string, object?> data in dataArray) {
            count++;
            string columns = string.Join(", ", data.Keys.Select(EscapeIdentifier));
            int countCopy = count;
            string paramNames = string.Join(
                ", ",
                data.Keys.Select(key => $"@{key}__ID_{alreadyExistedParamsCount + countCopy}"));
            foreach ((string key, object? value) in data) {
                command.Parameters.AddWithValue(
                    $"@{key}__ID_{alreadyExistedParamsCount + countCopy}",
                    value ?? DBNull.Value);
            }

            command.CommandText += $"INSERT INTO {EscapeIdentifier(tableName)} ({columns}) VALUES ({paramNames});";
        }

        // ReSharper disable once ConvertIfStatementToReturnStatement
        if (count == 0)
            throw new ArgumentException("插入数据不能为空。", nameof(dataArray));
    }

    /// <summary>
    ///     插入多条数据。不写入。
    /// </summary>
    public SqliteCommand InsertMultipleNonExecute(
        string tableName,
        in IEnumerable<Dictionary<string, object?>> dataArray) {
        SqliteCommand command = NewCommand();
        InsertMultipleNonExecute(ref command, tableName, dataArray);
        return command;
    }

    /// <summary>
    ///     插入一条数据。不写入。
    /// </summary>
    public void InsertNonExecute(ref SqliteCommand command, string tableName, Dictionary<string, object?> data) {
        InsertMultipleNonExecute(ref command, tableName, [data]);
    }

    /// <summary>
    ///     插入一条数据。不写入。
    /// </summary>
    public SqliteCommand InsertNonExecute(string tableName, Dictionary<string, object?> data) {
        return InsertMultipleNonExecute(tableName, [data]);
    }

    /// <summary>
    ///     插入一条数据并立即写入。
    /// </summary>
    public void Insert(string tableName, Dictionary<string, object?> data) {
        Execute(InsertNonExecute(tableName, data));
    }

    /// <summary>
    ///     插入多条数据并立即写入。
    /// </summary>
    public void InsertMultiple(string tableName, in IEnumerable<Dictionary<string, object?>> dataArray) {
        Execute(InsertMultipleNonExecute(tableName, dataArray));
    }

    #endregion

    #region 数据操作拓展-Update

    /// <summary>
    ///     更新多条数据。不写入。
    /// </summary>
    public void UpdateMultipleNonExecute(
        ref SqliteCommand command,
        string tableName,
        in IEnumerable<Dictionary<string, object?>> dataArray,
        string whereClause,
        in IEnumerable<Dictionary<string, object?>> whereParamsArray) {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("表名不能为空。", nameof(tableName));

        using IEnumerator<Dictionary<string, object?>> parameters = whereParamsArray.GetEnumerator();

        int count = 0;
        int alreadyExistedParamsCount = command.Parameters.Count;
        foreach (Dictionary<string, object?> data in dataArray) {
            count++;
            Dictionary<string, object?> whereParams = parameters.Current;
            int countCopy = count;
            if (string.IsNullOrWhiteSpace(whereClause))
                throw new ArgumentException("WHERE 条件不能为空。", nameof(whereClause));

            string setClause = string.Join(
                ", ",
                data.Keys.Select(key => $"{EscapeIdentifier(key)} = @{key}__ID_{alreadyExistedParamsCount + countCopy
                }"));

            foreach ((string key, object? value) in data) {
                command.Parameters.AddWithValue(
                    $"@{key}__ID_{alreadyExistedParamsCount + countCopy}",
                    value ?? DBNull.Value);
            }

            foreach ((string key, object? value) in whereParams) {
                command.Parameters.AddWithValue($"@{key}", value ?? DBNull.Value);
            }

            command.CommandText += $"UPDATE {EscapeIdentifier(tableName)} SET {setClause} WHERE {whereClause};";
            parameters.MoveNext();
        }
    }

    /// <summary>
    ///     更新多条数据。不写入。
    /// </summary>
    public SqliteCommand UpdateMultipleNonExecute(
        string tableName,
        in IEnumerable<Dictionary<string, object?>> dataArray,
        string whereClause,
        in IEnumerable<Dictionary<string, object?>> whereParamsArray) {
        SqliteCommand command = NewCommand();
        UpdateMultipleNonExecute(ref command, tableName, dataArray, whereClause, whereParamsArray);
        return command;
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
        UpdateMultipleNonExecute(ref command, tableName, [data], whereClause, [whereParams]);
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
    public void Update(
        string tableName,
        Dictionary<string, object?> data,
        string whereClause,
        Dictionary<string, object?> whereParams) {
        Execute(UpdateNonExecute(tableName, data, whereClause, whereParams));
    }

    /// <summary>
    ///     更新多条数据并立即写入。
    /// </summary>
    public void UpdateMultiple(
        string tableName,
        in IEnumerable<Dictionary<string, object?>> dataArray,
        string whereClause,
        in IEnumerable<Dictionary<string, object?>> whereParamsArray) {
        Execute(UpdateMultipleNonExecute(tableName, dataArray, whereClause, whereParamsArray));
    }

    #endregion

    #region 数据操作拓展-Delete

    /// <summary>
    ///     删除多条数据。不写入。
    /// </summary>
    public void DeleteMultipleNonExecute(
        ref SqliteCommand command,
        string tableName,
        string whereClause,
        IEnumerable<Dictionary<string, object>> whereParamsArray) {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("表名不能为空。", nameof(tableName));

        if (string.IsNullOrWhiteSpace(whereClause))
            throw new ArgumentException("WHERE 条件不能为空。", nameof(whereClause));

        int count = 0;
        int alreadyExistedParamsCount = command.Parameters.Count;
        foreach (Dictionary<string, object> whereParams in whereParamsArray) {
            count++;
            foreach ((string key, object value) in whereParams) {
                command.Parameters.AddWithValue($"@{key}[{alreadyExistedParamsCount + count}", value);
            }

            command.CommandText += $"DELETE FROM {EscapeIdentifier(tableName)} WHERE {whereClause};";
        }
    }

    /// <summary>
    ///     删除多条数据。不写入。
    /// </summary>
    public SqliteCommand DeleteMultipleNonExecute(
        string tableName,
        string whereClause,
        IEnumerable<Dictionary<string, object>> whereParamsArray) {
        SqliteCommand command = NewCommand();
        DeleteMultipleNonExecute(ref command, tableName, whereClause, whereParamsArray);
        return command;
    }

    /// <summary>
    ///     删除一条数据。不写入。
    /// </summary>
    public void DeleteNonExecute(
        ref SqliteCommand command,
        string tableName,
        string whereClause,
        Dictionary<string, object> whereParams) {
        DeleteMultipleNonExecute(ref command, tableName, whereClause, [whereParams]);
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
    public void Delete(string tableName, string whereClause, Dictionary<string, object> whereParams) {
        Execute(DeleteNonExecute(tableName, whereClause, whereParams));
    }

    /// <summary>
    ///     删除多条数据并立即写入。
    /// </summary>
    public void DeleteMultiple(
        string tableName,
        string whereClause,
        IEnumerable<Dictionary<string, object>> whereParamsArray) {
        Execute(DeleteMultipleNonExecute(tableName, whereClause, whereParamsArray));
    }

    #endregion

    #region 事务支持

    private SqliteTransaction? _transaction;

    /// <summary>
    ///     开始事务
    /// </summary>
    public void BeginTransaction() {
        if (_transaction != null)
            throw new InvalidOperationException("事务已在进行中");

        _transaction = _connection.BeginTransaction();
    }

    /// <summary>
    ///     提交事务
    /// </summary>
    public void Commit() {
        if (_transaction == null)
            throw new InvalidOperationException("没有活动的事务");

        _transaction.Commit();
        _transaction.Dispose();
        _transaction = null;
    }

    /// <summary>
    ///     回滚事务
    /// </summary>
    public void Rollback() {
        if (_transaction == null)
            throw new InvalidOperationException("没有活动的事务");

        _transaction.Rollback();
        _transaction.Dispose();
        _transaction = null;
    }

    /// <summary>
    ///     执行 SQL 命令（用于 UPDATE/DELETE 等操作）
    /// </summary>
    public void Execute(string sql, Dictionary<string, object>? parameters = null) {
        if (string.IsNullOrWhiteSpace(sql))
            throw new ArgumentException("SQL 语句不能为空。", nameof(sql));

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;

        if (parameters != null) {
            foreach ((string key, object value) in parameters) {
                cmd.Parameters.AddWithValue($"@{key}", value);
            }
        }

        cmd.Transaction = _transaction;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    ///     执行 SQL 命令（用于 UPDATE/DELETE 等操作）
    /// </summary>
    public void Execute(SqliteCommand command) {
        command.ExecuteNonQuery();
        command.Dispose();
    }

    #endregion
}

public static class ParseHelpers {
    public static T? TryParse<T>(Dictionary<string, object?> dict, string key) where T : struct, IParsable<T> {
        if (!dict.TryGetValue(key, out object? value) || value is null)
            return null;
        if (value is T rst)
            return rst;
        T.TryParse(value.ToString(), null, out T result);
        return result;
    }

    public static string? TryParse(Dictionary<string, object?> dict, string key) {
        if (!dict.TryGetValue(key, out object? value) || value is not string valueString)
            return null;
        return valueString;
    }
}