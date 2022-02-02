using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace VotingApp.Data.Interface
{
    public interface IRepositoryBase<T>
    {
        public bool Any(Expression<Func<T, bool>> expression);
        T Get(object key);
        T Get(Expression<Func<T, bool>> expression);
        Task<T> GetAsync(object key);
        Task<T> GetAsync(Expression<Func<T, bool>> expression);
        IQueryable<T> GetAll(Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null);
        Task<List<T>> GetAllAsync(Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null);
        IQueryable<T> GetAll(Expression<Func<T, bool>> expression, Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null);
        Task<List<T>> GetAllAsync(Expression<Func<T, bool>> expression, Func<IQueryable<T>, IOrderedQueryable<T>> orderBy = null);

        void Add(T entity);
        Task AddAsync(T entity);
        void Update(T entity);
        void UpdateRange(IEnumerable<T> entities);
        void Delete(T entity);

        //void DeleteByCondition(Expression<Func<T, bool>> expression);
        //Task<int> DeleteByConditionAsync(Expression<Func<T, bool>> expression);

        int Count();

        void Dispose(bool disposing);
        void Dispose();
        Task AddRangeAsync(IList<T> entities);
        void AddRange(IList<T> entities);
        void Delete(Expression<Func<T, bool>> expression);
    }
}
