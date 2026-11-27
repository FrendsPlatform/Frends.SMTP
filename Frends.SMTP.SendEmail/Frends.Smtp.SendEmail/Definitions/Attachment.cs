using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Frends.SMTP.SendEmail.Definitions;

/// <summary>
/// Attachment
/// </summary>
public class Attachment
{
    /// <summary>
    /// Chooses if the attachment file is created from a string or copied from disk.
    /// </summary>
    /// <example>AttachmentType.FileAttachment</example>
    public AttachmentType AttachmentType { get; set; }

    /// <summary>
    /// Attachment from string.
    /// </summary>
    /// <example>AttachmentFromString { "Test file content", "testFile.txt" }</example>
    [UIHint(nameof(AttachmentType), "", AttachmentType.AttachmentFromString)]
    public AttachmentFromString StringAttachment { get; set; }

    /// <summary>
    /// Attachment file's path. Uses Directory.GetFiles(string, string) as a pattern matching technique. See https://msdn.microsoft.com/en-us/library/wz42302f(v=vs.110).aspx.
    /// Exception: If the path ends in a directory, all files in that folder are added as attachments.
    /// </summary>
    /// <example>C:\path\to\file.txt</example>
    [DefaultValue("")]
    [UIHint(nameof(AttachmentType), "", AttachmentType.FileAttachment)]
    public string FilePath { get; set; }

    /// <summary>
    /// If set true and no files match the given path, an exception is thrown.
    /// </summary>
    /// <example>true</example>
    [UIHint(nameof(AttachmentType), "", AttachmentType.FileAttachment)]
    public bool ThrowExceptionIfAttachmentNotFound { get; set; }

    /// <summary>
    /// If set true and no files match the given path, email will be sent nevertheless.
    /// </summary>
    /// <example>true</example>
    [UIHint(nameof(AttachmentType), "", AttachmentType.FileAttachment)]
    public bool SendIfNoAttachmentsFound { get; set; }
}
