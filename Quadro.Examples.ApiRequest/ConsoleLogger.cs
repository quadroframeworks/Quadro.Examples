namespace QuadroApiExample;

/// <summary>
/// Small console logger with consistent timestamps, log levels, and colors.
/// Sensitive OAuth values such as access tokens, authorization codes, client secrets,
/// and PKCE verifiers should never be written to the log.
/// </summary>
public static class ConsoleLogger
{
    private static readonly object Sync = new();

    public static void Info(string message) =>
        Write("INFO", message, ConsoleColor.White);

    public static void Success(string message) =>
        Write("OK", message, ConsoleColor.Green);

    public static void Warning(string message) =>
        Write("WARN", message, ConsoleColor.Yellow);

    public static void Error(string message) =>
        Write("ERROR", message, ConsoleColor.Red, useErrorStream: true);

    public static void Exception(Exception exception, string? context = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var message = string.IsNullOrWhiteSpace(context)
            ? exception.ToString()
            : $"{context}{Environment.NewLine}{exception}";

        Write("EXCEPTION", message, ConsoleColor.Red, useErrorStream: true);
    }

    private static void Write(
        string level,
        string message,
        ConsoleColor color,
        bool useErrorStream = false)
    {
        lock (Sync)
        {
            var previousColor = Console.ForegroundColor;

            try
            {
                Console.ForegroundColor = color;
                var writer = useErrorStream ? Console.Error : Console.Out;
                writer.WriteLine($"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] [{level}] {message}");
            }
            finally
            {
                Console.ForegroundColor = previousColor;
            }
        }
    }
}
