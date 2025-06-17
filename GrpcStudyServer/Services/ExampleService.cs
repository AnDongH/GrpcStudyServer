using System.Runtime.InteropServices;
using System.Threading.Channels;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace GrpcStudyServer.Services;

// 허브와 마찬가지로 호출이 종료된 후 변수를 계속해서 사용해서는 안됨.
// 즉 백그라운드 Task를 실행할 때 rpc 호출이 종료된 후에도 계속해서 실행되는 작업을 만들지 말 것.
public class ExampleService : global::ExampleService.ExampleServiceBase
{
    public override Task<ExampleResponse> UnaryCall(ExampleRequest request, ServerCallContext context)
    {
        var response = new ExampleResponse();
        return Task.FromResult(response);
    }
    
    // 꼭 요청으로만 데이터를 보내지 않아도 헤더를 사용할 수 있음
    public override Task<ExampleResponse> UnaryHeaderCall(ExampleRequest request, ServerCallContext context)
    {
        var userAgent = context.RequestHeaders.GetValue("user-agent");
        return Task.FromResult(new ExampleResponse());
    }
    
    
    // 참고로 읽고 쓰는 거는 동시에 해도 되지만,
    // 여러 스레드에서 동시에 읽거나, 동시에 쓰는건 하면 안됨 (IAsyncStreamReader, IServerStreamWriter)
    public override async Task StreamingFromServer(ExampleRequest request, IServerStreamWriter<ExampleResponse> responseStream, ServerCallContext context)
    {
        for (var i = 0; i < 5; i++)
        {
            await responseStream.WriteAsync(new ExampleResponse());
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
        
        // 이런식으로도 가능
        // while (!context.CancellationToken.IsCancellationRequested)
        // {
        //     await responseStream.WriteAsync(new ExampleResponse());
        //     await Task.Delay(TimeSpan.FromSeconds(1), context.CancellationToken);
        // }
    }
    
    // 클라이언트로부터 메시지를 수신하지 않고도 시작됨
    public override async Task<ExampleResponse> StreamingFromClient(IAsyncStreamReader<ExampleRequest> requestStream, ServerCallContext context)
    {
        await foreach (var message in requestStream.ReadAllAsync())
        {
            // ...
        }
        return new ExampleResponse();
    }
    
    // 양방향 스트리밍. 동시에 읽고 쓰는것도 가능함
    public override async Task StreamingBothWays(IAsyncStreamReader<ExampleRequest> requestStream, IServerStreamWriter<ExampleResponse> responseStream, ServerCallContext context)
    {
        // Read requests in a background task.
        var readTask = Task.Run(async () =>
        {
            await foreach (var message in requestStream.ReadAllAsync())
            {
                // Process request.
            }
        });
        
        // Send responses until the client signals that it is complete.
        while (!readTask.IsCompleted)
        {
            await responseStream.WriteAsync(new ExampleResponse());
            await Task.Delay(TimeSpan.FromSeconds(1), context.CancellationToken);
        }
    }
    
    public override Task<DataResponse> DownloadResults(DataRequest request, ServerCallContext context)
    {
        var data = new byte[] {1,2,3,4,5};
        var response = new DataResponse();
        
        // bytes 형식은 이렇게 protobuf에서 제공하는 ByteString을 사용해야함
        // 근데 이거는 복사, 새로운 메모리 할당 오버헤드가 있음
        response.Data = ByteString.CopyFrom(data);
        
        // 이거는 오버헤드 없음. 대신 사용되는 동안 data의 값이 바뀌면 안됨(복사되지 않고 참조인듯?)
        response.Data = UnsafeByteOperations.UnsafeWrap(data);
        
        // 읽을 때는 이것들로 읽으면 됨~
        // var data1 = response.Data.ToByteArray();
        // var data2 = response.Data.Span; // 당연한 얘기지만 이게 효율적임
        // var data3 = response.Data.Memory;
        
        // 혹시나 byte[]를 써야만 하는 경우라면
        // if (MemoryMarshal.TryGetArray(response.Data.Memory, out var segment))
        // {
        //     // segment 사용
        // }
        // else
        // {
        //     // 이거 사용
        //     response.Data.ToByteArray();
        // }
        
        return Task.FromResult(response);
    }
}