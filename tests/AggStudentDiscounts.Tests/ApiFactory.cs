using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AggStudentDiscounts.Api.DTOs;
using AggStudentDiscounts.Domain.Entities;
using AggStudentDiscounts.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AggStudentDiscounts.Api.Services;

namespace AggStudentDiscounts.Tests;

/// <summary>Поднимает API целиком в памяти: реальный пайплайн, JWT и валидация, БД — EF InMemory.</summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly string _storagePath = Path.Combine(Path.GetTempPath(), "agg-tests-" + Guid.NewGuid().ToString("N"));

    public StubGeoHandler Geo { get; } = new();

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_storagePath))
        {
            Directory.Delete(_storagePath, true);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=test;Database=test;Username=test;Password=test");
        builder.UseSetting("Jwt:Key", "test-signing-key-test-signing-key-123456");
        builder.UseSetting("Storage:Path", _storagePath);

        builder.ConfigureServices(services =>
        {
            var toRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                            d.ServiceType.IsGenericType &&
                            d.ServiceType.GetGenericTypeDefinition().Name.StartsWith("IDbContextOptionsConfiguration"))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(_dbName));

            // Внешний геосервис заменён заглушкой — тесты не ходят в сеть
            services.AddHttpClient<IGeocodingService, NominatimGeocodingService>()
                .ConfigurePrimaryHttpMessageHandler(() => Geo);
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

    public static readonly byte[] JpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];

    public static MultipartFormDataContent NewApplicationForm(string placeName = "Кофейня «Зерно»", bool withPhoto = true, string? externalId = null)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(placeName), "PlaceName" },
            { new StringContent("Москва, Тверская, 10"), "Address" },
            { new StringContent("-15% на напитки"), "Discount" },
            { new StringContent("По студенческому билету"), "Conditions" }
        };

        if (externalId != null)
        {
            form.Add(new StringContent(externalId), "ExternalId");
        }

        if (withPhoto)
        {
            var file = new ByteArrayContent(JpegBytes);
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

/// <summary>Заглушка Nominatim: считает вызовы и умеет «падать».</summary>
public class StubGeoHandler : HttpMessageHandler
{
    public const string CafeJson = """
        [{"osm_type":"node","osm_id":12345,"lat":"55.7649","lon":"37.6061","name":"Зерно",
          "display_name":"Зерно, Тверская улица, Москва","type":"cafe",
          "extratags":{"phone":"+7 495 000-00-00","website":"https://zerno.example","opening_hours":"Mo-Su 08:00-22:00"}}]
        """;

    public int Calls { get; private set; }
    public bool Fail { get; set; }
    public string Json { get; set; } = CafeJson;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        if (Fail)
        {
            throw new HttpRequestException("upstream down");
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(Json, System.Text.Encoding.UTF8, "application/json")
        });
    }
}
