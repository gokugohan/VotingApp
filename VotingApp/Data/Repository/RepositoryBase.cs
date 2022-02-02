using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using VotingApp.Data.Interface;

namespace VotingApp.Data.Repository
{
    public class RepositoryBase<T> : IRepositoryBase<T> where T : class
    {
        protected AppDbContext Context { get; set; }
        protected DbSet<T> dbSet;
        protected bool disposed = false;
        public RepositoryBase(AppDbContext context)
        {
            this.Context = context;
            this.dbSet = this.Context.Set<T>();
        }

        public void Add(T entity)
        {
            if (entity == null) throw new ArgumentNullException("entity");
            this.dbSet.Add(entity);
        }

        public async Task AddAsync(T entity)
        {
            if (entity == null) throw new ArgumentNullException("entity");
            await this.dbSet.AddAsync(entity);
        }

        public void AddRange(IList<T> entities)
        {
            if (entities == null) throw new ArgumentNullException("entities");
            this.dbSet.AddRange(entities);
        }

        public async Task AddRangeAsync(IList<T> entities)
        {
            if (entities == null) throw new ArgumentNullException("entities");
            await this.dbSet.AddRangeAsync(entities);
        }

        public void Delete(T entity)
        {
            if (entity == null) throw new ArgumentNullException("entity");
            if (this.Context.Entry(entity).State == EntityState.Detached)
            {
                this.dbSet.Attach(entity);
            }
            this.dbSet.Remove(entity);
        }

        public void Delete(Expression<Func<T,bool>> expression)
        {
            var entity = this.Get(expression);
            this.Delete(entity);
        }

        public T Get(object key)
        {
            if (key == null) throw new ArgumentNullException("key");
            return this.dbSet.Find(key);
        }

        public T Get(Expression<Func<T, bool>> expression)
        {
            return this.dbSet.FirstOrDefault(expression);
        }


        public async Task<T> GetAsync(object key)
        {
            if (key == null) throw new ArgumentNullException("key");
            return await this.dbSet.FindAsync(key);
        }

        public async Task<T> GetAsync(Expression<Func<T, bool>> expression)
        {
            return await this.dbSet.FirstOrDefaultAsync(expression);
        }

        public IQueryable<T> GetAll(Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null)
        {
            if (orderBy != null)
            {
                return orderBy(this.dbSet).AsNoTracking();
            }
            return this.dbSet.AsNoTracking();
        }

        public async Task<List<T>> GetAllAsync(Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null)
        {
            if (orderBy != null)
            {
                return await orderBy(this.dbSet).AsNoTracking().ToListAsync();
            }
            return await this.dbSet.AsNoTracking().ToListAsync();
        }


        public IQueryable<T> GetAll(Expression<Func<T, bool>> expression, Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null)
        {
            var data = this.dbSet.Where(expression);
            if (orderBy != null)
            {
                return orderBy(data).AsNoTracking();
            }
            return data.AsNoTracking();
        }

        public async Task<List<T>> GetAllAsync(Expression<Func<T, bool>> expression, Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null)
        {
            var data = this.dbSet.Where(expression);
            if (orderBy != null)
            {
                return await orderBy(data).AsNoTracking().ToListAsync();
            }
            return await data.AsNoTracking().ToListAsync();
        }

        public int Count()
        {
            return this.dbSet.Count();
        }


        public void Update(T entity)
        {
            if (entity == null) throw new ArgumentNullException("entity");
            this.dbSet.Attach(entity);
            this.Context.Entry(entity).State = EntityState.Modified;
            this.dbSet.Update(entity);
        }

        public void UpdateRange(IEnumerable<T> entities)
        {
            //this.dbSet.AttachRange(entities);
            //this.Context.Entry(entities).State = EntityState.Modified;

            entities.ToList().ForEach(entity =>
            {
                this.Context.Entry(entity).State = EntityState.Modified;
            });

            this.dbSet.UpdateRange(entities);
        }


        public void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    this.Context.Dispose();
                }
            }
            this.disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public bool Any(Expression<Func<T, bool>> expression)
        {
            return this.dbSet.Any(expression);
        }
    }
}
