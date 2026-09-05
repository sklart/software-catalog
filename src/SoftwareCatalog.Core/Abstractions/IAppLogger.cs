namespace SoftwareCatalog.Core.Abstractions;

public interface IAppLogger
{
    void Information(string operation, string message);
    void Error(string operation, string message);
}
