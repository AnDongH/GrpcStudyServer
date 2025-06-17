using Google.Protobuf.WellKnownTypes;
using Google.Rpc;
using Grpc.Core;

namespace GrpcStudyServer.Services;

// 클라이언트에게 주는 에러는 항상 RpcException을 사용해야 함.
// 그게 아니라면 클라이언트쪽은 에러를 받긴 하지만, 에러의 종류를 알 수 없음
public class ErrorHandleService : ErrorHandle.ErrorHandleBase
{
    // 일반적인 오류 일으키는 방법
    public override async Task<ErrorResponse> StandardError(ErrorRequest request, ServerCallContext context)
    {
        await Task.Delay(100);
        
        if (string.IsNullOrEmpty(request.Name))
        {
            // 이렇게 서버 에러 상태코드를 리턴해줌
            throw new RpcException(new Grpc.Core.Status(StatusCode.InvalidArgument, "Name cannot be empty"));
        }

        return new ErrorResponse
        {
            Message = request.Name + " - Ok"
        };
    }

    public override async Task<ErrorResponse> ErrorWithRetry(ErrorRequest request, ServerCallContext context)
    {
        int num = Random.Shared.Next();
        if (num % 2 == 0) throw new RpcException(new Grpc.Core.Status(StatusCode.Unavailable, "Service is unavailable. Please retry later."));
        await Task.Delay(100);
        return new ErrorResponse
        {
            Message = request.Name + " - Ok"
        };
    }

    public override async Task<ErrorResponse> ErrorHandlePattern(ErrorRequest request, ServerCallContext context)
    {
        try
        {
            await Task.Delay(100);
            
            if (string.IsNullOrEmpty(request.Name))
            {
                // 이건 클라이언트가 잘못된 요청을 보낸 경우
                int num = Random.Shared.Next();
                if (num % 2 == 0)
                {
                    throw new UnauthorizedAccessException();
                }
                throw new Exception();
            }

            return new ErrorResponse
            {
                Message = request.Name + " - Ok"
            };
        }
        catch (UnauthorizedAccessException)
        {
            // 일반 에러가 발생시 클라이언트에게는 RpcException로 바꿔서 던지기
            // 바꿔 던지지 않으면 클라이언트는 에러를 받지만, 에러의 종류를 알 수 없음 UNKNOWN
            throw new RpcException(new Grpc.Core.Status(StatusCode.PermissionDenied, "접근 권한이 없습니다."));
        }
        catch (Exception)
        {
            throw new RpcException(new Grpc.Core.Status(StatusCode.Internal, "서버 에러 발생."));
        }
    }

    public override async Task<ErrorResponse> RichError(ErrorRequest request, ServerCallContext context)
    {
        await Task.Delay(100);

        var errors = new List<BadRequest.Types.FieldViolation>();

        if (Random.Shared.Next() % 2 == 0)
        {
            errors.Add(new BadRequest.Types.FieldViolation
            {
                Field = "email",
                Description = "이메일은 필수입니다."
            });
        
            errors.Add(new BadRequest.Types.FieldViolation
            {
                Field = "id",
                Description = "id는 숫자여야 합니다."
            });
        
            errors.Add(new BadRequest.Types.FieldViolation
            {
                Field = "age",
                Description = "나이는 0보다 커야 합니다."
            });
        }

        if (errors.Any())
        {
            var badRequest = new BadRequest();
            badRequest.FieldViolations.AddRange(errors);

            var status = new Google.Rpc.Status
            {
                Code = (int)Code.InvalidArgument,
                Message = "유효성 검사 실패",
                Details = { Any.Pack(badRequest) }
            };

            throw status.ToRpcException(); 
        }
        
        return new ErrorResponse
        {
            Message = request.Name + " - Ok"
        };
    }
}