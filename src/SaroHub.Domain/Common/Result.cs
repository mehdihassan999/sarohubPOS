// src/SaroHub.Domain/Common/Result.cs
namespace SaroHub.Domain.Common;

public class Result
{
    public bool IsSuccess { get; protected init; }
    public string Error { get; protected init; } = "";

    public static Result Ok() => new() { IsSuccess = true };
    public static Result Fail(string error) => new() { IsSuccess = false, Error = error };
}

public sealed class Result<T> : Result
{
    public T? Value { get; private init; }

    public static Result<T> Ok(T value) => new() { IsSuccess = true, Value = value };
    public static new Result<T> Fail(string error) => new() { IsSuccess = false, Error = error };
}