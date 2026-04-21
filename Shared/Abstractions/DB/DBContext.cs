using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Abstractions.DB
{
    public class DBContext<TEntity> : SqlSugarClient, IUnitOfWork<TEntity> where TEntity : class, new()
    {
        public DBContext(ConnectionConfig config, bool isSplitTable = false) : base(config)
        {
            TEntity entity = new TEntity();
            if (isSplitTable)//如果是分表
                base.CodeFirst.SetStringDefaultLength(200) //设置varchar默认长度为200
                    .SplitTables() //标识分表
                    .InitTables(entity.GetType()); //执行完数据库就有这个表了
            else
                base.CodeFirst.SetStringDefaultLength(200).InitTables(entity.GetType()); //执行完数据库就有这个表了
            //base.CurrentConnectionConfig.ConfigureExternalServices.SplitTableService= new yyyyMMService();
        }

        public Task<int> SaveChangesAsync(TEntity entity)
        {
            return Task.FromResult(1);
            //return base.Storageable(entity).ExecuteCommandAsync();
        }
    }
}
