using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SOUP.Infrastructure.Services;
using Xunit;

namespace Infrastructure.Tests.Services;

/// <summary>
/// Unit tests for FileImportExportService
/// </summary>
public class FileImportExportServiceTests : IDisposable
{
    private readonly FileImportExportService _service;
    private readonly Mock<ILogger<FileImportExportService>> _loggerMock;
    private readonly List<string> _tempFiles;

    public FileImportExportServiceTests()
    {
        _loggerMock = new Mock<ILogger<FileImportExportService>>();
        _service = new FileImportExportService(_loggerMock.Object);
        _tempFiles = new List<string>();
    }

    public void Dispose()
    {
        // Clean up temporary test files
        foreach (var file in _tempFiles.Where(File.Exists))
        {
            try
            {
                File.Delete(file);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
        GC.SuppressFinalize(this);
    }

    private string CreateTempFile(string extension, string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}{extension}");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    [Fact]
    public async Task ImportFromCsvAsync_NonExistingFile_ReturnsFailure()
    {
        // Arrange
        var nonExistingPath = Path.Combine(Path.GetTempPath(), $"nonexisting_{Guid.NewGuid()}.csv");

        // Act
        var result = await _service.ImportFromCsvAsync<TestImportModel>(nonExistingPath);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("File not found");
    }

    [Fact]
    public async Task ImportFromCsvAsync_InvalidFileExtension_ReturnsFailure()
    {
        // Arrange
        var txtFile = CreateTempFile(".txt", "test data");

        // Act
        var result = await _service.ImportFromCsvAsync<TestImportModel>(txtFile);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid file type");
    }

    [Fact]
    public async Task ImportFromCsvAsync_ValidCsvFile_ImportsSuccessfully()
    {
        // Arrange
        var csvContent = @"Name,Description,Value
Item1,Description1,100
Item2,Description2,200
Item3,Description3,300";
        var csvFile = CreateTempFile(".csv", csvContent);

        // Act
        var result = await _service.ImportFromCsvAsync<TestImportModel>(csvFile);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(3);

        var items = result.Value!.ToList();
        items[0].Name.Should().Be("Item1");
        items[0].Description.Should().Be("Description1");
        items[0].Value.Should().Be(100);

        items[1].Name.Should().Be("Item2");
        items[2].Name.Should().Be("Item3");
    }

    [Fact]
    public async Task ImportFromCsvAsync_EmptyCsvFile_ReturnsEmptyCollection()
    {
        // Arrange
        var csvContent = "Name,Description,Value";
        var csvFile = CreateTempFile(".csv", csvContent);

        // Act
        var result = await _service.ImportFromCsvAsync<TestImportModel>(csvFile);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ImportFromCsvAsync_MissingColumns_ImportsPartialData()
    {
        // Arrange
        var csvContent = @"Name,Value
Item1,100
Item2,200";
        var csvFile = CreateTempFile(".csv", csvContent);

        // Act
        var result = await _service.ImportFromCsvAsync<TestImportModel>(csvFile);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        var items = result.Value!.ToList();
        items[0].Name.Should().Be("Item1");
        items[0].Description.Should().BeNullOrEmpty();
        items[0].Value.Should().Be(100);
    }

    [Fact]
    public async Task ImportFromExcelAsync_NonExistingFile_ReturnsFailure()
    {
        // Arrange
        var nonExistingPath = Path.Combine(Path.GetTempPath(), $"nonexisting_{Guid.NewGuid()}.xlsx");

        // Act
        var result = await _service.ImportFromExcelAsync<TestImportModel>(nonExistingPath);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("File not found");
    }

    [Fact]
    public async Task ImportFromExcelAsync_InvalidFileExtension_ReturnsFailure()
    {
        // Arrange
        var txtFile = CreateTempFile(".txt", "test data");

        // Act
        var result = await _service.ImportFromExcelAsync<TestImportModel>(txtFile);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid file type");
    }

    [Fact]
    public async Task ImportFromCsvAsync_WithSpecialCharacters_ImportsCorrectly()
    {
        // Arrange - use simpler escaping that CsvHelper handles
        var csvContent = "Name,Description,Value\n\"Item, with comma\",\"Description with quotes\",100\nItem2,Normal Description,200";
        var csvFile = CreateTempFile(".csv", csvContent);

        // Act
        var result = await _service.ImportFromCsvAsync<TestImportModel>(csvFile);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        var items = result.Value!.ToList();
        items[0].Name.Should().Be("Item, with comma");
        items[0].Description.Should().Be("Description with quotes");
    }

    [Fact]
    public async Task ImportFromCsvAsync_WithEmptyRows_SkipsEmptyRows()
    {
        // Arrange
        var csvContent = @"Name,Description,Value
Item1,Description1,100

Item2,Description2,200
,,
Item3,Description3,300";
        var csvFile = CreateTempFile(".csv", csvContent);

        // Act
        var result = await _service.ImportFromCsvAsync<TestImportModel>(csvFile);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
        result.Value!.All(item => !string.IsNullOrEmpty(item.Name)).Should().BeTrue();
    }

    [Fact]
    public async Task ImportFromCsvAsync_WithDifferentDataTypes_ConvertsCorrectly()
    {
        // Arrange
        var csvContent = @"Name,Description,Value
Item1,Desc1,100
Item2,Desc2,0
Item3,Desc3,-50";
        var csvFile = CreateTempFile(".csv", csvContent);

        // Act
        var result = await _service.ImportFromCsvAsync<TestImportModel>(csvFile);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var items = result.Value!.ToList();
        items[0].Value.Should().Be(100);
        items[1].Value.Should().Be(0);
        items[2].Value.Should().Be(-50);
    }

    [Fact]
    public async Task ImportFromCsvAsync_NullFilePath_ReturnsFailure()
    {
        // Act & Assert
        var result = await _service.ImportFromCsvAsync<TestImportModel>(null!);

        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("File path is required");
    }

    [Fact]
    public async Task ImportFromExcelAsync_NullFilePath_ReturnsFailure()
    {
        // Act
        var result = await _service.ImportFromExcelAsync<TestImportModel>(null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("File path is required");
    }
}

/// <summary>
/// Test model for import/export testing
/// </summary>
public class TestImportModel
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Value { get; set; }
}
