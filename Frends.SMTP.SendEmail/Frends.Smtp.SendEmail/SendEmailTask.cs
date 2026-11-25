using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Frends.SMTP.SendEmail.Definitions;
using Frends.SMTP.SendEmail.Helpers;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;

namespace Frends.SMTP.SendEmail;

/// <summary>
/// Main class of the Task.
/// </summary>
public static class SMTP
{
    /// <summary>
    /// Sends email message with optional attachments.
    /// [Documentation](https://tasks.frends.com/tasks/frends-tasks/Frends.SMTP.SendEmail)
    /// </summary>
    /// <param name="input">Message parameters.</param>
    /// <param name="options">SMTP connection and error-handling parameters.</param>
    /// <param name="cancellationToken">Token given by Frends to terminate the Task.</param>
    /// <returns>object { bool Success, bool EmailSent, string StatusString, object Error { string Message, Exception AdditionalInfo } }</returns>
    public static async Task<Result> SendEmail([PropertyTab] Input input, [PropertyTab] Options options, CancellationToken cancellationToken)
    {
        try
        {
            return await SendEmailCore(input, options, cancellationToken);
        }
        catch (Exception ex)
        {
            return ex.Handle(options);
        }
    }

    internal static async Task<Result> SendEmailCore(Input input, Options options, CancellationToken cancellationToken)
    {
        using var mail = InitializeMimeMessage(input);

        if (input.Attachments != null && input.Attachments.Length > 0)
        {
            var builder = new BodyBuilder();

            if (input.IsMessageHtml)
                builder.HtmlBody = input.Message;
            else
                builder.TextBody = input.Message;

            foreach (var attachment in input.Attachments)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (attachment.AttachmentType == AttachmentType.FileAttachment)
                {
                    ICollection<string> allAttachmentFilePaths = GetAttachmentFiles(attachment.FilePath);

                    if (attachment.ThrowExceptionIfAttachmentNotFound && allAttachmentFilePaths.Count == 0)
                        throw new FileNotFoundException($@"The given filepath '{attachment.FilePath}' had no matching files", attachment.FilePath);

                    if (allAttachmentFilePaths.Count == 0 && !attachment.SendIfNoAttachmentsFound)
                        return new Result(false, $@"No attachments found matching path '{attachment.FilePath}'. No email sent.");

                    foreach (var fp in allAttachmentFilePaths)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        builder.Attachments.Add(fp, cancellationToken);
                    }
                }

                // Create attachment only if content is not empty.
                if (attachment.AttachmentType == AttachmentType.AttachmentFromString && !string.IsNullOrEmpty(attachment.StringAttachment.FileContent))
                {
                    var path = CreateTemporaryFile(attachment);
                    builder.Attachments.Add(path, cancellationToken);
                    CleanUpTempWorkDir(path);
                }
            }

