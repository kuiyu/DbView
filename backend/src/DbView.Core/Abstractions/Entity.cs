using DbView.Core.Events;
using DbView.Core.Exceptions;

namespace DbView.Core.Abstractions
{
    /// <summary>
    /// 实体抽象基类
    /// </summary>
    public abstract class Entity<TId> : IEquatable<Entity<TId>>, IEntity<TId>
            where TId : notnull
    {
        // 私有字段，防止子类直接修改
        private TId _id;
        private int? _requestedHashCode;
        protected virtual bool IsDefaultValueAllowed => false; // 默认不允许默认值
        /// <summary>
        /// 实体ID - 只读，只能通过构造函数设置
        /// </summary>
        public TId Id => _id;

        /// <summary>
        /// 用于EF Core的私有构造函数
        /// </summary>
        protected Entity() { }

        /// <summary>
        /// 构造函数 - 设置实体ID
        /// </summary>
        /// <param name="id">实体ID</param>
        /// <exception cref="ArgumentNullException">id为null时抛出</exception>
        /// <exception cref="ArgumentException">id为默认值时抛出（可选）</exception>
        protected Entity(TId id)
        {
            _id = id ?? throw new ArgumentNullException(nameof(id));

            // 可选：检查ID是否为默认值（根据业务规则决定）
            if (!IsDefaultValueAllowed && IsDefaultId(id))
                throw new ArgumentException($"实体ID不能为默认值: {typeof(TId).Name}", nameof(id));
        }

        #region 相等性比较

        /// <summary>
        /// 判断两个实体是否相等（基于ID和类型）
        /// </summary>
        public override bool Equals(object? obj)
        {
            if (obj is null)
                return false;

            if (ReferenceEquals(this, obj))
                return true;

            if (obj is not Entity<TId> other)
                return false;

            // 不同类型视为不同实体
            if (GetType() != other.GetType())
                return false;

            // 比较ID
            return Id.Equals(other.Id);
        }

        /// <summary>
        /// 强类型相等性比较
        /// </summary>
        public bool Equals(Entity<TId>? other)
        {
            return Equals((object?)other);
        }

        /// <summary>
        /// 获取哈希码
        /// </summary>
        public override int GetHashCode()
        {
            if (!_requestedHashCode.HasValue)
            {
                // 使用类型和ID的组合，但确保ID不为空
                _requestedHashCode = HashCode.Combine(GetType(), Id);
            }

            return _requestedHashCode.Value;
        }

        #endregion

        #region 操作符重载

        public static bool operator ==(Entity<TId>? left, Entity<TId>? right)
        {
            if (left is null && right is null)
                return true;

            if (left is null || right is null)
                return false;

            return left.Equals(right);
        }

        public static bool operator !=(Entity<TId>? left, Entity<TId>? right)
        {
            return !(left == right);
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 检查ID是否为默认值
        /// </summary>
        private static bool IsDefaultId(TId id)
        {
            if (id is Guid guid)
                return guid == Guid.Empty;

            if (id is int intId)
                return intId == 0;

            if (id is long longId)
                return longId == 0;

            if (id is string str)
                return string.IsNullOrWhiteSpace(str);

            return id.Equals(default(TId));
        }

        /// <summary>
        /// 判断实体是否为瞬态（尚未持久化）
        /// </summary>
        public virtual bool IsTransient()
        {
            return Id.Equals(default(TId));
        }

        #endregion

        #region 领域事件支持（可选）

        private readonly List<IDomainEvent> _domainEvents = new();

        /// <summary>
        /// 领域事件集合
        /// </summary>
        public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        /// <summary>
        /// 添加领域事件
        /// </summary>
        protected void AddDomainEvent(IDomainEvent domainEvent)
        {
            _domainEvents.Add(domainEvent);
        }

        /// <summary>
        /// 移除领域事件
        /// </summary>
        protected void RemoveDomainEvent(IDomainEvent domainEvent)
        {
            _domainEvents.Remove(domainEvent);
        }

        /// <summary>
        /// 清除所有领域事件
        /// </summary>
        public void ClearDomainEvents()
        {
            _domainEvents.Clear();
        }

        #endregion

        #region 调试支持

        /// <summary>
        /// 返回实体的字符串表示
        /// </summary>
        public override string ToString()
        {
            return $"{GetType().Name} [Id={Id}]";
        }

        #endregion
    }

    /// <summary>
    /// 实体标识接口
    /// </summary>
    public interface IEntity<out TId>
        where TId : notnull
    {
        /// <summary>
        /// 实体唯一标识
        /// </summary>
        TId Id { get; }
    }

    /// <summary>
    /// 非泛型实体标识接口（用于仓储等场景）
    /// </summary>
    public interface IEntity
    {
        /// <summary>
        /// 获取实体ID的对象形式
        /// </summary>
        object GetId();

        /// <summary>
        /// 获取实体类型
        /// </summary>
        Type GetEntityType();
    }

    /// <summary>
    /// 支持审计的实体基类
    /// </summary>
    public abstract class AuditableEntity<TId> : Entity<TId>
        where TId : notnull, IEquatable<TId>
    {
        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; private set; }

        /// <summary>
        /// 创建者
        /// </summary>
        public string CreatedBy { get; private set; }

        /// <summary>
        /// 最后修改时间
        /// </summary>
        public DateTime? LastModifiedAt { get; private set; }

        /// <summary>
        /// 最后修改者
        /// </summary>
        public string? LastModifiedBy { get; private set; }

        protected AuditableEntity(TId id, string createdBy) : base(id)
        {
            CreatedAt = DateTime.UtcNow;
            CreatedBy = createdBy ?? throw new ArgumentNullException(nameof(createdBy));
        }

        protected AuditableEntity() { } // EF Core

        /// <summary>
        /// 更新审计信息
        /// </summary>
        protected void UpdateAudit(string modifiedBy)
        {
            LastModifiedAt = DateTime.UtcNow;
            LastModifiedBy = modifiedBy ?? throw new ArgumentNullException(nameof(modifiedBy));
        }
    }

