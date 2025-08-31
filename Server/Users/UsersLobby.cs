#nullable disable
using System.Collections.Concurrent;
using CG.Match;
using Core;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Configuration;

namespace CG.Users;

public class UsersLobby : IUsersLobby
{
    private readonly IHubContext<WebsocketHub> hubContext;
    private readonly ILogger<UsersLobby> logger;
    private readonly ConcurrentDictionary<string, UserSession> userSessionsByConnection = new ConcurrentDictionary<string, UserSession>();
    private readonly ConcurrentDictionary<string, UserSession> userSessionsByUsername = new ConcurrentDictionary<string, UserSession>();

    public UsersLobby(IHubContext<WebsocketHub> hubContext, ILogger<UsersLobby> logger)
    {
        this.hubContext = hubContext;
        this.logger = logger;
    }

    public void AddUser(UserConnectionData connectionData, string username)
    {
        var userSession = new UserSession(connectionData, username);
        userSessionsByUsername.TryAdd(username, userSession);
        userSessionsByConnection.TryAdd(connectionData.ConnectionId, userSession);
    }
    
    public bool RemoveUserByConnection(string connectionId, out UserSession userSession)
    {
        if(userSessionsByConnection.TryRemove(connectionId, out userSession))
        {
            userSessionsByUsername.TryRemove(userSession.Username, out _);
            EndMatchSession(userSession.MatchSession);
            ClearUserMatchmakingState(userSession);
            return true;
        }
        
        return false;
    }
    
    public bool RemoveUserByUsername(string username, out UserSession userSession)
    {
        if(userSessionsByUsername.TryRemove(username, out userSession))
        {
            userSessionsByConnection.TryRemove(userSession.ConnectionData.ConnectionId, out _);
            EndMatchSession(userSession.MatchSession);
            ClearUserMatchmakingState(userSession);
            return true;
        }
        
        return false;
    }

    public bool HasUser(string username)
    {
        return userSessionsByUsername.ContainsKey(username);
    }
    
    public bool HasConnection(string connectionId)
    {
        return userSessionsByConnection.ContainsKey(connectionId);
    }

    public async Task SendToUsernameAsync<T>(string username, T message) where T : WebSocketMessage
    {
        if (userSessionsByUsername.TryGetValue(username, out var userSession))
        {
            await hubContext.Clients.Client(userSession.ConnectionData.ConnectionId).SendAsync(typeof(T).Name, message);
        }
    }
    
    public async Task SendToConnectionAsync<T>(string connectionId, T message) where T : WebSocketMessage
    {
        if (userSessionsByConnection.TryGetValue(connectionId, out var userSession))
        {
            await hubContext.Clients.Client(userSession.ConnectionData.ConnectionId).SendAsync(typeof(T).Name, message);
        }
    }
    
    public async Task<TR> RequestFromUsernameAsync<TI, TR>(string username, TI message) where TI : WebSocketMessage where TR : WebSocketMessage
    {
        if (userSessionsByUsername.TryGetValue(username, out var userSession))
        {
            return await hubContext.Clients.Client(userSession.ConnectionData.ConnectionId).InvokeAsync<TR>(typeof(TI).Name, message, CancellationToken.None);
        }
        throw new Exception($"User {username} not connected.");
    }
    
    public async Task<TR> RequestFromConnectionAsync<TI, TR>(string connectionId, TI message) where TI : WebSocketMessage where TR : WebSocketMessage
    {
        if (userSessionsByConnection.TryGetValue(connectionId, out var userSession))
        {
            return await hubContext.Clients.Client(userSession.ConnectionData.ConnectionId).InvokeAsync<TR>(typeof(TI).Name, message, CancellationToken.None);
        }
        throw new Exception($"Connection {connectionId} not connected.");
    }
    
    #region MATCHMAKING
    
    private readonly ConcurrentDictionary<string, string> invites = new ConcurrentDictionary<string, string>();
    private string randomUsernameWaiting = null;
    private string ranked100UsernameWaiting = null;
    private string ranked250UsernameWaiting = null;
    private string ranked500UsernameWaiting = null;
    private readonly ConcurrentDictionary<Guid, MatchSession> activeMatches = new ConcurrentDictionary<Guid, MatchSession>();

