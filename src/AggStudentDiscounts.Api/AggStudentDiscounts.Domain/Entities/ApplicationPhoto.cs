namespace AggStudentDiscounts.Domain.Entities;

/// <summary>Файл-подтверждение скидки (фото меню, буклета, рекламы).</summary>
public class ApplicationPhoto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApplicationId { get; set; }
    public Application Application { get; set; } = null!;

    public string FileName { get; set; } = string.Empty;
    public string StoredName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
