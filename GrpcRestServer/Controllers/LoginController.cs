using GrpcRestServer.Protocols;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SignalRStudyServer.Services;

namespace SignalRStudyServer.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class LoginController : ControllerBase
    {

        private readonly LoginService _loginService;
        private readonly ILogger<LoginController> _logger;
        
        public LoginController(LoginService loginService, ILogger<LoginController> logger)
        {
            _loginService = loginService;
            _logger = logger;
        }
        
        [HttpPost("login")]
        public async Task<IActionResult> LoginAsync(LoginReq req)
        {
            try
            {
                var loginInfo = await _loginService.LoginAsync(req);
                
                if (string.IsNullOrEmpty(loginInfo.authToken) || loginInfo.suid == 0)
                {
                    throw new Exception("_loginService.LoginAsync failed");
                }
                
                // 자바스크립트는 long을 못받는덴다 (실화냐?)
                var res = new LoginRes
                {
                    AuthToken = loginInfo.authToken,
                    Suid = loginInfo.suid.ToString(),
                };
                return Ok(res);
            }
            catch (Exception ex)
            {
                var res = new LoginRes
                {
                    ProtocolResult = ProtocolResult.Error,
                };
                _logger.LogError(ex, "Login error");

                return Ok(res);
            }
        }
        
        [HttpPost("register")]
        public async Task<IActionResult> RegisterAsync(RegisterReq req)
        {
            try
            {
                var result = await _loginService.RegisterAsync(req.Id, req.Password);
                
                var res = new RegisterRes
                {
                    ProtocolResult = ProtocolResult.Success
                };

                if (result) return Ok(res);
                
                res.ProtocolResult = ProtocolResult.Fail;
                return Ok(res);
            }
            catch (Exception ex)
            {
                var res = new RegisterRes()
                {
                    ProtocolResult = ProtocolResult.Error,
                };
                 _logger.LogError(ex, "register error");
                return Ok(res);
            }
        }
    }
}
