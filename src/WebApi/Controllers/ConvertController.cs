using Microsoft.AspNetCore.Mvc;
using CoreConverter; // Reference to the CoreConverter project
using System.IO;
using System.Threading.Tasks;
using System;
using System.Linq; // For parsing bbox
using System.Globalization; // For parsing bbox

namespace WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConvertController : ControllerBase
    {
        private readonly ILogger<ConvertController> _logger;

        public ConvertController(ILogger<ConvertController> logger)
        {
            _logger = logger;
        }

        [HttpPost("UploadAndProcess")]
        [RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = long.MaxValue)] // Allow large file uploads
        [DisableRequestSizeLimit] // Disable request size limit
        public async Task<IActionResult> UploadAndProcess([FromForm] IFormFile file, [FromForm] string targetSrs = "EPSG:4326", [FromForm] string? bbox = null)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No file uploaded." });
            }

            // It's good practice to check the file extension, though not foolproof security.
            var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            if (extension != ".gdb" && extension != ".geodatabase") // Mobile GDBs might have .gdb or .geodatabase
            {
                // Note: File Geodatabases (.gdb) are directories. This endpoint expects a single file,
                // suitable for Mobile Geodatabases (.geodatabase or a .gdb file that IS the database).
                // If full FGDB support (uploading a ZIP of the .gdb dir) is needed, this logic would expand.
                _logger.LogWarning($"File with potentially unsupported extension uploaded: {file.FileName}");
                // Depending on strictness, you might return BadRequest here.
                // For now, we'll allow it and let CoreConverter handle it.
            }
            
            string tempInputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + "_" + file.FileName);
            string dummyOutputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".gpkg"); // Dummy, as CoreConverter doesn't write yet

            try
            {
                _logger.LogInformation($"Saving uploaded file to temporary path: {tempInputPath}");
                using (var stream = new FileStream(tempInputPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
                _logger.LogInformation("File saved successfully.");

                double[]? parsedBbox = null;
                if (!string.IsNullOrWhiteSpace(bbox))
                {
                    try
                    {
                        parsedBbox = bbox.Split(',')
                                         .Select(s => double.Parse(s.Trim(), CultureInfo.InvariantCulture))
                                         .ToArray();
                        if (parsedBbox.Length != 4)
                        {
                            _logger.LogWarning($"Invalid bounding box format: {bbox}. Expected 4 comma-separated numbers.");
                            return BadRequest(new { message = "Invalid bounding box format. Expected 4 comma-separated numbers (minX,minY,maxX,maxY)." });
                        }
                    }
                    catch (FormatException ex)
                    {
                        _logger.LogError(ex, $"Bounding box parsing error: {bbox}");
                        return BadRequest(new { message = "Bounding box contains invalid numbers." });
                    }
                }

                var converter = new Converter();
                var consoleOutput = new StringWriter();
                var originalConsoleOut = Console.Out;
                
                string capturedOutput;

                try
                {
                    Console.SetOut(consoleOutput);
                    _logger.LogInformation($"Starting CoreConverter.Process with input: {tempInputPath}, output: {dummyOutputPath}, SRS: {targetSrs}, BBox: {(parsedBbox != null ? string.Join(",", parsedBbox) : "null")}");
                    converter.Process(tempInputPath, dummyOutputPath, targetSrs, parsedBbox);
                }
                finally
                {
                    Console.SetOut(originalConsoleOut); // Restore original console output
                    capturedOutput = consoleOutput.ToString();
                    _logger.LogInformation("CoreConverter.Process finished.");
                    _logger.LogInformation($"Captured output length: {capturedOutput.Length}");
                    if (capturedOutput.Length > 2000) { // Log snippet if too long
                        _logger.LogInformation($"Captured output (snippet): {capturedOutput.Substring(0, 2000)}...");
                    } else {
                        _logger.LogInformation($"Captured output: {capturedOutput}");
                    }
                }
                
                return Ok(new { metadataLog = capturedOutput });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during file processing.");
                return StatusCode(500, new { message = $"An error occurred: {ex.Message}", details = ex.ToString() });
            }
            finally
            {
                // Clean up temporary files
                if (System.IO.File.Exists(tempInputPath))
                {
                    _logger.LogInformation($"Deleting temporary input file: {tempInputPath}");
                    System.IO.File.Delete(tempInputPath);
                }
                if (System.IO.File.Exists(dummyOutputPath)) // Though dummy, good practice
                {
                     _logger.LogInformation($"Deleting temporary output file: {dummyOutputPath}");
                    System.IO.File.Delete(dummyOutputPath);
                }
            }
        }
    }
}
