using DbView.Core.Abstractions;

namespace DbView.Core.Models
{
    public class SqlHistory : IEntity<long>
    {
        public long Id { get; set; }
        public long ConnectionId { get; set; }
        public long UserId { get; set; }
        public string SqlText { get; set; } = string.Empty;
        public DateTime ExecutionTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public int ResultRows { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}