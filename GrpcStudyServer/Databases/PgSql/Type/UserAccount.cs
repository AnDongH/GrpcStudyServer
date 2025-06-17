namespace GrpcStudyServer.Databases.PgSql.Type;

public class UserAccount
{
    public long Suid { get; set; }
    public string Id { get; set; }
    public string HashedPassword { get; set; }
    public string SaltValue { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastLogin { get; set; }
}