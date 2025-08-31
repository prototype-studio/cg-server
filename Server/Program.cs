using CG.API;

namespace CG;

public class Program
{
    public static void Main(string[] args)
    {
        IWebApi webApi = new WebApi();
        var webApp = new WebApp(webApi);
        webApp.Run();
    }
}