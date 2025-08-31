namespace CG.Users;

public interface IWebsocketClient<T>
{
    Task Send(T message);
    Task<T> Request(T message);
}