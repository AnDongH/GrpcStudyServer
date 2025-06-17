using Microsoft.AspNetCore.SignalR;

namespace GrpcRestServer;

// 이 클래스는 SignalR에서 User의 커넥션과 이름을 매핑하기 위한 클래스
public class NameBasedUserIdProvider : IUserIdProvider
{
    public string GetUserId(HubConnectionContext connection)
    {
        return connection.User.Identity?.Name;
    }
}