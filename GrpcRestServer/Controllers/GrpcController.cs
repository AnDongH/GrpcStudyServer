using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Google.Rpc;
using Grpc.Core;
using Grpc.Net.ClientFactory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GrpcRestServer.Controllers
{
    [Route("[controller]")]
    [ApiController]
    [Authorize]
    public class GrpcController : ControllerBase
    {
        private readonly ILogger<GrpcController> _logger;
        
        public GrpcController(ILogger<GrpcController> logger, ILoggerFactory loggerFactory)
        {
            _logger = logger;
        }
        
        [HttpGet("[action]")]
        [Authorize("Admin")]
        public async Task<IActionResult> GreeterTestAsync([FromServices] GrpcClientFactory grpcClientFactory)
        {
            // 클라이언트쪽 Grpc로그는 ILoggerFactory + 채널을 통해 설정이 가능함
            // 만약 클라이언트 팩토리를 사용한다면 자동으로 그냥 설정됨
            // + appsettings.json과 같은 곳에 Grpc 로그 레벨을 설정하면 됨
            var client = grpcClientFactory.CreateClient<Greeter.GreeterClient>("Greeter");
            
            var reply = await client.SayHelloAsync(new HelloRequest { Name = "GreeterClient" });
            // 응답 출력
            _logger.LogInformation("Greeting: " + reply.Message);
            
            return Ok("Greeter test completed.");
        }
        
        [HttpGet("[action]")]
        public async Task<IActionResult> CollectionTypeTestAsync([FromServices] GrpcClientFactory grpcClientFactory)
        {
            var client = grpcClientFactory.CreateClient<CollectionType.CollectionTypeClient>("CollectionType");
        
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
            
            return Ok("Collection type test completed.");
        }
        
        [HttpGet("[action]")]
        public async Task<IActionResult> FlexibleTestAsync([FromServices] GrpcClientFactory grpcClientFactory)
        {
            var client = grpcClientFactory.CreateClient<Flexible.FlexibleClient>("Flexible");

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
            
            return Ok("Flexible test completed.");
        }
        
        [HttpGet("[action]")]
        public async Task<IActionResult> ServerStreamingTestAsync([FromServices] GrpcClientFactory grpcClientFactory)
        {
            var client = grpcClientFactory.CreateClient<StreamingAndHeader.StreamingAndHeaderClient>("StreamingAndHeader");
            using var call = client.ServerStreaming(new ServerStreamRequest { StreamCount = 10 });
            while (await call.ResponseStream.MoveNext(CancellationToken.None))
            {
                Console.WriteLine(call.ResponseStream.Current.Num);
            }
            
            return Ok("Server streaming completed.");
        }
        
        [HttpGet("[action]")]
        public async Task<IActionResult> ClientStreamingTestAsync([FromServices] GrpcClientFactory grpcClientFactory)
        {
            var client = grpcClientFactory.CreateClient<StreamingAndHeader.StreamingAndHeaderClient>("StreamingAndHeader");
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
            return Ok("Client streaming completed.");
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> BidirectionalStreamingTestAsync(
            [FromServices] GrpcClientFactory grpcClientFactory)
        {
            var client =
                grpcClientFactory.CreateClient<StreamingAndHeader.StreamingAndHeaderClient>("StreamingAndHeader");
            using var call = client.BidirectionalStreaming();
        
            var readTask = Task.Run(async () =>
            {
                while (await call.ResponseStream.MoveNext(CancellationToken.None))
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

            return Ok("Bidirectional streaming completed.");
        }
        
        [HttpGet("[action]")]
        public async Task<IActionResult> HeaderTestAsync([FromServices] GrpcClientFactory grpcClientFactory)
        {
            var client = grpcClientFactory.CreateClient<StreamingAndHeader.StreamingAndHeaderClient>("StreamingAndHeader");
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
            
            return Ok("Header test completed.");
        }
        
        [HttpGet("[action]")]
        public async Task<IActionResult> TrailerTestAsync([FromServices] GrpcClientFactory grpcClientFactory)
        {
            try
            {
                var client = grpcClientFactory.CreateClient<StreamingAndHeader.StreamingAndHeaderClient>("StreamingAndHeader");
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
            
            return Ok("Trailer test completed.");
        }
        
        [HttpGet("[action]")]
        public async Task<IActionResult> DeadlineTestAsync([FromServices] GrpcClientFactory grpcClientFactory)
        {
            var client = grpcClientFactory.CreateClient<Deadline.DeadlineClient>("Deadline");
            try
            {
                var reply = await client.SayHelloAsync(new DeadlineRequest { Name = "GrpcRestServer" }, deadline: DateTime.UtcNow.AddSeconds(1));
                Console.WriteLine(reply.Message);
            }
            catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.DeadlineExceeded)
            {
                Console.WriteLine("Deadline exceeded.");
            }
            
            return Ok("Deadline test completed.");
        }
        
        [HttpGet("[action]")]
        public async Task<IActionResult> DeadlineTest2Async([FromServices] GrpcClientFactory grpcClientFactory)
        {
            var client = grpcClientFactory.CreateClient<Deadline.DeadlineClient>("Deadline");
            try
            {
                var reply = await client.SayHello2Async(new DeadlineRequest { Name = "GrpcRestServer" }, deadline: DateTime.UtcNow.AddSeconds(1));
                Console.WriteLine(reply.Message);
            }
            catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.DeadlineExceeded)
            {
                Console.WriteLine("Deadline exceeded.");
            }
            
            return Ok("Deadline test completed.");
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> StandardErrorTestAsync([FromServices] GrpcClientFactory grpcClientFactory)
        {
            var client = grpcClientFactory.CreateClient<ErrorHandle.ErrorHandleClient>("ErrorHandle");
            try
            {
                var response = await client.StandardErrorAsync(new ErrorRequest { Name = "" });
                Console.WriteLine(response.Message);
            }
            catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            return Ok("Standard error test completed.");
        }

        // 체널 설정에서 리트라이 로직을 적어두면 여기서 뭘 안해도 저절로 리트라이 시도
        // 채널 설정에는 Unavailable 상태코드면 재시도 하도록 설정했음
        [HttpGet("[action]")]
        public async Task<IActionResult> ErrorWithRetryTestAsync([FromServices] GrpcClientFactory grpcClientFactory)
        {
            var client = grpcClientFactory.CreateClient<ErrorHandle.ErrorHandleClient>("ErrorHandle");
            try
            {
                var response = await client.ErrorWithRetryAsync(new ErrorRequest { Name = "Retry Test" });
                Console.WriteLine(response.Message);
            }
            catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Unavailable)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            return Ok("Error with retry test completed.");
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> ErrorHandlePatternTestAsync([FromServices] GrpcClientFactory grpcClientFactory)
        {
            var client = grpcClientFactory.CreateClient<ErrorHandle.ErrorHandleClient>("ErrorHandle");
            try
            {
                var response = await client.ErrorHandlePatternAsync(new ErrorRequest { Name = "" });
                Console.WriteLine(response.Message);
            }
            catch (RpcException ex)
            {
                Console.WriteLine($"Error: {ex.Message}, Status Code: {ex.StatusCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
            }

            return Ok("Error handle pattern test completed.");
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> RichErrorTestAsync([FromServices] GrpcClientFactory grpcClientFactory)
        {
            var client = grpcClientFactory.CreateClient<ErrorHandle.ErrorHandleClient>("ErrorHandle");
            try
            {
                var response = await client.RichErrorAsync(new ErrorRequest { Name = "Rich Error Test" });
                Console.WriteLine(response.Message);
            }
            catch (RpcException ex)
            {
                var status = ex.GetRpcStatus();
                if (status != null)
                {
                    Console.WriteLine($"🚨 오류 코드: {status.Code}");
                    Console.WriteLine($"📝 오류 메시지: {status.Message}");

                    var badRequest = status.GetDetail<BadRequest>();
                    if (badRequest != null)
                    {
                        Console.WriteLine("❌ 유효성 검사 오류:");
                        foreach (var violation in badRequest.FieldViolations)
                        {
                            Console.WriteLine($"   • {violation.Field}: {violation.Description}");
                        }
                    }
                }
            }
            
            return Ok("Rich error test completed.");
        }
    }
}
