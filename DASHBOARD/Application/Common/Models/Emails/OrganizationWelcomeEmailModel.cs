namespace DASHBOARD.Application.Common.Models.Emails;

/// <summary>Everything the organisation welcome email needs to render.</summary>
/// <remarks>
/// Grouped into a record rather than passed as a parameter list: the fields are nearly all strings,
/// so a transposed pair would compile cleanly and only surface as a wrong email in someone's inbox.
/// </remarks>
/// <param name="ToEmail">The admin's email address, which is also their sign-in identity.</param>
/// <param name="AdminName">The admin's display name.</param>
/// <param name="OrgName">Organisation display name.</param>
/// <param name="LoginUrl">The organisation's sign-in page.</param>
/// <param name="SetPasswordUrl">One-time password-setup URL carrying its token in the fragment.</param>
/// <param name="ExpiryHours">Token validity window in hours, displayed in the email body.</param>
public sealed record OrganizationWelcomeEmailModel(
    string ToEmail,
    string AdminName,
    string OrgName,
    string LoginUrl,
    string SetPasswordUrl,
    int    ExpiryHours);