    private void EndMatchSession(MatchSession session)
    {
        if(session == null) return;

        if (activeMatches.TryRemove(session.Id, out _))
        {
            logger.LogInformation("Online match ended: {MatchId}", session.Id);
            if (userSessionsByUsername.TryGetValue(session.WhiteUsername, out var whiteSession))
            {
                whiteSession.MatchSession = null;
                _ = SendToUsernameAsync(whiteSession.Username, new OnlineMatchEnded());
            }

            if (userSessionsByUsername.TryGetValue(session.BlackUsername, out var blackSession))
            {
                blackSession.MatchSession = null;
                _ = SendToUsernameAsync(blackSession.Username, new OnlineMatchEnded());
            }
        }
    }

    public void OnFriendMatchRequest(string connectionId, OnlineFriendMatchRequest request)
    {
        logger.LogInformation($"Friend match request received.");
        if (!userSessionsByConnection.TryGetValue(connectionId, out var userSession)) return;
        logger.LogInformation($"Friend match request from {userSession.Username} to {request.FriendUsername}");
        if (!invites.TryAdd(userSession.Username, request.FriendUsername)) return;
        if (invites.TryGetValue(request.FriendUsername, out var friendInvite))
        {
            if (friendInvite == userSession.Username)
            {
                var newMatchSession = new MatchSession()
                {
                    Id = Guid.NewGuid(),
                    WhiteUsername = request.FriendUsername,
                    BlackUsername = userSession.Username,
                    Stake = 0
                };
                activeMatches.TryAdd(newMatchSession.Id, newMatchSession);
                logger.LogInformation("Match started between {White} and {Black}", newMatchSession.WhiteUsername, newMatchSession.BlackUsername);
                invites.TryRemove(userSession.Username, out _);
                invites.TryRemove(request.FriendUsername, out _);
                userSession.MatchSession = newMatchSession;
                _ = SendToUsernameAsync(userSession.Username, new MatchSessionData
                {
                    WhiteUsername = newMatchSession.WhiteUsername,
                    BlackUsername = newMatchSession.BlackUsername,
                    Stake = newMatchSession.Stake
                });
                if (userSessionsByUsername.TryGetValue(request.FriendUsername, out var friendSession))
                {
                    friendSession.MatchSession = newMatchSession;
                    _ = SendToUsernameAsync(friendSession.Username, new MatchSessionData
                    {
                        WhiteUsername = newMatchSession.WhiteUsername,
                        BlackUsername = newMatchSession.BlackUsername,
                        Stake = newMatchSession.Stake
                    });
                }
            }
        }
    }

