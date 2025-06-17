using GrpcRestServer;
using Microsoft.AspNetCore.Mvc;
using GrpcRestServer.Databases.PgSql;
using GrpcRestServer.Databases.PgSql.Type;
using GrpcRestServer.Databases.Redis;
using GrpcRestServer.Protocols;

namespace SignalRStudyServer.Services;

public class LoginService
{
    private readonly RedisClient _redisClient;
    private readonly ILogger<LoginService> _logger;
    private readonly PgSqlClient _pgSqlClient;
    
    public LoginService(RedisClient redisClient, ILogger<LoginService> logger, PgSqlClient pgSqlClient)
    {
        _redisClient = redisClient;
        _logger = logger;
        _pgSqlClient = pgSqlClient;
    }

    public async Task<(string authToken, long suid)> LoginAsync(LoginReq req)
    {
        // Check if the user exists in the database
        var user = await _pgSqlClient.GetUserAccount(req.Id);
        if (user == null)
        {
            return (null, 0);
        }
        
        if (Security.MakeHashingPassWord(user.SaltValue, req.Password) != user.HashedPassword)
        {
            return (null, 0);
        }
        
        var authToken = Security.CreateAuthToken();

        await _redisClient.RegistUserAsync(authToken, user.Suid, req.IsAdmin);

        return (authToken, user.Suid);
    }

    public async Task<bool> RegisterAsync(string id, string password)
    {
        var user = new UserAccount();
        var salt = Security.SaltString();
        var hashedPassword = Security.MakeHashingPassWord(salt, password);

        user.Suid = Security.CreateSUID();
        user.Id = id;
        user.HashedPassword = hashedPassword;
        user.SaltValue = salt;
        user.CreatedAt = DateTime.UtcNow;
        
        return await _pgSqlClient.AddUserAccount(user);
    }

    public async Task<bool> AuthAsync(string authToken, long suid)
    {
        var user = await _redisClient.GetUserAsync(suid);
        if (!user.flag || user.data.AuthToken != authToken) return false;
        return true;
    }
}