using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Amazon.S3;
using Amazon.Extensions.NETCore.Setup;
using EsizzleAPI.Middleware;
using EsizzleAPI.Repositories;
using EsizzleAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.AddFilter("Microsoft", LogLevel.Information);

// For local development, use connection string from appsettings
string connectionString = builder.Configuration.GetConnectionString("LoanmasterDatabase") 
    ?? await GetConnectionStringFromSecretsAsync();

// Add the connection string to the configuration so it can be accessed by repositories
builder.Configuration["ConnectionStrings:LoanmasterDatabase"] = connectionString;

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);

// Register the connection string as a service to be injected
builder.Services.AddScoped<IDbConnectionFactory>(sp => new DbConnectionFactory(connectionString));

// Register repositories with DI
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IOfferingRepository, OfferingRepository>();
builder.Services.AddScoped<ISaleRepository, SaleRepository>();
builder.Services.AddScoped<ILoanRepository, LoanRepository>();
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<ISecurityRepository, SecurityRepository>();

// Register ArrayClient service for external authentication
builder.Services.AddHttpClient<IArrayClient, ArrayClient>();
builder.Services.AddScoped<IArrayClient, ArrayClient>();

// Register AWS services
builder.Services.AddAWSService<Amazon.S3.IAmazonS3>();
builder.Services.AddScoped<IS3DocumentService, S3DocumentService>();

// Register PDF token service for secure PDF access
builder.Services.AddScoped<IPdfTokenService, PdfTokenService>();

// Configure JWT Bearer authentication
string secretKeyStr = Environment.GetEnvironmentVariable("TokenSecretKey") ?? "4R27CP2zd//HL2TXVbIhI+304UM2IMhetUXhJRcbYgg=";
var secretKey = Encoding.ASCII.GetBytes(secretKeyStr);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // In development environment, use relaxed token validation for mock authentication
    if (builder.Environment.IsDevelopment())
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = false, // Don't validate the signing key
            IssuerSigningKey = new SymmetricSecurityKey(secretKey),
            ValidateIssuer = false,          // Don't validate the issuer
            ValidateAudience = false,        // Don't validate the audience
            ValidateLifetime = false,        // Don't validate token expiration
            RequireExpirationTime = false    // Token doesn't need to have an expiration time
        };
        
        // For development: log token validation issues
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"Authentication failed: {context.Exception.Message}");
                return Task.CompletedTask;
            }
        };
    }
    else
    {
        // Production validation remains strict
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(secretKey),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidIssuer = "https://lsn.ffncorp.com",
            ValidAudience = "https://esizzle-api.ffncorp.com"
        };
    }
});

builder.Services.AddAuthorization();

// Add CORS for local development
builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalDevelopment", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "https://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add Swagger for API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("LocalDevelopment");
}

// Middleware to log incoming requests
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Incoming request: {Method} {Path}", context.Request.Method, context.Request.Path);
    await next();
});

app.UseAuthentication();
app.UseAuthorization();

// Add custom AuthorizedUser middleware after authorization
app.UseMiddleware<AuthorizedUserMiddleware>();

app.MapControllers();
app.MapGet("/", () => "Welcome to Esizzle API - Hydra Due Diligence Application");

app.Run();

async Task<string> GetConnectionStringFromSecretsAsync()
{
    using var client = new AmazonSecretsManagerClient();
    var secretArn = Environment.GetEnvironmentVariable("SECRET_ARN");

    if (string.IsNullOrEmpty(secretArn))
    {
        throw new InvalidOperationException("SECRET_ARN environment variable is not set.");
    }

    try
    {
        var request = new GetSecretValueRequest { SecretId = secretArn };
        var response = await client.GetSecretValueAsync(request);
        var secretString = response.SecretString;

        var json = JObject.Parse(secretString);
        var connStr = json["ConnStr"]?.ToString();

        if (string.IsNullOrEmpty(connStr))
        {
            throw new InvalidOperationException("Connection string (ConnStr) is missing from the secret.");
        }

        return connStr;
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException($"Failed to retrieve connection string from Secrets Manager: {ex.Message}", ex);
    }
}