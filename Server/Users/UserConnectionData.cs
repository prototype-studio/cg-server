using System.Net;

namespace CG.Users;

public struct UserConnectionData
{
    public string ConnectionId { get; init; }
    public string Platform { get; init; }
    public string Token { get; init; }
    public IPAddress Endpoint { get; init; }
}