using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

//配置 JWT 認證

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };
});

builder.Services.AddAuthorization(options =>
{

    options.AddPolicy("SecureEndpointPolicy", policy =>
        policy.RequireRole("SecureRole"));

    options.FallbackPolicy = options.DefaultPolicy;
});

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/login", [AllowAnonymous] async (HttpContext context) =>
{
    var request = await context.Request.ReadFromJsonAsync<LoginRequest>();
    if (request == null || request.Username != "test" || request.Password != "password")
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }

    var claims = new[]
    {
        new Claim(JwtRegisteredClaimNames.Sub, request.Username),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        new Claim(ClaimTypes.Role, "SecureRole") // 賦予角色
    };

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
        issuer: builder.Configuration["Jwt:Issuer"],
        audience: builder.Configuration["Jwt:Audience"],
        claims: claims,
        expires: DateTime.Now.AddMinutes(30),
        signingCredentials: creds);

    await context.Response.WriteAsync(new JwtSecurityTokenHandler().WriteToken(token));
});

app.MapGet("/login", () => "This is a public endpoint").AllowAnonymous();
app.MapGet("/secure", [Authorize] () => "This is a secure endpoint.");

app.MapGet("/secure/endpoint1", () => "This is a secure endpoint 1").RequireAuthorization("SecureEndpointPolicy");
app.MapGet("/secure/endpoint2", () => "This is a secure endpoint 2").RequireAuthorization("SecureEndpointPolicy");
app.MapGet("/public/endpoint", () => "This is a public endpoint").AllowAnonymous();


app.Run();

public record LoginRequest(string Username, string Password);
