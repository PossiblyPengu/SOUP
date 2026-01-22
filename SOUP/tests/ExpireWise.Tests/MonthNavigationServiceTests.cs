using System;
using System.Collections.Generic;
using System.Linq;
using SOUP.Core.Entities.ExpireWise;
using SOUP.Features.ExpireWise.Services;
using Xunit;

namespace ExpireWise.Tests
{
    public class MonthNavigationServiceTests
    {
        [Fact]
        public void BuildMonthGroups_CreatesExpectedRangeAndCounts()
        {
            var svc = new ExpireWiseMonthNavigationService();
            var center = new DateTime(2025, 6, 1);

            var items = new List<ExpirationItem>
            {
                new ExpirationItem { Id = Guid.NewGuid(), ItemNumber = "A1", ExpiryDate = new DateTime(2025,6,5), Units = 1, CreatedAt = DateTime.UtcNow },
                new ExpirationItem { Id = Guid.NewGuid(), ItemNumber = "B1", ExpiryDate = new DateTime(2025,7,10), Units = 2, CreatedAt = DateTime.UtcNow },
                new ExpirationItem { Id = Guid.NewGuid(), ItemNumber = "C1", ExpiryDate = new DateTime(2025,5,15), Units = 3, CreatedAt = DateTime.UtcNow }
            };

            var groups = svc.BuildMonthGroups(items, center, "MMMM yyyy", monthsBefore:1, monthsAfter:1);

            // we asked for 3 months (May, Jun, Jul)
            Assert.Equal(3, groups.Count);
            var may = groups.First(g => g.Month.Month == 5);
            var jun = groups.First(g => g.Month.Month == 6);
            var jul = groups.First(g => g.Month.Month == 7);

            Assert.Equal(1, may.ItemCount);
            Assert.Equal(1, jun.ItemCount);
            Assert.Equal(1, jul.ItemCount);

            Assert.True(jun.IsCurrentMonth);
        }
    }
}
