using System.Text.Json.Serialization;

namespace DbView.Core.Models
{
    /// <summary>
    /// 分页列表
    /// </summary>
    public class PagedList<T> : IPagedList<T>
    {
        [JsonPropertyName("currentPage")]
        public int PageIndex { get; }
        public int PageSize { get; }
        [JsonPropertyName("totalItems")]
        public int TotalCount { get; }
        [JsonPropertyName("totalPages")]
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;
        [JsonPropertyName("items")]
        public IReadOnlyList<T> Items { get; }
        public PagedList(IEnumerable<T> items, int pageIndex, int pageSize, int totalCount)
        {
            PageIndex = pageIndex;
            PageSize = pageSize;
            TotalCount = totalCount;
            Items = items?.ToList().AsReadOnly();
        }

        public static PagedList<T> Empty(int pageIndex = 1, int pageSize = 10)
        {
            return new PagedList<T>(Enumerable.Empty<T>(), pageIndex, pageSize, 0);
        }
    }

    public interface IPagedList<T>
    {
        [JsonPropertyName("currentPage")]
        int PageIndex { get; }
        int PageSize { get; }
        [JsonPropertyName("totalItems")]
        int TotalCount { get; }
        [JsonPropertyName("totalPages")]
        int TotalPages { get; }
        bool HasPreviousPage { get; }
        bool HasNextPage { get; }
        [JsonPropertyName("items")]
        IReadOnlyList<T> Items { get; }
    }
}



