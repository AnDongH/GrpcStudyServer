using Grpc.Core;
using Grpc.Core.Interceptors;

namespace GrpcRestServer.Interceptors;

// Grpc에서 미들웨어 같은거임.
// 클라이언트 인터셉터와 서버 인터셉터 개념이 있음
// 클라이언트 인터셉터 -> 클라이언트가 서버에 요청을 보낼 때, 요청을 가로채서 수정하거나 추가 작업을 수행할 수 있음
// 서버 인터셉터 -> 서버가 클라이언트의 요청을 처리하기 전에 요청을 가로채서 수정하거나 추가 작업을 수행할 수 있음
public class TokenPropagationInterceptor : Interceptor // Interceptor 상속받기
{
    // 인터셉터에서 http context를 사용하기 위해서는 IHttpContextAccessor를 주입받아야 함.
    private IHttpContextAccessor _httpContextAccessor;
    
    public TokenPropagationInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }
    
    // AsyncUnaryCall는 비동기 단항 호출에 대한 인터셉터 메서드
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var headers = context.Options.Headers ?? new Metadata();
        AddForwardedHeaders(headers);
        
        var newContext = new ClientInterceptorContext<TRequest, TResponse>(
            context.Method,
            context.Host,
            context.Options.WithHeaders(headers));
        
        // continuation은 다음 인터셉터나 기본 호출을 실행하는 델리게이트
        return continuation(request, newContext);
        
        // 만약 반환값을 만들어서 리턴한다면 다음 인터셉터로 넘기지 않고 끝남.
        // 해당 패턴은 사용자 정의 에러를 리턴하고 싶다던가, 로깅이 필요하던가 등에 쓰임
        // var call = continuation(request, context);
        //
        // return new AsyncUnaryCall<TResponse>(
        //     HandleResponse(call.ResponseAsync),
        //     call.ResponseHeadersAsync,
        //     call.GetStatus,
        //     call.GetTrailers,
        //     call.Dispose);
    }
    
    private async Task<TResponse> HandleResponse<TResponse>(Task<TResponse> inner)
    {
        try
        {
            return await inner;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Custom error", ex);
        }
    }

    // BlockingUnaryCall는 동기 단항 호출에 대한 인터셉터 메서드
    public override TResponse BlockingUnaryCall<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context,
        BlockingUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var headers = context.Options.Headers ?? new Metadata();
        AddForwardedHeaders(headers);
        
        var newContext = new ClientInterceptorContext<TRequest, TResponse>(
            context.Method,
            context.Host,
            context.Options.WithHeaders(headers));

        return continuation(request, newContext);
    }

    // AsyncClientStreamingCall는 비동기 클라이언트 스트리밍 호출에 대한 인터셉터 메서드
    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(ClientInterceptorContext<TRequest, TResponse> context,
        AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        var headers = context.Options.Headers ?? new Metadata();
        AddForwardedHeaders(headers);
        
        var newContext = new ClientInterceptorContext<TRequest, TResponse>(
            context.Method,
            context.Host,
            context.Options.WithHeaders(headers));

        return continuation(newContext);
    }
    
    // AsyncServerStreamingCall는 비동기 서버 스트리밍 호출에 대한 인터셉터 메서드
    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context,
        AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        var headers = context.Options.Headers ?? new Metadata();
        AddForwardedHeaders(headers);
        
        var newContext = new ClientInterceptorContext<TRequest, TResponse>(
            context.Method,
            context.Host,
            context.Options.WithHeaders(headers));

        return continuation(request, newContext);
    }
    
    // AsyncDuplexStreamingCall는 비동기 양방향 스트리밍 호출에 대한 인터셉터 메서드
    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(ClientInterceptorContext<TRequest, TResponse> context,
        AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        var headers = context.Options.Headers ?? new Metadata();
        AddForwardedHeaders(headers);
        
        var newContext = new ClientInterceptorContext<TRequest, TResponse>(
            context.Method,
            context.Host,
            context.Options.WithHeaders(headers));

        return continuation(newContext);
    }

    private void AddForwardedHeaders(Metadata metadata)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null) return;

        var headersToForward = new[] { "suid", "Authorization" };
        
        foreach (var headerName in headersToForward)
        {
            if (httpContext.Request.Headers.TryGetValue(headerName, out var values))
            {
                metadata.Add(headerName, values.FirstOrDefault());
            }
        }
    }
}