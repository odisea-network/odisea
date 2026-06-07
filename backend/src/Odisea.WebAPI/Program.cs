using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Odisea.Application;
using Odisea.Application.Common.Interfaces;
using Odisea.Domain.Enums;
using Odisea.Infrastructure;
using Odisea.Infrastructure.Data;
using Odisea.WebAPI.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, _, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// JWT bearer token validation — lives here because JwtBearerDefaults is Web-SDK-only.
var jwtSecret = builder.Configuration["Jwt:Secret"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "odisea-api",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "odisea-client",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        };
    });

// Authorization policies — PlatformAdmin satisfies every lower-tier policy.
builder.Services.AddAuthorization(opts =>
{
    opts.AddPolicy("PlatformAdmin", p =>
        p.RequireRole(UserRole.PlatformAdmin.ToString()));

    opts.AddPolicy("OperatorAdmin", p =>
        p.RequireRole(
            UserRole.PlatformAdmin.ToString(),
            UserRole.OperatorAdmin.ToString()));

    opts.AddPolicy("AgencyAdmin", p =>
        p.RequireRole(
            UserRole.PlatformAdmin.ToString(),
            UserRole.AgencyAdmin.ToString()));

    opts.AddPolicy("AgencyMember", p =>
        p.RequireRole(
            UserRole.PlatformAdmin.ToString(),
            UserRole.AgencyAdmin.ToString(),
            UserRole.AgencyEditor.ToString()));
});

// IHttpContextAccessor is needed by RequestContext to read claims.
builder.Services.AddHttpContextAccessor();

// Single scoped service fulfils both context interfaces.
builder.Services.AddScoped<RequestContext>();
builder.Services.AddScoped<IUserContext>(sp => sp.GetRequiredService<RequestContext>());
builder.Services.AddScoped<IAgencyContext>(sp => sp.GetRequiredService<RequestContext>());

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Odisea Network API", Version = "v1" });

    // Allow Swagger UI to send the Bearer token for protected endpoints.
    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT access token.",
    };
    c.AddSecurityDefinition("Bearer", scheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

const string DevCors = "DevCors";
builder.Services.AddCors(o => o.AddPolicy(DevCors, p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseCors(DevCors);
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Odisea API v1"));
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await Seeder.SeedAsync(db);
}

app.Run();
