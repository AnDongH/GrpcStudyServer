using System.Text.Json;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;

namespace GrpcStudyServer.Services;

[Authorize]
public class FlexibleService : Flexible.FlexibleBase
{
    public override Task<Status> GetStatusStruct(StatusRequest request, ServerCallContext context)
    {
        var status = new Status();
        status.Message = "Hello World!";

        status.Data = Value.ForStruct(new Struct
        {
            Fields =
            {
                ["enabled"] = Value.ForBool(true),
                ["metadata"] = Value.ForList(
                    Value.ForString("value1"),
                    Value.ForString("value2"))
            }
        });
        
        // 이때 안에 넣을 수 있는건 protobuf 메시지 뿐인듯?
        status.Detail = Any.Pack(new Person() {Age = 10});
        return Task.FromResult(status);
    }
    
    public override Task<Status> GetStatusJson(StatusRequest request, ServerCallContext context)
    {
        var status = new Status();
        status.Message = "Hello World!";
        
        status.Data = Value.Parser.ParseJson(@"
        {
            ""enabled"": true,
            ""metadata"": [ ""value1"", ""value2"" ]
        }");
        
        // 이때 안에 넣을 수 있는건 protobuf 메시지 뿐인듯?
        status.Detail = Any.Pack(new Person() {Age = 9});
        return Task.FromResult(status);
    }

    public override Task<ResponseMessage> GetResponseMessage(ResponseMessageRequest request, ServerCallContext context)
    {
        var response = new ResponseMessage();
        try
        {
            // 뭔가의 로직
            response.Person = "any";
            long tick = Random.Shared.NextInt64();
            if (tick % 2 == 0) throw new Exception();
        }
        catch (Exception ex)
        {
            // 실패
            Console.WriteLine(ex);
            response.Error = 1;
        }
        
        return Task.FromResult(response);
    }
}