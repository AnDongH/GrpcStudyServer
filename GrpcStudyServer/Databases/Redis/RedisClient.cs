using CloudStructures;
using CloudStructures.Structures;
using Microsoft.Extensions.Options;
using GrpcStudyServer.Databases.Redis.Type;

namespace GrpcStudyServer.Databases.Redis;

public partial class RedisClient : IDisposable
{
    public RedisConnection _redisConn;
    private readonly ILogger<RedisClient> _logger;

    public RedisClient(ILogger<RedisClient> logger, IOptions<DBOptions> dbConfig)
    {
        _logger = logger;

        RedisConfig config = new("default", dbConfig.Value.Redis);
        _redisConn = new RedisConnection(config);
    }

    public void Dispose()
    {
        _redisConn?.GetConnection().Dispose();
    }
}

public static class MemoryDbKeyMaker {
    public static string MakeUserDataKey(string id) => "UID_" + id;
    public static string MakeClientKeyKey(string kuid) => "KUID_" + kuid;
    public static string MakeMasterCacheKey(string key) => "MASTER_DB_" + key;
}