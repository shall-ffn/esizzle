using EsizzleAPI.Models;

namespace EsizzleAPI.Repositories
{
    public interface IUserRepository
    {
        Task<SecureUser?> GetUserByEmailAsync(string email);
        Task<SecureUser?> GetUserByIdAsync(int userId);
        Task<List<int>> GetUserOfferingAccessAsync(int userId);
        Task<bool> VerifyPasswordAsync(int userId, string password);
    }
}