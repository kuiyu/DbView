using System.Linq.Expressions;

namespace DbView.Core.Specifications
{
    /// <summary>
    /// 规约接口 - 用于封装查询条件
    /// </summary>
    public interface ISpecification<T>
        where T : class
    {
        /// <summary>
        /// 规约是否满足
        /// </summary>
        bool IsSatisfiedBy(T entity);

        /// <summary>
        /// 获取规约表达式（用于LINQ查询）
        /// </summary>
        Expression<Func<T, bool>>? ToExpression();
    }
    /// <summary>
    /// 可排序规约接口
    /// </summary>
    public interface ISortableSpecification<T> : ISpecification<T>
        where T : class
    {
        /// <summary>
        /// 排序表达式
        /// </summary>
        Expression<Func<T, object>>? OrderBy { get; }

        /// <summary>
        /// 排序方向
        /// </summary>
        bool OrderByDescending { get; }

        /// <summary>
        /// 然后排序表达式
        /// </summary>
        Expression<Func<T, object>>? ThenBy { get; }
    }
    /// <summary>
    /// 可分页规约接口
    /// </summary>
    public interface IPagedSpecification<T> : ISpecification<T>
        where T : class
    {
        int PageIndex { get; }
        int PageSize { get; }
    }
    /// <summary>
    /// 规约基类
    /// </summary>
    public abstract class Specification<T> : ISpecification<T>
        where T : class
    {
        public abstract Expression<Func<T, bool>>? ToExpression();

        public bool IsSatisfiedBy(T entity)
        {
            var expression = ToExpression();
            if (expression == null) return true;

            var predicate = expression.Compile();
            return predicate(entity);
        }

    }

}



