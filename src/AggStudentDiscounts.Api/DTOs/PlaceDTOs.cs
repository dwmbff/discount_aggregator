namespace AggStudentDiscounts.Api.DTOs;

public class PlaceResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Website { get; set; }
    public string? WorkingHours { get; set; }
    public string Discount { get; set; } = string.Empty;
    public string Conditions { get; set; } = string.Empty;
    public string? ValidityPeriod { get; set; }
    public List<string> Photos { get; set; } = new();
    public int VotesYes { get; set; }
    public int VotesNo { get; set; }
    public DateTime? LastVoteDateYes { get; set; }
    public DateTime? LastVoteDateNo { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class PlaceListResponse
{
    public List<PlaceResponse> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class GeoPlaceResponse
{
    public string ExternalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? Category { get; set; }
    public string? Phone { get; set; }
    public string? Website { get; set; }
    public string? WorkingHours { get; set; }
}
