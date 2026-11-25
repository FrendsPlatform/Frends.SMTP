namespace Frends.SMTP.SendEmail.Definitions;

/// <summary>
/// Result class of the Task.
/// </summary>
public class Result
{
    internal Result(bool emailSent = false, string status = null, Error error = null)
    {
        EmailSent = emailSent;
        Success = emailSent;
        StatusString = status;
        Error = error;
    }

    /// <summary>
    /// Indicates whether the email was sent successfully.
    /// </summary>
    /// <example>true</example>
    public bool Success { get; set; }

    /// <summary>
    /// Error that occurred while sending the email.
    /// </summary>
    /// <example>object { string Message, Exception AdditionalInfo }</example>
    public Error Error { get; set; }

    /// <summary>
    /// Value is true if email was sent.
    /// </summary>
    /// <example>true</example>
    public bool EmailSent { get; private set; }

    /// <summary>
    /// Contains information about the task's result.
    /// </summary>
    /// <example>Email sent to: john.doe@somedomain.com</example>
    public string StatusString { get; private set; }
}
