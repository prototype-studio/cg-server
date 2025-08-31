using Core;

namespace CG.Users;

public interface IUsersLobby
{
    void AddUser(UserConnectionData connection, string username);

    bool RemoveUserByConnection(string connectionId, out UserSession session);

    bool RemoveUserByUsername(string username, out UserSession session);
    
    bool HasUser(string username);
    bool HasConnection(string connectionId);

    Task SendToUsernameAsync<T>(string username, T message) where T : WebSocketMessage;

    Task SendToConnectionAsync<T>(string connectionId, T message) where T : WebSocketMessage;

    Task<TR> RequestFromUsernameAsync<TI, TR>(string username, TI message) where TI : WebSocketMessage where TR : WebSocketMessage;

    Task<TR> RequestFromConnectionAsync<TI, TR>(string connectionId, TI message) where TI : WebSocketMessage where TR : WebSocketMessage;
    
    void OnFriendMatchRequest(string connectionId, OnlineFriendMatchRequest request);
    void OnRandomMatchRequest(string connectionId, OnlineRandomMatchRequest request);
    void OnRankedMatchRequest(string connectionId, OnlineRankedMatchRequest request);
    void OnCancelMatchRequest(string connectionId, CancelMatchRequest request);
    void OnEndMatchRequest(string connectionId, EndOnlineMatchRequest request);
}