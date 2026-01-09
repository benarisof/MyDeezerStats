using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MyDeezerStats.Application.Dtos.LastStream;
using MyDeezerStats.Application.Interfaces;
using MyDeezerStats.Application.Services;
using MyDeezerStats.Domain.Entities;
using MyDeezerStats.Domain.Repositories;
using MyDeezerStats.Infrastructure.Mongo.Authentification;
using MyDeezerStats.Infrastructure.Mongo.Repositories;
using MyDeezerStats.Infrastructure.Mongo.Search;
using MyDeezerStats.Infrastructure.Services;
using MyDeezerStats.Infrastructure.Settings;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Configuration hiérarchique
builder.Configuration
    .AddJsonFile("appsettings.json")
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

// Gestion du host Mongo selon si on est dans un conteneur
var isInContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
var mongoHost = isInContainer ? "mongodb" : "localhost";
builder.Configuration["MongoDbSettings:ConnectionString"] =
    builder.Configuration["MongoDbSettings:ConnectionString"]!.Replace("localhost", mongoHost);

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Configuration JWT et LastFm
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<LastFmOptions>(builder.Configuration.GetSection("LastFm"));
builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection("MongoDbSettings"));

// ⚡ MongoDB : registration unique et test de connexion
builder.Services.AddSingleton<IMongoDatabase>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger("MongoInit");

    var mongoSettings = config.GetSection("MongoDbSettings").Get<MongoDbSettings>();
    if (mongoSettings == null)
        throw new InvalidOperationException("MongoDbSettings section is missing.");

    var client = new MongoClient(mongoSettings.ConnectionString);
    var database = client.GetDatabase(mongoSettings.DatabaseName);

    try
    {
        database.RunCommandAsync((Command<BsonDocument>)"{ping:1}").Wait();
        logger.LogInformation("Mongo connecté sur {ConnectionString}, base {Database}",
            mongoSettings.ConnectionString, mongoSettings.DatabaseName);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Impossible de se connecter à MongoDB");
        throw;
    }

    return database;
});

// Services Application
builder.Services.AddScoped<IDeezerService, DeezerService>();
builder.Services.AddHttpClient<ILastFmService, LastFmService>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<IOrchestratorService, OrchestratorService>();
builder.Services.AddScoped<IExcelService, ExcelService>();
builder.Services.AddScoped<IAuthService, AuthService>();

//Services techniques
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IPasswordService, PasswordService>();

// Repositories
builder.Services.AddScoped<ISearchRepository, SearchRepository>();
builder.Services.AddScoped<IListeningRepository, ListeningRepository>();
builder.Services.AddScoped<IAlbumRepository, AlbumRepository>();
builder.Services.AddScoped<IArtistRepository, ArtistRepository>();
builder.Services.AddScoped<ITrackRepository, TrackRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

builder.Services.AddScoped<PasswordHasher<User>>();
builder.Services.AddHttpClient();

// Controllers + JSON options
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

// Limite upload
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 100 * 1024 * 1024; // 100MB
});

// Auth JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SecretKey"]!))
        };
    });

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost", policy =>
    {
        policy.WithOrigins("http://localhost:4200", "http://localhost")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Middleware
app.UseCors("AllowLocalhost");

app.Use(async (context, next) =>
{
    Console.WriteLine($"Incoming request: {context.Request.Method} {context.Request.Path}");
    await next.Invoke();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
