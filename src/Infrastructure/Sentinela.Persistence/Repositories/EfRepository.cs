using Microsoft.EntityFrameworkCore;
using Sentinela.Shared.Core.Entities;
using Sentinela.Shared.Core.Interfaces;

namespace Sentinela.Persistence.Repositories;

public class EfRepository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly SentinelaDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public EfRepository(SentinelaDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _dbSet.FindAsync([id], cancellationToken);

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _dbSet.ToListAsync(cancellationToken);

    public async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        if (!_context.ChangeTracker.Entries<T>().Any(e => e.Entity.Id == entity.Id))
            _dbSet.Update(entity);
        else
            _context.Entry(entity).State = EntityState.Modified;
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        entity.MarkAsDeleted();
        _context.Entry(entity).State = EntityState.Modified;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public IQueryable<T> Query() => _dbSet.AsQueryable();

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);
}
