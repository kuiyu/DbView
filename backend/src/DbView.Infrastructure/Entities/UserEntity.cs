using FreeSql.DataAnnotations;

namespace DbView.Infrastructure.Entities
{
    [Table(Name ="dbview_user")]
    public class UserEntity
    {
        [Column(IsPrimary = true, IsIdentity = true)]
        public long Id { get; set; }
        public string UserId { get; set; }=Guid.NewGuid().ToString();
        public DateTime CreatedAt  { get; set; }=DateTime.Now;
        
        public DateTime? UpdatedAt  { get; set; }
    }
}





