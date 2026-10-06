using AggStudentDiscounts.Api.DTOs;
using AggStudentDiscounts.Api.Services;
using AggStudentDiscounts.Domain.Entities;
using AggStudentDiscounts.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AggStudentDiscounts.Api.Controllers;

[ApiController]
[Authorize(Roles = Roles.Moderator)]
[Route("api/admin")]
public class AdminController(ApplicationDbContext context) : ControllerBase
{
    private IQueryable<Application> Applications =>
        context.Applications.Include(a => a.Photos).Where(a => a.Status != ApplicationStatus.Cancelled);

    [HttpGet("applications")]
    public async Task<ActionResult<ApplicationListResponse>> GetModerationQueue([FromQuery] string? status)
    {
        var query = Applications.AsNoTracking();

        if (ApplicationsController.TryParseStatus(status, out var parsedStatus))
        {
            query = query.Where(a => a.Status == parsedStatus);
        }
        else if (string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(a => a.Status == ApplicationStatus.OnModeration);
        }

        var items = await query.OrderBy(a => a.CreatedAt).ToListAsync();

        return Ok(new ApplicationListResponse
        {
            Items = items.Select(a => a.ToResponse()).ToList(),
            Total = items.Count
        });
    }

    [HttpPut("applications/{id:guid}/approve")]
    public async Task<ActionResult<PlaceResponse>> ApproveApplication(Guid id)
    {
        var application = await Applications.FirstOrDefaultAsync(a => a.Id == id);
        if (application == null)
        {
            return NotFound(new { message = "Заявка не найдена." });
        }

        try
        {
            application.Approve();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }

        await context.SaveChangesAsync();
        return Ok(application.ToPlace());
    }

    [HttpPut("applications/{id:guid}/reject")]
    public async Task<ActionResult<ApplicationResponse>> RejectApplication(Guid id, [FromBody] RejectApplicationRequest request)
    {
        var application = await Applications.FirstOrDefaultAsync(a => a.Id == id);
        if (application == null)
        {
            return NotFound(new { message = "Заявка не найдена." });
        }

        try
        {
            application.Reject(request.Reason.Trim());
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }

        await context.SaveChangesAsync();
        return Ok(application.ToResponse());
    }

    [HttpPut("applications/{id:guid}")]
    public async Task<ActionResult<ApplicationResponse>> EditApplication(Guid id, [FromBody] UpdateApplicationRequest request)
    {
        var application = await Applications.FirstOrDefaultAsync(a => a.Id == id);
        if (application == null)
        {
            return NotFound(new { message = "Заявка не найдена." });
        }

        ApplyChanges(application, request);
        await context.SaveChangesAsync();

        return Ok(application.ToResponse());
    }

    [HttpPut("places/{id:guid}")]
    public async Task<ActionResult<PlaceResponse>> EditPublishedPlace(Guid id, [FromBody] UpdateApplicationRequest request)
    {
        var application = await Applications.FirstOrDefaultAsync(a => a.Id == id && a.Status == ApplicationStatus.Published);
        if (application == null)
        {
            return NotFound(new { message = "Опубликованное заведение не найдено." });
        }

        ApplyChanges(application, request);
        await context.SaveChangesAsync();

        return Ok(application.ToPlace());
    }

    private static void ApplyChanges(Application application, UpdateApplicationRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.PlaceName))
        {
            application.EstablishmentName = request.PlaceName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Address))
        {
            application.Address = request.Address.Trim();
        }

        if (request.Latitude.HasValue)
        {
            application.Latitude = request.Latitude.Value;
        }

        if (request.Longitude.HasValue)
        {
            application.Longitude = request.Longitude.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.Discount))
        {
            application.DiscountDescription = request.Discount.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Conditions))
        {
            application.Conditions = request.Conditions.Trim();
        }

        // Для необязательных полей пустая строка очищает значение, отсутствие поля — не меняет
        if (request.ValidityPeriod is not null) application.ValidityPeriod = request.ValidityPeriod.NullIfBlank();
        if (request.SourceUrl is not null) application.SourceLink = request.SourceUrl.NullIfBlank();
        if (request.Category is not null) application.Category = request.Category.NullIfBlank();
        if (request.Phone is not null) application.Phone = request.Phone.NullIfBlank();
        if (request.Website is not null) application.Website = request.Website.NullIfBlank();
        if (request.WorkingHours is not null) application.WorkingHours = request.WorkingHours.NullIfBlank();
    }
}
