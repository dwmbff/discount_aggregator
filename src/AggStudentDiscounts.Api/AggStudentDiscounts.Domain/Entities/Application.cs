namespace AggStudentDiscounts.Domain.Entities;

public class Application
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string EstablishmentName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public string DiscountDescription { get; set; } = string.Empty;
    public string Conditions { get; set; } = string.Empty;
    public string? ValidityPeriod { get; set; }
    public string? SourceLink { get; set; }

    public ApplicationStatus Status { get; set; } = ApplicationStatus.OnModeration;
    public string? RejectionReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ModeratedAt { get; set; }

    /// <summary>Переход OnModeration → Published. Из других статусов невозможен.</summary>
    public void Approve()
    {
        EnsureOnModeration();
        Status = ApplicationStatus.Published;
        RejectionReason = null;
        ModeratedAt = DateTime.UtcNow;
    }

    /// <summary>Переход OnModeration → Rejected с обязательной причиной.</summary>
    public void Reject(string reason)
    {
        EnsureOnModeration();
        Status = ApplicationStatus.Rejected;
        RejectionReason = reason;
        ModeratedAt = DateTime.UtcNow;
    }

    private void EnsureOnModeration()
    {
        if (Status != ApplicationStatus.OnModeration)
        {
            throw new InvalidOperationException(
                $"Заявка уже обработана (статус: {Status}). Модерация возможна только для заявок со статусом OnModeration.");
        }
    }
}
