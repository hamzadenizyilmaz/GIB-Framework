namespace GIBFramework.Base;

public sealed class NotFoundException(string message) : Exception(message)
{
    public NotFoundException() : this("Kayıt bulunamadı.") { }

    public NotFoundException(string message, Exception innerException) : this(message) => _ = innerException;
}

public sealed class ConflictException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class ForbiddenOperationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class ValidationFailedException(string code, string message, IReadOnlyList<string> details) : Exception(message)
{
    public string Code { get; } = code;

    public IReadOnlyList<string> Details { get; } = details;
}

public sealed class ProviderUnavailableException(string message) : Exception(message);
