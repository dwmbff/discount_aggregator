using AggStudentDiscounts.Api.DTOs;
using AggStudentDiscounts.Api.Services;
using AggStudentDiscounts.Domain.Entities;
using AggStudentDiscounts.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AggStudentDiscounts.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/geocode")]
public class GeocodeController(IGeocodingService geocoding) : ControllerBase
{
    /// <summary>Автоподсказки: название/адрес → список объектов с адресом.</summary>
    [HttpGet("suggest")]
    public async Task<ActionResult<List<GeoPlaceResponse>>> Suggest([FromQuery] string? q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 3)
        {
            return BadRequest(new { message = "Введите не менее 3 символов." });
        }

        try
        {
            var places = await geocoding.SuggestAsync(q, ct);
            return Ok(places.Select(ToResponse).ToList());
        }
        catch (GeocodingUnavailableException ex)
        {
            return Unavailable(ex);
        }
    }

    /// <summary>Объект по координатам точки на карте.</summary>
    [HttpGet("reverse")]
    public async Task<ActionResult<GeoPlaceResponse>> Reverse([FromQuery] double lat, [FromQuery] double lng, CancellationToken ct)
    {
        if (lat is < -90 or > 90 || lng is < -180 or > 180)
        {
            return BadRequest(new { message = "Некорректные координаты." });
        }

        try
        {
            var place = await geocoding.ReverseAsync(lat, lng, ct);
            return place == null ? NotFound(new { message = "Объект не найден." }) : Ok(ToResponse(place));
        }
        catch (GeocodingUnavailableException ex)
        {
            return Unavailable(ex);
        }
    }

    // 503 с понятным сообщением: клиент предлагает ввести данные вручную
    private ObjectResult Unavailable(GeocodingUnavailableException ex) =>
        StatusCode(StatusCodes.Status503ServiceUnavailable,
            new { message = ex.Message + " Введите название и адрес вручную." });

    internal static GeoPlaceResponse ToResponse(GeoPlace p) => new()
    {
        ExternalId = p.ExternalId,
        Name = p.Name,
        Address = p.Address,
        Latitude = p.Latitude,
        Longitude = p.Longitude,
        Category = p.Category,
        Phone = p.Phone,
        Website = p.Website,
        WorkingHours = p.WorkingHours
    };
}

[ApiController]
[AllowAnonymous]
[Route("api/photos")]
public class PhotosController(ApplicationDbContext context, IFileStorage storage) : ControllerBase
{
    /// <summary>Файл опубликованного заведения доступен всем; файлы заявок — автору и модератору.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var photo = await context.ApplicationPhotos
            .AsNoTracking()
            .Include(p => p.Application)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (photo == null || photo.Application.Status == ApplicationStatus.Cancelled)
        {
            return NotFound();
        }

        if (photo.Application.Status != ApplicationStatus.Published)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Unauthorized();
            }

            if (photo.Application.UserId != User.GetUserId() && !User.IsModerator())
            {
                return Forbid();
            }
        }

        var stream = storage.Open(photo.StoredName);
        if (stream == null)
        {
            return NotFound();
        }

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(stream, UploadRules.ContentTypeFor(photo.StoredName));
    }
}
