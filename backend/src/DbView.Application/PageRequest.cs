using FastEndpoints;
namespace DbView.Application
{
   public class PageRequest
    {
        [QueryParam]
        public int Page { get; set; } = 1;

        [QueryParam]
        public int PageSize { get; set; } = 10;
    }
}




