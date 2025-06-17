using Grpc.Core;
using Microsoft.AspNetCore.Authorization;

namespace GrpcStudyServer.Services;

[Authorize]
public class StreamingAndHeaderService : StreamingAndHeader.StreamingAndHeaderBase
{
    public override async Task ServerStreaming(ServerStreamRequest request, IServerStreamWriter<ServerStreamResponse> responseStream, ServerCallContext context)
    {
        for (var i = 0; i < request.StreamCount; i++)
        {
            var response = new ServerStreamResponse
            {
                Num = Random.Shared.Next()
            };
            await Task.Delay(100);
            await responseStream.WriteAsync(response);
        }
    }

    public override async Task<ClientStreamResponse> ClientStreaming(IAsyncStreamReader<ClientStreamRequest> requestStream, ServerCallContext context)
    {
        var response = new ClientStreamResponse();
        try
        {
            await foreach (var request in requestStream.ReadAllAsync())
            {
                // 요청을 처리하는 로직
                Console.WriteLine(request.Num);
            }
            response.IsSuccess = true;
            return response;
        }
        catch (Exception)
        {
            response.IsSuccess = false;
            return response;
        }
    }

    public override async Task BidirectionalStreaming(IAsyncStreamReader<ClientStreamRequest> requestStream, IServerStreamWriter<ServerStreamResponse> responseStream,
        ServerCallContext context)
    {
        // Read requests in a background task.
        var readTask = Task.Run(async () =>
        {
            await foreach (var message in requestStream.ReadAllAsync())
            {
                await Task.Delay(100);
                Console.WriteLine(message.Num);
            }
        });

        // Send responses until the client signals that it is complete.
        while (!readTask.IsCompleted)
        {
            var response = new ServerStreamResponse
            {
                Num = Random.Shared.Next()
            };
            await Task.Delay(100);
            await responseStream.WriteAsync(response);
        }
    }

    public override async Task<HeaderResponse> Header(HeaderRequest request, ServerCallContext context)
    {
        var value = context.RequestHeaders.GetValue("request-header");
        Console.WriteLine(value);
        
        var headers = new Metadata
        {
            { "response-header", "response!" },
        };
        
        await context.WriteResponseHeadersAsync(headers);
        return new HeaderResponse();
    }

    public override async Task<HeaderResponse> Trailer(HeaderRequest request, ServerCallContext context)
    {
        if (Random.Shared.Next() % 2 == 0)
        {
            await Task.Delay(100);
            context.ResponseTrailers.Add("response-trailer", "response!");
            return new HeaderResponse();
        }

        throw new RpcException(new Grpc.Core.Status(StatusCode.PermissionDenied, "Permission denied"), 
            new Metadata(){{"response-trailer", "error!"}});
    }
}