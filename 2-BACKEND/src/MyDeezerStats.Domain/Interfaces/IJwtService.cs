using MyDeezerStats.Domain.Entities;
using System.Security.Claims;

namespace MyDeezerStats.Application.Interfaces
{
    public interface IJwtService
    {
        string GenerateToken(User user);
        ClaimsPrincipal ValidateToken(string token);
        double GetTokenExpirationInMinutes();
    }
}