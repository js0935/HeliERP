// ════════════════════════════════════════════════════════
// 軟體屬名：禾秝軟體開發團隊
// 代碼：洪俊士
// 版本：1.0.0
// ════════════════════════════════════════════════════════
using System.Data;
using System.IO;
using Microsoft.Data.Sqlite;

namespace HeliERP.Data;

/// <summary>
/// SQLite 資料存取核心：連線管理、查詢、執行、交易。
/// 全部查詢一律參數化，避免 SQL 注入；支援區網 UNC 路徑資料庫。
/// </summary>
public static class DbManager
{
    private static string? _databasePath;

    /// <summary>目前資料庫路徑</summary>
    public static string DatabasePath
    {
        get => _databasePath ?? DbConfig.DefaultDbPath();
        set => _databasePath = value;
    }

    /// <summary>建立並開啟連線</summary>
    public static SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection($"Data Source={DatabasePath}");
        conn.Open();
        return conn;
    }

    /// <summary>建立參數（名稱不需加 @ 前綴）</summary>
    public static SqliteParameter Param(string name, object? value)
    {
        var p = new SqliteParameter(name, value ?? DBNull.Value);
        return p;
    }

    /// <summary>執行查詢，回傳 DataTable</summary>
    public static DataTable QueryTable(string sql, params SqliteParameter[] parameters)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddRange(parameters);
        var dt = new DataTable();
        using (var reader = cmd.ExecuteReader())
        {
            dt.Load(reader);
        }
        return dt;
    }

    /// <summary>執行查詢，回傳第一列第一欄值（無結果回傳 null）</summary>
    public static object? QueryScalar(string sql, params SqliteParameter[] parameters)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddRange(parameters);
        var result = cmd.ExecuteScalar();
        return result is DBNull ? null : result;
    }

    /// <summary>執行非查詢指令，回傳受影響列數</summary>
    public static int ExecuteNonQuery(string sql, params SqliteParameter[] parameters)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddRange(parameters);
        return cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 在單一交易中執行多個動作；任一動作失敗即全部復原。
    /// </summary>
    public static void ExecuteTransaction(Action<SqliteTransaction> action)
    {
        using var conn = OpenConnection();
        using var tx = conn.BeginTransaction();
        try
        {
            action(tx);
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    /// <summary>在既有交易中建立指令</summary>
    public static SqliteCommand CreateCommand(SqliteTransaction tx, string sql,
        params SqliteParameter[] parameters)
    {
        var conn = tx.Connection ?? throw new InvalidOperationException("交易沒有連線");
        var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.Parameters.AddRange(parameters);
        return cmd;
    }

    /// <summary>在已開啟的連線上建立指令（用於 ExecuteImmediateTransaction 的 action 內）</summary>
    public static SqliteCommand CreateCommand(SqliteConnection conn, string sql,
        params SqliteParameter[] parameters)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddRange(parameters);
        return cmd;
    }

    /// <summary>
    /// 以 BEGIN IMMEDIATE 執行交易：先取得 SQLite 寫入鎖再動作，
    /// 將「取號 + 寫入」序列化，避免並發取到相同單號/序號。
    /// 任一動作失敗即全部 ROLLBACK。action 接收已開交易的連線，
    /// 內部請用 CreateCommand(conn, ...) 建立指令。
    /// </summary>
    public static void ExecuteImmediateTransaction(Action<SqliteConnection> action)
    {
        using var conn = OpenConnection();
        using (var begin = conn.CreateCommand())
        {
            begin.CommandText = "BEGIN IMMEDIATE";
            begin.ExecuteNonQuery();
        }
        try
        {
            action(conn);
            using var commit = conn.CreateCommand();
            commit.CommandText = "COMMIT";
            commit.ExecuteNonQuery();
        }
        catch
        {
            using var rollback = conn.CreateCommand();
            rollback.CommandText = "ROLLBACK";
            rollback.ExecuteNonQuery();
            throw;
        }
    }

    /// <summary>
    /// 以 VACUUM INTO 建立一致性資料庫快照備份（不需停止服務即可取得可開機備份）。
    /// </summary>
    public static void BackupTo(string targetPath)
    {
        var dir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"VACUUM INTO '{targetPath.Replace("'", "''")}'";
        cmd.ExecuteNonQuery();
    }

    /// <summary>以備份檔覆蓋目前資料庫（請先確認目前資料庫沒有其他連線占用）</summary>
    public static void RestoreFrom(string backupPath)
    {
        if (!File.Exists(backupPath))
            throw new FileNotFoundException("找不到備份檔：", backupPath);
        File.Copy(backupPath, DatabasePath, true);
    }
}
