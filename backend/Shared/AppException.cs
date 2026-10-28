namespace CaudalesPlatform.Api.Shared;

/// <summary>Base class for domain exceptions; custom exceptions must inherit from it.</summary>
public abstract class AppException(string message) : Exception(message)
{
    public abstract int StatusCode { get; }
}

public class NotFoundError(string message) : AppException(message)
{
    public override int StatusCode => StatusCodes.Status404NotFound;
}

public class ValidationError(string message) : AppException(message)
{
    public override int StatusCode => StatusCodes.Status400BadRequest;
}

public class ConflictError(string message) : AppException(message)
{
    public override int StatusCode => StatusCodes.Status409Conflict;
}
