namespace GrpcStudyServer.Databases.Redis.Type;

public class LoginData
{
    public string AuthToken { get; set; }
    public long Suid { get; set; }
    public bool IsAdmin { get; set; }
    public DateTimeOffset IssuedTime { get; set; }
}