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
