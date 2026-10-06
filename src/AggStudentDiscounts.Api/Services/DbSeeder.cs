using AggStudentDiscounts.Domain.Entities;
using AggStudentDiscounts.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AggStudentDiscounts.Api.Services;

/// <summary>Демо-данные для локального запуска (включается Seed:Enabled=true).</summary>
public static class DbSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db, IConfiguration config)
    {
        var password = config["Seed:Password"] ?? "Demo12345!";

        if (!await db.Users.AnyAsync())
        {
            var moderator = NewUser("moderator@example.com", "Модератор", Roles.Moderator, password);
            var student = NewUser("student@example.com", "Анна", Roles.User, password);
            db.Users.AddRange(moderator, student);

            db.Applications.AddRange(
                NewApp(student.Id, "Кофейня «Зерно»", "Москва, ул. Тверская, 10", 55.7649, 37.6061,
                    "-15% на все напитки", "По студенческому билету, будни до 16:00", ApplicationStatus.Published),
                NewApp(student.Id, "Книжный клуб «Страница»", "Москва, Покровка, 5", 55.7599, 37.6495,
                    "-10% на книги", "По студенческому билету", ApplicationStatus.Published),
                NewApp(student.Id, "Пиццерия «Сыр»", "Москва, Арбат, 20", 55.7522, 37.5927,
                    "Бесплатный напиток к пицце", "Только по четвергам", ApplicationStatus.OnModeration));

            await db.SaveChangesAsync();
        }
    }

    private static User NewUser(string email, string name, string role, string password) => new()
    {
        Email = email,
        Nickname = name,
        University = "МГУ",
        Department = "ВМК",
        Course = 2,
        Role = role,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
    };

    private static Application NewApp(Guid userId, string name, string address, double lat, double lng,
        string discount, string conditions, ApplicationStatus status) => new()
    {
        UserId = userId,
        EstablishmentName = name,
        Address = address,
        Latitude = lat,
        Longitude = lng,
        DiscountDescription = discount,
        Conditions = conditions,
        Status = status,
        ModeratedAt = status == ApplicationStatus.OnModeration ? null : DateTime.UtcNow
    };
}
