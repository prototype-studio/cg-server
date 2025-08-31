using CG.Users;

namespace CG.Databases;

public class MockDBClient : IDatabaseClient
{
    public async Task<bool> UserExists(string username, string? sha256 = null, string? token = null)
    {
        Console.WriteLine($"Checking if user {username} exists with sha256 {sha256} or token {token}");
        await Task.Delay(50);
        return DateTime.UtcNow.Second % 2 == 0; // exist every other second
    }
    
    public async Task<string> RegisterUser(string username, string password)
    {
        Console.WriteLine($"Creating user {username} with password {password}");
        await Task.Delay(50);
        if (DateTime.UtcNow.Second % 2 == 0) // fail every other second
        {
            throw new Exception($"User with username {username} already exists.");
        }
        return Guid.NewGuid().ToString();
    }
    
    public async Task<string> LoginUser(string username, string password)
    {
        Console.WriteLine($"Logging in user {username} with password {password}");
        await Task.Delay(50);
        if (DateTime.UtcNow.Second % 2 == 0) // fail every other second
        {
            throw new Exception($"Invalid username or password.");
        }
        return Guid.NewGuid().ToString();
    }
    
    public async Task<User> GetUserFromToken(string token)
    {
        await Task.Delay(50);

        return new User(new Dictionary<string, string>()
        {
            [DatabaseConstants.USERNAME_FIELD] = "mockuser",
        });
    }

    public Task AuthenticateConnection(string username, string token, string connectionId)
    {
        return Task.CompletedTask;
    }

    public Task RemoveConnection(string username, string connectionId)
    {
        return Task.CompletedTask;
    }
}