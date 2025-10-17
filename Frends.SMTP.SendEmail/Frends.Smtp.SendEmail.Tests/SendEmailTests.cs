using NUnit.Framework;
using Moq;
using System;
using System.IO;
using System.Threading.Tasks;
using Frends.SMTP.SendEmail.Definitions;
using MailKit.Net.Smtp;
using MailKit.Security;
using System.Net.Security;
using System.Threading;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;

namespace Frends.SMTP.SendEmail.Tests;

[TestFixture]
public class SendEmailTests
{
    // ****************************************** FILL THESE ******************************************************
    private static readonly string USERNAME = Environment.GetEnvironmentVariable("Frends_SMTP_Username");
    private static readonly string PASSWORD = Environment.GetEnvironmentVariable("Frends_SMTP_Password");
    private static readonly string SMTPADDRESS = Environment.GetEnvironmentVariable("Frends_SMTP_Address");
    private static readonly string TOEMAILADDRESS = Environment.GetEnvironmentVariable("Frends_SMTP_Email");
    private static readonly string FROMEMAILADDRESS = Environment.GetEnvironmentVariable("Frends_SMTP_Email");
    private const int PORT = 587;
    // ************************************************************************************************************


    private const string TEMP_ATTACHMENT_SOURCE = "emailtestattachments";
    private const string TEST_FILE_NAME = "testattachment.txt";
    private const string TEST_FILE_NOT_EXISTING = "doesntexist.txt";

    private string _localAttachmentFolder;
    private string _filepath;
    private Input _input;
    private Input _input2;
    private Options _options;

    [SetUp]
    public void EmailTestSetup()
    {
        _localAttachmentFolder = Path.Combine(Path.GetTempPath(), TEMP_ATTACHMENT_SOURCE);

        if (!Directory.Exists(_localAttachmentFolder))
            Directory.CreateDirectory(_localAttachmentFolder);

        _filepath = Path.Combine(_localAttachmentFolder, TEST_FILE_NAME);

        if (!File.Exists(_filepath))
        {
            File.Create(_filepath).Dispose();
        }

        _input = new Input()
        {
            From = FROMEMAILADDRESS,
            To = TOEMAILADDRESS,
            Cc = "",
            Bcc = "",
            Message = "testmsg",
            IsMessageHtml = false,
            SenderName = "EmailTestSender",
            MessageEncoding = "utf-8"
        };

        _input2 = new Input()
        {
            From = FROMEMAILADDRESS,
            To = TOEMAILADDRESS,
            Cc = null,
            Bcc = null,
            Message = "testmsg",
            IsMessageHtml = false,
            SenderName = "EmailTestSender",
            MessageEncoding = "utf-8"
        };

        _options = new Options()
        {
            UserName = USERNAME,
            Password = PASSWORD,
            SMTPServer = SMTPADDRESS,
            Port = PORT,
            UseOAuth2 = false,
            SecureSocket = SecureSocketOption.None,
            AcceptAllCerts = false
        };

    }

    [TearDown]
    public void EmailTestTearDown()
    {
        if (Directory.Exists(_localAttachmentFolder))
            Directory.Delete(_localAttachmentFolder, true);
    }

    [Test]
    public async Task SendEmailWithPlainText()
    {
        var input = _input;
        input.Subject = "Email test - PlainText";

        var result = await SMTP.SendEmail(input, null, _options, default);
        Assert.IsTrue(result.EmailSent);
    }

    [Test]
    public async Task SendEmailWithFileAttachment()
    {
        var input = _input;
        input.Subject = "Email test - FileAttachment";

        var attachment = new Attachment
        {
            AttachmentType = AttachmentType.FileAttachment,
            FilePath = _filepath,
            SendIfNoAttachmentsFound = false,
            ThrowExceptionIfAttachmentNotFound = true
        };

        var Attachments = new AttachmentOptions { Attachments = new Attachment[] { attachment } };

        var result = await SMTP.SendEmail(input, Attachments, _options, default);
        Assert.IsTrue(result.EmailSent);
    }

    [Test]
    public async Task SendEmailWithStringAttachment()
    {
        var input = _input;
        input.Subject = "Email test - AttachmentFromString";
        var fileAttachment = new AttachmentFromString() { FileContent = "teststring � �", FileName = "testfilefromstring.txt" };
        var attachment = new Attachment()
        {
            AttachmentType = AttachmentType.AttachmentFromString,
            StringAttachment = fileAttachment
        };
        var Attachments = new AttachmentOptions { Attachments = new Attachment[] { attachment } };

        var result = await SMTP.SendEmail(input, Attachments, _options, default);
        Assert.IsTrue(result.EmailSent);
    }

