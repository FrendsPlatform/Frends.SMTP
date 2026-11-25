using System;
using System.IO;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Frends.SMTP.SendEmail.Definitions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Moq;
using NUnit.Framework;

namespace Frends.SMTP.SendEmail.Tests;

[TestFixture]
public class SendEmailTests
{
    private const int PORT = 587;
    private const string TEMPATTACHMENTSOURCE = "emailtestattachments";
    private const string TESTFILENAME = "testattachment.txt";
    private const string TESTFILENOTEXISTING = "doesntexist.txt";

    private static readonly string USERNAME = Environment.GetEnvironmentVariable("Frends_SMTP_Username");
    private static readonly string PASSWORD = Environment.GetEnvironmentVariable("Frends_SMTP_Password");
    private static readonly string SMTPADDRESS = Environment.GetEnvironmentVariable("Frends_SMTP_Address");
    private static readonly string TOEMAILADDRESS = Environment.GetEnvironmentVariable("Frends_SMTP_Email");
    private static readonly string FROMEMAILADDRESS = Environment.GetEnvironmentVariable("Frends_SMTP_Email");

    private string localAttachmentFolder;
    private string filepath;
    private Input messageInput;
    private Input input2;
    private Options options;

    [SetUp]
    public void EmailTestSetup()
    {
        localAttachmentFolder = Path.Combine(Path.GetTempPath(), TEMPATTACHMENTSOURCE);

        if (!Directory.Exists(localAttachmentFolder))
            Directory.CreateDirectory(localAttachmentFolder);

        filepath = Path.Combine(localAttachmentFolder, TESTFILENAME);

        if (!File.Exists(filepath))
        {
            File.Create(filepath).Dispose();
        }

        messageInput = new Input()
        {
            From = FROMEMAILADDRESS ?? "sender@example.com",
            To = TOEMAILADDRESS ?? "recipient@example.com",
            Cc = string.Empty,
            Bcc = string.Empty,
            Message = "testmsg",
            IsMessageHtml = false,
            SenderName = "EmailTestSender",
            Subject = "Email test",
            MessageEncoding = "utf-8",
        };

        input2 = new Input()
        {
            From = FROMEMAILADDRESS ?? "sender@example.com",
            To = TOEMAILADDRESS ?? "recipient@example.com",
            Cc = null,
            Bcc = null,
            Message = "testmsg",
            IsMessageHtml = false,
            SenderName = "EmailTestSender",
            Subject = "Email test",
            MessageEncoding = "utf-8",
        };

        options = new Options()
        {
            UserName = USERNAME,
            Password = PASSWORD,
            SMTPServer = SMTPADDRESS,
            Port = PORT,
            UseOAuth2 = false,
            SecureSocket = SecureSocketOption.None,
            AcceptAllCerts = false,
        };
    }

    [TearDown]
    public void EmailTestTearDown()
    {
        if (Directory.Exists(localAttachmentFolder))
            Directory.Delete(localAttachmentFolder, true);
    }

    [Test]
    public async Task SendEmailWithPlainText()
    {
        var input = messageInput;
        input.Subject = "Email test - PlainText";

        var result = await SMTP.SendEmail(input, options, default);
        Assert.IsTrue(result.EmailSent);
    }

    [Test]
    public async Task SendEmailWithFileAttachment()
    {
        var input = messageInput;
        input.Subject = "Email test - FileAttachment";

        var attachment = new Attachment
        {
            AttachmentType = AttachmentType.FileAttachment,
            FilePath = filepath,
            SendIfNoAttachmentsFound = false,
            ThrowExceptionIfAttachmentNotFound = true,
        };

        input.Attachments = new[] { attachment };

        var result = await SMTP.SendEmail(input, options, default);
        Assert.IsTrue(result.EmailSent);
    }

    [Test]
    public async Task SendEmailWithStringAttachment()
    {
        var input = messageInput;
        input.Subject = "Email test - AttachmentFromString";
        var fileAttachment = new AttachmentFromString() { FileContent = "teststring � �", FileName = "testfilefromstring.txt" };
        var attachment = new Attachment()
        {
            AttachmentType = AttachmentType.AttachmentFromString,
            StringAttachment = fileAttachment,
        };
        input.Attachments = new[] { attachment };

        var result = await SMTP.SendEmail(input, options, default);
        Assert.IsTrue(result.EmailSent);
    }

