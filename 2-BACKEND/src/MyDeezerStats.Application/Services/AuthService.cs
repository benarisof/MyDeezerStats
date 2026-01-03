using Microsoft.Extensions.Logging;
using MyDeezerStats.Application.Interfaces;
using MyDeezerStats.Domain.Entities;
using MyDeezerStats.Domain.Repositories;
using System.Security.Claims;

namespace MyDeezerStats.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly ILogger<AuthService> _logger;
        private readonly IUserRepository _userRepository;
        private readonly IJwtService _jwtService;
        private readonly IPasswordService _passwordHasher;

        public AuthService(
            IUserRepository userRepository,
            IJwtService jwtService,
            IPasswordService passwordHasher,
            ILogger<AuthService> logger)
        {
            _logger = logger;
            _userRepository = userRepository;
            _jwtService = jwtService;
            _passwordHasher = passwordHasher;
        }

        public async Task<AuthResult> AuthenticateAsync(string email, string password)
        {
            _logger.LogInformation("Tentative de connexion pour l'utilisateur: {Email}", email);

            var user = await _userRepository.GetByEmailAsync(email);

            if (user == null)
            {
                _logger.LogWarning("Utilisateur non trouvé pour l'email: {Email}", email);
                return AuthResult.Failure("Invalid credentials");
            }

            var passwordVerificationResult = _passwordHasher.VerifyPassword(user.PasswordHash, password);

            if (!passwordVerificationResult)
            {
                _logger.LogWarning("Mot de passe incorrect pour {Email}", email);
                return AuthResult.Failure("Invalid credentials");
            }

            var token = _jwtService.GenerateToken(user);
            var expiresAt = DateTime.UtcNow.AddMinutes(_jwtService.GetTokenExpirationInMinutes());

            _logger.LogInformation("Connexion réussie pour {Email}", email);

            return AuthResult.SuccessResult(token, expiresAt, user.Id.ToString());
        }

        public async Task<AuthResult> RegisterAsync(string email, string password)
        {
            _logger.LogInformation("Tentative d'inscription pour l'email: {Email}", email);

            // Validation basique (une validation plus poussée serait dans le modèle/request)
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return AuthResult.Failure("Email and password are required.");
            }

            // Vérifier si l'utilisateur existe déjà
            var existingUser = await _userRepository.GetByEmailAsync(email);
            if (existingUser != null)
            {
                _logger.LogWarning("L'utilisateur avec l'email {Email} existe déjà.", email);
                return AuthResult.Failure($"Un utilisateur avec l'email '{email}' existe déjà.");
            }

            // Valider la complexité du mot de passe
            var passwordValidationResult = ValidatePassword(password);
            if (!passwordValidationResult.IsValid)
            {
                return AuthResult.Failure(passwordValidationResult.ErrorMessage ?? "");
            }

            try
            {
                // Créer un nouvel utilisateur
                var user = new User
                {
                    Email = email,
                    PasswordHash = _passwordHasher.HashPassword(password),
                };

                var result = await _userRepository.CreateAsync(user);

                if (!result)
                {
                    return AuthResult.Failure("Failed to create user.");
                }

                var token = _jwtService.GenerateToken(user);
                var expiresAt = DateTime.UtcNow.AddMinutes(_jwtService.GetTokenExpirationInMinutes());

                _logger.LogInformation("Inscription réussie pour {Email}", email);

                return AuthResult.SuccessResult(token, expiresAt, user.Id.ToString(), "User registered successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'inscription pour {Email}", email);
                return AuthResult.Failure("An error occurred during registration.");
            }
        }

        public async Task<AuthResult> RefreshTokenAsync(string token)
        {
            try
            {
                var principal = _jwtService.ValidateToken(token);
                var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return AuthResult.Failure("Invalid token.");
                }

                var user = await _userRepository.GetByIdAsync(userIdClaim);

                if (user == null)
                {
                    return AuthResult.Failure("User not found.");
                }

                var newToken = _jwtService.GenerateToken(user);
                var expiresAt = DateTime.UtcNow.AddMinutes(_jwtService.GetTokenExpirationInMinutes());

                return AuthResult.SuccessResult(newToken, expiresAt, user.Id.ToString(), "Token refreshed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                return AuthResult.Failure("Invalid or expired token.");
            }
        }

        public async Task<bool> ValidateUserExistsAsync(string email)
        {
            var user = await _userRepository.GetByEmailAsync(email);
            return user != null;
        }

        private PasswordValidationResult ValidatePassword(string password)
        {
            var errors = new List<string>();

            if (password.Length < 8)
                errors.Add("Password must be at least 8 characters long.");

            if (!password.Any(char.IsUpper))
                errors.Add("Password must contain at least one uppercase letter.");

            if (!password.Any(char.IsLower))
                errors.Add("Password must contain at least one lowercase letter.");

            if (!password.Any(char.IsDigit))
                errors.Add("Password must contain at least one digit.");

            return new PasswordValidationResult
            {
                IsValid = errors.Count == 0,
                ErrorMessage = errors.Count > 0 ? string.Join(" ", errors) : null
            };
        }
    }

    // Modèles auxiliaires (à placer dans un fichier séparé, par exemple Application/Models/Auth/)
    public class AuthResult
    {
        public bool Success { get; set; }
        public string? Token { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string? UserId { get; set; }
        public string? Message { get; set; }
        public IEnumerable<string> Errors { get; set; } = Enumerable.Empty<string>();

        public static AuthResult SuccessResult(string token, DateTime expiresAt, string userId, string? message = null)
        {
            return new AuthResult
            {
                Success = true,
                Token = token,
                ExpiresAt = expiresAt,
                UserId = userId,
                Message = message,
                Errors = Enumerable.Empty<string>()
            };
        }

        public static AuthResult Failure(string errorMessage)
        {
            return new AuthResult
            {
                Success = false,
                Message = errorMessage,
                Errors = new List<string> { errorMessage }
            };
        }

        public static AuthResult Failure(IEnumerable<string> errors)
        {
            return new AuthResult
            {
                Success = false,
                Message = string.Join("; ", errors),
                Errors = errors
            };
        }
    }

    public class PasswordValidationResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
    }
}