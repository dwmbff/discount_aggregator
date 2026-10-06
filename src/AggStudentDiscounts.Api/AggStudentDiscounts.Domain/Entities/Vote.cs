namespace AggStudentDiscounts.Domain.Entities;

/// <summary>Голос пользователя об актуальности опубликованной скидки.</summary>
public class Vote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApplicationId { get; set; }
    public Application Application { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>true — скидка актуальна ("yes"), false — не актуальна ("no").</summary>
    public bool IsRelevant { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
