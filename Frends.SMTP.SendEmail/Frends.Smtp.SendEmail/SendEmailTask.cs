using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using System.Security.Cryptography.X509Certificates;
using MimeKit;
using MimeKit.Text;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Frends.SMTP.SendEmail.Definitions;
using System.Net.Security;

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
    /// <param name="attachments">Parameters for adding attachments.</param>
    /// <param name="SMTPSettings">Connection parameters.</param>
    /// <param name="cancellationToken">Token given by Frends to terminate the Task.</param>
    /// <returns>Object { bool EmailSent, string StatusString }</returns>
    public static async Task<Result> SendEmail([PropertyTab] Input input, [PropertyTab] AttachmentOptions attachments, [PropertyTab] Options SMTPSettings, CancellationToken cancellationToken)
    {
        using var mail = InitializeMimeMessage(input);

        if (attachments != null && attachments.Attachments.Length > 0)
        {
            var builder = new BodyBuilder();

            if (input.IsMessageHtml)
                builder.HtmlBody = input.Message;
            else
                builder.TextBody = input.Message;

            foreach (var attachment in attachments.Attachments)
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
            //Set message encoding
            Encoding encoding = Encoding.GetEncoding(input.MessageEncoding);

            var body = (input.IsMessageHtml)
                ? new TextPart(TextFormat.Html) { Text = input.Message }
                : new TextPart(TextFormat.Plain) { Text = input.Message };
            body.SetText(encoding, input.Message);
            mail.Body = body;
        }

        using var client = await InitializeSmtpClient(SMTPSettings, cancellationToken);

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
#pragma warning disable S4830 // Server certificates should be verified during SSL/TLS connections
            client.ServerCertificateValidationCallback = (s, x509certificate, x590chain, sslPolicyErrors) =>
            {
                if (!string.IsNullOrEmpty(options.ServerCertificationThumbprint))
                {
                    if (x509certificate is X509Certificate2 cert && string.Equals(
                        options.ServerCertificationThumbprint?.Replace(" ", "").ToUpperInvariant(),
                        cert.Thumbprint?.Replace(" ", "").ToUpperInvariant(),
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
#pragma warning restore S4830 // Server certificates should be verified during SSL/TLS connections
        }
        else
        {
            client.ServerCertificateValidationCallback = (s, x509certificate, x590chain, sslPolicyErrors) =>
            {
                if (!string.IsNullOrEmpty(options.ServerCertificationThumbprint))
                {
                    if (x509certificate is X509Certificate2 cert && string.Equals(
                        options.ServerCertificationThumbprint?.Replace(" ", "").ToUpperInvariant(),
                        cert.Thumbprint?.Replace(" ", "").ToUpperInvariant(),
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
        //split recipients, either by comma or semicolon
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

        //Create mail object
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
        string fileMask = Path.GetFileName(filePath) != "" ? Path.GetFileName(filePath) : "*";

        string[] filePaths = Directory.GetFiles(folder, fileMask);

        return filePaths;
    }

    /// <summary>
    /// Create temp file of attachment from string.
    /// </summary>
    /// <param name="attachment"></param>
    private static string CreateTemporaryFile(Attachment attachment)
    {
        var TempWorkDirBase = InitializeTemporaryWorkPath();
        var filePath = Path.Combine(TempWorkDirBase, attachment.StringAttachment.FileName);
        var content = attachment.StringAttachment.FileContent;

        using var sw = File.CreateText(filePath);
        sw.Write(content);

        return filePath;
    }

    /// <summary>
    /// Remove the temporary workdir.
    /// </summary>
    /// <param name="tempWorkDir"></param>
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
