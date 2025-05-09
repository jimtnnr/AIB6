using Npgsql;
using AIB6.Helpers;
using System;
using System.Threading.Tasks;

namespace AIB6.Helpers
{
    public static class PostgresHelper
    {
        private const string ConnectionString = "Host=localhost;Username=postgres;Password=Kitten77;Database=postgres";

        public static async Task InsertLetterAsync(string filename, string letterType, DateTime timestamp, bool favorite, bool hidden)
        {
            await using var conn = new NpgsqlConnection(ConnectionString);
            await conn.OpenAsync();

            await using var cmd = new NpgsqlCommand("CALL insert_letter(@filename, @type, @timestamp, @favorite, @hidden)", conn);
            cmd.Parameters.AddWithValue("filename", filename);
            cmd.Parameters.AddWithValue("type", letterType);
            cmd.Parameters.AddWithValue("timestamp", timestamp);
            cmd.Parameters.AddWithValue("favorite", favorite);
            cmd.Parameters.AddWithValue("hidden", hidden);

            await cmd.ExecuteNonQueryAsync();
        }
    }
}