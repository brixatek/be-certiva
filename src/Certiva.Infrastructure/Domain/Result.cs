namespace Certiva.Infrastructure.Domain;

/// <summary>
/// Discriminated union representing the outcome of an operation that returns a value.
/// Use the static factory methods to construct instances.
/// </summary>
/// <typeparam name="T">The type of the success value.</typeparam>
public sealed record Result<T>
{
    /// <summary>Whether the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>Whether the operation failed. Inverse of <see cref="IsSuccess"/>.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>The success value. Only valid when <see cref="IsSuccess"/> is true.</summary>
    public T? Value { get; }

    /// <summary>The error message. Only populated when <see cref="IsSuccess"/> is false.</summary>
    public string? Error { get; }

    /// <summary>HTTP status code hint (e.g. 400, 404, 409). Only set on failure.</summary>
    public int? StatusCode { get; }

    private Result(bool isSuccess, T? value, string? error, int? statusCode)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
        StatusCode = statusCode;
    }

    /// <summary>Creates a successful result carrying <paramref name="value"/>.</summary>
    public static Result<T> Ok(T value) => new(true, value, null, null);

    /// <summary>Creates a failed result with the given error message and HTTP status code hint.</summary>
    public static Result<T> Fail(string error, int statusCode) => new(false, default, error, statusCode);

    /// <summary>404 Not Found failure.</summary>
    public static Result<T> NotFound(string error) => Fail(error, 404);

    /// <summary>409 Conflict failure.</summary>
    public static Result<T> Conflict(string error) => Fail(error, 409);

    /// <summary>403 Forbidden failure.</summary>
    public static Result<T> Forbidden(string error) => Fail(error, 403);

    /// <summary>400 Bad Request failure.</summary>
    public static Result<T> BadRequest(string error) => Fail(error, 400);

    /// <summary>422 Unprocessable Entity failure.</summary>
    public static Result<T> UnprocessableEntity(string error) => Fail(error, 422);

    /// <summary>429 Too Many Requests failure.</summary>
    public static Result<T> TooManyRequests(string error) => Fail(error, 429);
}

/// <summary>
/// Discriminated union representing the outcome of a void operation (no return value).
/// Use the static factory methods to construct instances.
/// </summary>
public sealed record Result
{
    /// <summary>Whether the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>Whether the operation failed. Inverse of <see cref="IsSuccess"/>.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>The error message. Only populated when <see cref="IsSuccess"/> is false.</summary>
    public string? Error { get; }

    /// <summary>HTTP status code hint (e.g. 400, 404, 409). Only set on failure.</summary>
    public int? StatusCode { get; }

    private Result(bool isSuccess, string? error, int? statusCode)
    {
        IsSuccess = isSuccess;
        Error = error;
        StatusCode = statusCode;
    }

    /// <summary>Creates a successful void result.</summary>
    public static Result Ok() => new(true, null, null);

    /// <summary>Creates a failed result with the given error message and HTTP status code hint.</summary>
    public static Result Fail(string error, int statusCode) => new(false, error, statusCode);

    /// <summary>404 Not Found failure.</summary>
    public static Result NotFound(string error) => Fail(error, 404);

    /// <summary>409 Conflict failure.</summary>
    public static Result Conflict(string error) => Fail(error, 409);

    /// <summary>403 Forbidden failure.</summary>
    public static Result Forbidden(string error) => Fail(error, 403);

    /// <summary>400 Bad Request failure.</summary>
    public static Result BadRequest(string error) => Fail(error, 400);

    /// <summary>422 Unprocessable Entity failure.</summary>
    public static Result UnprocessableEntity(string error) => Fail(error, 422);

    /// <summary>429 Too Many Requests failure.</summary>
    public static Result TooManyRequests(string error) => Fail(error, 429);
}
