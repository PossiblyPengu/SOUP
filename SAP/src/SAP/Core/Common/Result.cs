namespace SAP.Core.Common;

public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string Error { get; }
    public string ErrorMessage => Error; // Alias for compatibility
    public bool IsFailure => !IsSuccess;

    protected Result(bool isSuccess, T? value, string error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Success(T value) => new(true, value, string.Empty);
    public static Result<T> Failure(string error) => new(false, default, error);
}

public class Result
{
    public bool IsSuccess { get; }
    public string Error { get; }
    public string ErrorMessage => Error; // Alias for compatibility
    public bool IsFailure => !IsSuccess;

    protected Result(bool isSuccess, string error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, string.Empty);
    public static Result Failure(string error) => new(false, error);
}
