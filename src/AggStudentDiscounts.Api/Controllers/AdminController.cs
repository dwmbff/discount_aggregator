using AggStudentDiscounts.Api.DTOs;
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
    [HttpGet("applications")]
    public async Task<ActionResult<ApplicationListResponse>> GetModerationQueue([FromQuery] string? status)
    {
        var query = context.Applications.AsNoTracking();

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
            Items = items.Select(a => ApplicationsController.MapToResponse(a)).ToList(),
            Total = items.Count
        });
    }

    [HttpPut("applications/{id:guid}/approve")]
    public async Task<ActionResult<PlaceResponse>> ApproveApplication(Guid id)
    {
        var application = await context.Applications.FirstOrDefaultAsync(a => a.Id == id);
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
        return Ok(MapToPlaceResponse(application));
    }

    [HttpPut("applications/{id:guid}/reject")]
    public async Task<ActionResult<ApplicationResponse>> RejectApplication(Guid id, [FromBody] RejectApplicationRequest request)
    {
        var application = await context.Applications.FirstOrDefaultAsync(a => a.Id == id);
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
        return Ok(ApplicationsController.MapToResponse(application));
    }

    [HttpPut("applications/{id:guid}")]
    public async Task<ActionResult<ApplicationResponse>> EditApplication(Guid id, [FromBody] UpdateApplicationRequest request)
    {
        var application = await context.Applications.FirstOrDefaultAsync(a => a.Id == id);
        if (application == null)
        {
            return NotFound(new { message = "Заявка не найдена." });
        }

        ApplyApplicationChanges(application, request);
        await context.SaveChangesAsync();

        return Ok(ApplicationsController.MapToResponse(application));
    }

    [HttpPut("places/{id:guid}")]
    public async Task<ActionResult<PlaceResponse>> EditPublishedPlace(Guid id, [FromBody] UpdateApplicationRequest request)
    {
        var application = await context.Applications
            .FirstOrDefaultAsync(a => a.Id == id && a.Status == ApplicationStatus.Published);

        if (application == null)
        {
            return NotFound(new { message = "Опубликованное заведение не найдено." });
        }

        ApplyApplicationChanges(application, request);
        await context.SaveChangesAsync();

        return Ok(MapToPlaceResponse(application));
    }

    private static void ApplyApplicationChanges(Application application, UpdateApplicationRequest request)
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

        if (request.ValidityPeriod is not null)
        {
            application.ValidityPeriod = string.IsNullOrWhiteSpace(request.ValidityPeriod) ? null : request.ValidityPeriod.Trim();
        }

        if (request.SourceUrl is not null)
        {
            application.SourceLink = string.IsNullOrWhiteSpace(request.SourceUrl) ? null : request.SourceUrl.Trim();
        }
    }

    private static PlaceResponse MapToPlaceResponse(Application application) => new()
    {
        Id = application.Id,
        Name = application.EstablishmentName,
        Address = application.Address,
        Latitude = application.Latitude,
        Longitude = application.Longitude,
        Discount = application.DiscountDescription,
        Conditions = application.Conditions,
        ValidityPeriod = application.ValidityPeriod,
        Status = application.Status.ToString()
    };
}
