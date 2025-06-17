namespace GrpcStudyServer;

public interface ITokenProvider
{
    (string suid, string authToken) GetToken();
}

public class AuthTokenProvider : ITokenProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    
    public AuthTokenProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }
    
    public (string suid, string authToken) GetToken()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null) return (null, null);
        
        string suid = null;
        string authToken = null;

        if (httpContext.Request.Headers.TryGetValue("suid", out var item1))
        {
            suid = item1;
        }
        
        if (httpContext.Request.Headers.TryGetValue("Authorization", out var item2))
        {
            authToken = item2;
        }  
        
        return (suid, authToken);
    }
}