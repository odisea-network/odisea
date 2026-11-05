using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Catalog.Domain;
using Odisea.Modules.Catalog.Infrastructure;
using Odisea.SharedKernel;

namespace Odisea.Modules.Catalog.Features.Programs;

public sealed class ProgramImageNotFoundException(string message) : DomainException(message);

/// Gallery management. Listings and the detail view never load the bytes; only the public file
/// endpoint does, one image at a time.
public class ProgramImageService(CatalogDbContext db)
{
    public sealed record StoredImage(byte[] Data, string ContentType, string Sha256);

    public Task<List<ProgramImageDto>> ListAsync(Guid programId, CancellationToken ct) =>
        db.ProgramImages.AsNoTracking()
            .Where(i => i.ProgramId == programId)
            .OrderBy(i => i.SortOrder).ThenBy(i => i.CreatedAt)
            .Select(i => new ProgramImageDto(
                i.Id, ProgramDtoMapping.PublicImagePath + i.Id, i.ContentType, i.SizeBytes, i.Width, i.Height,
                i.Caption, i.AltText, i.SortOrder, i.IsCover, i.CreatedAt))
            .ToListAsync(ct);

    public Task<List<ProgramImageInfo>> InfoAsync(Guid programId, CancellationToken ct) =>
        db.ProgramImages.AsNoTracking()
            .Where(i => i.ProgramId == programId)
            .Select(i => new ProgramImageInfo(i.Id, i.IsCover, i.Width, i.Height, i.AltText != null && i.AltText != ""))
            .ToListAsync(ct);

    public Task<StoredImage?> GetFileAsync(Guid imageId, CancellationToken ct) =>
        db.ProgramImages.AsNoTracking()
            .Where(i => i.Id == imageId)
            .Select(i => new StoredImage(i.Data, i.ContentType, i.Sha256))
            .FirstOrDefaultAsync(ct);

    public async Task<ProgramImageDto> UploadAsync(
        Guid programId, byte[] bytes, string? caption, string? altText, bool isCover, CancellationToken ct)
    {
        await RequireProgramAsync(programId, ct);
        var existing = await db.ProgramImages
            .Where(i => i.ProgramId == programId)
            .Select(i => new { i.Id, i.SortOrder, i.IsCover })
            .ToListAsync(ct);
        if (existing.Count >= ProgramImage.MaxPerProgram)
            throw new ProgramValidationException($"A program can have at most {ProgramImage.MaxPerProgram} images.");

        var info = ImageFile.Inspect(bytes);
        var image = new ProgramImage
        {
            ProgramId = programId,
            ContentType = info.ContentType,
            Data = bytes,
            SizeBytes = bytes.Length,
            Width = info.Width,
            Height = info.Height,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
            Caption = Clean(caption),
            AltText = Clean(altText),
            SortOrder = existing.Count == 0 ? 0 : existing.Max(i => i.SortOrder) + 1,
            // The first picture is the cover until the operator says otherwise.
            IsCover = isCover || existing.Count == 0,
        };
        if (image.IsCover)
            await ClearCoverAsync(programId, ct);

        db.ProgramImages.Add(image);
        await db.SaveChangesAsync(ct);
        return ToDto(image);
    }

    public async Task<ProgramImageDto> UpdateAsync(
        Guid programId, Guid imageId, UpdateProgramImageRequest request, CancellationToken ct)
    {
        var image = await FindAsync(programId, imageId, ct);
        image.Caption = Clean(request.Caption);
        image.AltText = Clean(request.AltText);
        if (request.IsCover && !image.IsCover)
        {
            await ClearCoverAsync(programId, ct);
            image.IsCover = true;
        }
        else if (!request.IsCover && image.IsCover)
        {
            throw new ProgramValidationException("Choose another image as the cover instead of removing this one.");
        }
        await db.SaveChangesAsync(ct);
        return ToDto(image);
    }

    public async Task<List<ProgramImageDto>> ReorderAsync(Guid programId, Guid[] imageIds, CancellationToken ct)
    {
        var images = await db.ProgramImages.Where(i => i.ProgramId == programId).ToListAsync(ct);
        if (imageIds.Length != images.Count || imageIds.Distinct().Count() != images.Count
            || images.Any(i => !imageIds.Contains(i.Id)))
            throw new ProgramValidationException("Send every image id of the program exactly once.");

        foreach (var image in images)
            image.SortOrder = Array.IndexOf(imageIds, image.Id);
        await db.SaveChangesAsync(ct);
        return await ListAsync(programId, ct);
    }

    public async Task DeleteAsync(Guid programId, Guid imageId, CancellationToken ct)
    {
        var image = await FindAsync(programId, imageId, ct);
        db.ProgramImages.Remove(image);
        if (image.IsCover)
        {
            var next = await db.ProgramImages
                .Where(i => i.ProgramId == programId && i.Id != imageId)
                .OrderBy(i => i.SortOrder).ThenBy(i => i.CreatedAt)
                .FirstOrDefaultAsync(ct);
            if (next is not null)
                next.IsCover = true;
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task<ProgramImage> FindAsync(Guid programId, Guid imageId, CancellationToken ct) =>
        await db.ProgramImages.FirstOrDefaultAsync(i => i.ProgramId == programId && i.Id == imageId, ct)
        ?? throw new ProgramImageNotFoundException($"Image {imageId} does not belong to program {programId}.");

    private async Task RequireProgramAsync(Guid programId, CancellationToken ct)
    {
        if (!await db.Programs.AnyAsync(p => p.Id == programId, ct))
            throw new ProgramImageNotFoundException($"Program {programId} does not exist.");
    }

    private async Task ClearCoverAsync(Guid programId, CancellationToken ct)
    {
        var covers = await db.ProgramImages.Where(i => i.ProgramId == programId && i.IsCover).ToListAsync(ct);
        foreach (var cover in covers)
            cover.IsCover = false;
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static ProgramImageDto ToDto(ProgramImage i) => new(
        i.Id, ProgramDtoMapping.ImageUrl(i.Id), i.ContentType, i.SizeBytes, i.Width, i.Height,
        i.Caption, i.AltText, i.SortOrder, i.IsCover, i.CreatedAt);
}
