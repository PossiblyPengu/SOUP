namespace BusinessToolsSuite.Core.Common;

/// <summary>
/// Result pattern for operation outcomes
/// </summary>
public record Result
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public string[]? Errors { get; init; }

    protected Result(bool isSuccess, string? errorMessage, string[]? errors = null)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        Errors = errors;
    }

    public static Result Success() => new(true, null);
    public static Result Failure(string error) => new(false, error);
    public static Result Failure(string[] errors) => new(false, "Multiple errors occurred", errors);
}

/// <summary>
/// Generic result pattern with value
/// </summary>
public record Result<T> : Result
{
    public T? Value { get; init; }

    private Result(bool isSuccess, T? value, string? errorMessage, string[]? errors = null)
        : base(isSuccess, errorMessage, errors)
    {
        Value = value;
    }

    public static Result<T> Success(T value) => new(true, value, null);
    public new static Result<T> Failure(string error) => new(false, default, error);
    public new static Result<T> Failure(string[] errors) => new(false, default, "Multiple errors occurred", errors);
}
