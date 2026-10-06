using Microsoft.Extensions.Options;

namespace AggStudentDiscounts.Api.Services;

public class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Каталог для загруженных файлов. Должен входить в резервное копирование.</summary>
    public string Path { get; set; } = "uploads";
}

public interface IFileStorage
{
    Task<string> SaveAsync(IFormFile file, CancellationToken ct = default);
    Stream? Open(string storedName);
}

public class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IOptions<StorageOptions> options, IHostEnvironment env)
    {
        var path = options.Value.Path;
        _root = System.IO.Path.IsPathRooted(path) ? path : System.IO.Path.Combine(env.ContentRootPath, path);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(IFormFile file, CancellationToken ct = default)
    {
        var storedName = $"{Guid.NewGuid():N}{System.IO.Path.GetExtension(file.FileName).ToLowerInvariant()}";
        await using var target = File.Create(System.IO.Path.Combine(_root, storedName));
        await file.CopyToAsync(target, ct);
        return storedName;
    }

    public Stream? Open(string storedName)
    {
        // Имя генерируется сервером; проверка защищает от выхода за пределы каталога.
        if (storedName != System.IO.Path.GetFileName(storedName))
        {
            return null;
        }

        var full = System.IO.Path.Combine(_root, storedName);
        return File.Exists(full) ? File.OpenRead(full) : null;
    }
}

public static class UploadRules
{
    public const long MaxFileSize = 10 * 1024 * 1024;

    private static readonly Dictionary<string, string> ContentTypes = new()
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".pdf"] = "application/pdf",
        [".heic"] = "image/heic"
    };

    public static bool IsAllowedExtension(string fileName) =>
        ContentTypes.ContainsKey(System.IO.Path.GetExtension(fileName).ToLowerInvariant());

    public static string ContentTypeFor(string fileName) =>
        ContentTypes.GetValueOrDefault(System.IO.Path.GetExtension(fileName).ToLowerInvariant(), "application/octet-stream");

    /// <summary>Проверка сигнатуры файла: расширение не должно подменять реальный формат.</summary>
    public static bool HasValidSignature(IFormFile file)
    {
        var head = new byte[12];
        using var stream = file.OpenReadStream();
        var read = stream.Read(head, 0, head.Length);
        if (read < 4)
        {
            return false;
        }

        return System.IO.Path.GetExtension(file.FileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF,
            ".png" => head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47,
            ".pdf" => head[0] == 0x25 && head[1] == 0x50 && head[2] == 0x44 && head[3] == 0x46,
            ".heic" => read >= 12 && head[4] == 0x66 && head[5] == 0x74 && head[6] == 0x79 && head[7] == 0x70,
            _ => false
        };
    }
}
