using Microsoft.Extensions.Options;
using Veha.Api.Config;

namespace Veha.Api.Services;

/// <summary>Локальное хранилище артефактов портала (порт artifact_storage local).
/// Файл кладётся в {ArtifactsDir}/{projectId}/{guid}_{filename}; возвращается
/// относительный путь (storage_path).</summary>
public class ArtifactStorage(IOptions<VehaSettings> settings)
{
    public async Task<string> SaveAsync(Guid projectId, string filename, byte[] data, CancellationToken ct)
    {
        var rel = Path.Combine(projectId.ToString(), $"{Guid.NewGuid():N}_{Path.GetFileName(filename)}");
        var full = Path.Combine(settings.Value.ArtifactsDir, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllBytesAsync(full, data, ct);
        return rel;
    }

    public Task<byte[]> ReadAsync(string storagePath, CancellationToken ct)
        => File.ReadAllBytesAsync(Path.Combine(settings.Value.ArtifactsDir, storagePath), ct);
}
