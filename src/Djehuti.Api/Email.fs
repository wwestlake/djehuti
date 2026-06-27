module Djehuti.Api.Email

open System
open Amazon.SimpleEmail
open Amazon.SimpleEmail.Model

let private getClient () =
    new AmazonSimpleEmailServiceClient(Amazon.RegionEndpoint.USEast2)

let private getSenderEmail () =
    let email = Environment.GetEnvironmentVariable("MAIL_FROM_ADDRESS")
    if String.IsNullOrWhiteSpace(email) then
        "noreply@lagdaemon.com"
    else
        email

type EmailMessage =
    { To: string
      Subject: string
      HtmlBody: string }

let sendEmail (message: EmailMessage) : Async<bool> =
    async {
        try
            use client = getClient ()
            let toAddresses = new System.Collections.Generic.List<string>()
            toAddresses.Add(message.To)
            let request = SendEmailRequest(
                Source = getSenderEmail (),
                Destination = new Destination(ToAddresses = toAddresses),
                Message = new Message(
                    Subject = new Content(message.Subject),
                    Body = new Body(Html = new Content(message.HtmlBody))
                )
            )
            let! _ = client.SendEmailAsync(request) |> Async.AwaitTask
            return true
        with ex ->
            printfn "[Email] Failed to send email to %s: %s" message.To ex.Message
            return false
    }

// ── Email Templates ─────────────────────────────────────────────────────────

let verificationEmailTemplate (userName: string) (token: string) : string =
    let verifyLink = $"https://lagdaemon.com/verify?token={token}"
    sprintf """
    <html>
    <body style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; line-height: 1.6; color: #333;">
        <div style="max-width: 600px; margin: 0 auto; padding: 20px;">
            <h2>Confirm your email address</h2>
            <p>Hi %s,</p>
            <p>Click the link below to confirm your email address and activate your account:</p>
            <p style="margin: 30px 0;">
                <a href="%s" style="background-color: #58a6ff; color: white; padding: 12px 24px; text-decoration: none; border-radius: 6px; display: inline-block;">
                    Verify Email
                </a>
            </p>
            <p style="color: #666; font-size: 14px;">
                Or copy this link: <code>%s</code>
            </p>
            <p style="color: #999; font-size: 12px; margin-top: 40px;">
                This link expires in 24 hours.
            </p>
        </div>
    </body>
    </html>
    """ userName verifyLink verifyLink

let announcementEmailTemplate (title: string) (bodyHtml: string) (unsubscribeUrl: string) : string =
    sprintf """
    <html>
    <body style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; line-height: 1.6; color: #333; background: #f5f5f5;">
        <div style="max-width: 600px; margin: 0 auto; background: #fff; border-radius: 8px; overflow: hidden; box-shadow: 0 2px 8px rgba(0,0,0,0.08);">
            <div style="background: #0d1117; padding: 24px 32px;">
                <h1 style="color: #58a6ff; margin: 0; font-size: 20px;">Lagdaemon Announcement</h1>
            </div>
            <div style="padding: 32px;">
                <h2 style="margin-top: 0; color: #0d1117;">%s</h2>
                <div style="color: #444; font-size: 15px;">%s</div>
                <div style="margin-top: 32px; padding-top: 20px; border-top: 1px solid #eee; font-size: 12px; color: #999;">
                    You received this because you subscribed to announcements from lagdaemon.com.<br/>
                    <a href="%s" style="color: #999;">Unsubscribe</a>
                </div>
            </div>
        </div>
    </body>
    </html>
    """ title bodyHtml unsubscribeUrl

let confirmSubscriptionEmailTemplate (confirmUrl: string) : string =
    sprintf """
    <html>
    <body style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; line-height: 1.6; color: #333;">
        <div style="max-width: 600px; margin: 0 auto; padding: 20px;">
            <h2>Confirm your announcement subscription</h2>
            <p>Click the link below to start receiving announcements from lagdaemon.com:</p>
            <p style="margin: 30px 0;">
                <a href="%s" style="background-color: #58a6ff; color: white; padding: 12px 24px; text-decoration: none; border-radius: 6px; display: inline-block;">
                    Confirm Subscription
                </a>
            </p>
            <p style="color: #999; font-size: 12px; margin-top: 40px;">
                If you didn't request this, you can safely ignore this email.
            </p>
        </div>
    </body>
    </html>
    """ confirmUrl

// ── Community notification templates ────────────────────────────────────────

