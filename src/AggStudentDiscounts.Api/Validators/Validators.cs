using AggStudentDiscounts.Api.DTOs;
using FluentValidation;

namespace AggStudentDiscounts.Api.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(100)
            .WithMessage("Пароль должен содержать от 8 до 100 символов.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.University).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Department).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Course).InclusiveBetween(1, 6);
        RuleFor(x => x.Telegram).MaximumLength(64);
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class CreateApplicationRequestValidator : AbstractValidator<CreateApplicationRequest>
{
    public static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".pdf", ".heic"];

    public CreateApplicationRequestValidator()
    {
        RuleFor(x => x.PlaceName).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Discount).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.Conditions).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue);
        RuleFor(x => x.SourceUrl).Must(BeHttpUrl).When(x => !string.IsNullOrWhiteSpace(x.SourceUrl))
            .WithMessage("Ссылка на источник должна быть корректным http(s) URL.");
        RuleFor(x => x.Photos).NotEmpty().WithMessage("Нужно приложить хотя бы одно фото подтверждения.");
        RuleForEach(x => x.Photos)
            .Must(f => AllowedExtensions.Contains(Path.GetExtension(f.FileName).ToLowerInvariant()))
            .WithMessage("Допустимые форматы файлов: JPEG, HEIC, PDF, PNG.");
    }

    private static bool BeHttpUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme is "http" or "https");
}

public class UpdateApplicationRequestValidator : AbstractValidator<UpdateApplicationRequest>
{
    public UpdateApplicationRequestValidator()
    {
        RuleFor(x => x.PlaceName).MaximumLength(300);
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.Discount).MaximumLength(1000);
        RuleFor(x => x.Conditions).MaximumLength(1000);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue);
    }
}

public class RejectApplicationRequestValidator : AbstractValidator<RejectApplicationRequest>
{
    public RejectApplicationRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500).WithMessage("Причина отклонения обязательна.");
    }
}

public class VoteRequestValidator : AbstractValidator<VoteRequest>
{
    public VoteRequestValidator()
    {
        RuleFor(x => x.Vote).NotEmpty()
            .Must(v => v is not null && (v.Trim().ToLowerInvariant() is "yes" or "no"))
            .WithMessage("Допустимые значения голоса: yes или no.");
    }
}
