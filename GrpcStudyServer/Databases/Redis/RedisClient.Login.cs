using CloudStructures;
using CloudStructures.Structures;
using GrpcStudyServer.Databases.Redis.Type;

namespace GrpcStudyServer.Databases.Redis;

public partial class RedisClient : IDisposable
{
    public async Task<bool> RegistUserAsync(string authToken, long suid, bool isAdmin) {

        string key = MemoryDbKeyMaker.MakeUserDataKey(suid.ToString());

        LoginData user = new() {
            AuthToken = authToken,
            Suid = suid,
            IsAdmin = isAdmin,
            IssuedTime = DateTimeOffset.UtcNow
        };

        var expiryTime = TimeSpan.FromDays(1);
        RedisString<LoginData> redis = new(_redisConn, key, expiryTime);

        return await redis.SetAsync(user, expiryTime);
    }
    
    public async Task<(bool flag, LoginData data, TimeSpan? expTime)> GetUserAsync(long suid) {

        var userIDKey = MemoryDbKeyMaker.MakeUserDataKey(suid.ToString());

        RedisString<LoginData> redis = new(_redisConn, userIDKey, null);
        RedisResultWithExpiry<LoginData> user = await redis.GetWithExpiryAsync();
        
        if (!user.HasValue) {

            _logger.LogError($"[GetUserAsync] UID = {userIDKey}, ErrorMessage = Not Assigned User, RedisString get Error");
            return (false, null, null);
            
        }
        
        return (true, user.Value,  user.Expiry);
    }
}