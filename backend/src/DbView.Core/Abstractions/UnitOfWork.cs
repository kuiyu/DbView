using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbView.Core.Abstractions
{
    /// <summary>
    /// 工作单元接口 - 管理事务和仓储
    /// </summary>
    public interface IUnitOfWork : IDisposable
    {
        #region 事务管理

        /// <summary>
        /// 开始事务
        /// </summary>
        Task BeginTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 提交事务
        /// </summary>
        Task CommitTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 回滚事务
        /// </summary>
        Task RollbackTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 保存变更
        /// </summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        #endregion

        #region 领域事件

        /// <summary>
        /// 发布领域事件
        /// </summary>
        Task DispatchDomainEventsAsync(CancellationToken cancellationToken = default);

        #endregion

        #region 仓储获取

        /// <summary>
        /// 获取指定类型的仓库
        /// </summary>
        IRepository<TEntity, TId> GetRepository<TEntity, TId>()
            where TEntity : class, IEntity<TId>
            where TId : notnull;

  

        #endregion
    }
}

