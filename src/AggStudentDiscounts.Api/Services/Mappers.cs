using AggStudentDiscounts.Api.DTOs;
using AggStudentDiscounts.Domain.Entities;

namespace AggStudentDiscounts.Api.Services;

public static class Mappers
{
    public static string PhotoUrl(Guid photoId) => $"/api/photos/{photoId}";

    public static ApplicationResponse ToResponse(this Application a) => new()
    {
        Id = a.Id,
        UserId = a.UserId,
        PlaceName = a.EstablishmentName,
        Address = a.Address,
        Latitude = a.Latitude,
        Longitude = a.Longitude,
        Category = a.Category,
        Phone = a.Phone,
        Website = a.Website,
        WorkingHours = a.WorkingHours,
        Discount = a.DiscountDescription,
        Conditions = a.Conditions,
        ValidityPeriod = a.ValidityPeriod,
        SourceUrl = a.SourceLink,
        Photos = a.Photos.Select(p => PhotoUrl(p.Id)).ToList(),
        Status = a.Status.ToString(),
        RejectionReason = a.RejectionReason,
        CreatedAt = a.CreatedAt,
        ModeratedAt = a.ModeratedAt
    };

    public static PlaceResponse ToPlace(this Application a, VoteStatsResponse? stats = null) => new()
    {
        Id = a.Id,
        Name = a.EstablishmentName,
        Address = a.Address,
        Latitude = a.Latitude,
        Longitude = a.Longitude,
        Category = a.Category ?? string.Empty,
        Phone = a.Phone,
        Website = a.Website,
        WorkingHours = a.WorkingHours,
        Discount = a.DiscountDescription,
        Conditions = a.Conditions,
        ValidityPeriod = a.ValidityPeriod,
        Photos = a.Photos.Select(p => PhotoUrl(p.Id)).ToList(),
        VotesYes = stats?.Yes ?? 0,
        VotesNo = stats?.No ?? 0,
        LastVoteDateYes = stats?.LastVoteDateYes,
        LastVoteDateNo = stats?.LastVoteDateNo,
        Status = a.Status.ToString()
    };

    public static string? NullIfBlank(this string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
