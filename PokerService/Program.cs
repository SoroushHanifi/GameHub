// PokerService/Program.cs
using AutoMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using PokerService.Data;
using PokerService.Hubs;
using PokerService.Services;
using StackExchange.Redis;
using System;
using System.Text;
using System.Threading.Tasks;

namespace PokerService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new() { Title = "Poker Service API", Version = "v1" });

                // Add JWT Authentication to Swagger
                c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme",
                    Name = "Authorization",
                    In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                    Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });

                c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
                {
                    {
                        new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                        {
                            Reference = new Microsoft.OpenApi.Models.OpenApiReference
                            {
                                Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            // Configure AutoMapper
            var mapperConfig = new MapperConfiguration(cfg => cfg.AddProfile<AutoMapperProfile>());
            builder.Services.AddSingleton(mapperConfig.CreateMapper());

            // Configure Database
            builder.Services.AddDbContext<PokerDbContext>(options =>
                options.UseSqlServer(
                    builder.Configuration.GetConnectionString("DefaultConnection"),
                    sqlOptions => sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null)));

            // Configure Redis with retry logic
            builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var configuration = ConfigurationOptions.Parse(
                    builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379");

                configuration.AbortOnConnectFail = false;
                configuration.ConnectRetry = 3;
                configuration.ConnectTimeout = 5000;
                configuration.SyncTimeout = 5000;
                configuration.AsyncTimeout = 5000;

                return ConnectionMultiplexer.Connect(configuration);
            });

            // Register Services
            builder.Services.AddScoped<GameService>();
            builder.Services.AddScoped<GameLogic>();
            builder.Services.AddSingleton<CardDealer>();

            // Configure SignalR
            builder.Services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = builder.Environment.IsDevelopment();
                options.KeepAliveInterval = TimeSpan.FromSeconds(15);
                options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
                options.MaximumReceiveMessageSize = 102400; // 100KB
            })
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            });

            // Configure JWT Authentication
            var jwtSettings = builder.Configuration.GetSection("Jwt");
            var key = Encoding.UTF8.GetBytes(jwtSettings["Key"] ?? "vGWDpQVERufpwMVUhflgcahkNST3hDPF");
            var accessTokenCookieName = jwtSettings["AccessTokenCookieName"] ?? "auth_access";

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidAudience = jwtSettings["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = "username" // Map username claim to Name
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        // Read token from cookie for regular requests
                        var token = context.Request.Cookies[accessTokenCookieName];

                        // For SignalR, also check query string
                        if (string.IsNullOrEmpty(token) &&
                            context.Request.Path.StartsWithSegments("/pokerHub"))
                        {
                            token = context.Request.Query["access_token"];
                        }

                        if (!string.IsNullOrEmpty(token))
                        {
                            context.Token = token;
                        }

                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                        {
                            context.Response.Headers.Add("Token-Expired", "true");
                        }
                        return Task.CompletedTask;
                    }
                };
            });

            builder.Services.AddAuthorization();

            // Configure CORS
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("PokerCorsPolicy", policy =>
                {
                    policy.WithOrigins(
                            "http://localhost:3000",
                            "http://localhost:5173",
                            "https://localhost:44393",
                            "http://127.0.0.1:5500") // For Live Server
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials()
                        .WithExposedHeaders("Token-Expired");

                    // For development - allow all origins
                    if (builder.Environment.IsDevelopment())
                    {
                        policy.SetIsOriginAllowed(origin => true);
                    }
                });
            });

            // Add Health Checks with custom implementations
            builder.Services.AddHealthChecks()
                .AddTypeActivatedCheck<DatabaseHealthCheck>(
                    "database",
                    failureStatus: HealthStatus.Unhealthy,
                    tags: new[] { "db", "sql" })
                .AddTypeActivatedCheck<RedisHealthCheck>(
                    "redis",
                    failureStatus: HealthStatus.Unhealthy,
                    tags: new[] { "cache", "redis" });

            // Add Hosted Service for cleanup
            builder.Services.AddHostedService<CleanupService>();

            var app = builder.Build();

            // Configure the HTTP request pipeline
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Poker Service API v1");
                    c.RoutePrefix = string.Empty; // Swagger at root
                });
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseCors("PokerCorsPolicy");
            app.UseAuthentication();
            app.UseAuthorization();

            // Map endpoints
            app.MapControllers();
            app.MapHub<PokerHub>("/pokerHub");

            // Map health checks with detailed response
            app.MapHealthChecks("/health", new HealthCheckOptions
            {
                ResponseWriter = async (context, report) =>
                {
                    context.Response.ContentType = "application/json";

                    var result = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        status = report.Status.ToString(),
                        checks = report.Entries.Select(x => new
                        {
                            name = x.Key,
                            status = x.Value.Status.ToString(),
                            description = x.Value.Description,
                            duration = x.Value.Duration.TotalMilliseconds
                        }),
                        totalDuration = report.TotalDuration.TotalMilliseconds
                    });

                    await context.Response.WriteAsync(result);
                }
            });

            // Error handling endpoint
            app.Map("/error", (HttpContext context) =>
            {
                var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
                var error = feature?.Error;

                return Results.Problem(
                    detail: app.Environment.IsDevelopment() ? error?.StackTrace : null,
                    title: error?.Message ?? "An error occurred",
                    statusCode: 500
                );
            });

            // Initialize database on startup
            using (var scope = app.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<PokerDbContext>();
                try
                {
                    dbContext.Database.Migrate();
                    Console.WriteLine("Database migrated successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Database migration failed: {ex.Message}");
                    // In production, you might want to handle this differently
                }
            }

            app.Run();
        }
    }

    // Background service for cleaning up inactive rooms
    public class CleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CleanupService> _logger;
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(30);

        public CleanupService(IServiceProvider serviceProvider, ILogger<CleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var gameService = scope.ServiceProvider.GetRequiredService<GameService>();
                        await gameService.CleanupInactiveRoomsAsync();
                        _logger.LogInformation("Cleaned up inactive rooms");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error cleaning up inactive rooms");
                }

                await Task.Delay(_cleanupInterval, stoppingToken);
            }
        }
    }
}