using DbView.Core.Abstractions;
using DbView.Core.Exceptions;

namespace DbView.Core
{
    public partial class User : AggregateRoot<long>
    {
        
        #region 属性
        #endregion

        #region 方法
        // 业务方法 - 只操作自身状态 
        public User(long id) : base(id)
        {
            
        }
        #endregion
    }
}



