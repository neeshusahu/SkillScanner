namespace SkillScanner.ExceptionHandler;
public class ConsoleExceptionHandler : IExceptionHandler
{
    public void Handle(Exception exception)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error: {exception.Message}");
        Console.ResetColor();

        // Future:
        // _logger.LogError(exception, "Unhandled exception");
    }
}