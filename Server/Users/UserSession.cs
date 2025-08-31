#nullable disable
using CG.Match;

namespace CG.Users;

public class UserSession(UserConnectionData connectionData, string username)
{
    public readonly UserConnectionData ConnectionData = connectionData;
    public readonly string Username = username;
    
    //PROPERTIES
    public MatchSession MatchSession { get; set; } = null;
}