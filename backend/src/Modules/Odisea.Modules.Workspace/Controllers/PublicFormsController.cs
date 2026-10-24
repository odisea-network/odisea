using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Odisea.Modules.Workspace.Domain;
using Odisea.Modules.Workspace.Features.Inquiries;

namespace Odisea.Modules.Workspace.Controllers;

/// The endpoint an agency's website posts its contact form to. Accepts JSON (fetch) or a plain
/// HTML form post (url-encoded / multipart text fields); the key may be in the path or in an
/// "access_key" field. Anonymous, any origin, rate limited per IP.
[ApiController]
[Route("api/v1/public/forms")]
[AllowAnonymous]
[EnableCors(WorkspaceModule.PublicCorsPolicy)]
[EnableRateLimiting(WorkspaceModule.PublicRateLimitPolicy)]
public class PublicFormsController(InquiryFormService forms) : ControllerBase
{
    private const string ThankYou = "Thank you, your inquiry has been received.";

    [HttpPost]
    [RequestSizeLimit(InquirySubmission.MaxTotalBytes * 4)]
    public Task<IActionResult> SubmitWithKeyInBody(CancellationToken ct) => HandleAsync(null, ct);

    [HttpPost("{accessKey}")]
    [RequestSizeLimit(InquirySubmission.MaxTotalBytes * 4)]
    public Task<IActionResult> Submit(string accessKey, CancellationToken ct) => HandleAsync(accessKey, ct);

    private async Task<IActionResult> HandleAsync(string? accessKeyFromRoute, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        try
        {
            var parsed = await PublicSubmission.ReadAsync(Request, ct);
            var context = new SubmissionContext(
                Request.Headers.Origin.ToString(),
                Request.Headers.Referer.ToString(),
                Request.Headers.UserAgent.ToString(),
                HttpContext.Connection.RemoteIpAddress?.ToString());

            var result = await forms.SubmitAsync(accessKeyFromRoute ?? parsed.AccessKey, parsed.Fields, context, ct);

            // Classic HTML forms want to land back on the site; only onto an origin the form trusts.
            if (parsed.Redirect is { } redirect && Request.HasFormContentType
                && WebOrigin.Normalize(redirect) is { } target && result.AllowedOrigins.Contains(target))
                return Redirect(redirect);

            return Ok(new PublicSubmissionResponse(true, ThankYou, result.SubmissionId));
        }
        catch (WorkspaceNotFoundException)
        {
            return Problem(title: "Form not available", statusCode: StatusCodes.Status404NotFound);
        }
        catch (OriginNotAllowedException ex)
        {
            return Problem(title: "Origin not allowed", detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (WorkspaceValidationException ex)
        {
            return Problem(title: "Invalid submission", detail: ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (System.Text.Json.JsonException)
        {
            return Problem(title: "Invalid submission", detail: "The body is not valid JSON.", statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }
}
