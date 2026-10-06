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
public class ApplicationsController(ApplicationDbContext context) : ControllerBase
{
    public const int DailyLimit = 4;

    [HttpGet]
    public async Task<ActionResult<ApplicationListResponse>> GetApplications([FromQuery] string? status)
    {
        var userId = User.GetUserId();
        var query = context.Applications.AsNoTracking().Where(a => a.UserId == userId);

        if (TryParseStatus(status, out var parsedStatus))
        {
            query = query.Where(a => a.Status == parsedStatus);
        }

        var items = await query.OrderByDescending(a => a.CreatedAt).ToListAsync();

        return Ok(new ApplicationListResponse
        {
            Items = items.Select(a => MapToResponse(a)).ToList(),
            Total = items.Count
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApplicationResponse>> GetApplication(Guid id)
    {
        var application = await context.Applications.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        if (application == null)
        {
            return NotFound(new { message = "Заявка не найдена." });
        }

        if (application.UserId != User.GetUserId() && !User.IsModerator())
        {
            return Forbid();
        }

        return Ok(MapToResponse(application));
    }

    [HttpPost]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<ApplicationResponse>> CreateApplication([FromForm] CreateApplicationRequest request)
    {
        var userId = User.GetUserId();

        // Модераторы лимитом не ограничены (BR-03)
        if (!User.IsModerator())
        {
            var todayUtc = DateTime.UtcNow.Date;
            var tomorrowUtc = todayUtc.AddDays(1);
            var submittedToday = await context.Applications.CountAsync(a =>
                a.UserId == userId && a.CreatedAt >= todayUtc && a.CreatedAt < tomorrowUtc);

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
            ValidityPeriod = string.IsNullOrWhiteSpace(request.ValidityPeriod) ? null : request.ValidityPeriod.Trim(),
            SourceLink = string.IsNullOrWhiteSpace(request.SourceUrl) ? null : request.SourceUrl.Trim()
        };

        context.Applications.Add(application);
        await context.SaveChangesAsync();

        var response = MapToResponse(application, request.Photos.Select(p => p.FileName).ToList());
        return CreatedAtAction(nameof(GetApplication), new { id = application.Id }, response);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> CancelApplication(Guid id)
    {
        var application = await context.Applications.FirstOrDefaultAsync(a => a.Id == id);
        if (application == null)
        {
            return NotFound(new { message = "Заявка не найдена." });
        }

        if (application.UserId != User.GetUserId())
        {
            return Forbid();
        }

        if (application.Status != ApplicationStatus.OnModeration)
        {
            return Conflict(new { message = "Можно отменить только заявку со статусом 'на проверке'." });
        }

        context.Applications.Remove(application);
        await context.SaveChangesAsync();
        return NoContent();
    }

    internal static ApplicationResponse MapToResponse(Application application, List<string>? photos = null) => new()
    {
        Id = application.Id,
        UserId = application.UserId,
        PlaceName = application.EstablishmentName,
        Address = application.Address,
        Latitude = application.Latitude,
        Longitude = application.Longitude,
        Discount = application.DiscountDescription,
        Conditions = application.Conditions,
        ValidityPeriod = application.ValidityPeriod,
        SourceUrl = application.SourceLink,
        Photos = photos ?? [],
        Status = application.Status.ToString(),
        RejectionReason = application.RejectionReason,
        CreatedAt = application.CreatedAt,
        ModeratedAt = application.ModeratedAt
    };

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
