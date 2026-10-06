namespace AggStudentDiscounts.Domain.Entities;

/// <summary>Кэш ответов геоинформационного сервиса (минимум 24 часа).</summary>
public class GeocodeCacheEntry
{
    public string Key { get; set; } = string.Empty;
    public string ResponseJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
}
