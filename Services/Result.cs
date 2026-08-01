namespace KaijensonIventory_SalesMotorShopWeb.Services
{
    public class Result
    {
        public bool Succeeded { get; }

        public List<ResultError> Errors { get; } = new();

        protected Result(bool succeeded)
        {
            Succeeded = succeeded;
        }

        protected Result(bool succeeded, IEnumerable<ResultError>? errors) : this(succeeded)
        {
            if (errors != null)
                Errors.AddRange(errors);
        }

        public static Result Success() => new(true);

        public static Result Failure(string? key, string message) =>
            new(false, new[] { new ResultError(key, message) });

        public static Result Failure(IEnumerable<ResultError> errors) => new(false, errors);
    }

    public class Result<T> : Result
    {
        public T? Value { get; }

        private Result(bool succeeded, T? value, IEnumerable<ResultError>? errors) : base(succeeded, errors)
        {
            Value = value;
        }

        public static Result<T> Success(T value) => new(true, value, null);

        public static new Result<T> Failure(string? key, string message) =>
            new(false, default, new[] { new ResultError(key, message) });

        public static new Result<T> Failure(IEnumerable<ResultError> errors) => new(false, default, errors);
    }

    public class ResultError
    {
        public string? Key { get; }

        public string Message { get; }

        public ResultError(string? key, string message)
        {
            Key = key;
            Message = message;
        }
    }
}
