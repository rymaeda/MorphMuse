using System;
using System.Text;

public class ProjectLogger
{
    private readonly StringBuilder _logBuilder = new StringBuilder();

    public void Log(string message)
    {
        _logBuilder.AppendLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
    }

    public void LogError(string message)
    {
        _logBuilder.AppendLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [ERROR] - {message}");
    }

    public void LogInfo(string message)
    {
        _logBuilder.AppendLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [INFO] - {message}");
    }

    public string GetLog()
    {
        return _logBuilder.ToString();
    }

    public void Clear()
    {
        _logBuilder.Clear();
    }
}