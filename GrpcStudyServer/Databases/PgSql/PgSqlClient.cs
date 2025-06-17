using System.Data;
using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using GrpcStudyServer.Databases.PgSql.Type;

namespace GrpcStudyServer.Databases.PgSql;

public partial class PgSqlClient
{
    private readonly DBOptions _dbOptions;
    private readonly ILogger<PgSqlClient> _logger;
    
    static PgSqlClient()
    {
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
    }
    
    public PgSqlClient(IOptions<DBOptions> dbOptions, ILogger<PgSqlClient> logger)
    {
        _dbOptions = dbOptions.Value;
        _logger = logger;
    }
}