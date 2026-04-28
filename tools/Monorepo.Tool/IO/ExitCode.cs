namespace Monorepo.Tool.IO;

/// <summary>
/// Well-known exit codes returned by the CLI. Kept stable so CI scripts can branch on them.
/// </summary>
public enum ExitCode
{
    Success = 0,
    GeneralError = 1,
    InvalidInput = 2,
    ConfigNotFound = 3,
    ConfigCorrupt = 4,
    Drift = 5,
}
