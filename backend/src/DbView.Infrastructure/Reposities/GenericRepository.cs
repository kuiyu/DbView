using DbView.Core.Abstractions;
using DbView.Core.Models;
using Mapster;
using System.Linq.Expressions;

namespace DbView.Infrastructure
{
    public abstract class GenericRepository<TEntity, TDomain, TId> : IRepository<TDomain, TId>
       where TEntity : class
       where TDomain : class, IEntity<TId>
       where TId : notnull
    {
        protected readonly IFreeSql sql;

        protected GenericRepository(IFreeSql freeSql)
        {
            sql = freeSql;
        }

        public virtual async Task<TDomain?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
        {
            var entity = await sql.Select<TEntity>().WhereDynamic(id).ToOneAsync(cancellationToken);
            return entity == null ? null : entity.Adapt<TDomain>();
        }

        public virtual async Task<IReadOnlyList<TDomain>> GetByIdsAsync(
            IEnumerable<TId> ids, CancellationToken cancellationToken = default)
        {
            var entities = await sql.Select<TEntity>().WhereDynamic(ids).ToListAsync(cancellationToken);
            return entities.Adapt<List<TDomain>>();
        }

        public virtual async Task<IReadOnlyList<TDomain>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var entities = await sql.Select<TEntity>().ToListAsync(cancellationToken);
            return entities.Adapt<List<TDomain>>();
        }
        public virtual async Task<IReadOnlyList<TDomain>> GetAllAsync(
            Expression<Func<TDomain, bool>> expression,
            CancellationToken cancellationToken = default)
        {
            // 将 TDomain 的表达式转换为 TEntity 的表达式
            var entityExpression = ConvertToEntityExpression(expression);

            var query = sql.Select<TEntity>();

            // 先应用筛选条件
            var filteredQuery = query.Where(entityExpression);

            // 统计总数（先筛选后统计）
            var totalCount = await filteredQuery.CountAsync(cancellationToken);

            // 分页查询
            var entities = await filteredQuery.ToListAsync(cancellationToken);

            var items = entities.Adapt<List<TDomain>>();

            return items;
        }

        public virtual async Task<IPagedList<TDomain>> PageListAsync(
            int pageIndex, int pageSize, CancellationToken cancellationToken = default)
        {
            var query = sql.Select<TEntity>();

            var totalCount = await query.CountAsync(cancellationToken);
            var entities = await query
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var items = entities.Adapt<List<TDomain>>();

            return new PagedList<TDomain>(items, pageIndex, pageSize, (int)totalCount);
        }

        /// <summary>
        /// 带条件查询的分页方法
        /// 注意：这里接收的是 TDomain 的表达式，需要转换为 TEntity 的表达式
        /// </summary>
        public virtual async Task<IPagedList<TDomain>> PageListAsync(
            int pageIndex,
            int pageSize,
            Expression<Func<TDomain, bool>> expression,
            CancellationToken cancellationToken = default)
        {
            // 将 TDomain 的表达式转换为 TEntity 的表达式
            var entityExpression = ConvertToEntityExpression(expression);

            var query = sql.Select<TEntity>();

            // 先应用筛选条件
            var filteredQuery = query.Where(entityExpression);

            // 统计总数（先筛选后统计）
            var totalCount = await filteredQuery.CountAsync(cancellationToken);

            // 分页查询
            var entities = await filteredQuery
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var items = entities.Adapt<List<TDomain>>();

            return new PagedList<TDomain>(items, pageIndex, pageSize, (int)totalCount);
        }
        /// <summary>
        /// 将 TDomain 的表达式转换为 TEntity 的表达式
        /// </summary>
        private Expression<Func<TEntity, bool>> ConvertToEntityExpression(Expression<Func<TDomain, bool>> domainExpression)
        {
            // 获取 TDomain 的参数
            var domainParameter = domainExpression.Parameters[0];

            // 创建 TEntity 的参数
            var entityParameter = Expression.Parameter(typeof(TEntity), domainParameter.Name ?? "entity");

            // 替换表达式中的参数和成员访问
            var visitor = new ExpressionParameterReplacer(domainParameter, entityParameter, typeof(TEntity));
            var convertedBody = visitor.Visit(domainExpression.Body);

            // 创建新的表达式
            return Expression.Lambda<Func<TEntity, bool>>(convertedBody, entityParameter);
        }
 
   
        public virtual async Task<TDomain> AddAsync(TDomain domain, CancellationToken cancellationToken = default)
        {
            var entity = domain.Adapt<TEntity>();
            await sql.Insert<TEntity>(entity).ExecuteAffrowsAsync(cancellationToken);

            await DispatchDomainEvents(domain);

            return entity.Adapt<TDomain>();
        }

