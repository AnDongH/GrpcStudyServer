namespace GrpcRestServer.Protocols;

public class LoginReq : ProtocolReq
{
    public string Id { get; set; }
    public string Password { get; set; }
    public bool IsAdmin { get; set; }
}

public class LoginRes : ProtocolRes
{
    public string Suid { get; set; }
    public string AuthToken { get; set; }
}