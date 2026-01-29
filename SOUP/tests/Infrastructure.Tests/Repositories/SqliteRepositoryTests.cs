using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Moq;
using SOUP.Core.Common;
using SOUP.Infrastructure.Data;
using SOUP.Infrastructure.Repositories;
using Xunit;

namespace Infrastructure.Tests.Repositories;

/// <summary>
/// Unit tests for SqliteRepository
/// </summary>
public class SqliteRepositoryTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteDbContext _context;
    private readonly SqliteRepository<TestEntity> _repository;
    private readonly Mock<ILogger<SqliteRepository<TestEntity>>> _loggerMock;

    public SqliteRepositoryTests()
    {
        // Create a unique test database for each test run
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        _loggerMock = new Mock<ILogger<SqliteRepository<TestEntity>>>();
        _context = new SqliteDbContext(_testDbPath);
        _repository = new SqliteRepository<TestEntity>(_context, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();

        // Clear SQLite connection pool to release file handles
        SqliteConnection.ClearAllPools();

        // Force garbage collection to release SQLite connections
        GC.Collect();
        GC.WaitForPendingFinalizers();

        // Retry file deletion a few times to handle SQLite cleanup delay
        for (int i = 0; i < 5; i++)
        {
            try
            {
                if (File.Exists(_testDbPath))
                {
                    File.Delete(_testDbPath);

                    // Also delete WAL and SHM files if they exist
                    var walPath = _testDbPath + "-wal";
                    var shmPath = _testDbPath + "-shm";
                    if (File.Exists(walPath)) File.Delete(walPath);
                    if (File.Exists(shmPath)) File.Delete(shmPath);
                }
                break;
            }
            catch (IOException) when (i < 4)
            {
                System.Threading.Thread.Sleep(100);
            }
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task AddAsync_ValidEntity_ReturnsEntity()
    {
        // Arrange
        var entity = new TestEntity { Name = "Test Item" };

        // Act
        var result = await _repository.AddAsync(entity);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(entity.Id);
        result.Name.Should().Be("Test Item");
    }

    [Fact]
    public async Task AddAsync_NullEntity_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _repository.AddAsync(null!));
    }

    [Fact]
    public async Task GetByIdAsync_ExistingEntity_ReturnsEntity()
    {
        // Arrange
        var entity = new TestEntity { Name = "Test Item" };
        await _repository.AddAsync(entity);

        // Act
        var result = await _repository.GetByIdAsync(entity.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(entity.Id);
        result.Name.Should().Be("Test Item");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingEntity_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_MultipleEntities_ReturnsAll()
    {
        // Arrange
        var entity1 = new TestEntity { Name = "Item 1" };
        var entity2 = new TestEntity { Name = "Item 2" };
        var entity3 = new TestEntity { Name = "Item 3" };

        await _repository.AddAsync(entity1);
        await _repository.AddAsync(entity2);
        await _repository.AddAsync(entity3);

        // Act
        var results = await _repository.GetAllAsync();

        // Assert
        results.Should().HaveCount(3);
        results.Should().Contain(e => e.Name == "Item 1");
        results.Should().Contain(e => e.Name == "Item 2");
        results.Should().Contain(e => e.Name == "Item 3");
    }

    [Fact]
    public async Task GetAllAsync_WithDeletedEntities_ExcludesDeleted()
    {
        // Arrange
        var entity1 = new TestEntity { Name = "Active Item" };
        var entity2 = new TestEntity { Name = "Deleted Item" };

        await _repository.AddAsync(entity1);
        await _repository.AddAsync(entity2);
        await _repository.DeleteAsync(entity2.Id);

        // Act
        var results = await _repository.GetAllAsync();

        // Assert
        results.Should().HaveCount(1);
        results.First().Name.Should().Be("Active Item");
    }

    [Fact]
    public async Task UpdateAsync_ExistingEntity_UpdatesSuccessfully()
    {
        // Arrange
        var entity = new TestEntity { Name = "Original Name" };
        await _repository.AddAsync(entity);

        // Act
        entity.Name = "Updated Name";
        await _repository.UpdateAsync(entity);

        // Assert
        var result = await _repository.GetByIdAsync(entity.Id);
        result.Should().NotBeNull();
        result!.Name.Should().Be("Updated Name");
        result.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_NullEntity_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _repository.UpdateAsync(null!));
    }

    [Fact]
    public async Task DeleteAsync_ExistingEntity_SoftDeletesEntity()
    {
        // Arrange
        var entity = new TestEntity { Name = "To Delete" };
        await _repository.AddAsync(entity);

        // Act
        var result = await _repository.DeleteAsync(entity.Id);

        // Assert
        result.Should().BeTrue();

        // Verify soft delete - entity should not appear in GetAllAsync
        var allEntities = await _repository.GetAllAsync();
        allEntities.Should().BeEmpty();

        // Verify entity still exists but is marked deleted
        var deletedEntity = await _repository.GetByIdAsync(entity.Id);
        deletedEntity.Should().NotBeNull();
        deletedEntity!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_NonExistingEntity_ReturnsFalse()
    {
        // Act
        var result = await _repository.DeleteAsync(Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task AddRangeAsync_MultipleEntities_AddsAllSuccessfully()
    {
        // Arrange
        var entities = new[]
        {
            new TestEntity { Name = "Bulk 1" },
            new TestEntity { Name = "Bulk 2" },
            new TestEntity { Name = "Bulk 3" },
            new TestEntity { Name = "Bulk 4" },
            new TestEntity { Name = "Bulk 5" }
        };

        // Act
        await _repository.AddRangeAsync(entities);

        // Assert
        var results = await _repository.GetAllAsync();
        results.Should().HaveCount(5);
    }

    [Fact]
    public async Task AddRangeAsync_NullCollection_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _repository.AddRangeAsync(null!));
    }

    [Fact]
    public async Task FindAsync_WithPredicate_ReturnsMatchingEntities()
    {
        // Arrange
        await _repository.AddAsync(new TestEntity { Name = "Apple" });
        await _repository.AddAsync(new TestEntity { Name = "Banana" });
        await _repository.AddAsync(new TestEntity { Name = "Apricot" });

        // Act
        var results = await _repository.FindAsync(e => e.Name.StartsWith("A"));

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.Name == "Apple");
        results.Should().Contain(e => e.Name == "Apricot");
    }

    [Fact]
    public async Task FindAsync_NullPredicate_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _repository.FindAsync(null!));
    }

    [Fact]
    public async Task DeleteAllAsync_MultipleEntities_SoftDeletesAll()
    {
        // Arrange
        await _repository.AddAsync(new TestEntity { Name = "Item 1" });
        await _repository.AddAsync(new TestEntity { Name = "Item 2" });
        await _repository.AddAsync(new TestEntity { Name = "Item 3" });

        // Act
        await _repository.DeleteAllAsync();

        // Assert
        var results = await _repository.GetAllAsync();
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task HardDeleteAllAsync_MultipleEntities_PermanentlyDeletesAll()
    {
        // Arrange
        await _repository.AddAsync(new TestEntity { Name = "Item 1" });
        await _repository.AddAsync(new TestEntity { Name = "Item 2" });
        await _repository.AddAsync(new TestEntity { Name = "Item 3" });

        // Act
        var deletedCount = await _repository.HardDeleteAllAsync();

        // Assert
        deletedCount.Should().Be(3);
        var results = await _repository.GetAllAsync();
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task Repository_TransactionRollback_OnAddRangeError_PreservesDataIntegrity()
    {
        // Arrange - Add one valid entity first
        await _repository.AddAsync(new TestEntity { Name = "Existing" });

        // Create a list with duplicate ID to cause constraint violation
        var entities = new[]
        {
            new TestEntity { Name = "Valid 1" },
            new TestEntity { Name = "Valid 2" }
        };
        // Force same ID to cause primary key constraint violation on second insert attempt
        entities[1].Id = entities[0].Id;

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(
            () => _repository.AddRangeAsync(entities));

        // Verify original data is preserved and no partial inserts occurred
        var results = await _repository.GetAllAsync();
        results.Should().HaveCount(1);
        results.First().Name.Should().Be("Existing");
    }
}

/// <summary>
/// Test entity for repository testing
/// </summary>
public class TestEntity : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Value { get; set; }
}
