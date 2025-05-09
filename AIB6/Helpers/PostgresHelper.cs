using System;
using System.Threading.Tasks;
using Npgsql;

namespace AIB6.Helpers
{
    public static class PostgresHelper
    {
        public static async Task InsertLetterAsync(string filename, string letterType, DateTime timestamp, bool favorite, bool hidden)
        {
            var connStr = Program.AppSettings.ConnectionStrings.Postgres;
            await using var conn = new NpgsqlConnection(connStr);
            await conn.OpenAsync();

            await using var cmd = new NpgsqlCommand("CALL insert_letter(@filename, @letter_type, @timestamp, @favorite, @hidden)", conn);
            cmd.Parameters.AddWithValue("filename", filename);
            cmd.Parameters.AddWithValue("letter_type", letterType);
            cmd.Parameters.AddWithValue("timestamp", timestamp);
            cmd.Parameters.AddWithValue("favorite", favorite);
            cmd.Parameters.AddWithValue("hidden", hidden);

            await cmd.ExecuteNonQueryAsync();
        }
    }
}