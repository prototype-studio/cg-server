using System.Text.Json.Serialization;
using Core;

namespace CG.API;

[JsonSerializable(typeof(Credentials))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(AuthenticationSuccessResponse))]
[JsonSerializable(typeof(WebSocketMessage))]
[JsonSerializable(typeof(AuthenticationResult))]
[JsonSerializable(typeof(MatchSessionData))]
[JsonSerializable(typeof(CancelMatchRequest))]
[JsonSerializable(typeof(OnlineFriendMatchRequest))]
[JsonSerializable(typeof(OnlineRandomMatchRequest))]
[JsonSerializable(typeof(OnlineRankedMatchRequest))]
[JsonSerializable(typeof(EndOnlineMatchRequest))]
[JsonSerializable(typeof(OnlineMatchEnded))]
public partial class SerializationContext : JsonSerializerContext
{
    
}