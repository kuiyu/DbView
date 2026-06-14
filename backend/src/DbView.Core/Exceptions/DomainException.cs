
namespace DbView.Core.Exceptions
{
    /// <summary>
    /// 领域异常基类
    /// </summary>
    public class DomainException : Exception
    {
        public string ErrorCode { get; }

        public DomainException(string message, string errorCode = "DOMAIN_ERROR")
            : base(message)
        {
            ErrorCode = errorCode;
        }

        public DomainException(string message, Exception innerException, string errorCode = "DOMAIN_ERROR")
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }
    }


    /// <summary>
    /// 无效状态转换异常
    /// </summary>
    public class InvalidStateTransitionException : DomainException
    {
        public InvalidStateTransitionException(string message)
            : base(message, "INVALID_STATE_TRANSITION")
        {
        }
    }

    /// <summary>
    /// 聚合根已被删除异常
    /// </summary>
    public class AggregateDeletedException : DomainException
    {
        public AggregateDeletedException(string aggregateName)
            : base($"{aggregateName} 已被删除", "AGGREGATE_DELETED")
        {
        }
    }

    /// <summary>
    /// 并发冲突异常
    /// </summary>
    public class ConcurrencyConflictException : DomainException
    {
        public long ExpectedVersion { get; }
        public long ActualVersion { get; }

        public ConcurrencyConflictException(long expectedVersion, long actualVersion, string aggregateName)
            : base($"{aggregateName} 版本冲突: 期望版本 {expectedVersion}, 实际版本 {actualVersion}", "CONCURRENCY_CONFLICT")
        {
            ExpectedVersion
= expectedVersion;
            ActualVersion
= actualVersion;
        }
    }
}


