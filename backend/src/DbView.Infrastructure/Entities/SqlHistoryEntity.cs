using FreeSql.DataAnnotations;

namespace DbView.Infrastructure.Entities
{
    [Table(Name = "sql_history")]
    public class SqlHistoryEntity
    {
        [Column(IsIdentity = true, IsPrimary = true)]
        public long Id { get; set; }

        public long ConnectionId { get; set; }

        public long UserId { get; set; }

        [Column(StringLength = -1, IsNullable = false)]
        public string SqlText { get; set; } = string.Empty;

        [Column(CanUpdate = false)]
        public DateTime ExecutionTime { get; set; } = DateTime.Now;

        [Column(StringLength = 20)]
        public string Status { get; set; } = string.Empty;

        public int ResultRows { get; set; }

        [Column(StringLength = -1)]
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
