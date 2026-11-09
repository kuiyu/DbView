using System.Data.Common;

namespace DbView.Infrastructure.Services
{
    /// <summary>
    /// 备份会话：在同一个数据库连接（PostgreSQL 下为同一个 REPEATABLE READ 事务）内连续读取多张表。
    ///
    /// 目的：
    /// 1. 一致性快照 —— PostgreSQL 默认 READ COMMITTED，若每张表各开一条语句/连接，
    ///    备份里"订单表已含 10:00 的订单、库存表还是 09:59 的状态"，文件内部自相矛盾；
    ///    用 REPEATABLE READ 事务可让整个备份来自同一时点的数据。
    /// 2. 连接复用 —— 避免每张表新建连接，减小数据库连接压力与超时概率。
    /// </summary>
    public sealed class BackupSession : IAsyncDisposable
    {
        private readonly DbConnection _db;
        private readonly DbTransaction? _transaction;

        public BackupSession(DbConnection db, DbTransaction? transaction)
        {
            _db = db;
            _transaction = transaction;
        }

        /// <summary>底层连接</summary>
        public DbConnection Db => _db;

        /// <summary>当前事务（PostgreSQL 快照模式下为 REPEATABLE READ 事务）</summary>
        public DbTransaction? Transaction => _transaction;

        /// <summary>创建绑定了当前事务的命令</summary>
        public DbCommand CreateCommand()
        {
            var command = _db.CreateCommand();
            command.Transaction = _transaction;
            return command;
        }

        public async ValueTask DisposeAsync()
        {
            if (_transaction != null)
            {
                await _transaction.DisposeAsync();
            }
            await _db.DisposeAsync();
        }
    }
}
