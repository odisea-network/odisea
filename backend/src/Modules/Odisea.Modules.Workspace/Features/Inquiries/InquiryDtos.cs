using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Odisea.Modules.Workspace.Domain;

namespace Odisea.Modules.Workspace.Features.Inquiries;

public sealed record SaveInquiryFormRequest(
    [Required, MaxLength(100)] string Name,
    string[]? AllowedOrigins,
    string[]? NotifyEmails,
    bool AutoReplyEnabled,
    [MaxLength(200)] string? AutoReplySubject,
    [MaxLength(4000)] string? AutoReplyBody,
    [MaxLength(60)] string? EmailField,
    [MaxLength(60)] string? NameField,
    [MaxLength(60)] string? HoneypotField,
    bool IsEnabled = true);

public sealed record InquiryFormDto(
    Guid Id,
    string Name,
    string AccessKey,
    string SubmitPath,
    string[] AllowedOrigins,
    string[] NotifyEmails,
    bool AutoReplyEnabled,
    string? AutoReplySubject,
    string? AutoReplyBody,
    string EmailField,
    string NameField,
    string HoneypotField,
    bool IsEnabled,
    int SubmissionCount,
    int SpamCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record InquirySummaryDto(
    Guid Id,
    Guid FormId,
    string? CustomerName,
    string? CustomerEmail,
    InquiryStatus Status,
    string? Origin,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? HandledAt);

public sealed record InquiryDto(
    Guid Id,
    Guid FormId,
    JsonElement Fields,
    string? CustomerName,
    string? CustomerEmail,
    InquiryStatus Status,
    string? Origin,
    string? UserAgent,
    Guid? HandledByUserId,
    DateTimeOffset? HandledAt,
    string? InternalNote,
    DateTimeOffset ReceivedAt);

public sealed record SetInquiryStatusRequest([Required] InquiryStatus Status, [MaxLength(2000)] string? Note);

/// Request facts the public endpoint passes along; nothing here is trusted beyond logging and the origin check.
public sealed record SubmissionContext(string? Origin, string? Referer, string? UserAgent, string? RemoteIp);

/// Accepted is false for honeypot hits, which are dropped but still answered with success.
public sealed record SubmissionResult(Guid? SubmissionId, bool Accepted, IReadOnlyList<string> AllowedOrigins);

public sealed record PublicSubmissionResponse(bool Success, string Message, Guid? SubmissionId);

public static class InquiryMapping
{
    public static string SubmitPathFor(string accessKey) => $"/api/v1/public/forms/{accessKey}";

    public static InquiryFormDto ToDto(this InquiryForm f) => new(
        f.Id, f.Name, f.AccessKey, SubmitPathFor(f.AccessKey), f.AllowedOrigins, f.NotifyEmails,
        f.AutoReplyEnabled, f.AutoReplySubject, f.AutoReplyBody, f.EmailField, f.NameField, f.HoneypotField,
        f.IsEnabled, f.SubmissionCount, f.SpamCount, f.CreatedAt, f.UpdatedAt);

    public static InquirySummaryDto ToSummaryDto(this InquirySubmission s) => new(
        s.Id, s.FormId, s.CustomerName, s.CustomerEmail, s.Status, s.Origin, s.CreatedAt, s.HandledAt);

    public static InquiryDto ToDto(this InquirySubmission s) => new(
        s.Id, s.FormId, JsonDocument.Parse(s.Fields).RootElement.Clone(), s.CustomerName, s.CustomerEmail,
        s.Status, s.Origin, s.UserAgent, s.HandledByUserId, s.HandledAt, s.InternalNote, s.CreatedAt);
}
