# Changelog

## [3.0.0] - 2026-11-25
### Changed
- **[Breaking Change]** Attachments are now configured in the message `Input` instead of a separate task parameter.
- **[Breaking Change]** Moved SMTP server and authentication parameters from `Options` to the new `Connection` tab.
- Added a Success indicator and error details to the result, with options to return failures or customize thrown error messages.
- Updated the Task to .NET 8.
- Updated the email library to address a known security vulnerability.

## [2.1.0] - 2025-12-08
### Fixed
- Fix missing metadata about repository url and changelog.

## [2.0.0] - 2024-09-30
### Added
- [Breaking] Added parameter AcceptAllCerts which allows bypassing SSL/TLS certificate validation for SMTP servers.
	- Default value: false
    - Warning: Enabling this option poses security risks. Only use when connecting to trusted SMTP servers with self-signed certificates.
    - Usage: Enable only in controlled environments where certificate validation issues cannot be resolved through proper certificate installation.
- [Breaking] Added parameter ServerCertificationThumbprint which enables check for the certification thumbprint.
    - Default value: ""

## [1.2.1] - 2024-01-02
### Fixed
- Fixed issue with connecting to SMTP servers which do not support authentication.

## [1.2.0] - 2023-12-21
### Changed
- Changed Task to use MailKit library instead of deprecated System.Net.Mail.
- Changed the Task to create a temp file from the AttachmentFromString. The temp file will be removed afterwards.

### Added
- Added OAuth2 support.
- New parameters:
	- SecureSocket
	- UseOAuth2
	- Token

### Removed
- Removed UseWindowsAuthentication because it's not supported with the new library.

## [1.1.0] - 2023-11-29
### Fixed
- [Breaking] Fixed issue with the attachments can't be given as expression by adding AttachmentOptions class.

## [1.0.3] - 2023-11-03
### Added
- Fix missing XML comments.

## [1.0.2] - 2023-11-03
### Added
- Fix bug #20 - Missing Frends tasks metadata file

## [1.0.1] - 2023-05-15
### Fixed
- Fix bug #14 - NullReferenceException when field with 'Expression' type is empty

## [1.0.0] - 2022-02-09
### Added
- Initial implementation
