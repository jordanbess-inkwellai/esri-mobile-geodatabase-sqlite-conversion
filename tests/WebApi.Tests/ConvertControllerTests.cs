using System.Net;
using System.Net.Http.Json; // For ReadFromJsonAsync
using System.Text.Json; // For JsonSerializer to deserialize
using Microsoft.AspNetCore.Mvc.Testing; // For WebApplicationFactory
using Microsoft.AspNetCore.Http; // For IFormFile if needed, though typically not directly used in client
using Xunit;
using System.IO; // For Path and File
using System.Reflection; // For Assembly.GetExecutingAssembly()

namespace WebApi.Tests
{
    public class ConvertControllerTests : IClassFixture<WebApplicationFactory<Program>> // Program is the entry point of WebApi
    {
        private readonly WebApplicationFactory<Program> _factory;

        public ConvertControllerTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        private static string GetTestFilePath(string fileName)
        {
            var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (assemblyLocation == null)
            {
                throw new DirectoryNotFoundException("Could not determine the location of the test assembly.");
            }
            // Assuming the test file is copied to the output directory of the test project
            return Path.Combine(assemblyLocation, fileName);
        }


        [Fact]
        public async Task UploadAndProcess_WithValidFile_ReturnsOkWithMetadataLog()
        {
            // Arrange
            var client = _factory.CreateClient();
            var targetSrs = "EPSG:3857";
            var bbox = "-100,40,-90,50";
            var dummyFilePath = GetTestFilePath("test.geodatabase"); // Ensure this file exists and is copied to output

            if (!File.Exists(dummyFilePath))
            {
                 // Create a dummy file if it doesn't exist for some reason (e.g. build didn't copy)
                await File.WriteAllTextAsync(dummyFilePath, "This is a dummy geodatabase file for testing.");
            }
            
            using var formData = new MultipartFormDataContent();
            
            // File content
            await using var fileStream = File.OpenRead(dummyFilePath);
            var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream"); // Common for .gdb
            formData.Add(streamContent, "file", Path.GetFileName(dummyFilePath));

            // Other form fields
            formData.Add(new StringContent(targetSrs), "targetSrs");
            formData.Add(new StringContent(bbox), "bbox");

            // Act
            var response = await client.PostAsync("/api/convert/UploadAndProcess", formData);

            // Assert
            response.EnsureSuccessStatusCode(); // Status Code 200-299
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var jsonResponse = await response.Content.ReadFromJsonAsync<ProcessResponse>();
            Assert.NotNull(jsonResponse);
            Assert.NotNull(jsonResponse.MetadataLog);
            // We can also check if the log contains expected output from CoreConverter's placeholder
            Assert.Contains("Input GDB:", jsonResponse.MetadataLog); 
            Assert.Contains($"Target SRS: {targetSrs}", jsonResponse.MetadataLog);
            Assert.Contains($"Bounding Box (bbox): [{string.Join(", ", bbox.Split(',').Select(s => s.Trim()))}]", jsonResponse.MetadataLog);
        }

        [Fact]
        public async Task UploadAndProcess_WithValidFile_NoBbox_ReturnsOkWithMetadataLog()
        {
            // Arrange
            var client = _factory.CreateClient();
            var targetSrs = "EPSG:4326";
            var dummyFilePath = GetTestFilePath("test.geodatabase");

             if (!File.Exists(dummyFilePath))
            {
                await File.WriteAllTextAsync(dummyFilePath, "This is a dummy geodatabase file for testing.");
            }

            using var formData = new MultipartFormDataContent();
            await using var fileStream = File.OpenRead(dummyFilePath);
            var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            formData.Add(streamContent, "file", Path.GetFileName(dummyFilePath));
            formData.Add(new StringContent(targetSrs), "targetSrs");
            // No bbox for this test

            // Act
            var response = await client.PostAsync("/api/convert/UploadAndProcess", formData);

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var jsonResponse = await response.Content.ReadFromJsonAsync<ProcessResponse>();
            Assert.NotNull(jsonResponse);
            Assert.NotNull(jsonResponse.MetadataLog);
            Assert.Contains($"Target SRS: {targetSrs}", jsonResponse.MetadataLog);
            Assert.DoesNotContain("Bounding Box (bbox):", jsonResponse.MetadataLog); // Ensure bbox info is not present
        }


        [Fact]
        public async Task UploadAndProcess_MissingFile_ReturnsBadRequest()
        {
            // Arrange
            var client = _factory.CreateClient();
            using var formData = new MultipartFormDataContent();
            // No file added to formData

            formData.Add(new StringContent("EPSG:4326"), "targetSrs"); // Add other required parameters

            // Act
            var response = await client.PostAsync("/api/convert/UploadAndProcess", formData);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            
            var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            Assert.NotNull(errorResponse);
            Assert.Equal("No file uploaded.", errorResponse.Message);
        }

        // Helper classes for deserializing JSON responses
        private class ProcessResponse
        {
            public string? MetadataLog { get; set; }
        }
        private class ErrorResponse
        {
            public string? Message { get; set; }
            public string? Details { get; set; } // If your API returns details
        }
    }
}
