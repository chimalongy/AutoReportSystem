using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ARS.Classess.Utils
{
    public static class EmailSender
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private const string EmailEndpoint = "http://10.220.0.44:8096/api/Payarena/email";

        /// <summary>
        /// Sends an email via the Payarena email API.
        /// </summary>
        /// <param name="address">Recipient email address.</param>
        /// <param name="subject">Email subject.</param>
        /// <param name="message">Email body/message.</param>
        /// <returns>True if the request was successful; otherwise false.</returns>
        public static async Task<bool> SendEmail(string address, string subject, string message)
        {
            var payload = new
            {
                Message = message,
                Address = address,
                Subject = subject
            };

            string json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await _httpClient.PostAsync(EmailEndpoint, content);

            return response.IsSuccessStatusCode;
        }

        /// <summary>
        /// Sends a user creation email with default password and mandatory change instructions.
        /// </summary>
        /// <param name="address">Recipient email address.</param>
        /// <param name="firstName">Recipient's first name.</param>
        /// <param name="defaultPassword">The temporary default password assigned to the user.</param>
        /// <returns>True if the request was successful; otherwise false.</returns>
        public static async Task<bool> SendUserCreationEmail(
            string address,
            string firstName,
            string defaultPassword)
        {
            string htmlBody = BuildUserCreationTemplate(firstName, address, defaultPassword);
            return await SendEmail(address, "Welcome – Your Account Has Been Created", htmlBody);
        }

        /// <summary>
        /// Sends a password-updated notification email using the HTML template.
        /// </summary>
        /// <param name="address">Recipient email address.</param>
        /// <param name="firstName">Recipient's first name.</param>
        /// <param name="updatedDate">Date/time the password was changed.</param>
        /// <param name="role">User's role in the system.</param>
        /// <returns>True if the request was successful; otherwise false.</returns>
        public static async Task<bool> SendPasswordUpdatedEmail(
            string address,
            string firstName,
            string updatedDate,
            string role)
        {
            string htmlBody = BuildPasswordUpdatedTemplate(firstName, address, updatedDate, role);
            return await SendEmail(address, "Password Updated – Automated Reporting System", htmlBody);
        }

        /// <summary>
        /// Builds the HTML email template for user creation with default password.
        /// </summary>
        private static string BuildUserCreationTemplate(
            string firstName,
            string emailAddress,
            string defaultPassword)
        {
            return $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0""/>
  <title>Welcome – Your Account Has Been Created</title>
  <style>
    @import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap');
    * {{ margin: 0; padding: 0; box-sizing: border-box; }}
    body {{ background-color: #f0f4fa; font-family: 'Inter', sans-serif; -webkit-font-smoothing: antialiased; padding: 24px 12px; }}
    .wrapper {{ max-width: 540px; margin: 0 auto; }}

    .header {{ background: #2563eb; border-radius: 12px 12px 0 0; padding: 16px 24px; display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 8px; }}
    .sys-name {{ font-size: 12px; font-weight: 600; letter-spacing: 0.6px; text-transform: uppercase; color: rgba(255,255,255,0.85); }}
    .header-badge {{ background: rgba(255,255,255,0.2); color: #fff; font-size: 10px; font-weight: 600; letter-spacing: 0.8px; text-transform: uppercase; padding: 3px 10px; border-radius: 20px; white-space: nowrap; }}

    .hero {{ background: #3b82f6; padding: 32px 24px 28px; }}
    .icon-wrap {{ width: 40px; height: 40px; background: rgba(255,255,255,0.2); border-radius: 10px; display: flex; align-items: center; justify-content: center; margin-bottom: 16px; }}
    .icon-wrap svg {{ width: 20px; height: 20px; stroke: #fff; fill: none; stroke-width: 2; stroke-linecap: round; stroke-linejoin: round; }}
    .hero h1 {{ font-size: 22px; font-weight: 700; color: #fff; line-height: 1.3; margin-bottom: 6px; }}
    .hero p {{ font-size: 13px; color: rgba(255,255,255,0.8); line-height: 1.6; }}

    .body-card {{ background: #fff; padding: 28px 24px 24px; }}
    .greeting {{ font-size: 14px; color: #374151; line-height: 1.75; margin-bottom: 22px; }}
    .greeting strong {{ color: #111827; font-weight: 600; }}

    .warning-banner {{ background: #fffbeb; border: 1px solid #fef3c7; border-radius: 10px; padding: 13px 16px; margin-bottom: 18px; display: flex; gap: 10px; align-items: flex-start; }}
    .warning-banner svg {{ width: 15px; height: 15px; min-width: 15px; stroke: #d97706; fill: none; stroke-width: 2.2; stroke-linecap: round; stroke-linejoin: round; margin-top: 1px; }}
    .warning-banner-text {{ font-size: 13px; color: #92400e; line-height: 1.6; }}
    .warning-banner-text strong {{ font-weight: 600; display: block; margin-bottom: 2px; color: #b45309; }}

    .password-box {{ background: #f8faff; border: 1px solid #dbeafe; border-radius: 10px; padding: 18px; margin-bottom: 22px; text-align: center; }}
    .password-label {{ font-size: 10px; font-weight: 600; letter-spacing: 1px; text-transform: uppercase; color: #60a5fa; margin-bottom: 8px; }}
    .password-value {{ font-family: 'Courier New', monospace; font-size: 18px; font-weight: 700; color: #2563eb; letter-spacing: 1px; background: #eff6ff; padding: 12px 16px; border-radius: 6px; display: inline-block; border: 1px dashed #93c5fd; word-break: break-all; }}

    .steps-box {{ background: #f8faff; border: 1px solid #dbeafe; border-radius: 10px; padding: 16px 18px; margin-bottom: 22px; }}
    .steps-title {{ font-size: 13px; font-weight: 600; color: #1e40af; margin-bottom: 10px; }}
    .steps-list {{ list-style: none; padding: 0; margin: 0; }}
    .steps-list li {{ font-size: 13px; color: #374151; line-height: 1.7; margin-bottom: 6px; display: flex; align-items: flex-start; gap: 8px; }}
    .step-num {{ background: #2563eb; color: #fff; font-size: 10px; font-weight: 700; width: 18px; height: 18px; border-radius: 50%; display: flex; align-items: center; justify-content: center; flex-shrink: 0; margin-top: 2px; }}

    .divider {{ border: none; border-top: 1px solid #f3f4f6; margin: 18px 0; }}

    .security-note {{ background: #fef2f2; border: 1px solid #fecaca; border-radius: 10px; padding: 12px 16px; margin-bottom: 16px; display: flex; gap: 10px; align-items: flex-start; }}
    .security-note svg {{ width: 14px; height: 14px; min-width: 14px; stroke: #ef4444; fill: none; stroke-width: 2; margin-top: 1px; }}
    .security-note p {{ font-size: 12px; color: #991b1b; line-height: 1.6; }}

    .closing {{ font-size: 12px; color: #9ca3af; line-height: 1.65; text-align: center; }}

    .footer {{ background: #f8faff; border: 1px solid #dbeafe; border-top: none; border-radius: 0 0 12px 12px; padding: 16px 24px; text-align: center; }}
    .footer-copy {{ font-size: 11px; color: #9ca3af; line-height: 1.6; }}

    @media (max-width: 420px) {{
      body {{ padding: 12px 8px; }}
      .header, .hero, .body-card, .footer {{ padding-left: 16px; padding-right: 16px; }}
      .hero h1 {{ font-size: 20px; }}
      .password-value {{ font-size: 16px; }}
    }}
  </style>
</head>
<body>
<div class=""wrapper"">
  <div class=""header"">
    <span class=""sys-name"">Automated Reporting System</span>
    <span class=""header-badge"">New Account</span>
  </div>
  <div class=""hero"">
    <div class=""icon-wrap"">
      <svg viewBox=""0 0 24 24""><path d=""M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2""/><circle cx=""12"" cy=""7"" r=""4""/></svg>
    </div>
    <h1>Welcome, {EscapeHtml(firstName)}!</h1>
    <p>Your account has been created on the Automated Reporting System.</p>
  </div>
  <div class=""body-card"">
    <p class=""greeting"">
      Hi <strong>{EscapeHtml(firstName)}</strong>,<br/><br/>
      An account has been created for you on the <strong>Automated Reporting System</strong>. Below is your temporary password. You <strong>must</strong> change this password before you can access your account.
    </p>
    <div class=""warning-banner"">
      <svg viewBox=""0 0 24 24""><path d=""M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z""/><line x1=""12"" y1=""9"" x2=""12"" y2=""13""/><line x1=""12"" y1=""17"" x2=""12.01"" y2=""17""/></svg>
      <div class=""warning-banner-text"">
        <strong>Action Required</strong>
        You cannot access your account until you change your default password. This is a mandatory security step.
      </div>
    </div>
    <div class=""password-box"">
      <div class=""password-label"">Your Temporary Password</div>
      <div class=""password-value"">{EscapeHtml(defaultPassword)}</div>
    </div>
    <div class=""steps-box"">
      <div class=""steps-title"">Next Steps:</div>
      <ul class=""steps-list"">
        <li><span class=""step-num"">1</span>Log in to the system using your email and the temporary password above.</li>
        <li><span class=""step-num"">2</span>You will be prompted to change your password immediately.</li>
        <li><span class=""step-num"">3</span>Choose a strong, unique password that you have not used elsewhere.</li>
        <li><span class=""step-num"">4</span>Once changed, you will gain full access to your account.</li>
      </ul>
    </div>
    <hr class=""divider""/>
    <div class=""security-note"">
      <svg viewBox=""0 0 24 24""><path d=""M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z""/></svg>
      <p><strong>Security Notice:</strong> Do not share this password with anyone. If you did not request this account, please contact your system administrator immediately.</p>
    </div>
    <p class=""closing"">Thank you for joining the Automated Reporting System.</p>
  </div>
  <div class=""footer"">
    <p class=""footer-copy"">© {DateTime.Now.Year} Automated Reporting System. All rights reserved.<br/>This is an automated message — please do not reply to this email.</p>
  </div>
</div>
</body>
</html>";
        }

        /// <summary>
        /// Builds the HTML email template for password update notification.
        /// </summary>
        private static string BuildPasswordUpdatedTemplate(
            string firstName,
            string emailAddress,
            string updatedDate,
            string role)
        {
            return $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0""/>
  <title>Password Updated – Automated Reporting System</title>
  <style>
    @import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap');
    * {{ margin: 0; padding: 0; box-sizing: border-box; }}
    body {{ background-color: #f0f4fa; font-family: 'Inter', sans-serif; -webkit-font-smoothing: antialiased; padding: 24px 12px; }}
    .wrapper {{ max-width: 540px; margin: 0 auto; }}

    .header {{ background: #2563eb; border-radius: 12px 12px 0 0; padding: 16px 24px; display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 8px; }}
    .sys-name {{ font-size: 12px; font-weight: 600; letter-spacing: 0.6px; text-transform: uppercase; color: rgba(255,255,255,0.85); }}
    .header-badge {{ background: rgba(255,255,255,0.2); color: #fff; font-size: 10px; font-weight: 600; letter-spacing: 0.8px; text-transform: uppercase; padding: 3px 10px; border-radius: 20px; white-space: nowrap; }}

    .hero {{ background: #3b82f6; padding: 32px 24px 28px; }}
    .icon-wrap {{ width: 40px; height: 40px; background: rgba(255,255,255,0.2); border-radius: 10px; display: flex; align-items: center; justify-content: center; margin-bottom: 16px; }}
    .icon-wrap svg {{ width: 20px; height: 20px; stroke: #fff; fill: none; stroke-width: 2; stroke-linecap: round; stroke-linejoin: round; }}
    .hero h1 {{ font-size: 22px; font-weight: 700; color: #fff; line-height: 1.3; margin-bottom: 6px; }}
    .hero p {{ font-size: 13px; color: rgba(255,255,255,0.8); line-height: 1.6; }}

    .body-card {{ background: #fff; padding: 28px 24px 24px; }}
    .greeting {{ font-size: 14px; color: #374151; line-height: 1.75; margin-bottom: 22px; }}
    .greeting strong {{ color: #111827; font-weight: 600; }}

    .success-banner {{ background: #f8faff; border: 1px solid #dbeafe; border-radius: 10px; padding: 13px 16px; margin-bottom: 18px; display: flex; gap: 10px; align-items: flex-start; }}
    .success-banner svg {{ width: 15px; height: 15px; min-width: 15px; stroke: #2563eb; fill: none; stroke-width: 2.2; stroke-linecap: round; stroke-linejoin: round; margin-top: 1px; }}
    .success-banner-text {{ font-size: 13px; color: #1e40af; line-height: 1.6; }}
    .success-banner-text strong {{ font-weight: 600; display: block; margin-bottom: 2px; color: #1d4ed8; }}

    .meta-box {{ background: #f8faff; border: 1px solid #dbeafe; border-radius: 10px; padding: 14px 18px; margin-bottom: 22px; display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 12px; }}
    .meta-item {{ text-align: center; flex: 1; min-width: 80px; }}
    .meta-label {{ font-size: 10px; font-weight: 600; letter-spacing: 1px; text-transform: uppercase; color: #60a5fa; margin-bottom: 4px; }}
    .meta-val {{ font-size: 13px; font-weight: 600; color: #111827; word-break: break-all; }}
    .meta-divider {{ width: 1px; height: 26px; background: #dbeafe; flex-shrink: 0; }}

    .divider {{ border: none; border-top: 1px solid #f3f4f6; margin: 18px 0; }}

    .security-note {{ background: #f9fafb; border: 1px solid #e5e7eb; border-radius: 10px; padding: 12px 16px; margin-bottom: 16px; display: flex; gap: 10px; align-items: flex-start; }}
    .security-note svg {{ width: 14px; height: 14px; min-width: 14px; stroke: #9ca3af; fill: none; stroke-width: 2; margin-top: 1px; }}
    .security-note p {{ font-size: 12px; color: #9ca3af; line-height: 1.6; }}

    .closing {{ font-size: 12px; color: #9ca3af; line-height: 1.65; text-align: center; }}

    .footer {{ background: #f8faff; border: 1px solid #dbeafe; border-top: none; border-radius: 0 0 12px 12px; padding: 16px 24px; text-align: center; }}
    .footer-copy {{ font-size: 11px; color: #9ca3af; line-height: 1.6; }}

    @media (max-width: 420px) {{
      body {{ padding: 12px 8px; }}
      .header, .hero, .body-card, .footer {{ padding-left: 16px; padding-right: 16px; }}
      .hero h1 {{ font-size: 20px; }}
      .meta-box {{ flex-direction: column; align-items: stretch; }}
      .meta-item {{ text-align: left; }}
      .meta-divider {{ width: 100%; height: 1px; }}
    }}
  </style>
</head>
<body>
<div class=""wrapper"">
  <div class=""header"">
    <span class=""sys-name"">Automated Reporting System</span>
    <span class=""header-badge"">Security Update</span>
  </div>
  <div class=""hero"">
    <div class=""icon-wrap"">
      <svg viewBox=""0 0 24 24""><rect x=""3"" y=""11"" width=""18"" height=""11"" rx=""2"" ry=""2""/><path d=""M7 11V7a5 5 0 0 1 10 0v4""/></svg>
    </div>
    <h1>Password Updated!</h1>
    <p>Your account password has been successfully changed.</p>
  </div>
  <div class=""body-card"">
    <p class=""greeting"">
      Hi <strong>{EscapeHtml(firstName)}</strong>,<br/><br/>
      You have successfully updated your password on the <strong>Automated Reporting System</strong>. You may now log in with your new credentials.
    </p>
    <div class=""success-banner"">
      <svg viewBox=""0 0 24 24""><polyline points=""20 6 9 17 4 12""/></svg>
      <div class=""success-banner-text"">
        <strong>Password changed successfully.</strong>
        Your new password is active. You may now log in at your convenience.
      </div>
    </div>
    <div class=""meta-box"">
      <div class=""meta-item"">
        <div class=""meta-label"">Account</div>
        <div class=""meta-val"">{EscapeHtml(emailAddress)}</div>
      </div>
      <div class=""meta-divider""></div>
      <div class=""meta-item"">
        <div class=""meta-label"">Changed On</div>
        <div class=""meta-val"">{EscapeHtml(updatedDate)}</div>
      </div>
      <div class=""meta-divider""></div>
      <div class=""meta-item"">
        <div class=""meta-label"">Role</div>
        <div class=""meta-val"">{EscapeHtml(role)}</div>
      </div>
    </div>
    <hr class=""divider""/>
    <div class=""security-note"">
      <svg viewBox=""0 0 24 24""><path d=""M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z""/></svg>
      <p>If you did not make this change, please contact your system administrator immediately as your account may be compromised.</p>
    </div>
    <p class=""closing"">Thank you for keeping your account secure.</p>
  </div>
  <div class=""footer"">
    <p class=""footer-copy"">© {DateTime.Now.Year} Automated Reporting System. All rights reserved.<br/>This is an automated message — please do not reply to this email.</p>
  </div>
</div>
</body>
</html>";
        }

        /// <summary>
        /// Escapes special HTML characters to prevent XSS.
        /// </summary>
        private static string EscapeHtml(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return input
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
        }
    }
}