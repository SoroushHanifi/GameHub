using AutoMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PokerService.Data;
using PokerService.Hubs;
using PokerService.Services;
using StackExchange.Redis;
using System.Security.Claims;



namespace PokerService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            // Add services to the container.
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // تنظیم AutoMapper
            var mapperConfig = new MapperConfiguration(cfg => cfg.AddProfile<AutoMapperProfile>());
            builder.Services.AddSingleton(mapperConfig.CreateMapper());

            // تنظیم دیتابیس
            builder.Services.AddDbContext<PokerDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            // تنظیم Redis
            builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")));
            builder.Services.AddScoped<GameService>();
            builder.Services.AddScoped<GameLogic>();
            builder.Services.AddSingleton<CardDealer>();

            // تنظیم SignalR
            builder.Services.AddSignalR();

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
              .AddJwtBearer(options =>
              {
                  options.Authority = builder.Configuration["AuthService:Authority"]; // یا نگذار
                  options.TokenValidationParameters = new TokenValidationParameters
                  {
                      ValidateIssuer = true,
                      ValidateAudience = true,
                      ValidateLifetime = true,
                      ValidateIssuerSigningKey = true,
                      ValidIssuer = builder.Configuration["Jwt:Issuer"],
                      ValidAudience = builder.Configuration["Jwt:Audience"],

                      NameClaimType = ClaimTypes.Name,               // این claim برای Identity.Name
                      RoleClaimType = ClaimTypes.Role               // این claim برای نقش‌ها
                  };

                  options.Events = new JwtBearerEvents
                  {
                      OnMessageReceived = context =>
                      {
                          var accessToken = context.Request.Query["access_token"];
                          var path = context.HttpContext.Request.Path;
                          if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/pokerHub"))
                          {
                              context.Token = accessToken;
                          }
                          return Task.CompletedTask;
                      }
                  };
              });


            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials()
                        .SetIsOriginAllowed(origin => true); // اجازه همه origin ها
                });
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseCors("AllowAll"); // قبل از Authentication و Authorization
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();
            app.MapHub<PokerHub>("/pokerHub");

            app.Run();
        }
    }
}
