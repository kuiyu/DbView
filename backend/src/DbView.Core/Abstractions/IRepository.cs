using System.Linq.Expressions;
using DbView.Core.Models;
using DbView.Core.Specifications;

namespace DbView.Core.Abstractions
{
/// <summary>
/// 泛型仓库接口 - 定义基本的CRUD操作
/// </summary>
public interface IRepository<TDomain, TId>
    where TDomain : class, IEntity<TId>
    where TId : notnull
{
    #region 查询方法

    /// <summary>
    /// 根据ID获取实体
    /// </summary>
    Task<TDomain?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据ID列表获取实体列表
    /// </summary>
    Task<IReadOnlyList<TDomain>> GetByIdsAsync(IEnumerable<TId> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有实体
    /// </summary>
    Task<IReadOnlyList<TDomain>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TDomain>> GetAllAsync( Expression<Func<TDomain, bool>> expression,CancellationToken cancellationToken = default);
    /// <summary>
    /// 获取分页数据（无条件）
    /// </summary>
    Task<IPagedList<TDomain>> PageListAsync(
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取分页数据（带条件查询）- 使用领域类型表达式
    /// </summary>
    Task<IPagedList<TDomain>> PageListAsync(
        int pageIndex,
        int pageSize,
        Expression<Func<TDomain, bool>> expression,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查实体是否存在
    /// </summary>
    Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取实体数量
    /// </summary>
    Task<int> CountAsync(CancellationToken cancellationToken = default);

    #endregion

    #region 添加/更新/删除

    /// <summary>
    /// 添加实体
    /// </summary>
    Task AddAsync(TDomain entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量添加实体
    /// </summary>
    Task AddRangeAsync(IEnumerable<TDomain> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新实体
    /// </summary>
    Task UpdateAsync(TDomain entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量更新实体
    /// </summary>
    Task UpdateRangeAsync(IEnumerable<TDomain> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除实体
    /// </summary>
    Task DeleteAsync(TDomain entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据ID删除实体
    /// </summary>
    Task DeleteByIdAsync(TId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量删除实体
    /// </summary>
    Task DeleteRangeAsync(List<TId> ids, CancellationToken cancellationToken = default);

    #endregion
}
}
