// Dans Infrastructure/Mongo/Authentification/UserRepository.cs
using MongoDB.Driver;
using MyDeezerStats.Domain.Entities;
using MyDeezerStats.Domain.Repositories;
using System;
using System.Threading.Tasks;

namespace MyDeezerStats.Infrastructure.Mongo.Authentification
{
    public class UserRepository : IUserRepository
    {
        private readonly IMongoCollection<User> _users;

        public UserRepository(IMongoDatabase database)
        {
            _users = database.GetCollection<User>("users");
        }

        public async Task<bool> CreateAsync(User user)
        {
            await _users.InsertOneAsync(user);
            return true;
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _users.Find(user => user.Email == email).FirstOrDefaultAsync();
        }

        public async Task<User?> GetByIdAsync(string id)
        {
            return await _users.Find(user => user.Id.ToString() == id).FirstOrDefaultAsync();
        }
    }
}