    [Test]
    public async Task TrySendingEmailWithNoFileAttachmentFound()
    {
        var input = _input;
        input.Subject = "Email test";

        var attachment = new Attachment
        {
            FilePath = Path.Combine(_localAttachmentFolder, TEST_FILE_NOT_EXISTING),
            SendIfNoAttachmentsFound = false,
            ThrowExceptionIfAttachmentNotFound = false
        };

        var Attachments = new AttachmentOptions { Attachments = new Attachment[] { attachment } };

        var result = await SMTP.SendEmail(input, Attachments, _options, default);
        Assert.IsFalse(result.EmailSent);
    }

    [Test]
    public async Task TrySendingEmailWithNoCcAndBcc()
    {
        var input = _input2;
        input.Subject = "Email test";

        var attachment = new Attachment
        {
            FilePath = Path.Combine(_localAttachmentFolder, TEST_FILE_NOT_EXISTING),
            SendIfNoAttachmentsFound = false,
            ThrowExceptionIfAttachmentNotFound = false
        };

        var Attachments = new AttachmentOptions { Attachments = new Attachment[] { attachment } };

        var result = await SMTP.SendEmail(input, Attachments, _options, default);
        Assert.IsFalse(result.EmailSent);
    }

    [Test]
    public void TrySendingEmailWithNoFileAttachmentFoundException()
    {
        var input = _input;
        input.Subject = "Email test";

        var attachment = new Attachment
        {
            FilePath = Path.Combine(_localAttachmentFolder, TEST_FILE_NOT_EXISTING),
            SendIfNoAttachmentsFound = false,
            ThrowExceptionIfAttachmentNotFound = true
        };

        var Attachments = new AttachmentOptions { Attachments = new Attachment[] { attachment } };

        var ex = Assert.ThrowsAsync<FileNotFoundException>(async () => await SMTP.SendEmail(input, Attachments, _options, default));
        Assert.AreEqual(@$"The given filepath '{attachment.FilePath}' had no matching files", ex.Message);
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
        mockSmtpClient.Verify(client => client.ConnectAsync(
        options.SMTPServer,
        options.Port,
        SecureSocketOptions.StartTls,
        default), Times.Once);

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
        var validThumbprint = validCert.Thumbprint?.Replace(" ", "").ToUpperInvariant();
        var invalidThumbprint = "DEADBEEF1234567890ABCDEF1234567890ABCDEF";


        var mockSmtpClient = new Mock<SmtpClient> { CallBase = true };

        mockSmtpClient.Setup(c => c.ConnectAsync(
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<SecureSocketOptions>(),
            It.IsAny<CancellationToken>()
        )).Returns(Task.CompletedTask);

        mockSmtpClient.Setup(c => c.AuthenticateAsync(
            It.IsAny<SaslMechanism>(),
            It.IsAny<CancellationToken>()
        )).Returns(Task.CompletedTask);

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
            "AcceptAllCerts=true: Valid thumbprint should be accepted."
        );

        options.ServerCertificationThumbprint = invalidThumbprint;

        await SMTP.InitializeSmtpClient(options, default, mockSmtpClient.Object);
        callback = mockSmtpClient.Object.ServerCertificateValidationCallback;

        Assert.IsFalse(
            callback.Invoke(null, validCert, null, SslPolicyErrors.RemoteCertificateChainErrors),
            "AcceptAllCerts=true: Invalid thumbprint should be rejected."
        );

        options.AcceptAllCerts = false;
        options.ServerCertificationThumbprint = validThumbprint;

        await SMTP.InitializeSmtpClient(options, default, mockSmtpClient.Object);
        callback = mockSmtpClient.Object.ServerCertificateValidationCallback;

        Assert.IsTrue(
            callback.Invoke(null, validCert, null, SslPolicyErrors.RemoteCertificateChainErrors),
            "AcceptAllCerts=false: Valid thumbprint should be accepted even with SSL errors."
        );

        options.ServerCertificationThumbprint = invalidThumbprint;

        await SMTP.InitializeSmtpClient(options, default, mockSmtpClient.Object);
        callback = mockSmtpClient.Object.ServerCertificateValidationCallback;

        Assert.IsFalse(
            callback.Invoke(null, validCert, null, SslPolicyErrors.RemoteCertificateChainErrors),
            "AcceptAllCerts=false: Invalid thumbprint should be rejected."
        );
    }
}
