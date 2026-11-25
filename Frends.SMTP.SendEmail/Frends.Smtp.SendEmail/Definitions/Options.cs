using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Frends.SMTP.SendEmail.Definitions;

/// <summary>
/// SMTP task options.
/// </summary>
public class Options
{
    /// <summary>
    /// Whether to throw an error on failure.
    /// </summary>
    /// <example>true</example>
    [DefaultValue(true)]
    public bool ThrowErrorOnFailure { get; set; } = true;

    /// <summary>
    /// Overrides the error message on failure.
    /// </summary>
    /// <example>Unable to send email</example>
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string ErrorMessageOnFailure { get; set; } = string.Empty;
}
