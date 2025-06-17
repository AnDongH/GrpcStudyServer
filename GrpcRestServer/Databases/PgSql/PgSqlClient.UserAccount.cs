using Dapper;
using GrpcRestServer.Databases.PgSql.Type;
using Npgsql;

namespace GrpcRestServer.Databases.PgSql;

public partial class PgSqlClient
{
    public async Task<UserAccount> GetUserAccount(string id)
    {
        using var conn = new NpgsqlConnection(_dbOptions.PgSql);
        conn.Open();
        var sql = "SELECT * FROM user_account WHERE id = @Id";
        var result = await conn.QuerySingleOrDefaultAsync<UserAccount>(sql, new { Id = id });
        return result;
    }
    
    public async Task<UserAccount> GetUserAccount(long suid)
    {
        using var conn = new NpgsqlConnection(_dbOptions.PgSql);
        conn.Open();
        var sql = "SELECT * FROM user_account WHERE suid = @Suid";
        var result = await conn.QuerySingleOrDefaultAsync<UserAccount>(sql, new { Suid = suid });
        return result;
    }

    
    public async Task<bool> AddUserAccount(UserAccount userAccount)
    {
        using var conn = new NpgsqlConnection(_dbOptions.PgSql);
        conn.Open();
        var sql = "INSERT INTO user_account (suid, id, hashed_password, salt_value, last_login) VALUES (@Suid, @Id, @HashedPassword, @SaltValue, @LastLogin) ON CONFLICT DO NOTHING";
        var res = await conn.ExecuteAsync(sql, userAccount);
        return res > 0;
    }
    
    public async Task<bool> UpdateUserAccount(UserAccount userAccount)
    {
        using var conn = new NpgsqlConnection(_dbOptions.PgSql);
        conn.Open();
        var sql = "UPDATE user_account SET hashed_password = @HashedPassword, salt_value = @SaltValue, last_login = @LastLogin WHERE id = @Id";
        var res = await conn.ExecuteAsync(sql, userAccount);
        return res > 0;
    }
    
    public async Task<bool> DeleteUserAccount(string id)
    {
        using var conn = new NpgsqlConnection(_dbOptions.PgSql);
        conn.Open();
        var sql = "DELETE FROM user_account WHERE id = @Id";
        var res = await conn.ExecuteAsync(sql, new { Id = id });
        return res > 0;
    }
    
    public async Task<bool> DeleteUserAccount(long suid)
    {
        using var conn = new NpgsqlConnection(_dbOptions.PgSql);
        conn.Open();
        var sql = "DELETE FROM user_account WHERE suid = @Suid";
        var res = await conn.ExecuteAsync(sql, new { Suid = suid });
        return res > 0;
    }
    
    public async Task<bool> UpsertUserAccount(UserAccount userAccount)
    {
        using var conn = new NpgsqlConnection(_dbOptions.PgSql);
        conn.Open();
        var sql = "INSERT INTO user_account (suid, id, hashed_password, salt_value, last_login) VALUES (@Suid, @Id, @HashedPassword, @SaltValue, @LastLogin) ON CONFLICT (suid) DO UPDATE SET hashed_password = @HashedPassword, salt_value = @SaltValue, last_login = @LastLogin";
        var res = await conn.ExecuteAsync(sql, userAccount);
        return res > 0;
    }
}