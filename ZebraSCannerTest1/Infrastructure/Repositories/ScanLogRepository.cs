using Microsoft.Data.Sqlite;
using ZebraSCannerTest1.Core.Enums;
using ZebraSCannerTest1.Core.Interfaces;
using ZebraSCannerTest1.Core.Models;
using ZebraSCannerTest1.Data;

namespace ZebraSCannerTest1.Infrastructure.Repositories
{
    public class ScanLogRepository : IScanLogRepository
    {
        private readonly Dictionary<InventoryMode, SqliteConnection> _cachedConnections = new();

        private static string GetTable(InventoryMode mode)
            => mode == InventoryMode.Loots ? "LootsScanLogs" : "ScanLogs";

        // ✅ Cached connection getter
        private SqliteConnection GetConnection(InventoryMode mode)
        {
            if (_cachedConnections.TryGetValue(mode, out var existing) && existing.State == System.Data.ConnectionState.Open)
                return existing;

            var conn = DatabaseInitializer.GetConnection(mode);
            _cachedConnections[mode] = conn;
            return conn;
        }

        public async Task InsertAsync(ScanLog log, InventoryMode mode = InventoryMode.Standard)
        {
            var table = GetTable(mode);
            using var cmd = GetConnection(mode).CreateCommand();

            if (mode == InventoryMode.Loots)
            {
                cmd.CommandText = $@"
                    INSERT INTO {table}
                    (Barcode, Was, IncrementBy, IsValue, UpdatedAt, Section, IsManual, Product_id, Box_Id)
                    VALUES ($b, $w, $i, $v, $u, $s, $m, $pid, $box)";
                cmd.Parameters.AddWithValue("$box", log.Box_Id ?? (object)DBNull.Value);
            }
            else
            {
                cmd.CommandText = $@"
                    INSERT INTO {table}
                    (Barcode, Was, IncrementBy, IsValue, UpdatedAt, Section, IsManual, Product_id)
                    VALUES ($b, $w, $i, $v, $u, $s, $m, $pid)";
            }

            cmd.Parameters.AddWithValue("$b", log.Barcode);
            cmd.Parameters.AddWithValue("$w", log.Was);
            cmd.Parameters.AddWithValue("$i", log.IncrementBy);
            cmd.Parameters.AddWithValue("$v", log.IsValue);
            cmd.Parameters.AddWithValue("$u", log.UpdatedAt.ToString("o"));
            cmd.Parameters.AddWithValue("$s", log.Section ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("$m", log.IsManual ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("$pid", log.ProductId);


            Console.WriteLine($"------ Inserting → Mode={mode}, Table={table}, Barcode={log.Barcode}, Box={log.Box_Id ?? "NULL"}");

            await cmd.ExecuteNonQueryAsync();
            Console.WriteLine($"[+++✅ Inserted into {table}");

        }

        public async Task<IEnumerable<ScanLog>> GetByBarcodeAsync(string barcode, InventoryMode mode = InventoryMode.Standard)
        {
            var logs = new List<ScanLog>();
            var table = GetTable(mode);

            using var cmd = GetConnection(mode).CreateCommand();
            cmd.CommandText = $@"
                SELECT Barcode, Was, IncrementBy, IsValue, UpdatedAt, IsManual, Section, Product_id
                       {(mode == InventoryMode.Loots ? ", Box_Id" : "")}
                FROM {table}
                WHERE Barcode=$b
                ORDER BY UpdatedAt DESC";
            cmd.Parameters.AddWithValue("$b", barcode);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var log = new ScanLog
                {
                    Barcode = reader.GetString(0),
                    Was = reader.GetInt32(1),
                    IncrementBy = reader.GetInt32(2),
                    IsValue = reader.GetInt32(3),
                    UpdatedAt = DateTime.Parse(reader.GetString(4)),
                    IsManual = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                    Section = reader.IsDBNull(6) ? null : reader.GetString(6),
                    ProductId = reader.IsDBNull(7) ? 0 : reader.GetInt32(7)
                };
                if (mode == InventoryMode.Loots)
                    log.Box_Id = reader.IsDBNull(8) ? null : reader.GetString(8);

                logs.Add(log);
            }
            return logs;
        }

        public async Task<List<ScanLog>> GetLogsAsync(InventoryMode mode)
        {
            var list = new List<ScanLog>();
            var table = GetTable(mode);

            using var cmd = GetConnection(mode).CreateCommand();
            cmd.CommandText = $@"
        SELECT Id, Barcode, Was, IncrementBy, IsValue, UpdatedAt, IsManual, Section, Product_id
               {(mode == InventoryMode.Loots ? ", Box_Id" : "")}
        FROM {table}
        ORDER BY UpdatedAt ASC";   // server expects chronological order

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var log = new ScanLog
                {
                    Id = reader.GetInt32(0),
                    Barcode = reader.GetString(1),
                    Was = reader.GetInt32(2),
                    IncrementBy = reader.GetInt32(3),
                    IsValue = reader.GetInt32(4),
                    UpdatedAt = DateTime.Parse(reader.GetString(5)),
                    IsManual = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                    Section = reader.IsDBNull(7) ? null : reader.GetString(7),
                    ProductId = reader.IsDBNull(8) ? 0 : reader.GetInt32(8)

                };

                if (mode == InventoryMode.Loots)
                    log.Box_Id = reader.IsDBNull(9) ? null : reader.GetString(9);

                list.Add(log);
            }

            return list;
        }


        public async Task ClearAsync(InventoryMode mode = InventoryMode.Standard)
        {
            var table = GetTable(mode);
            using var cmd = GetConnection(mode).CreateCommand();
            cmd.CommandText = $"DELETE FROM {table}";
            await cmd.ExecuteNonQueryAsync();
        }

        // ✅ Dispose cached connections when done (optional but polite)
        public void Dispose()
        {
            foreach (var conn in _cachedConnections.Values)
            {
                try { 
                    conn.Close(); conn.Dispose(); 
                } catch(Exception ex) {
                    Console.WriteLine("Error in ScanLogRepsotory" + ex.Message);
                }
            }
            _cachedConnections.Clear();
        }
    }
}