    [Test]
    public async Task TrySendingEmailWithNoFileAttachmentFound()
    {
        var input = messageInput;
        input.Subject = "Email test";

        var attachment = new Attachment
        {
            FilePath = Path.Combine(localAttachmentFolder, TESTFILENOTEXISTING),
            SendIfNoAttachmentsFound = false,
            ThrowExceptionIfAttachmentNotFound = false,
        };

        input.Attachments = new[] { attachment };

        var result = await SMTP.SendEmail(input, options, default);
        Assert.IsFalse(result.EmailSent);
    }

    [Test]
    public async Task TrySendingEmailWithNoCcAndBcc()
    {
        var input = input2;
        input.Subject = "Email test";

        var attachment = new Attachment
        {
            FilePath = Path.Combine(localAttachmentFolder, TESTFILENOTEXISTING),
            SendIfNoAttachmentsFound = false,
            ThrowExceptionIfAttachmentNotFound = false,
        };

        input.Attachments = new[] { attachment };

        var result = await SMTP.SendEmail(input, options, default);
        Assert.IsFalse(result.EmailSent);
    }

    [Test]
    public void TrySendingEmailWithNoFileAttachmentFoundException()
    {
        var input = messageInput;
        input.Subject = "Email test";

        var attachment = new Attachment
        {
            FilePath = Path.Combine(localAttachmentFolder, TESTFILENOTEXISTING),
            SendIfNoAttachmentsFound = false,
            ThrowExceptionIfAttachmentNotFound = true,
        };

        input.Attachments = new[] { attachment };

        var ex = Assert.ThrowsAsync<FileNotFoundException>(async () => await SMTP.SendEmail(input, options, default));
        Assert.AreEqual(@$"The given filepath '{attachment.FilePath}' had no matching files", ex.Message);
    }

    [Test]
    public async Task MissingAttachmentReturnsErrorWhenConfigured()
    {
        messageInput.Attachments = new[]
        {
            new Attachment
            {
                FilePath = Path.Combine(localAttachmentFolder, TESTFILENOTEXISTING),
                ThrowExceptionIfAttachmentNotFound = true,
            },
        };
        options.ThrowErrorOnFailure = false;
        options.ErrorMessageOnFailure = "Unable to send email";

        var result = await SMTP.SendEmail(messageInput, options, default);

        Assert.IsFalse(result.Success);
        Assert.IsFalse(result.EmailSent);
        StringAssert.StartsWith("Unable to send email:", result.Error.Message);
        Assert.IsInstanceOf<FileNotFoundException>(result.Error.AdditionalInfo);
    }

    [Test]
    public void MissingAttachmentThrowsCustomMessageWhenConfigured()
    {
        messageInput.Attachments = new[]
        {
            new Attachment
            {
                FilePath = Path.Combine(localAttachmentFolder, TESTFILENOTEXISTING),
                ThrowExceptionIfAttachmentNotFound = true,
            },
        };
        options.ErrorMessageOnFailure = "Unable to send email";

        var exception = Assert.ThrowsAsync<Exception>(async () =>
            await SMTP.SendEmail(messageInput, options, default));

        Assert.AreEqual("Unable to send email", exception.Message);
        Assert.IsInstanceOf<FileNotFoundException>(exception.InnerException);
    }

