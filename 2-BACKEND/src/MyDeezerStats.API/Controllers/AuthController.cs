using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using MyDeezerStats.Application.Interfaces;



namespace MyDeezerStats.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        [HttpPost("signup")]
        public async Task<IActionResult> SignUp([FromBody] LoginRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                _logger.LogWarning("Signup failed: Request body is invalid.");
                return BadRequest(new { message = "Email et mot de passe sont requis." });
            }

            try
            {
                _logger.LogInformation("Signup attempt for {Email}", request.Email);

                var createUserResult = await _authService.RegisterAsync(request.Email, request.Password);

                _logger.LogInformation("Signup successful for {Email}", request.Email);

                return Ok(new { success = true, message = "Votre compte a été créé avec succès !" });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Signup failed: user already exists ({Email})", request.Email);
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during signup for {Email}", request?.Email);
                return StatusCode(500, new { message = "Une erreur interne est survenue. Veuillez réessayer plus tard." });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                _logger.LogInformation($"Login attempt for {request.Email}");

                var token = await _authService.AuthenticateAsync(request.Email, request.Password);

                if (!token.Success)
                {
                    _logger.LogWarning($"Login failed for {request.Email}");
                    return Unauthorized(new { message = "Email ou mot de passe incorrect" });
                }

                _logger.LogInformation($"Login successful for {request.Email}");

                return Ok(new
                {
                    token = token,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during login for {request?.Email}");
                return StatusCode(500, new { message = "Une erreur interne est survenue" });
            }
        }
    }
}
