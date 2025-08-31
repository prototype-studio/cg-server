using CG.Databases;
using CG.Users;
using Core;
using Microsoft.AspNetCore.Mvc;

namespace CG.API;

public class WebApi : IWebApi
{
    public void Setup(WebApplication app)
    {
        app.MapGet("/", Index);
        app.MapGet("/status", GetStatus);
        app.MapPost("/register", Register)
            .WithName("Register User")
            .WithOpenApi();
        app.MapPost("/login", Login)
            .WithName("Login User")
            .WithOpenApi();
        app.MapHub<WebsocketHub>("/session");
    }
    
    private IResult Index(HttpContext httpContext)
    {
        return Results.Ok();
    }
    
    private IResult GetStatus(HttpContext httpContext)
    {
        return Results.Ok();
    }
    
    private async Task<IResult> Register(HttpContext httpContext, [FromServices] IDatabaseClient dbClient, Credentials credentials)
    {
        if(string.IsNullOrWhiteSpace(credentials.Username))
        {
            return Results.BadRequest( new ErrorResponse()
            {
                ErrorMessage = "Provided username is invalid. Try another."
            });
        }
        
        if (string.IsNullOrWhiteSpace(credentials.Password))
        {
            return Results.BadRequest(new ErrorResponse()
            {
                ErrorMessage = "Provided password is invalid. Try another."
            });
        }

        try
        {
            var loginToken = await dbClient.RegisterUser(credentials.Username, credentials.Password);
            return Results.Ok(new AuthenticationSuccessResponse()
            {
                Token = loginToken
            });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new ErrorResponse()
            {
                ErrorMessage = ex.Message
            });
        }
    }

    private async Task<IResult> Login(HttpContext httpContext, [FromServices] IDatabaseClient dbClient, Credentials credentials)
    {
        if(string.IsNullOrWhiteSpace(credentials.Username))
        {
            return Results.BadRequest( new ErrorResponse()
            {
                ErrorMessage = "Provided username is invalid. Try another."
            });
        }
        
        if (string.IsNullOrWhiteSpace(credentials.Password))
        {
            return Results.BadRequest(new ErrorResponse()
            {
                ErrorMessage = "Provided password is invalid. Try another."
            });
        }

        try
        {
            var loginToken = await dbClient.LoginUser(credentials.Username, credentials.Password);
            return Results.Ok(new AuthenticationSuccessResponse()
            {
                Token = loginToken
            });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new ErrorResponse()
            {
                ErrorMessage = ex.Message
            });
        }
    }
}