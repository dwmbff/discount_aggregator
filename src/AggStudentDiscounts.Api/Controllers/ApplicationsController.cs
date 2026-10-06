using AggStudentDiscounts.Api.DTOs;
using AggStudentDiscounts.Api.Services;
using AggStudentDiscounts.Domain.Entities;
using AggStudentDiscounts.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AggStudentDiscounts.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/applications")]
public class ApplicationsController(
    ApplicationDbContext context,
    IFileStorage storage,
    IGeocodingService geocoding) : ControllerBase
{
    public const int DailyLimit = 4;

    [HttpGet]
    public async Task<ActionResult<ApplicationListResponse>> GetApplications([FromQuery] string? status)
    {
        var userId = User.GetUserId();

        // Отменённые заявки пользователю не показываются (но учитываются в суточном лимите)
        var query = context.Applications
            .AsNoTracking()
            .Include(a => a.Photos)
            .Where(a => a.UserId == userId && a.Status != ApplicationStatus.Cancelled);

        if (TryParseStatus(status, out var parsedStatus))
        {
            query = query.Where(a => a.Status == parsedStatus);
        }

        var items = await query.OrderByDescending(a => a.CreatedAt).ToListAsync();

        return Ok(new ApplicationListResponse
        {
            Items = items.Select(a => a.ToResponse()).ToList(),
            Total = items.Count
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApplicationResponse>> GetApplication(Guid id)
    {
        var application = await context.Applications
            .AsNoTracking()
            .Include(a => a.Photos)
            .FirstOrDefaultAsync(a => a.Id == id && a.Status != ApplicationStatus.Cancelled);

        if (application == null)
        {
            return NotFound(new { message = "Заявка не найдена." });
        }

        if (application.UserId != User.GetUserId() && !User.IsModerator())
        {
            return Forbid();
        }

        return Ok(application.ToResponse());
    }

    [HttpPost]
    [RequestSizeLimit(30_000_000)]
    public async Task<ActionResult<ApplicationResponse>> CreateApplication([FromForm] CreateApplicationRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();

        // Лимит проверяется по данным БД. Отменённые заявки входят в счётчик. Модераторы не ограничены (BR-03).
        if (!User.IsModerator())
        {
            var todayUtc = DateTime.UtcNow.Date;
            var tomorrowUtc = todayUtc.AddDays(1);
            var submittedToday = await context.Applications.CountAsync(a =>
                a.UserId == userId && a.CreatedAt >= todayUtc && a.CreatedAt < tomorrowUtc, ct);

            if (submittedToday >= DailyLimit)
            {
                return StatusCode(StatusCodes.Status429TooManyRequests,
                    new { message = $"Лимит заявок: не более {DailyLimit} в сутки." });
            }
        }

        var application = new Application
        {
            UserId = userId,
            EstablishmentName = request.PlaceName.Trim(),
            Address = request.Address.Trim(),
            Latitude = request.Latitude ?? 0,
            Longitude = request.Longitude ?? 0,
            DiscountDescription = request.Discount.Trim(),
            Conditions = request.Conditions.Trim(),
            ValidityPeriod = request.ValidityPeriod.NullIfBlank(),
            SourceLink = request.SourceUrl.NullIfBlank(),
            Category = request.Category.NullIfBlank(),
            Phone = request.Phone.NullIfBlank(),
            Website = request.Website.NullIfBlank(),
            WorkingHours = request.WorkingHours.NullIfBlank()
        };

        await EnrichAsync(application, request.ExternalId, ct);

        foreach (var file in request.Photos)
        {
            application.Photos.Add(new ApplicationPhoto
            {
                FileName = Path.GetFileName(file.FileName),
                StoredName = await storage.SaveAsync(file, ct),
                ContentType = UploadRules.ContentTypeFor(file.FileName),
                Size = file.Length
            });
        }

        context.Applications.Add(application);
        await context.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetApplication), new { id = application.Id }, application.ToResponse());
    }

    /// <summary>Отмена заявки до рассмотрения. Заявка скрывается от пользователя, но остаётся в суточном счётчике.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> CancelApplication(Guid id)
    {
        var application = await context.Applications
            .FirstOrDefaultAsync(a => a.Id == id && a.Status != ApplicationStatus.Cancelled);

        if (application == null)
        {
            return NotFound(new { message = "Заявка не найдена." });
        }

        if (application.UserId != User.GetUserId())
        {
            return Forbid();
        }

        try
        {
            application.Cancel();
        }
        catch (InvalidOperationException)
        {
            return Conflict(new { message = "Можно отменить только заявку со статусом 'на проверке'." });
        }

        await context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Автообогащение: пустые атрибуты заявки заполняются данными объекта из геосервиса.
    /// Недоступность сервиса не блокирует подачу заявки — данные остаются введёнными вручную.
    /// </summary>
    private async Task EnrichAsync(Application application, string? externalId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(externalId))
        {
            return;
        }

        try
        {
            var place = await geocoding.LookupAsync(externalId.Trim(), ct);
            if (place == null)
            {
                return;
            }

            application.Category ??= place.Category;
            application.Phone ??= place.Phone;
            application.Website ??= place.Website;
            application.WorkingHours ??= place.WorkingHours;
        }
        catch (GeocodingUnavailableException)
        {
            // см. комментарий к методу
        }
    }

    internal static bool TryParseStatus(string? status, out ApplicationStatus parsed)
    {
        parsed = default;
        switch (status?.Trim().ToLowerInvariant())
        {
            case "onmoderation" or "pending" or "на проверке":
                parsed = ApplicationStatus.OnModeration;
                return true;
            case "published" or "approved" or "опубликовано":
                parsed = ApplicationStatus.Published;
                return true;
            case "rejected" or "отклонено":
                parsed = ApplicationStatus.Rejected;
                return true;
            default:
                return false;
        }
    }
}
