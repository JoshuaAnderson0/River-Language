namespace Safety;

public class Result
{
    public string? Message { get; }
    public bool IsOk { get; }

    private Result()
    {
        Message = null;
        IsOk = true;
    }

    private Result(string message)
    {
        Message = message;
        IsOk = false;
    }

    public static Result Ok() => new();

    public static Result Error(string message) => new(message);

    public Result<TOut> Map<TOut>(Func<TOut> mapper) =>
        IsOk ? Result<TOut>.Ok(mapper()) : Result<TOut>.Error(Message!);
}

public class Result<TValue>
{
    public TValue? Value { get; }
    public string? Message { get; }
    public bool IsOk { get; }

    private Result(TValue value)
    {
        Value = value;
        Message = null;
        IsOk = true;
    }

    private Result(string message)
    {
        Value = default;
        Message = message;
        IsOk = false;
    }

    public static Result<TValue> Ok(TValue value) => new(value);

    public static Result<TValue> Error(string message) => new(message);

    public TValue Unwrap() =>
        IsOk ? Value! : throw new InvalidOperationException($"Cannot unwrap failed result: {Message}");

    public TValue Or(TValue defaultValue) =>
        IsOk ? Value! : defaultValue;

    public Result<TOut> Map<TOut>(Func<TValue, TOut> mapper) =>
        IsOk ? Result<TOut>.Ok(mapper(Value!)) : Result<TOut>.Error(Message!);

    public Result<TOut> FlatMap<TOut>(Func<TValue, Result<TOut>> mapper) =>
        IsOk ? mapper(Value!) : Result<TOut>.Error(Message!);

    public Result FlatMap(Func<TValue, Result> mapper) =>
        IsOk ? mapper(Value!) : Result.Error(Message!);
}