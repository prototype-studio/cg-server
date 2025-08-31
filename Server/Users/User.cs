namespace CG.Users;

public class User(Dictionary<string, string> attributes)
{
    public readonly Dictionary<string, string> Attributes = attributes;
}

public class UserAuthData(string username, string token, string platform)
{
    public readonly string Username = username;
    public readonly string Token = token;
    public readonly string Platform = platform;
}