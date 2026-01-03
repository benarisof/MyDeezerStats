using MyDeezerStats.Application.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyDeezerStats.Application.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResult> AuthenticateAsync(string email, string password);
        Task<AuthResult> RegisterAsync(string email, string password);
        Task<AuthResult> RefreshTokenAsync(string token);
        Task<bool> ValidateUserExistsAsync(string email);
    }

}
