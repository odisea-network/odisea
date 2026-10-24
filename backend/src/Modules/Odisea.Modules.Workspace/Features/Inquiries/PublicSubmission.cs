using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Odisea.Modules.Workspace.Domain;

namespace Odisea.Modules.Workspace.Features.Inquiries;

/// Reads a public form post the way website forms send it: JSON from fetch(), url-encoded or
/// multipart from a plain HTML form. Values become strings; repeated keys are joined.
public static class PublicSubmission
{
    public const string AccessKeyField = "access_key";
    public const string RedirectField = "redirect";

    public static bool IsReserved(string key) =>
        string.Equals(key, AccessKeyField, StringComparison.OrdinalIgnoreCase)
        || string.Equals(key, RedirectField, StringComparison.OrdinalIgnoreCase);

    public sealed record Parsed(Dictionary<string, string> Fields, string? AccessKey, string? Redirect);

    public static async Task<Parsed> ReadAsync(HttpRequest request, CancellationToken ct)
    {
        if (request.ContentLength is > InquirySubmission.MaxTotalBytes * 2)
            throw new WorkspaceValidationException("The submission is too large.");

        var fields = new Dictionary<string, string>();
        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync(ct);
            foreach (var (key, values) in form)
                fields[key] = string.Join(", ", values.Where(v => !string.IsNullOrEmpty(v)));
        }
        else
        {
            using var buffer = new MemoryStream();
            await CopyBoundedAsync(request.Body, buffer, InquirySubmission.MaxTotalBytes * 2, ct);
            if (buffer.Length == 0)
                throw new WorkspaceValidationException("The submission has no fields.");
            buffer.Position = 0;
            using var json = await JsonDocument.ParseAsync(buffer, new JsonDocumentOptions { MaxDepth = 8 }, ct);
            if (json.RootElement.ValueKind != JsonValueKind.Object)
                throw new WorkspaceValidationException("Send a JSON object of fields.");
            foreach (var property in json.RootElement.EnumerateObject())
                fields[property.Name] = Stringify(property.Value);
        }

        fields.Remove(AccessKeyField, out var accessKey);
        fields.Remove(RedirectField, out var redirect);
        return new Parsed(fields, string.IsNullOrWhiteSpace(accessKey) ? null : accessKey.Trim(), string.IsNullOrWhiteSpace(redirect) ? null : redirect.Trim());
    }

    private static string Stringify(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
        JsonValueKind.Array => string.Join(", ", value.EnumerateArray().Select(Stringify).Where(s => s.Length > 0)),
        JsonValueKind.Object => value.GetRawText(),
        _ => value.GetRawText(),
    };

    private static async Task CopyBoundedAsync(Stream source, Stream target, int limit, CancellationToken ct)
    {
        var chunk = new byte[8192];
        var total = 0;
        int read;
        while ((read = await source.ReadAsync(chunk, ct)) > 0)
        {
            total += read;
            if (total > limit)
                throw new WorkspaceValidationException("The submission is too large.");
            await target.WriteAsync(chunk.AsMemory(0, read), ct);
        }
    }
}
