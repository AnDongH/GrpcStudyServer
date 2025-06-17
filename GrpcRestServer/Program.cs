using Grpc.Core;
using Grpc.Net.Client.Configuration;
using GrpcRestServer.Authentication;
using GrpcRestServer.Authorizations;
using GrpcRestServer.Databases;
using GrpcRestServer.Databases.PgSql;
using GrpcRestServer.Databases.Redis;
using GrpcRestServer.Interceptors;
using Microsoft.AspNetCore.SignalR;
using Microsoft.OpenApi.Models;
using SignalRStudyServer.Services;

namespace GrpcRestServer;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer(); // API 탐색 활성화
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Grpc Test API", Version = "v1" });
            c.AddSecurityDefinition("Suid", new OpenApiSecurityScheme
            {
                Description = "Suid Header",
                Name = "suid",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
            });
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "Authorization header",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Suid"
                        }
                    },
                    new string[] {}
                },
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    new string[] {}
                }
            });
        });

        builder.Services.AddHttpContextAccessor();
        
        builder.Services.Configure<DBOptions>(builder.Configuration.GetSection(DBOptions.DbConfig));
        builder.Services.AddTransient<PgSqlClient>();
        builder.Services.AddSingleton<RedisClient>();
        builder.Services.AddTransient<LoginService>();
        builder.Services.AddSingleton<IUserIdProvider, NameBasedUserIdProvider>();

        builder.Services.AddTransient<TokenPropagationInterceptor>();

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
        
        // gRPC Client Factory 설정. 명명된 클라이언트 사용
        builder.Services.AddGrpcClient<Greeter.GreeterClient>("Greeter", o =>
        {
            o.Address = new Uri("https://localhost:7110");
        }).ConfigureChannel(o =>
        {
            o.ServiceConfig = new ServiceConfig { MethodConfigs = { defaultMethodConfig } };
            o.HttpHandler = new SocketsHttpHandler()
            {
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(10),
                KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                EnableMultipleHttp2Connections = true
            };
        }).AddInterceptor<TokenPropagationInterceptor>(); //.EnableCallContextPropagation(); // gRPC 서비스 내에서 gRPC 호출 시 CallContext 전파 활성화 => deadline, cancellation token 등 전파
        // 근데 gRPC 요청에 대해서만 사용이 가능함. 일반적인 컨트롤러에서 gRPC 호출 시에는 사용 불가
        
        builder.Services.AddGrpcClient<CollectionType.CollectionTypeClient>("CollectionType", o =>
        {
            o.Address = new Uri("https://localhost:7110");
        }).ConfigureChannel(o =>
        {
            o.ServiceConfig = new ServiceConfig {MethodConfigs = { defaultMethodConfig }};
            o.HttpHandler = new SocketsHttpHandler()
            {
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(10),
                KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                EnableMultipleHttp2Connections = true
            };
        }).AddInterceptor<TokenPropagationInterceptor>();
        
        builder.Services.AddGrpcClient<Flexible.FlexibleClient>("Flexible", o =>
        {
            o.Address = new Uri("https://localhost:7110");
        }).ConfigureChannel(o =>
        {
            o.ServiceConfig = new ServiceConfig {MethodConfigs = { defaultMethodConfig }};
            o.HttpHandler = new SocketsHttpHandler()
            {
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(10),
                KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                EnableMultipleHttp2Connections = true
            };
        }).AddInterceptor<TokenPropagationInterceptor>(); ;
        
        builder.Services.AddGrpcClient<StreamingAndHeader.StreamingAndHeaderClient>("StreamingAndHeader", o =>
        {
            o.Address = new Uri("https://localhost:7110");
        }).ConfigureChannel(o =>
        {
            o.ServiceConfig = new ServiceConfig {MethodConfigs = { defaultMethodConfig }};
            o.HttpHandler = new SocketsHttpHandler()
            {
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(10),
                KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                EnableMultipleHttp2Connections = true
            };
        }).AddInterceptor<TokenPropagationInterceptor>(); ;
        
        builder.Services.AddGrpcClient<Deadline.DeadlineClient>("Deadline", o =>
        {
            o.Address = new Uri("https://localhost:7110");
        }).ConfigureChannel(o =>
        {
            o.ServiceConfig = new ServiceConfig {MethodConfigs = { defaultMethodConfig }};
            o.HttpHandler = new SocketsHttpHandler()
            {
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(10),
                KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                EnableMultipleHttp2Connections = true
            };
        }).AddInterceptor<TokenPropagationInterceptor>(); ;
        
        builder.Services.AddGrpcClient<ErrorHandle.ErrorHandleClient>("ErrorHandle", o =>
        {
            o.Address = new Uri("https://localhost:7110");
        }).ConfigureChannel(o =>
        {
            o.ServiceConfig = new ServiceConfig {MethodConfigs = { defaultMethodConfig }};
            o.HttpHandler = new SocketsHttpHandler()
            {
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(10),
                KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                EnableMultipleHttp2Connections = true
            };
        }).AddInterceptor<TokenPropagationInterceptor>();
        
        var app = builder.Build();
        
        app.UseAuthentication();
        app.UseAuthorization();
        
        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Grpc Test API v1"));
        app.MapControllers();
        
        app.Run();
    }
}