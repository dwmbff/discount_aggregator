using AggStudentDiscounts.Api.DTOs;
using AggStudentDiscounts.Api.Services;
using AggStudentDiscounts.Domain.Entities;
using AggStudentDiscounts.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AggStudentDiscounts.Api.Controllers;

[ApiController]
[Route("api/establishments")]
public class EstablishmentsController(ApplicationDbContext context) : ControllerBase
{
    public static readonly TimeSpan RevoteInterval = TimeSpan.FromDays(15);

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PlaceListResponse>> GetAllEstablishments(
        [FromQuery] string? search,
        [FromQuery] string? district,
        [FromQuery] double? radius,
        [FromQuery] double? lat,
        [FromQuery] double? lng,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortOrder,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = context.Applications
            .AsNoTracking()
            .Include(a => a.Photos)
            .Where(a => a.Status == ApplicationStatus.Published);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(a => a.EstablishmentName.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(district))
        {
            var term = district.Trim().ToLower();
            query = query.Where(a => a.Address.ToLower().Contains(term));
        }

        var establishments = await query.ToListAsync();

        if (radius.HasValue && lat.HasValue && lng.HasValue)
        {
            establishments = establishments
                .Where(a => CalculateDistanceMeters(lat.Value, lng.Value, a.Latitude, a.Longitude) <= radius.Value)
                .ToList();
        }

        establishments = ApplySorting(establishments, sortBy, sortOrder);

        var pageItems = establishments.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var ids = pageItems.Select(a => a.Id).ToList();

        var votes = await context.Votes
            .AsNoTracking()
            .Where(v => ids.Contains(v.ApplicationId))
            .ToListAsync();

        return Ok(new PlaceListResponse
        {
            Items = pageItems
                .Select(a => a.ToPlace(BuildStats(votes.Where(v => v.ApplicationId == a.Id).ToList(), null, false)))
                .ToList(),
            Total = establishments.Count,
            Page = page,
            PageSize = pageSize
        });
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<PlaceResponse>> GetEstablishment(Guid id)
    {
        var application = await context.Applications
            .AsNoTracking()
            .Include(a => a.Photos)
            .FirstOrDefaultAsync(a => a.Id == id && a.Status == ApplicationStatus.Published);

        if (application == null)
        {
            return NotFound(new { message = "Заведение не найдено." });
        }

        var votes = await context.Votes.AsNoTracking().Where(v => v.ApplicationId == id).ToListAsync();
        return Ok(application.ToPlace(BuildStats(votes, null, false)));
    }

    [HttpGet("{id:guid}/votes")]
    [AllowAnonymous]
    public async Task<ActionResult<VoteStatsResponse>> GetVotes(Guid id)
    {
        if (!await IsPublished(id))
        {
            return NotFound(new { message = "Заведение не найдено." });
        }

        var votes = await context.Votes.AsNoTracking().Where(v => v.ApplicationId == id).ToListAsync();
        return Ok(BuildStats(votes, currentUserId: null, isModerator: false));
    }

    [HttpPost("{id:guid}/vote")]
    [Authorize]
    public async Task<ActionResult<VoteStatsResponse>> CastVote(Guid id, [FromBody] VoteRequest request)
    {
        if (!await IsPublished(id))
        {
            return NotFound(new { message = "Заведение не найдено." });
        }

        var userId = User.GetUserId();
        var isModerator = User.IsModerator();

        var lastUserVote = await context.Votes
            .Where(v => v.ApplicationId == id && v.UserId == userId)
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefaultAsync();

        // BR-05: повторное голосование — не чаще раза в 15 дней (модераторы без ограничения)
        if (!isModerator && lastUserVote != null && DateTime.UtcNow - lastUserVote.CreatedAt < RevoteInterval)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                message = "Повторное голосование доступно не раньше чем через 15 дней."
            });
        }

        context.Votes.Add(new Vote
        {
            ApplicationId = id,
            UserId = userId,
            IsRelevant = request.Vote.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase)
        });
        await context.SaveChangesAsync();

        var votes = await context.Votes.AsNoTracking().Where(v => v.ApplicationId == id).ToListAsync();
        return Ok(BuildStats(votes, userId, isModerator));
    }

    private Task<bool> IsPublished(Guid id) =>
        context.Applications.AsNoTracking().AnyAsync(a => a.Id == id && a.Status == ApplicationStatus.Published);

    private static List<Application> ApplySorting(List<Application> items, string? sortBy, string? sortOrder)
    {
        var descending = string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase);
        return sortBy?.Trim().ToLowerInvariant() switch
        {
            "createdat" => descending
                ? items.OrderByDescending(a => a.CreatedAt).ToList()
                : items.OrderBy(a => a.CreatedAt).ToList(),
            _ => descending
                ? items.OrderByDescending(a => a.EstablishmentName).ToList()
                : items.OrderBy(a => a.EstablishmentName).ToList()
        };
    }

    internal static VoteStatsResponse BuildStats(List<Vote> votes, Guid? currentUserId, bool isModerator)
    {
        var lastUserVote = currentUserId.HasValue
            ? votes.Where(v => v.UserId == currentUserId.Value).MaxBy(v => v.CreatedAt)
            : null;

        return new VoteStatsResponse
        {
            Yes = votes.Count(v => v.IsRelevant),
            No = votes.Count(v => !v.IsRelevant),
            LastVoteDateYes = votes.Where(v => v.IsRelevant).Select(v => (DateTime?)v.CreatedAt).Max(),
            LastVoteDateNo = votes.Where(v => !v.IsRelevant).Select(v => (DateTime?)v.CreatedAt).Max(),
            CanVoteAgain = isModerator || lastUserVote == null || DateTime.UtcNow - lastUserVote.CreatedAt >= RevoteInterval
        };
    }

    private static double CalculateDistanceMeters(double lat1, double lng1, double lat2, double lng2)
    {
        const double earthRadiusMeters = 6371000;
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLng = DegreesToRadians(lng2 - lng1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLng / 2) * Math.Sin(dLng / 2);

        return earthRadiusMeters * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;
}
