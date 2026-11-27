namespace Frends.SMTP.SendEmail.Definitions;

/// <summary>
/// Enumeration for Attachment type.
/// </summary>
public enum AttachmentType
{
    /// <summary>
    /// Select this if the attachment is a file.
    /// </summary>
    FileAttachment,

    /// <summary>
    /// Select this if the attachment file should be created from a string.
    /// </summary>
    AttachmentFromString,
}