let private baseUrl () =
    let v = Environment.GetEnvironmentVariable("BASE_URL")
    if String.IsNullOrWhiteSpace(v) then "https://lagdaemon.com" else v.TrimEnd('/')

let private wrapper (innerHtml: string) =
    sprintf """<html>
<body style="margin:0;padding:0;background:#0d1117;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;">
<div style="max-width:600px;margin:0 auto;background:#161b22;border-radius:8px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,.4);">
  <div style="background:#0d1117;padding:20px 32px;border-bottom:1px solid #30363d;">
    <span style="color:#58a6ff;font-size:18px;font-weight:700;">Lagdaemon</span>
  </div>
  <div style="padding:28px 32px;color:#c9d1d9;font-size:15px;line-height:1.6;">
    %s
  </div>
  <div style="padding:16px 32px;border-top:1px solid #30363d;font-size:12px;color:#6e7681;">
    You received this because your notification preferences are enabled.
    <a href="%s/settings#notifications" style="color:#58a6ff;text-decoration:none;">Manage preferences</a>
  </div>
</div>
</body></html>""" innerHtml (baseUrl ())

let achievementEmailTemplate (displayName: string) (icon: string) (achievementName: string) (description: string) : string =
    wrapper (sprintf """
    <h2 style="color:#e6edf3;margin-top:0;">You earned a badge! %s</h2>
    <p>Hi %s,</p>
    <p>You've unlocked the <strong style="color:#ffd700;">%s</strong> achievement:</p>
    <blockquote style="border-left:3px solid #58a6ff;margin:16px 0;padding:8px 16px;color:#8b949e;">%s</blockquote>
    <p><a href="%s/achievements" style="background:#58a6ff;color:#0d1117;padding:10px 20px;border-radius:6px;text-decoration:none;display:inline-block;font-weight:600;">View your achievements</a></p>
    """ icon displayName achievementName description (baseUrl ()))

let mentionEmailTemplate (displayName: string) (mentionedBy: string) (threadTitle: string) (threadLink: string) (preview: string) : string =
    wrapper (sprintf """
    <h2 style="color:#e6edf3;margin-top:0;">You were mentioned</h2>
    <p>Hi %s,</p>
    <p><strong>%s</strong> mentioned you in <em>%s</em>:</p>
    <blockquote style="border-left:3px solid #58a6ff;margin:16px 0;padding:8px 16px;color:#8b949e;font-style:italic;">%s</blockquote>
    <p><a href="%s%s" style="background:#58a6ff;color:#0d1117;padding:10px 20px;border-radius:6px;text-decoration:none;display:inline-block;font-weight:600;">View thread</a></p>
    """ displayName mentionedBy threadTitle preview (baseUrl ()) threadLink)

let threadReplyEmailTemplate (displayName: string) (repliedBy: string) (threadTitle: string) (threadLink: string) (preview: string) : string =
    wrapper (sprintf """
    <h2 style="color:#e6edf3;margin-top:0;">New reply in your thread</h2>
    <p>Hi %s,</p>
    <p><strong>%s</strong> replied to <em>%s</em>:</p>
    <blockquote style="border-left:3px solid #58a6ff;margin:16px 0;padding:8px 16px;color:#8b949e;font-style:italic;">%s</blockquote>
    <p><a href="%s%s" style="background:#58a6ff;color:#0d1117;padding:10px 20px;border-radius:6px;text-decoration:none;display:inline-block;font-weight:600;">View thread</a></p>
    """ displayName repliedBy threadTitle preview (baseUrl ()) threadLink)

let passwordResetEmailTemplate (userName: string) (resetLink: string) : string =
    sprintf """
    <html>
    <body style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; line-height: 1.6; color: #333;">
        <div style="max-width: 600px; margin: 0 auto; padding: 20px;">
            <h2>Reset your password</h2>
            <p>Hi %s,</p>
            <p>Click the link below to set a new password for your account:</p>
            <p style="margin: 30px 0;">
                <a href="%s" style="background-color: #58a6ff; color: white; padding: 12px 24px; text-decoration: none; border-radius: 6px; display: inline-block;">
                    Reset Password
                </a>
            </p>
            <p style="color: #666; font-size: 14px;">
                Or copy this link: <code>%s</code>
            </p>
            <p style="color: #999; font-size: 12px; margin-top: 40px;">
                This link expires in 1 hour. If you didn't request this, you can ignore this email.
            </p>
        </div>
    </body>
    </html>
    """ userName resetLink resetLink
