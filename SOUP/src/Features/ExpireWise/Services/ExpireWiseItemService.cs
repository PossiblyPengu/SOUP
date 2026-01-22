using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SOUP.Core.Entities.ExpireWise;
using SOUP.Core.Interfaces;

namespace SOUP.Features.ExpireWise.Services;

public class ExpireWiseItemService
{
    private readonly IExpireWiseRepository _repository;
    private readonly ILogger<ExpireWiseItemService>? _logger;

    public ExpireWiseItemService(IExpireWiseRepository repository, ILogger<ExpireWiseItemService>? logger = null)
    {
        _repository = repository;
        _logger = logger;
    }

    public Task<ExpirationItem> AddAsync(ExpirationItem item)
    {
        return _repository.AddAsync(item);
    }

    public Task<ExpirationItem> UpdateAsync(ExpirationItem item)
    {
        return _repository.UpdateAsync(item);
    }

    public Task<bool> DeleteAsync(Guid id)
    {
        return _repository.DeleteAsync(id);
    }

    public async Task<int> DeleteRangeAsync(IEnumerable<ExpirationItem> items)
    {
        var deleted = 0;
        foreach (var it in items)
        {
            var ok = await _repository.DeleteAsync(it.Id);
            if (ok) deleted++;
        }
        return deleted;
    }

    public async Task<ExpirationItem> QuickAddAsync(string sku, int month, int year, int units, string description)
    {
        var item = new ExpirationItem
        {
            Id = Guid.NewGuid(),
            ItemNumber = sku,
            ExpiryDate = new DateTime(year, month, 1).AddMonths(1).AddDays(-1),
            Units = units,
            Description = description ?? string.Empty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        return await _repository.AddAsync(item);
    }
}
