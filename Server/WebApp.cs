using System.Text.Json;
using CG.API;
using CG.Databases;
using CG.Users;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace CG;

public class WebApp
{
    private WebApplicationBuilder builder;
    private WebApplication app;
    
    public WebApp(IWebApi webApi)
    {
        builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://0.0.0.0:80");
        builder.Services.AddSingleton<IDatabaseClient, DynamoDbClient>();
        builder.Services.AddSingleton<IUsersLobby, UsersLobby>();
        builder.Services.AddAuthorization();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddSignalR()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.TypeInfoResolver = SerializationContext.Default;
                options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            });
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Add(SerializationContext.Default);
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        });
        app = builder.Build();
        app.UseDefaultFiles();
        // Create a custom content type provider
        var provider = new FileExtensionContentTypeProvider
        {
            Mappings =
            {
                [".data"] = "application/octet-stream",
                [".wasm"] = "application/wasm",
                [".mem"] = "application/octet-stream", // old Unity builds
                [".symbols.json"] = "application/json"
            }
        };

        // Brotli compressed files
        provider.Mappings.TryAdd(".br", "application/octet-stream");
        // Gzip compressed files
        provider.Mappings.TryAdd(".gz", "application/gzip");

        app.UseStaticFiles(new StaticFileOptions
        {
            ContentTypeProvider = provider
        });
        /*app.UseFileServer(new FileServerOptions
        {
            FileProvider = new PhysicalFileProvider(
                Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/unity")),
            RequestPath = "/cg-web",
            EnableDefaultFiles = true
        });*/
        app.UseHttpsRedirection();
        app.UseSwagger();
        app.UseSwaggerUI();
        /*if (app.Environment.IsDevelopment())
        {
        }*/
        app.UseAuthorization();
        webApi.Setup(app);
    }

    public void Run() => app.Run();
}