    /// <summary>
    /// 支持软删除的实体基类
    /// </summary>
    public abstract class SoftDeletableEntity<TId> : AuditableEntity<TId>
        where TId : notnull, IEquatable<TId>
    {
        /// <summary>
        /// 是否已删除
        /// </summary>
        public bool IsDeleted { get; private set; }

        /// <summary>
        /// 删除时间
        /// </summary>
        public DateTime? DeletedAt { get; private set; }

        /// <summary>
        /// 删除者
        /// </summary>
        public string? DeletedBy { get; private set; }

        protected SoftDeletableEntity(TId id, string createdBy) : base(id, createdBy)
        {
            IsDeleted = false;
        }

        protected SoftDeletableEntity() { } // EF Core

        /// <summary>
        /// 软删除
        /// </summary>
        public virtual void Delete(string deletedBy)
        {
            if (IsDeleted)
                throw new DomainException("实体已被删除");

            IsDeleted = true;
            DeletedAt = DateTime.UtcNow;
            DeletedBy = deletedBy ?? throw new ArgumentNullException(nameof(deletedBy));

            //AddDomainEvent(new EntityDeletedEvent<TId>(Id, deletedBy, DeletedAt.Value));
            UpdateAudit(deletedBy);
        }

        /// <summary>
        /// 恢复删除
        /// </summary>
        public virtual void Restore(string restoredBy)
        {
            if (!IsDeleted)
                throw new DomainException("实体未被删除");

            IsDeleted = false;
            DeletedAt = null;
            DeletedBy = null;

            //AddDomainEvent(new EntityRestoredEvent<TId>(Id, restoredBy));
            UpdateAudit(restoredBy);
        }
    }
}


