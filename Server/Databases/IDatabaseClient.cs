using CG.Users;

namespace CG.Databases;

public interface IDatabaseClient
{
    Task<bool> UserExists(string username, string? sha256 = null, string? token = null);
    Task<string> RegisterUser(string username, string password);
    Task<string> LoginUser(string username, string password);
    Task<User> GetUserFromToken(string token);
    Task AuthenticateConnection(string username, string token, string connectionId);
    Task RemoveConnection(string username, string connectionId);
}

public record RegisterResponse(int Code, object Response);