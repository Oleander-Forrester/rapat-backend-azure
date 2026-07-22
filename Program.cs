using rapat_backend.Helpers;
using rapat_backend.Repositories.Implementations;
using rapat_backend.Repositories.Interfaces;
using rapat_backend.Services;
using rapat_backend.Services.Implementations;
using rapat_backend.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OfficeOpenXml; 
using System.Text;

namespace rapat_backend
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var configuration = builder.Configuration;


            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            var corsOrigins = configuration.GetSection("Key:corsAllowFrom").Get<string[]>()
                              ?? new[] { "http://localhost:3000" };

            var jwtKey = Environment.GetEnvironmentVariable("DECRYPT_KEY_JWT");
            if (string.IsNullOrEmpty(jwtKey))
                throw new InvalidOperationException("JWT Key not found in Env.");

            var issuer = configuration.GetSection("Key:jwtIssuer").Get<string[]>();
            var audience = configuration["Key:jwtAudience"];
            bool validateIssuer = issuer != null && !issuer.Contains("*");

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowSpecificOrigin",
                    policy =>
                    {
                        policy.SetIsOriginAllowed(origin => true)
                              .AllowAnyHeader()
                              .AllowAnyMethod()
                              .AllowCredentials();
                    });
            });

            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();

            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "ASTRATECH API",
                    Version = "v1"
                });

                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    In = ParameterLocation.Header,
                    Description = "Masukkan token: Bearer {token}",
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    BearerFormat = "JWT",
                    Scheme = "bearer"
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            builder.Services.AddAutoMapper(typeof(Program));
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddHttpClient();

            builder.Services.AddScoped<ILdapService, LdapService>();
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<IAuthorizationHandler, HasPermissionHandler>();
            builder.Services.AddScoped<IMicrosoftTeamsService, MicrosoftTeamsService>();
            builder.Services.AddScoped<IRapatRepository, RapatRepository>();
            builder.Services.AddScoped<IAzureStorageService, AzureStorageService>();

            builder.Services.AddAuthorizationBuilder()
                .AddPolicy("HasPermission", policy =>
                {
                    policy.AddRequirements(new HasPermissionRequirement(""));
                });

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = validateIssuer,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuers = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtKey!)
                    ),
                    ClockSkew = TimeSpan.Zero
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = HandleJwtMessageReceived,
                    OnAuthenticationFailed = HandleJwtAuthenticationFailed
                };
            });

            builder.Services.Configure<ApiBehaviorOptions>(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var errors = context.ModelState
                        .Where(ms => ms.Value?.Errors.Count > 0)
                        .Select(ms => new
                        {
                            field = ms.Key,
                            error = ms.Value!.Errors.Select(e => e.ErrorMessage)
                        });

                    return new BadRequestObjectResult(new
                    {
                        message = "Payload invalid",
                        errors
                    });
                };
            });

            var app = builder.Build();

            var wwwrootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            if (!Directory.Exists(wwwrootPath))
            {
                Directory.CreateDirectory(wwwrootPath);
            }

            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(wwwrootPath),
                RequestPath = ""
            });

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseCors("AllowSpecificOrigin");

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();
            app.Run();
        }

        private static Task HandleJwtMessageReceived(MessageReceivedContext context)
        {
            var authHeader = context.Request.Headers["Authorization"].ToString();
            if (!string.IsNullOrEmpty(authHeader) &&
                authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                context.Token = authHeader.Substring("Bearer ".Length).Trim();
            }

            if (string.IsNullOrEmpty(context.Token) && context.Request.Cookies.Count > 0)
            {
                context.Token = GetTokenFromCookies(context);
            }

            return Task.CompletedTask;
        }

        private static string? GetTokenFromCookies(MessageReceivedContext context)
        {
            var tokenNames = new[] { "jwtToken", "token", "JWT" };
            foreach (var name in tokenNames)
            {
                if (context.Request.Cookies.TryGetValue(name, out var cookieValue))
                {
                    return cookieValue;
                }
            }
            return null;
        }

        private static Task HandleJwtAuthenticationFailed(AuthenticationFailedContext context)
        {
            if (context.Exception is SecurityTokenExpiredException)
            {
                context.Response.Headers["Token-Expired"] = "true";
            }
            return Task.CompletedTask;
        }
    }
}
