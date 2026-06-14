namespace DbView.Core.Models
{

    /// <summary>
    /// 操作结果
    /// </summary>
    public class Result
    {
        public bool IsSuccess { get; }
        public string? Error { get; }
        public bool IsFailure => !IsSuccess;

        protected Result(bool isSuccess, string? error)
        {
            if (isSuccess && !string.IsNullOrEmpty(error))
                throw new InvalidOperationException("成功的结果不能有错误信息");

            if (!isSuccess && string.IsNullOrEmpty(error))
                throw new InvalidOperationException("失败的结果必须有错误信息");

            IsSuccess
= isSuccess;
            Error
= error;
        }

        public static Result Success() => new(true, null);
        public static Result Failure(string error) => new(false, error);
        public static Result<T> Success<T>(T value) => new(value, true, null);
        public static Result<T> Failure<T>(string error) => new(default!, false, error);
    }

    public class Result<T> : Result
    {
        private readonly T? _value;

        public T Value =>
 IsSuccess
            ? _value!
            : throw new InvalidOperationException("失败的结果没有值");

        protected internal Result(T? value, bool isSuccess, string? error)
            : base(isSuccess, error)
        {
            _value
= value;
        }
    }
}



