using MyDeezerStats.Domain.Entities;

namespace MyDeezerStats.Domain.Repositories
{
    public interface IUserRepository
    {
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByIdAsync(string id);
        Task<bool> CreateAsync(User user);
    }
}