            mail.Body = builder.ToMessageBody();
        }
        else
        {
            // Set message encoding
            Encoding encoding = Encoding.GetEncoding(input.MessageEncoding);

            var body = input.IsMessageHtml
                ? new TextPart(TextFormat.Html) { Text = input.Message }
                : new TextPart(TextFormat.Plain) { Text = input.Message };
            body.SetText(encoding, input.Message);
            mail.Body = body;
        }

        using var client = await InitializeSmtpClient(options, cancellationToken);

        await client.SendAsync(mail, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        return new Result(true, $"Email sent to: {mail.To}");
    }

    /// <summary>
    /// Initializes new SmtpClient with given parameters.
    /// </summary>
    internal static async Task<SmtpClient> InitializeSmtpClient(Options options, CancellationToken cancellationToken, SmtpClient client = null)
    {
        client ??= new SmtpClient();

        // Accept all certs?
        if (options.AcceptAllCerts)
        {
            client.ServerCertificateValidationCallback = (s, x509certificate, x590chain, sslPolicyErrors) =>
            {
                if (!string.IsNullOrEmpty(options.ServerCertificationThumbprint))
                {
                    if (x509certificate is X509Certificate2 cert && string.Equals(
                        options.ServerCertificationThumbprint?.Replace(" ", string.Empty).ToUpperInvariant(),
                        cert.Thumbprint?.Replace(" ", string.Empty).ToUpperInvariant(),
                        StringComparison.Ordinal))
                        return true;
                    else
                        return false;
                }
                else
                {
                    return true;
                }
            };
        }
        else
        {
            client.ServerCertificateValidationCallback = (s, x509certificate, x590chain, sslPolicyErrors) =>
            {
                if (!string.IsNullOrEmpty(options.ServerCertificationThumbprint))
                {
                    if (x509certificate is X509Certificate2 cert && string.Equals(
                        options.ServerCertificationThumbprint?.Replace(" ", string.Empty).ToUpperInvariant(),
                        cert.Thumbprint?.Replace(" ", string.Empty).ToUpperInvariant(),
                        StringComparison.Ordinal))
                        return true;
                    else
                        return false;
                }

                return sslPolicyErrors == SslPolicyErrors.None;
            };
        }

        var secureSocketOption = options.SecureSocket switch
        {
            SecureSocketOption.None => SecureSocketOptions.None,
            SecureSocketOption.SslOnConnect => SecureSocketOptions.SslOnConnect,
            SecureSocketOption.StartTls => SecureSocketOptions.StartTls,
            SecureSocketOption.StartTlsWhenAvailable => SecureSocketOptions.StartTlsWhenAvailable,
            _ => SecureSocketOptions.Auto,
        };

        await client.ConnectAsync(options.SMTPServer, options.Port, secureSocketOption, cancellationToken);

        SaslMechanism mechanism;

        if (options.UseOAuth2)
            mechanism = new SaslMechanismOAuth2(options.UserName, options.Token);
        else if (string.IsNullOrEmpty(options.Password))
            return client;
        else
            mechanism = new SaslMechanismLogin(new NetworkCredential(options.UserName, options.Password));

        await client.AuthenticateAsync(mechanism, cancellationToken);

        return client;
    }

    /// <summary>
    /// Initializes new MailMessage with given parameters. Uses default value 'true' for IsBodyHtml
    /// </summary>
    private static MimeMessage InitializeMimeMessage(Input input)
    {
        // split recipients, either by comma or semicolon
        var separators = new[] { ',', ';' };

        MailboxAddress[] recipients = string.IsNullOrEmpty(input.To)
            ? Array.Empty<MailboxAddress>()
            : input.To.Split(separators, StringSplitOptions.RemoveEmptyEntries).Select(x => MailboxAddress.Parse(x)).ToArray();
        MailboxAddress[] ccRecipients = string.IsNullOrEmpty(input.Cc)
            ? Array.Empty<MailboxAddress>()
            : input.Cc.Split(separators, StringSplitOptions.RemoveEmptyEntries).Select(x => MailboxAddress.Parse(x)).ToArray();
        MailboxAddress[] bccRecipients = string.IsNullOrEmpty(input.Bcc)
            ? Array.Empty<MailboxAddress>()
            : input.Bcc.Split(separators, StringSplitOptions.RemoveEmptyEntries).Select(x => MailboxAddress.Parse(x)).ToArray();

        // Create mail object
        var mail = new MimeMessage();
        mail.From.Add(new MailboxAddress(input.SenderName, input.From));
        mail.Sender = new MailboxAddress(input.SenderName, input.From);
        mail.To.AddRange(recipients);
        mail.Cc.AddRange(ccRecipients);
        mail.Bcc.AddRange(bccRecipients);
        mail.Subject = input.Subject;

        return mail;
    }

    /// <summary>
    /// Gets all actual file names of attachments matching given file path
    /// </summary>
    private static ICollection<string> GetAttachmentFiles(string filePath)
    {
        string folder = Path.GetDirectoryName(filePath);
        string fileMask = Path.GetFileName(filePath) != string.Empty ? Path.GetFileName(filePath) : "*";

        string[] filePaths = Directory.GetFiles(folder, fileMask);

        return filePaths;
    }

    /// <summary>
    /// Create temp file of attachment from string.
    /// </summary>
    /// <param name="attachment">Attachment content to write to a temporary file.</param>
    private static string CreateTemporaryFile(Attachment attachment)
    {
        var tempWorkDirBase = InitializeTemporaryWorkPath();
        var filePath = Path.Combine(tempWorkDirBase, attachment.StringAttachment.FileName);
        var content = attachment.StringAttachment.FileContent;

        using var sw = File.CreateText(filePath);
        sw.Write(content);

        return filePath;
    }

    /// <summary>
    /// Remove the temporary workdir.
    /// </summary>
    /// <param name="tempWorkDir">Temporary directory to remove.</param>
    private static void CleanUpTempWorkDir(string tempWorkDir)
    {
        if (!string.IsNullOrEmpty(tempWorkDir) && Directory.Exists(tempWorkDir)) Directory.Delete(tempWorkDir, true);
    }

    /// <summary>
    /// Create temperary directory for temp file.
    /// </summary>
    private static string InitializeTemporaryWorkPath()
    {
        var tempWorkDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempWorkDir);
        return tempWorkDir;
    }
}
