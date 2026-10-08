namespace GIBFramework.Base;

public class DomainException : Exception
{
    public DomainException() : this("DOMAIN", "Domain kuralı ihlal edildi.") { }

    public DomainException(string message) : this("DOMAIN", message) { }

    public DomainException(string message, Exception innerException) : base(message, innerException) => Code = "DOMAIN";

    public DomainException(string code, string message) : base(message) => Code = code;

    public string Code { get; }
}
