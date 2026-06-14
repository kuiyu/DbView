using FreeSql.DataAnnotations;

namespace DbView.Infrastructure.Entities
{
    [Table(Name = "db_connections")]
    public class ConnectionEntity
    {
        [Column(IsIdentity = true, IsPrimary = true)]
        public long Id { get; set; }

        [Column(StringLength = 100, IsNullable = false)]
        public string Name { get; set; } = string.Empty;

        [Column(StringLength = 255, IsNullable = false)]
        public string Host { get; set; } = string.Empty;

        [Column(IsNullable = false)]
        public int Port { get; set; }

        [Column(StringLength = 100, IsNullable = false)]
        public string DatabaseName { get; set; } = string.Empty;

        [Column(StringLength = 100, IsNullable = false)]
        public string Username { get; set; } = string.Empty;

        [Column(StringLength = 255, IsNullable = false)]
        public string Password { get; set; } = string.Empty;

        [Column(StringLength = 50, IsNullable = false)]
        public string DbType { get; set; } = string.Empty;

        public long UserId { get; set; }

        [Column(CanUpdate = false)]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
