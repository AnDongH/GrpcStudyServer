using Grpc.Core;
using Microsoft.AspNetCore.Authorization;

namespace GrpcStudyServer.Services;

[Authorize]
public class CollectionTypeService : CollectionType.CollectionTypeBase
{
    public override Task<Group> GetGroup(GetGroupRequest request, ServerCallContext context)
    {
        var g = new Group();
        
        // 리스트
        g.Persons.Add("any");
        g.Persons.Add(["bob", "june"]);
        
        // 딕셔너리
        g.PersonJob["any"] = "developer";
        g.PersonJob.Add(new Dictionary<string, string>()
        {
            { "bob", "designer" },
            { "june", "manager" }
        });
        
        return Task.FromResult(g);
    }
}