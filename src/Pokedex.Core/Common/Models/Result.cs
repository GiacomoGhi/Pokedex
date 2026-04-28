using System.Diagnostics.CodeAnalysis;

namespace Pokedex.Core.Common.Models;

public enum ResultStatus
{
    Success,
    Error,
    InvalidArgument,
    NotFound,
}

public readonly struct Result
{
    /// <summary>
    /// Indicates whether the result has a non-success status code.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Message))]
    public bool HasNonSuccessStatusCode => StatusCode != ResultStatus.Success;

    /// <summary>
    /// Status code that indicates the result of the operation.
    /// </summary>
    public ResultStatus StatusCode { get; init; }

    /// <summary>
    /// Message.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Will return null. This will allow for a non-generic result.
    /// </summary>
    public object? Data => null;

    /// <summary>
    /// Create a new result with a success status code.
    /// </summary>
    public static Result Success()
        => new()
        {
            StatusCode = ResultStatus.Success
        };

    /// <summary>
    /// Create a new result with a success status code and the provided data.
    /// </summary>
    public static Result<T> Success<T>(T data)
        where T : class
        => new()
        {
            Data = data,
            StatusCode = ResultStatus.Success
        };

    /// <summary>
    /// Create a new result with error status code and the provided error message.
    /// </summary>
    public static Result Error(string msg)
        => new()
        {
            StatusCode = ResultStatus.Error,
            Message = msg
        };

    /// <summary>
    /// Forwards the failure status of an upstream <see cref="Result{TInput}"/> onto a
    /// non-generic <see cref="Result"/>. Use to propagate a typed error to a caller that
    /// does not care about the original payload type.
    /// </summary>
    public static Result Propagate<TInput>(Result<TInput> source) where TInput : class
        => new()
        {
            StatusCode = source.StatusCode,
            Message = source.Message
        };

    /// <summary>
    /// Create a new result with invalid argument status code and the provided error message.
    /// </summary>
    public static Result InvalidArgument(string msg)
        => new()
        {
            StatusCode = ResultStatus.InvalidArgument,
            Message = $"Invalid argument: {msg}"
        };

    /// <summary>
    /// Create a new result with not found status code and the provided error message.
    /// </summary>
    public static Result NotFound(string msg)
        => new()
        {
            StatusCode = ResultStatus.NotFound,
            Message = $"Item not found: {msg}"
        };
}

public readonly struct Result<T>
    where T : class
{
    /// <inheritdoc cref="Result.HasNonSuccessStatusCode"/>
    [MemberNotNullWhen(true, nameof(Message))]
    public readonly bool HasNonSuccessStatusCode => StatusCode != ResultStatus.Success;

    /// <inheritdoc cref="Result.StatusCode"/>
    public ResultStatus StatusCode { get; init; }

    /// <inheritdoc cref="Result.Message"/>
    public string? Message { get; init; }

    /// <inheritdoc/>
    public T? Data { get; init; }

    /// <summary>
    /// Implicitly convert a Result to a Result&lt;T&gt;.
    /// </summary>
    public static implicit operator Result<T>(Result result)
    {
        return new Result<T>
        {
            StatusCode = result.StatusCode,
            Message = result.Message,
            Data = null
        };
    }

    /// <summary>
    /// Create a new result with a success status code and the provided data.
    /// </summary>
    public static Result<T> Success(T data)
        => new()
        {
            StatusCode = ResultStatus.Success,
            Data = data,
        };

    /// <summary>
    /// Create a new result with error status code and the provided error message.
    /// </summary>
    public static Result<T> Error(string msg)
        => new()
        {
            StatusCode = ResultStatus.Error,
            Message = msg,
        };

    /// <summary>
    /// Create a new result with invalid arguments status code and the provided error message.
    /// </summary>
    public static Result<T> InvalidArgument(string msg)
        => new()
        {
            StatusCode = ResultStatus.InvalidArgument,
            Message = $"Invalid argument: {msg}",
        };

    /// <summary>
    /// Create a new result with not found status code and the provided error message.
    /// </summary>
    public static Result<T> NotFound(string msg)
        => new()
        {
            StatusCode = ResultStatus.NotFound,
            Message = $"Item not found: {msg}",
        };
}
