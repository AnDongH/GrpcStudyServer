namespace GrpcRestServer.Protocols;

public class RegisterReq : ProtocolReq
{
    public string Id { get; set; }
    public string Password { get; set; }
}

public class RegisterRes : ProtocolRes
{
    
}