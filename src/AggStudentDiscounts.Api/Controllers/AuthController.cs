using AggStudentDiscounts.Api.DTOs;
using AggStudentDiscounts.Api.Services;
using AggStudentDiscounts.Domain.Entities;
using AggStudentDiscounts.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AggStudentDiscounts.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(ApplicationDbContext context, ITokenService tokens) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await context.Users.AnyAsync(u => u.Email == email))
        {
            return Conflict(new { message = "Пользователь с таким email уже существует." });
        }

        var user = new User
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Nickname = request.Name.Trim(),
            University = request.University.Trim(),
            Department = request.Department.Trim(),
            Course = request.Course,
            Telegram = string.IsNullOrWhiteSpace(request.Telegram) ? null : request.Telegram.Trim(),
            Role = Roles.User
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return Created("/api/auth/me", BuildAuthResponse(user));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await context.Users.FirstOrDefaultAsync(u => u.Email == email);

        // Одно сообщение для неверного email и пароля — не раскрываем, какие аккаунты существуют.
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Неверный email или пароль." });
        }

        return Ok(BuildAuthResponse(user));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserResponse>> GetCurrentUser()
    {
        var user = await context.Users.FindAsync(User.GetUserId());
        if (user == null)
        {
            return NotFound(new { message = "Пользователь не найден." });
        }

        user.RefreshCourse(DateTime.UtcNow);
        await context.SaveChangesAsync();

        return Ok(MapToResponse(user));
    }

    private AuthResponse BuildAuthResponse(User user)
    {
        var (token, expiresIn) = tokens.Create(user);
        return new AuthResponse { AccessToken = token, ExpiresIn = expiresIn, User = MapToResponse(user) };
    }

    private static UserResponse MapToResponse(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        Name = user.Nickname,
        University = user.University,
        Department = user.Department,
        Course = user.Course,
        Telegram = user.Telegram,
        Role = user.Role
    };
}
