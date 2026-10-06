using System.ComponentModel.DataAnnotations;

namespace AggStudentDiscounts.Domain.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public string Nickname { get; set; } = string.Empty;
    public string University { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public int Course { get; set; }
    public string? Telegram { get; set; }

    public string Role { get; set; } = "User"; 

    public DateTime DateRegistered { get; set; } = DateTime.UtcNow;
    public DateTime LastProfileUpdate { get; set; } = DateTime.UtcNow;
    public bool IsProfileConfirmed { get; set; } = true;

    /// <summary>Повышает курс на 1 за каждое 1 октября, прошедшее с последнего обновления профиля.</summary>
    public void RefreshCourse(DateTime utcNow)
    {
        var passed = 0;
        for (var year = LastProfileUpdate.Year; year <= utcNow.Year; year++)
        {
            var october = new DateTime(year, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            if (october > LastProfileUpdate && october <= utcNow)
            {
                passed++;
            }
        }

        if (passed > 0)
        {
            Course += passed;
            LastProfileUpdate = utcNow;
        }
    }
}
