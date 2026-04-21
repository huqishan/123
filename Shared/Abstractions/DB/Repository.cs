using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Abstractions.DB
{
    public class Repository<TEntity, TDbContext> : IRepository<TEntity>
        where TEntity : Entity, IAggregateRoot, new()
        where TDbContext : DBContext<TEntity>
    {
        protected virtual TDbContext _context { get; set; }
        public IUnitOfWork<TEntity> UnitOfWork => _context;

        /// <summary>
        /// 是否分表
        /// </summary>
        protected bool IsSplitTable { get; set; } = false;

        public Repository(TDbContext context)
        {
            _context = context;
        }

        public bool Add(TEntity entity)
        {
            if (IsSplitTable)
                return _context.Insertable(entity).SplitTable().ExecuteCommand() > 0;
            else
                return _context.Insertable(entity).ExecuteCommand() > 0;
        }

        public bool Add(List<TEntity> entity)
        {
            if (IsSplitTable)
                return _context.Insertable(entity).SplitTable().ExecuteCommand() == entity.Count;
            else
                return _context.Insertable(entity).ExecuteCommand() == entity.Count;
        }

        public Task<bool> AddAsync(TEntity entity)
        {
            return Task.FromResult(Add(entity));
        }

        public int Remove(TEntity entity)
        {
            if (IsSplitTable)
                return _context.Deleteable(entity).SplitTable().ExecuteCommand();
            else
                return _context.Deleteable(entity).ExecuteCommand();
        }

        public Task<int> RemoveAsync(TEntity entity)
        {
            return Task.FromResult(Remove(entity));
        }

        public Task<int> RemoveAsync(Expression<Func<TEntity, bool>> express)
        {
            if (IsSplitTable)
                return Task.FromResult(_context.Deleteable(express).SplitTable().ExecuteCommand());
            else
                return Task.FromResult(_context.Deleteable(express).ExecuteCommand());
        }

        public int Update(TEntity entity)
        {
            if (IsSplitTable)
                return _context.Updateable(entity).SplitTable().ExecuteCommand();
            else
                return _context.Storageable(entity).ExecuteCommand();
        }

        public Task<int> UpdateAsync(TEntity entity)
        {
            return Task.FromResult(Update(entity));
        }
    }

    public abstract class Repository<TEntity, TKey, TDbContext>
        : Repository<TEntity, TDbContext>,
            IRepository<TEntity, TKey>
        where TEntity : Entity<TKey>, IAggregateRoot, new()
        where TDbContext : DBContext<TEntity>
    {
        public Repository(TDbContext context)
            : base(context) { }

        public int Delete(TKey id)
        {
            var entity = Get(id);
            if (entity == null)
            {
                return -1;
            }
            return base._context.Deleteable(entity).ExecuteCommand();
        }

        public async Task<int> DeleteAsync(TKey id)
        {
            var entity = await _context.Queryable<TEntity>().InSingleAsync(id);
            if (entity == null)
            {
                return -1;
            }
            return base._context.Deleteable(entity).ExecuteCommand();
        }

        public TEntity Get(TKey id)
        {
            if (IsSplitTable)
            {
                return _context
                    .Queryable<TEntity>()
                    .SplitTable(tabs => tabs.Take(3))
                    .ToList()
                    .FirstOrDefault(t => t.Id.Equals(id));
            }
            else
            {
                return _context.Queryable<TEntity>().InSingle(id);
            }
        }

        public async Task<TEntity> GetAsync(TKey id)
        {
            if (IsSplitTable)
                return await _context
                  .Queryable<TEntity>()
                  .SplitTable(tabs => tabs.Take(3))
                  .InSingleAsync(id);
            else
                return await _context.Queryable<TEntity>().InSingleAsync(id);
        }
    }
}
