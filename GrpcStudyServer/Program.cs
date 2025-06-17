using Grpc.Core;
using Grpc.Net.Client.Configuration;
using GrpcStudyServer;
using GrpcStudyServer.Authentication;
using GrpcStudyServer.Authorizations;
using GrpcStudyServer.Databases;
using GrpcStudyServer.Databases.PgSql;
using GrpcStudyServer.Databases.Redis;
using GrpcStudyServer.Interceptors;
using GrpcStudyServer.Services;

// Grpc를 사용할 때는 큰 이진 페이로드를 가능한 만들지 않는 것이 중요하다. (85000 바이트 이상)
// 대형 개체 힙 문제가 생기기 때문이다.
// 따라서 큰 메시지를 보내야 한다면 스트리밍을 사용하거나, 메시지를 분할해서 보내는 것이 좋다.
// 그냥 가능하면 아예 그런 곳에 쓰지 말자. => asp.net core 기능을 사용하자

// 서비스가 있는 쪽은 Kestrel 엔드포인트를 구성하자
var builder = WebApplication.CreateBuilder(args);

// 앱이 데이터로 과부화 되는 것을 방지하기 위한 기능
// http2의 스트림 윈도우 크기를 조정
// 기본 스트림 창 크기인 768kb보다 큰 메시지를 받는 경우가 많으면 조정해주는게 좋음
// 연결 창 크기는 무조건 스트림 창 크기와 같거나 더 커야함
// 근데 너무 많이 키우면 메모리 잡아먹으니 주의
builder.WebHost.ConfigureKestrel(options =>
{
    var http2 = options.Limits.Http2;
    http2.InitialConnectionWindowSize = 1024 * 1024 * 2; // 2 MB
    http2.InitialStreamWindowSize = 1024 * 1024; // 1 MB
});

// 참고로 Grpc를 사용할 때는 L7 로드밸런서를 사용해야함!
// L4 로드밸런서를 사용하면 gRPC의 HTTP/2 기능을 사용할 수 없음
// 정확히 말하면 라우팅이 1개의 서버로만 감
// 따라서 L7을 사용하거나 클라이언트 에서 부하 분산을 해야함
builder.Services.AddGrpc(o =>
{
    // 보내고 받는 메시지 크기 1MB로 제한
    // 디폴트는 받는건 4MB, 보내는건 제한 X
    o.MaxReceiveMessageSize = 1 * 1024 * 1024;
    o.MaxSendMessageSize = 1 * 1024 * 1024;
});

builder.Services.AddHttpContextAccessor();

builder.Services.Configure<DBOptions>(builder.Configuration.GetSection(DBOptions.DbConfig));
builder.Services.AddTransient<PgSqlClient>();
builder.Services.AddSingleton<RedisClient>();

builder.Services.AddTransient<TokenPropagationInterceptor>();

builder.Services.AddScoped<ITokenProvider, AuthTokenProvider>();

// 채널의 메서드 설정으로, gRPC 클라이언트의 기본 재시도 정책을 설정
var defaultMethodConfig = new MethodConfig
{
    Names = { MethodName.Default }, // 기본 메서드 이름 -> 모든 메서드에 적용. 따로 적용하고싶으면 Names에 메서드 이름을 추가
    RetryPolicy = new RetryPolicy
    {
        MaxAttempts = 5,
        InitialBackoff = TimeSpan.FromSeconds(1),
        MaxBackoff = TimeSpan.FromSeconds(5),
        BackoffMultiplier = 1.5,
        RetryableStatusCodes = { StatusCode.Unavailable } // 재시도 가능한 상태 코드 설정. Unavailable 상태 코드에 대해서만 재시도
    }
};

// 이건 인터셉터를 이용한 토큰 전파 방식
// builder.Services.AddGrpcClient<Greeter.GreeterClient>(o =>
// {
//     o.Address = new Uri("https://localhost:7110");
// }).AddInterceptor<TokenPropagationInterceptor>();

// 이건 AddCallCredentials를 이용한 토큰 전파 방식 (https로만 가능함)
builder.Services.AddGrpcClient<Greeter.GreeterClient>(o =>
{
    o.Address = new Uri("https://localhost:7110");
}).AddCallCredentials((context, metadata, serviceProvider) =>
{
    var provider = serviceProvider.GetRequiredService<ITokenProvider>();
    var token = provider.GetToken();
    metadata.Add("suid", token.suid);
    metadata.Add("Authorization", token.authToken);
    return Task.CompletedTask;
}).ConfigureChannel(o =>
{
    o.ServiceConfig = new ServiceConfig{ MethodConfigs = { defaultMethodConfig }};
    
    /*
     * Http2 연결에는 하나의 연결에서 한번에 사용 가능한 최대 동시 스트림 수에 한도가 있음
     * 보통 100개정도.
     * 이게 초과되면 새로운 요청들은 큐에 대기됨
     * 이때 작업이 오래 걸리는 스트리밍 요청이 있다면 큐에 계속해서 요청이 쌓여서 응답이 오래걸림
     * 해당 옵션은 만약 한도를 넘어서면 새로운 클라이언트를 생성해도록 하는 옵션
     */
    o.HttpHandler = new SocketsHttpHandler
    {
        // 연결 유지 설정은 웬만한 경우 해주는 것이 좋음. 대신 누수 조심!
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(10),
        KeepAlivePingDelay = TimeSpan.FromSeconds(60),
        KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
        EnableMultipleHttp2Connections = true
    };
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "TestAuth";
    options.DefaultChallengeScheme = "TestAuth";
}).AddScheme<TestAuthOptions, TestAuthHandler>("TestAuth", options => { });
        
// 권한 정책 추가. 디폴트 정척 + 추가 정책
builder.Services.AddAuthorization(options => 
{
    options.AddPolicy("Admin", policy =>
    {
        policy.Requirements.Add(new AdminRequirement());
    });
});

var app = builder.Build();

// gRPC 요청도 미들웨어를 거침 =>
// 미들웨어를 통해 권한 검증이 가능하다는 소리
app.UseAuthentication();
app.UseAuthorization();

// Configure the HTTP request pipeline.
// gRPC 서비스 등록 
app.MapGrpcService<GreeterService>();
app.MapGrpcService<FlexibleService>();
app.MapGrpcService<CollectionTypeService>();
app.MapGrpcService<StreamingAndHeaderService>();
app.MapGrpcService<DeadlineService>();
app.MapGrpcService<ErrorHandleService>();

app.MapGet("/",
    () =>
        "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();