    [Test]
    public void CancellationIsRethrownEvenWhenErrorsAreReturned()
    {
        options.ThrowErrorOnFailure = false;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        messageInput.Attachments = new[] { new Attachment { AttachmentType = AttachmentType.FileAttachment } };

        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await SMTP.SendEmail(messageInput, options, cancellation.Token));
    }

    [Test]
    public async Task TestAcceptAllCerts()
    {
        var options = new Options
        {
            AcceptAllCerts = true,
            SMTPServer = "smtp.example.com",
            Port = 587,
            SecureSocket = SecureSocketOption.StartTls,
            UseOAuth2 = false,
        };

        var mockSmtpClient = new Mock<SmtpClient> { CallBase = true };

        mockSmtpClient.Setup(client => client.ConnectAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<SecureSocketOptions>(),
                It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        mockSmtpClient.Setup(client => client.AuthenticateAsync(
            It.IsAny<SaslMechanism>(),
            It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await SMTP.InitializeSmtpClient(options, default, mockSmtpClient.Object);

        // Verify connection parameters
        mockSmtpClient.Verify(
            client => client.ConnectAsync(
            options.SMTPServer,
            options.Port,
            SecureSocketOptions.StartTls,
            default),
            Times.Once);

        // Verify certificate validation behavior
        Assert.IsNotNull(mockSmtpClient.Object.ServerCertificateValidationCallback);

        var callback = mockSmtpClient.Object.ServerCertificateValidationCallback;

        // Test various SSL policy errors
        Assert.Multiple(() =>
        {
            Assert.IsTrue(callback.Invoke(null, null, null, SslPolicyErrors.None), "Should accept valid certificates");
            Assert.IsTrue(callback.Invoke(null, null, null, SslPolicyErrors.RemoteCertificateNotAvailable), "Should accept missing certificates");
            Assert.IsTrue(callback.Invoke(null, null, null, SslPolicyErrors.RemoteCertificateNameMismatch), "Should accept mismatched certificates");
            Assert.IsTrue(callback.Invoke(null, null, null, SslPolicyErrors.RemoteCertificateChainErrors), "Should accept invalid certificate chains");
        });

        // Test with AcceptAllCerts = false
        options.AcceptAllCerts = false;
        await SMTP.InitializeSmtpClient(options, default, mockSmtpClient.Object);
        callback = mockSmtpClient.Object.ServerCertificateValidationCallback;

        Assert.Multiple(() =>
        {
            Assert.IsTrue(callback.Invoke(null, null, null, SslPolicyErrors.None), "Should accept valid certificates");
            Assert.IsFalse(callback.Invoke(null, null, null, SslPolicyErrors.RemoteCertificateNotAvailable), "Should reject missing certificates");
            Assert.IsFalse(callback.Invoke(null, null, null, SslPolicyErrors.RemoteCertificateNameMismatch), "Should reject mismatched certificates");
            Assert.IsFalse(callback.Invoke(null, null, null, SslPolicyErrors.RemoteCertificateChainErrors), "Should reject invalid certificate chains");
        });
    }

    [Test]
    public async Task TestCertificateThumbprintValidation()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=UnitTestCert", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var validCert = request.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(1));
        var validThumbprint = validCert.Thumbprint?.Replace(" ", string.Empty).ToUpperInvariant();
        var invalidThumbprint = "DEADBEEF1234567890ABCDEF1234567890ABCDEF";

        var mockSmtpClient = new Mock<SmtpClient> { CallBase = true };

        mockSmtpClient.Setup(c => c.ConnectAsync(
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<SecureSocketOptions>(),
            It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        mockSmtpClient.Setup(c => c.AuthenticateAsync(
            It.IsAny<SaslMechanism>(),
            It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var options = new Options
        {
            SMTPServer = "smtp.example.com",
            Port = 587,
            SecureSocket = SecureSocketOption.StartTls,
            UseOAuth2 = false,
        };

        options.AcceptAllCerts = true;
        options.ServerCertificationThumbprint = validThumbprint;

        await SMTP.InitializeSmtpClient(options, default, mockSmtpClient.Object);
        var callback = mockSmtpClient.Object.ServerCertificateValidationCallback;

        Assert.IsTrue(
            callback.Invoke(null, validCert, null, SslPolicyErrors.RemoteCertificateChainErrors),
            "AcceptAllCerts=true: Valid thumbprint should be accepted.");

        options.ServerCertificationThumbprint = invalidThumbprint;

        await SMTP.InitializeSmtpClient(options, default, mockSmtpClient.Object);
        callback = mockSmtpClient.Object.ServerCertificateValidationCallback;

        Assert.IsFalse(
            callback.Invoke(null, validCert, null, SslPolicyErrors.RemoteCertificateChainErrors),
            "AcceptAllCerts=true: Invalid thumbprint should be rejected.");

        options.AcceptAllCerts = false;
        options.ServerCertificationThumbprint = validThumbprint;

        await SMTP.InitializeSmtpClient(options, default, mockSmtpClient.Object);
        callback = mockSmtpClient.Object.ServerCertificateValidationCallback;

        Assert.IsTrue(
            callback.Invoke(null, validCert, null, SslPolicyErrors.RemoteCertificateChainErrors),
            "AcceptAllCerts=false: Valid thumbprint should be accepted even with SSL errors.");

        options.ServerCertificationThumbprint = invalidThumbprint;

        await SMTP.InitializeSmtpClient(options, default, mockSmtpClient.Object);
        callback = mockSmtpClient.Object.ServerCertificateValidationCallback;

        Assert.IsFalse(
            callback.Invoke(null, validCert, null, SslPolicyErrors.RemoteCertificateChainErrors),
            "AcceptAllCerts=false: Invalid thumbprint should be rejected.");
    }
}
