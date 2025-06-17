using System.Text.Json;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;

namespace GrpcClient;

class Program
{
    static GrpcChannel channel;
    
    static async Task Main(string[] args)
    {
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
        
        // 채널은 초기화 하고 재사용
        // 알아서 소켓 등을 관리해줌
        channel = GrpcChannel.ForAddress("http://localhost:5224", new GrpcChannelOptions
        {
            ServiceConfig = new ServiceConfig { MethodConfigs = { defaultMethodConfig } },
        });
        
        // await GreeterTestAsync();
        // await CollectionTypeTestAsync();
        // await FlexibleTestAsync();

        await ServerStreamingTestAsync();
        Console.WriteLine();
        await ClientStreamingTestAsync();
        Console.WriteLine();
        await BidirectionalStreamingTestAsync();
        Console.WriteLine();
        await HeaderTestAsync();
        Console.WriteLine();
        await TrailerTestAsync();
        
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
    
    static async Task GreeterTestAsync()
    {
        // gRPC 클라이언트 생성
        // 기본적으로 요청마다 그냥 생성하면 됨. 경량 개체임
        // client는 쓰레드 안전하므로 여러 쓰레드에서 동시에 사용 가능
        var channel = GrpcChannel.ForAddress("http://localhost:5224");
        var client = new Greeter.GreeterClient(channel);
        
        // 서버에 요청 보내기
        var reply = await client.SayHelloAsync(new HelloRequest { Name = "GreeterClient" });
        
        // 응답 출력
        Console.WriteLine("Greeting: " + reply.Message);
    }
    
    static async Task CollectionTypeTestAsync()
    {
        var client = new CollectionType.CollectionTypeClient(channel);
        
        // 서버에 요청 보내기
        var group = await client.GetGroupAsync(new GetGroupRequest());
        
        // 응답 출력
        Console.WriteLine("Group:");
        
        // 리스트와 딕셔너리 출력
        foreach (var person in group.Persons)
        {
            Console.WriteLine($"- {person}");
        }
        
        foreach (var kvp in group.PersonJob)
        {
            Console.WriteLine($"{kvp.Key}: {kvp.Value}");
        }
    }

    static async Task FlexibleTestAsync()
    {
        var client = new Flexible.FlexibleClient(channel);

        var status = await client.GetStatusStructAsync(new StatusRequest());
        
        // struct 구조 읽을 때
        switch (status.Data.KindCase)
        {
            case Value.KindOneofCase.StructValue:
                foreach (var field in status.Data.StructValue.Fields)
                {
                    Console.WriteLine($"{field.Key}: {field.Value}");
                }
                break;
        }
        
        status = await client.GetStatusJsonAsync(new StatusRequest());
        
        // JSON 구조 읽을 때
        var json = JsonFormatter.Default.Format(status.Data);
        Console.WriteLine(json);
        
        // Any 타입 읽을 때. 이때 무조건 protobuf 메시지여야 함
        if (status.Detail.Is(Person.Descriptor))
        {
            var person = status.Detail.Unpack<Person>();
            Console.WriteLine(person.Age);
        }
        
        // oneof 타입 읽을 때
        var response = await client.GetResponseMessageAsync(new ResponseMessageRequest());
        switch (response.ResultCase)
        {
            case ResponseMessage.ResultOneofCase.Person:
                Console.WriteLine($"성공. {response.Person}");
                break;
            case ResponseMessage.ResultOneofCase.Error:
                Console.WriteLine($"실패. 에러 코드: {response.Error}");
                break;
            default:
                throw new ArgumentException("Unexpected result.");
        }
    }
    
    static async Task ServerStreamingTestAsync()
    {
        var client = new StreamingAndHeader.StreamingAndHeaderClient(channel);
        using var call = client.ServerStreaming(new ServerStreamRequest { StreamCount = 10 });
        while (await call.ResponseStream.MoveNext())
        {
            Console.WriteLine(call.ResponseStream.Current.Num);
        }

        // C# 8.0 이상에서는 아래와 같이 읽을 수 있음
        // await foreach (var res in call.ResponseStream.ReadAllAsync())
        // {
        //     Console.WriteLine(res.Num);
        // }
    }
    
    static async Task ClientStreamingTestAsync()
    {
        var client = new StreamingAndHeader.StreamingAndHeaderClient(channel);
        using var call = client.ClientStreaming();
        
        for (int i = 0; i < 10; i++)
        {
            int num = Random.Shared.Next();
            await call.RequestStream.WriteAsync(new ClientStreamRequest { Num = num });
        }
        await call.RequestStream.CompleteAsync();
        
        var response = await call;
        if (response.IsSuccess)
        {
            Console.WriteLine("클라이언트 스트리밍 성공");
        }
        else
        {
            Console.WriteLine("클라이언트 스트리밍 실패");
        }
    }
    
    static async Task BidirectionalStreamingTestAsync()
    {
        var client = new StreamingAndHeader.StreamingAndHeaderClient(channel);
        using var call = client.BidirectionalStreaming();
        
        var readTask = Task.Run(async () =>
        {
            while (await call.ResponseStream.MoveNext())
            {
                await Task.Delay(100);
                Console.WriteLine(call.ResponseStream.Current.Num);
            }
        });
        
        for (int i = 0; i < 10; i++)
        {
            int num = Random.Shared.Next();
            await Task.Delay(100);
            await call.RequestStream.WriteAsync(new ClientStreamRequest { Num = num });
        }
        
        await call.RequestStream.CompleteAsync();
        await readTask;
    }
    
    static async Task HeaderTestAsync()
    {
        var client = new StreamingAndHeader.StreamingAndHeaderClient(channel);
        var headers = new Metadata
        {
            { "request-header", "request!" }
        };
        
        // 헤더는 응답을 받기 전에 받을 수 있음
        // Deadline은 선택사항. 지정하지 않으면 무한 대기임
        using var call = client.HeaderAsync(new HeaderRequest(), headers, deadline: DateTime.UtcNow.AddSeconds(10));
        
        var resHeaders = await call.ResponseHeadersAsync;
        var value = resHeaders.GetValue("response-header");
        
        Console.WriteLine($"Response Header: {value}");
        
        var response = await call;
    }
    
    static async Task TrailerTestAsync()
    {
        try
        {
            var client = new StreamingAndHeader.StreamingAndHeaderClient(channel);
            using var call = client.TrailerAsync(new HeaderRequest());
        
            // 트레일러는 응답이 끝난 후에 읽을 수 있음
            // 스트리밍의 경우에도 스트리밍이 끝난 후에 읽을 수 있음
            await call.ResponseAsync;
        
            var trailers = call.GetTrailers();
            var value = trailers.GetValue("response-trailer");
        
            Console.WriteLine($"Trailer Header: {value}");
        }
        catch (RpcException ex)
        {
            // 이렇게 RpcException 예외로 받는것도 가능
            var trailers = ex.Trailers;
            var value = trailers.GetValue("response-trailer");
            Console.WriteLine($"Trailer Header (from exception): {value}");
        }
    }
}