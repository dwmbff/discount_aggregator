namespace AggStudentDiscounts.Domain.Entities;

public enum ApplicationStatus
{
    OnModeration,
    Published,
    Rejected,
    /// <summary>Отменена автором до рассмотрения. Пользователю не показывается, но учитывается в суточном лимите.</summary>
    Cancelled
}
