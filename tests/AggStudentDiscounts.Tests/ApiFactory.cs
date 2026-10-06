using System.Net.Http.Headers;
using System.Net.Http.Json;
using AggStudentDiscounts.Api.DTOs;
using AggStudentDiscounts.Domain.Entities;
using AggStudentDiscounts.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AggStudentDiscounts.Tests;

/// <summary>Поднимает API целиком в памяти: реальный пайплайн, JWT и валидация, БД — EF InMemory.</summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=test;Database=test;Username=test;Password=test");
        builder.UseSetting("Jwt:Key", "test-signing-key-test-signing-key-123456");

        builder.ConfigureServices(services =>
        {
            var toRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                            d.ServiceType.IsGenericType &&
                            d.ServiceType.GetGenericTypeDefinition().Name.StartsWith("IDbContextOptionsConfiguration"))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(_dbName));
        });
    }

    public async Task<HttpClient> CreateUserClientAsync(string email = "student@test.com")
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "Password123",
            Name = "Анна",
            University = "МГУ",
            Department = "ВМК",
            Course = 2
        });
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    public async Task<HttpClient> CreateModeratorClientAsync()
    {
        const string email = "moderator@test.com";
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Users.Add(new User
            {
                Email = email,
                Nickname = "Модератор",
                University = "МГУ",
                Department = "ВМК",
                Course = 3,
                Role = Roles.Moderator,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123")
            });
            await db.SaveChangesAsync();
        }

        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = "Password123" });
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    public static MultipartFormDataContent NewApplicationForm(string placeName = "Кофейня «Зерно»", bool withPhoto = true)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(placeName), "PlaceName" },
            { new StringContent("Москва, Тверская, 10"), "Address" },
            { new StringContent("-15% на напитки"), "Discount" },
            { new StringContent("По студенческому билету"), "Conditions" }
        };

        if (withPhoto)
        {
            var file = new ByteArrayContent([1, 2, 3]);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            form.Add(file, "Photos", "proof.jpg");
        }

        return form;
    }

    public static async Task<Guid> SubmitApplicationAsync(HttpClient client, string placeName = "Кофейня «Зерно»")
    {
        var response = await client.PostAsync("/api/applications", NewApplicationForm(placeName));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApplicationResponse>())!.Id;
    }
}
