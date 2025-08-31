using System.Net;
using CG.Databases;
using Core;
using Microsoft.AspNetCore.SignalR;

namespace CG.Users;

public class WebsocketHub(IDatabaseClient dbClient, ILogger<WebsocketHub> logger, IUsersLobby lobby) : Hub
{
    //CONSTANTS
    private const string AUTHORIZATION_HEADER = "Authorization";
    private const string PLATFORM_HEADER = "Platform";
    
    #region CONNECTION

    public override async Task OnConnectedAsync()
    {
        logger.LogInformation("WebSocket connection established");
        try
        {
            var headers = Context.GetHttpContext()?.Request.Headers ?? null;
            if (headers == null || !headers.TryGetValue(AUTHORIZATION_HEADER, out var authorizationHeaderValue))
            {
                throw new InvalidDataException("Authorization header is missing.");
            }
        
            var authorizationHeader = authorizationHeaderValue.ToString();
            if (string.IsNullOrWhiteSpace(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
            {
                throw new InvalidDataException("Authorization header is invalid.");
            }
        
            string token = authorizationHeader.Substring("Bearer ".Length).Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidDataException("Authorization token is missing.");
            }
        
            string platform = "Unknown";
            if (headers.TryGetValue(PLATFORM_HEADER, out var platformHeaderValue) && !string.IsNullOrWhiteSpace(platformHeaderValue.ToString()))
            {
                platform = platformHeaderValue.ToString();
            }
        
            var user = await dbClient.GetUserFromToken(token);
            var username = user.Attributes[DatabaseConstants.USERNAME_FIELD];
            if(lobby.HasUser(username)) throw new InvalidDataException("User is already connected.");
            var userConnectionData = new UserConnectionData()
            {
                ConnectionId = Context.ConnectionId,
                Endpoint = Context.GetHttpContext()?.Connection.RemoteIpAddress ?? IPAddress.None,
                Platform = platform,
                Token = token,
            };
            await dbClient.AuthenticateConnection(username, token, Context.ConnectionId);
            lobby.AddUser(userConnectionData, username);
            logger.LogInformation("User {Username} connected", username);
            await Clients.Client(Context.ConnectionId).SendAsync(nameof(AuthenticationResult), new AuthenticationResult()
            {
                Username = username,
            });
        }
        catch (Exception ex)
        {
            logger.LogInformation("OnConnectedAsync(): {error}", ex.ToString());
            await Clients.Client(Context.ConnectionId).SendAsync(nameof(AuthenticationResult), new AuthenticationResult()
            {
                Error = ex.Message,
            });
            Context.Abort();
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation("WebSocket connection disconnected: {exception}", exception);
        if (lobby.RemoveUserByConnection(Context.ConnectionId, out var session))
        {
            try
            {
                await dbClient.RemoveConnection(session.Username, Context.ConnectionId);
                logger.LogInformation("OnDisconnectedAsync() -> Connection removed from database.");
            }
            catch (Exception ex)
            {
                logger.LogInformation("OnDisconnectedAsync() -> Could not remove connection from database: {error}", ex.ToString());
            }
        }
    }
    
    #endregion
    
    #region API
    
    public Task OnlineFriendMatchRequest(OnlineFriendMatchRequest request)
    {
        lobby.OnFriendMatchRequest(Context.ConnectionId, request);
        return Task.CompletedTask;
    }
    
    public Task OnlineRandomMatchRequest(OnlineRandomMatchRequest request)
    {
        lobby.OnRandomMatchRequest(Context.ConnectionId, request);
        return Task.CompletedTask;
    }
    
    public Task OnlineRankedMatchRequest(OnlineRankedMatchRequest request)
    {
        lobby.OnRankedMatchRequest(Context.ConnectionId, request);
        return Task.CompletedTask;
    }
    
    public Task CancelMatchRequest(CancelMatchRequest request)
    {
        lobby.OnCancelMatchRequest(Context.ConnectionId, request);
        return Task.CompletedTask;
    }
    
    public Task EndOnlineMatchRequest(EndOnlineMatchRequest request)
    {
        lobby.OnEndMatchRequest(Context.ConnectionId, request);
        return Task.CompletedTask;
    }
    
    #endregion
}