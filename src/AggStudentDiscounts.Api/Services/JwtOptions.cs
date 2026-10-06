namespace AggStudentDiscounts.Api.Services;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "AggStudentDiscounts";
    public string Audience { get; set; } = "AggStudentDiscounts.Clients";
    public int ExpiresInSeconds { get; set; } = 3600;
}
