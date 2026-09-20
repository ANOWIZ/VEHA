using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Veha.Api.Auth;
using Veha.Api.Config;
using Veha.Api.Dtos;
using Veha.Api.Services;
using Veha.Domain.Common;

namespace Veha.Api.Controllers;

/// <summary>Портал Заказчика (роль client): свои проекты, что требуется от него,
/// предоставление данных и артефактов. Порт api/v1/portal.py.</summary>
[ApiController]
[Route("api/v1/portal")]
[Authorize(Roles = "client")]
public class PortalController(
    CurrentUserAccessor current, CustomerService customer, IOptions<VehaSettings> settings) : ControllerBase
{
    [HttpGet("projects")]
    public async Task<List<PortalProjectDto>> MyProjects(CancellationToken ct)
        => await customer.PortalProjectsAsync(await current.GetAsync(ct), ct);

    [HttpGet("projects/{projectId:guid}")]
    public async Task<PortalProjectDetailDto> ProjectDetail(Guid projectId, CancellationToken ct)
        => await customer.PortalProjectDetailAsync(await current.GetAsync(ct), projectId, ct);

    [HttpPost("action-items/{itemId:guid}/provide")]
    public async Task<ActionItemOutDto> Provide(Guid itemId, ActionItemProvideDto body, CancellationToken ct)
        => await customer.ProvideAsync(await current.GetAsync(ct), itemId, body.Note, ct);

    [HttpPost("action-items/{itemId:guid}/artifacts")]
    public async Task<IActionResult> UploadArtifact(Guid itemId, IFormFile file, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        var item = await customer.GetItemEntityAsync(itemId, ct);
        await customer.AssertClientAccessAsync(user, item.ProjectId, ct);

        var maxBytes = (long)settings.Value.ArtifactMaxMb * 1024 * 1024;
        if (file.Length > maxBytes)
            throw new DomainValidationException($"Файл больше {settings.Value.ArtifactMaxMb} МБ");

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var dto = await customer.SaveArtifactAsync(
            item.ProjectId, itemId, file.FileName, file.ContentType, ms.ToArray(), user.Id, ct);
        return StatusCode(StatusCodes.Status201Created, dto);
    }

    [HttpGet("artifacts/{artifactId:guid}/download")]
    public async Task<IActionResult> Download(Guid artifactId, CancellationToken ct)
    {
        var user = await current.GetAsync(ct);
        var artifact = await customer.GetArtifactEntityAsync(artifactId, ct);
        await customer.AssertClientAccessAsync(user, artifact.ProjectId, ct);
        var data = await customer.ReadArtifactAsync(artifact, ct);
        return File(data, artifact.ContentType ?? "application/octet-stream", artifact.Filename);
    }
}