        public virtual async Task UpdateAsync(TDomain domain, CancellationToken cancellationToken = default)
        {
            var entity = domain.Adapt<TEntity>();
            await sql.Update<TEntity>().SetSource(entity).ExecuteAffrowsAsync();

            await DispatchDomainEvents(domain);
        }

        public virtual async Task DeleteAsync(TDomain domain, CancellationToken cancellationToken = default)
        {
            var entity = domain.Adapt<TEntity>();
            if (entity != null)
            {
                await sql.Delete<TEntity>(entity).ExecuteAffrowsAsync();
            }
        }

        protected virtual async Task DispatchDomainEvents(TDomain domain)
        {
            if (domain is AggregateRoot<TId> aggregate)
            {
                var events = aggregate.DomainEvents.ToList();
                aggregate.ClearDomainEvents();

                foreach (var domainEvent in events)
                {
                    // 发布领域事件到消息总线
                }
            }
        }

        public async Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default)
        {
            var info = await GetByIdAsync(id, cancellationToken);
            return info != null;
        }

        public async Task<int> CountAsync(CancellationToken cancellationToken = default)
        {
            var count = await sql.Select<TEntity>().CountAsync(cancellationToken);
            return (int)count;
        }

        public async Task AddRangeAsync(IEnumerable<TDomain> entities, CancellationToken cancellationToken = default)
        {
            var infos = entities.Adapt<List<TEntity>>();
            await sql.Insert<TEntity>(infos).ExecuteAffrowsAsync();
        }

        public async Task UpdateRangeAsync(IEnumerable<TDomain> entities, CancellationToken cancellationToken = default)
        {
            var infos = entities.Adapt<List<TEntity>>();
            await sql.Update<TEntity>().SetSource(infos).ExecuteAffrowsAsync();
        }

        public async Task DeleteByIdAsync(TId id, CancellationToken cancellationToken = default)
        {
            await sql.Delete<TEntity>().WhereDynamic(id).ExecuteAffrowsAsync(cancellationToken);
        }

        public async Task DeleteRangeAsync(List<TId> ids, CancellationToken cancellationToken = default)
        {
            await sql.Delete<TEntity>().WhereDynamic(ids).ExecuteAffrowsAsync(cancellationToken);
        }
    }


    /// <summary>
    /// 表达式参数替换器（作为内部类）
    /// </summary>
    public class ExpressionParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _oldParameter;
        private readonly ParameterExpression _newParameter;
        private readonly Type _targetType;

        public ExpressionParameterReplacer(
            ParameterExpression oldParameter,
            ParameterExpression newParameter,
            Type targetType)
        {
            _oldParameter = oldParameter;
            _newParameter = newParameter;
            _targetType = targetType;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            // 如果遇到旧的参数，替换为新的参数
            return node == _oldParameter ? _newParameter : base.VisitParameter(node);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            // 如果访问的是 TDomain 的属性，尝试映射到 TEntity 的同名属性
            if (node.Expression is ParameterExpression parameter && parameter == _oldParameter)
            {
                // 在 TEntity 中查找同名的属性
                var targetProperty = _targetType.GetProperty(node.Member.Name);
                if (targetProperty != null)
                {
                    // 创建访问 TEntity 属性的表达式
                    return Expression.MakeMemberAccess(_newParameter, targetProperty);
                }
            }

            // 递归处理嵌套属性访问
            if (node.Expression != null)
            {
                var visitedExpression = Visit(node.Expression);
                if (visitedExpression != node.Expression)
                {
                    var targetMember = visitedExpression.Type.GetMember(node.Member.Name).FirstOrDefault();
                    if (targetMember != null)
                    {
                        return Expression.MakeMemberAccess(visitedExpression, targetMember);
                    }
                }
            }

            return base.VisitMember(node);
        }
    }
}