    public void OnRandomMatchRequest(string connectionId, OnlineRandomMatchRequest request)
    {
        logger.LogInformation($"Random match request received.");
        if (!userSessionsByConnection.TryGetValue(connectionId, out var userSession)) return;
        logger.LogInformation("Random match request from {Username}", userSession.Username);
        while (true)
        {
            var opponentUsername = randomUsernameWaiting;
            if (Interlocked.CompareExchange(ref randomUsernameWaiting, userSession.Username, null) == null)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref randomUsernameWaiting, null, opponentUsername) == null) continue;
            var newMatchSession = new MatchSession()
            {
                Id = Guid.NewGuid(),
                WhiteUsername = opponentUsername!,
                BlackUsername = userSession.Username,
                Stake = 0
            };
            var sessionData = new MatchSessionData
            {
                WhiteUsername = newMatchSession.WhiteUsername,
                BlackUsername = newMatchSession.BlackUsername,
                Stake = newMatchSession.Stake
            };
            activeMatches.TryAdd(newMatchSession.Id, newMatchSession);
            logger.LogInformation("Match started between {White} and {Black}", newMatchSession.WhiteUsername, newMatchSession.BlackUsername);
            userSession.MatchSession = newMatchSession;
            _ = SendToUsernameAsync(userSession.Username, sessionData);
            if (userSessionsByUsername.TryGetValue(opponentUsername!, out var opponentSession))
            {
                opponentSession.MatchSession = newMatchSession;
                _ = SendToUsernameAsync(opponentSession.Username, sessionData);
            }
            return;
        }
    }

    public void OnRankedMatchRequest(string connectionId, OnlineRankedMatchRequest request)
    {
        logger.LogInformation($"Ranked match request received.");
        if (!userSessionsByConnection.TryGetValue(connectionId, out var userSession)) return;
        
        logger.LogInformation("Ranked match request from {Username} with stake {Stake}", userSession.Username, request.Stake);

        string opponentUsername = null;
        switch (request.Stake)
        {
            case 100:
            {
                while (true)
                {
                    opponentUsername = ranked100UsernameWaiting;
                    if (Interlocked.CompareExchange(ref ranked100UsernameWaiting, userSession.Username, null) == null)
                    {
                        break;
                    }

                    if (Interlocked.CompareExchange(ref ranked100UsernameWaiting, null, opponentUsername) == null) continue;
                }

                break;
            }
            case 250:
            {
                while (true)
                {
                    opponentUsername = ranked250UsernameWaiting;
                    if (Interlocked.CompareExchange(ref ranked250UsernameWaiting, userSession.Username, null) == null)
                    {
                        break;
                    }

                    if (Interlocked.CompareExchange(ref ranked250UsernameWaiting, null, opponentUsername) == null) continue;
                }
                break;
            }
            case 500:
            {
                while (true)
                {
                    opponentUsername = ranked500UsernameWaiting;
                    if (Interlocked.CompareExchange(ref ranked500UsernameWaiting, userSession.Username, null) == null)
                    {
                        break;
                    }

                    if (Interlocked.CompareExchange(ref ranked500UsernameWaiting, null, opponentUsername) == null) continue;
                }
                break;
            }
        }
        
        var newMatchSession = new MatchSession()
        {
            Id = Guid.NewGuid(),
            WhiteUsername = opponentUsername,
            BlackUsername = userSession.Username,
            Stake = request.Stake
        };
        var sessionData = new MatchSessionData
        {
            WhiteUsername = newMatchSession.WhiteUsername,
            BlackUsername = newMatchSession.BlackUsername,
            Stake = newMatchSession.Stake
        };
        activeMatches.TryAdd(newMatchSession.Id, newMatchSession);
        logger.LogInformation("Match started between {White} and {Black} with stake {Stake}", newMatchSession.WhiteUsername, newMatchSession.BlackUsername, newMatchSession.Stake);
        userSession.MatchSession = newMatchSession;
        _ = SendToUsernameAsync(userSession.Username, sessionData);
        if (userSessionsByUsername.TryGetValue(opponentUsername, out var opponentSession))
        {
            opponentSession.MatchSession = newMatchSession;
            _ = SendToUsernameAsync(opponentSession.Username, sessionData);
        }
    }

    public void OnCancelMatchRequest(string connectionId, CancelMatchRequest request)
    {
        logger.LogInformation($"Cancel match request received.");
        if (!userSessionsByConnection.TryGetValue(connectionId, out var userSession)) return;
        ClearUserMatchmakingState(userSession);
    }
    
    public void OnEndMatchRequest(string connectionId, EndOnlineMatchRequest request)
    {
        logger.LogInformation($"End match request received.");
        if (!userSessionsByConnection.TryGetValue(connectionId, out var userSession)) return;
        EndMatchSession(userSession.MatchSession);
    }

    private void ClearUserMatchmakingState(UserSession userSession)
    {
        if (userSession == null) return;
        if (invites.TryRemove(userSession.Username, out _))
        {
            logger.LogInformation("Friend match request from {Username} cancelled due to user disconnect.", userSession.Username);
        }
        if (Interlocked.CompareExchange(ref randomUsernameWaiting, null, userSession.Username) == userSession.Username)
        {
            logger.LogInformation("Random match request from {Username} cancelled due to user disconnect.", userSession.Username);
        }
        if (Interlocked.CompareExchange(ref ranked100UsernameWaiting, null, userSession.Username) == userSession.Username)
        {
            logger.LogInformation("Ranked 100 match request from {Username} cancelled due to user disconnect.", userSession.Username);
        }
        if(Interlocked.CompareExchange(ref ranked250UsernameWaiting, null, userSession.Username) == userSession.Username)
        {
            logger.LogInformation("Ranked 250 match request from {Username} cancelled due to user disconnect.", userSession.Username);
        }
        if(Interlocked.CompareExchange(ref ranked500UsernameWaiting, null, userSession.Username) == userSession.Username)
        {
            logger.LogInformation("Ranked 500 match request from {Username} cancelled due to user disconnect.", userSession.Username);
        }
    }

    #endregion
}