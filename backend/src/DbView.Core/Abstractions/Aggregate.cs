using DbView.Core.Events;

namespace DbView.Core.Abstractions
{
    /// <summary>
    /// 聚合根抽象基类
    /// 聚合根是聚合的根实体，负责维护聚合内的一致性边界
    /// </summary>
    public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot
        where TId : notnull
    {
        private readonly List<IDomainEvent> _domainEvents = new();
        // 覆盖为允许默认值（用于数据库自增ID）
        protected override bool IsDefaultValueAllowed => true;
        /// <summary>
        /// 领域事件列表
        /// </summary>
        public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        protected AggregateRoot(TId id) : base(id)
        {
        }

        /// <summary>
        /// 添加领域事件
        /// </summary>
        protected void AddDomainEvent(IDomainEvent domainEvent)
        {
            _domainEvents.Add(domainEvent);
        }

        /// <summary>
        /// 清除领域事件（通常在事件发布后调用）
        /// </summary>
        public void ClearDomainEvents()
        {
            _domainEvents.Clear();
        }

        /// <summary>
        /// 获取未发布的领域事件
        /// </summary>
        public IReadOnlyCollection<IDomainEvent> GetUnpublishedEvents()
        {
            return _domainEvents.Where(e => !e.IsPublished).ToList().AsReadOnly();
        }

        /// <summary>
        /// 标记事件为已发布
        /// </summary>
        public void MarkEventsAsPublished()
        {
            foreach (var domainEvent in _domainEvents)
            {
                domainEvent.MarkAsPublished();
            }
        }
        public override bool Equals(object? obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override bool IsTransient()
        {
            return base.IsTransient();
        }

        public override string ToString()
        {
            return base.ToString();
        }
    }

    /// <summary>
    /// 聚合根标识接口
    /// </summary>
    public interface IAggregateRoot { }
}

