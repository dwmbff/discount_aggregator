using Microsoft.AspNetCore.Http;

namespace AggStudentDiscounts.Api.DTOs;

public class ApplicationResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string PlaceName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? Category { get; set; }
    public string? Phone { get; set; }
    public string? Website { get; set; }
    public string? WorkingHours { get; set; }
    public string Discount { get; set; } = string.Empty;
    public string Conditions { get; set; } = string.Empty;
    public string? ValidityPeriod { get; set; }
    public string? SourceUrl { get; set; }
    /// <summary>Ссылки на файлы-подтверждения (GET /api/photos/{id}).</summary>
    public List<string> Photos { get; set; } = new();
    public string Status { get; set; } = string.Empty;
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModeratedAt { get; set; }
}

public class CreateApplicationRequest
{
    public string PlaceName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string Discount { get; set; } = string.Empty;
    public string Conditions { get; set; } = string.Empty;
    public string? ValidityPeriod { get; set; }
    public string? SourceUrl { get; set; }

    /// <summary>Идентификатор объекта из автоподсказок (GET /api/geocode/suggest). По нему сервер дополняет заявку атрибутами заведения.</summary>
    public string? ExternalId { get; set; }
    public string? Category { get; set; }
    public string? Phone { get; set; }
    public string? Website { get; set; }
    public string? WorkingHours { get; set; }

    public List<IFormFile> Photos { get; set; } = new();
}

public class ApplicationListResponse
{
    public List<ApplicationResponse> Items { get; set; } = new();
    public int Total { get; set; }
}

public class UpdateApplicationRequest
{
    public string? PlaceName { get; set; }
    public string? Address { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Category { get; set; }
    public string? Phone { get; set; }
    public string? Website { get; set; }
    public string? WorkingHours { get; set; }
    public string? Discount { get; set; }
    public string? Conditions { get; set; }
    public string? ValidityPeriod { get; set; }
    public string? SourceUrl { get; set; }
}

public class RejectApplicationRequest
{
    public string Reason { get; set; } = string.Empty;
}
