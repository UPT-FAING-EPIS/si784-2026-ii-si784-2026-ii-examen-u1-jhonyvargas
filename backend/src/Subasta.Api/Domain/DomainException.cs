namespace Subasta.Api.Domain;

/// <summary>Error de regla de negocio que debe devolverse al cliente como 400/409.</summary>
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}
