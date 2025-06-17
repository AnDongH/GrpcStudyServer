using Grpc.Core;
using Microsoft.AspNetCore.Authorization;

namespace GrpcStudyServer.Authorizations;

// 특정 권한에 대한 요구 사항을 정의하는 클래스
public class AdminRequirement : 
    AuthorizationHandler<AdminRequirement>,
    IAuthorizationRequirement
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, 
        AdminRequirement requirement)
    {
        var claim = context.User.FindFirst("IsAdmin");
        if (claim != null && 
            !string.IsNullOrEmpty(claim.Value) && 
            claim.Value.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}