using Microsoft.Extensions.Options;
using MyDeezerStats.Application.Interfaces;
using MyDeezerStats.Domain.Entities;
using MyDeezerStats.Domain.Repositories;
using MyDeezerStats.Infrastructure.Settings;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Logging;
using DnsClient.Internal;
using Microsoft.AspNetCore.Identity;

namespace MyDeezerStats.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly ILogger<AuthService> _logger;
        private readonly PasswordHasher<User> _passwordHasher;
        private readonly IUserRepository _userRepository;
        private readonly JwtSettings _jwtSettings;

        public AuthService(IUserRepository userRepository, IOptions<JwtSettings> jwtSettings, ILogger<AuthService> logger, PasswordHasher<User> passwordHasher)
        {
            _logger = logger;
            _userRepository = userRepository;
            _jwtSettings = jwtSettings.Value;
            _passwordHasher = passwordHasher;
        }

        public async Task<string> Authenticate(string username, string password)
        {
            _logger.LogInformation("Tentative de connexion pour l'utilisateur: {Email}", username);

            var user = await _userRepository.GetByUsername(username);
            if (user == null) {
                _logger.LogWarning("Utilisateur non trouvé pour l'email: {Email}", username);
                throw new InvalidOperationException("Invalid credentials");
            }
            if (!VerifyPassword(user, password))
            {
                _logger.LogWarning("Mot de passe incorrect de {email}", username);
                throw new UnauthorizedAccessException("Invalid credentials");
            }

            return GenerateJwtToken(user);
        }

        async Task<bool> IAuthService.CreateUser(string email, string password)
        {
            // Vérifier si l'utilisateur existe déjà
            var existingUser = await _userRepository.GetByUsername(email);
            if (existingUser != null)
            {
                _logger.LogWarning("L'utilisateur avec l'email {Email} existe déjà.", email);
                throw new InvalidOperationException($"Un utilisateur avec l'email '{email}' existe déjà.");
            }

            // Créer un nouvel utilisateur
            var user = new User { Email = email };

            // Hachage du mot de passe avant de le stocker
            user.PasswordHash = _passwordHasher.HashPassword(user, password);

            var result = await _userRepository.CreateAsync(user);
            return result;
        }

        private bool VerifyPassword(User user, string password)
        {
            var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
            return verificationResult == PasswordVerificationResult.Success;
        }

        private string GenerateJwtToken(User user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Email),
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                _jwtSettings.Issuer,
                _jwtSettings.Audience,
                claims,
                expires: DateTime.Now.AddMinutes(_jwtSettings.ExpirationInMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
