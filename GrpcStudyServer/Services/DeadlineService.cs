using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using GrpcStudyServer.Interceptors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GrpcStudyServer.Services;

[Authorize]
public class DeadlineService : Deadline.DeadlineBase
{
    private readonly Greeter.GreeterClient _greeterClient;
    
    public DeadlineService(Greeter.GreeterClient greeterClient)
    {
        _greeterClient = greeterClient;
    }
    
    public override async Task<DeadlineResponse> SayHello(DeadlineRequest request, ServerCallContext context)
    {
        await Task.Delay(2000, context.CancellationToken);
        return new DeadlineResponse() {Message = request.Name + " from DeadlineService"};
    }

    public override async Task<DeadlineResponse> SayHello2(DeadlineRequest request, ServerCallContext context)
    {
        // 만약 Grpc Client Factory의 EnableCallContextPropagation(); 설정을 안한다면 이렇게 손수 전파해줘야함
        // 참고로 EnableCallContextPropagation는 Grpc 서비스 내부에서만 사용 가능. 일반적으로 사용할땐 불가능
        var reply = await _greeterClient.SayHelloAsync(new HelloRequest {Name = request.Name}, deadline: context.Deadline, cancellationToken: context.CancellationToken);
        
        return new DeadlineResponse() {Message = reply.Message + " from DeadlineService"};
    }
}