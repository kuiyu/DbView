
namespace DbView.Core.Events
{
    /// <summary>
    /// 领域事件接口
    /// </summary>
    public interface IDomainEvent
    {
        /// <summary>
        /// 事件ID
        /// </summary>
        Guid EventId { get; }

        /// <summary>
        /// 事件发生时间
        /// </summary>
        DateTime OccurredOn { get; }

        /// <summary>
        /// 事件类型名称
        /// </summary>
        string EventType { get; }

        /// <summary>
        /// 聚合根ID
        /// </summary>
        string AggregateId { get; }

        /// <summary>
        /// 聚合根类型
        /// </summary>
        string AggregateType { get; }

        /// <summary>
        /// 聚合根版本
        /// </summary>
        long AggregateVersion { get; }

        /// <summary>
        /// 是否已发布
        /// </summary>
        bool IsPublished { get; set; }

        /// <summary>
        /// 标记为已发布
        /// </summary>
        void MarkAsPublished();
    }

    /// <summary>
    /// 领域事件处理器接口
    /// </summary>
    public interface IDomainEventHandler<TEvent>
        where TEvent : IDomainEvent
    {
        Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 领域事件发布器接口
    /// </summary>
    public interface IDomainEventPublisher
    {
        Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
        Task PublishAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
    }
    
     /// <summary>
     /// 领域事件基类
     /// </summary>
     public abstract class DomainEvent : IDomainEvent
     {
         public Guid EventId { get; }
         public DateTime OccurredOn { get; }
         public string EventType { get; }
         public string AggregateId { get; }
         public string AggregateType { get; }
         public long AggregateVersion { get; }
         public bool IsPublished { get; set; }

         protected DomainEvent(string aggregateId, string aggregateType, long aggregateVersion = 0)
         {
             EventId = Guid.NewGuid();
             OccurredOn = DateTime.UtcNow;
             EventType = GetType().Name;
             AggregateId = aggregateId ?? throw new ArgumentNullException(nameof(aggregateId));
             AggregateType = aggregateType ?? throw new ArgumentNullException(nameof(aggregateType));
             AggregateVersion = aggregateVersion;
             IsPublished = false;
         }

         public void MarkAsPublished()
         {
             IsPublished = true;
         }
     }
   
}


