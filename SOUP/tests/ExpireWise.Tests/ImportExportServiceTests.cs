using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SOUP.Core.Entities.ExpireWise;
using SOUP.Core.Interfaces;
using SOUP.Features.ExpireWise.Services;
using Xunit;

namespace ExpireWise.Tests
{
    class FakeExpireWiseRepository : IExpireWiseRepository
    {
        private List<ExpirationItem> _store = new();

        public Task<ExpirationItem> AddAsync(ExpirationItem entity)
        {
            _store.Add(entity);
            return Task.FromResult(entity);
        }

        public Task<IEnumerable<ExpirationItem>> AddRangeAsync(IEnumerable<ExpirationItem> entities)
        {
            var list = entities.ToList();
            _store.AddRange(list);
            return Task.FromResult<IEnumerable<ExpirationItem>>(list);
        }

        public Task<bool> DeleteAsync(Guid id)
        {
            var removed = _store.RemoveAll(i => i.Id == id) > 0;
            return Task.FromResult(removed);
        }

        public Task DeleteAllAsync()
        {
            _store.Clear();
            return Task.CompletedTask;
        }

        public Task<int> HardDeleteAllAsync()
        {
            var count = _store.Count;
            _store.Clear();
            return Task.FromResult(count);
        }

        public Task<IEnumerable<ExpirationItem>> FindAsync(System.Linq.Expressions.Expression<Func<ExpirationItem, bool>> predicate)
        {
            var func = predicate.Compile();
            var matches = _store.Where(func).ToList();
            return Task.FromResult<IEnumerable<ExpirationItem>>(matches);
        }

        public Task<IEnumerable<ExpirationItem>> GetAllAsync() => Task.FromResult<IEnumerable<ExpirationItem>>(_store);

        public Task<ExpirationItem?> GetByIdAsync(Guid id) => Task.FromResult(_store.FirstOrDefault(i => i.Id == id));

        public Task<IEnumerable<ExpirationItem>> GetExpiredItemsAsync()
        {
            var now = DateTime.UtcNow;
            var results = _store.Where(i => !i.IsDeleted && i.ExpiryDate < now).ToList();
            return Task.FromResult<IEnumerable<ExpirationItem>>(results);
        }

        public Task<IEnumerable<ExpirationItem>> GetExpiringSoonAsync(int days = 7)
        {
            var threshold = DateTime.UtcNow.AddDays(days);
            var results = _store.Where(i => !i.IsDeleted && i.ExpiryDate <= threshold && i.ExpiryDate >= DateTime.UtcNow).ToList();
            return Task.FromResult<IEnumerable<ExpirationItem>>(results);
        }

        public Task<IEnumerable<ExpirationItem>> GetByDateRangeAsync(DateTime start, DateTime end)
        {
            var results = _store.Where(i => !i.IsDeleted && i.ExpiryDate >= start && i.ExpiryDate <= end).ToList();
            return Task.FromResult<IEnumerable<ExpirationItem>>(results);
        }

        public Task<bool> ReplaceAllAsync(List<ExpirationItem> newItems)
        {
            _store = newItems.ToList();
            return Task.FromResult(true);
        }

        public Task<ExpirationItem> UpdateAsync(ExpirationItem entity)
        {
            var idx = _store.FindIndex(i => i.Id == entity.Id);
            if (idx >= 0) _store[idx] = entity;
            return Task.FromResult(entity);
        }
    }

    public class ImportExportServiceTests
    {
        [Fact]
        public async Task ImportItemsAsync_ReplacesRepositoryContents()
        {
            var repo = new FakeExpireWiseRepository();
            var svc = new ExpireWiseImportExportService(repo, null);

            var items = new List<ExpirationItem>
            {
                new ExpirationItem { Id = Guid.NewGuid(), ItemNumber = "X1", ExpiryDate = DateTime.UtcNow.AddDays(30), CreatedAt = DateTime.UtcNow },
                new ExpirationItem { Id = Guid.NewGuid(), ItemNumber = "X2", ExpiryDate = DateTime.UtcNow.AddDays(60), CreatedAt = DateTime.UtcNow }
            };

            var res = await svc.ImportItemsAsync(items);
            Assert.True(res.IsSuccess);
            Assert.Equal(2, res.Value);

            var stored = await repo.GetAllAsync();
            Assert.Equal(2, stored.Count());
        }
    }
}
