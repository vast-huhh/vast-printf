namespace VastPrint.App.Models;

public sealed record PrintExecutionResult(bool Success, int? PageCount, string Message)
{
    public static PrintExecutionResult Failed(string message)
    {
        return new PrintExecutionResult(false, null, message);
    }

    public static PrintExecutionResult Completed(int pageCount, string? message = null)
    {
        return new PrintExecutionResult(true, pageCount, message ?? "打印完成");
    }
}
