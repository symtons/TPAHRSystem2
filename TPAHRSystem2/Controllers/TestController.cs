// =============================================================================
// TPAHRSystem2/Controllers/TestController.cs - FIXED VERSION (NO AMBIGUOUS METHODS)
// File: TPAHRSystem2/Controllers/TestController.cs (Replace existing completely)
// =============================================================================

using Microsoft.AspNetCore.Mvc;

namespace TPAHRSystemSimple.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly ILogger<TestController> _logger;

        public TestController(ILogger<TestController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Simple health check
        /// </summary>
        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new
            {
                success = true,
                message = "Test controller is healthy",
                timestamp = DateTime.UtcNow,
                controller = "TestController"
            });
        }

        /// <summary>
        /// Echo test with message
        /// </summary>
        [HttpGet("echo/{message}")]
        public IActionResult Echo(string message)
        {
            return Ok(new
            {
                success = true,
                echo = message,
                timestamp = DateTime.UtcNow,
                controller = "TestController"
            });
        }

        /// <summary>
        /// Get current time
        /// </summary>
        [HttpGet("time")]
        public IActionResult GetTime()
        {
            return Ok(new
            {
                success = true,
                utcTime = DateTime.UtcNow,
                localTime = DateTime.Now,
                timezone = TimeZoneInfo.Local.DisplayName
            });
        }

        /// <summary>
        /// Test POST endpoint
        /// </summary>
        [HttpPost("simple-test")]
        public IActionResult SimpleTest([FromBody] TestRequest request)
        {
            return Ok(new
            {
                success = true,
                message = $"Received: {request.Message}",
                timestamp = DateTime.UtcNow,
                receivedData = request
            });
        }

        /// <summary>
        /// Get simple info
        /// </summary>
        [HttpGet("info")]
        public IActionResult GetInfo()
        {
            return Ok(new
            {
                success = true,
                controller = "TestController",
                version = "1.0.0",
                endpoints = new[]
                {
                    "GET /api/test/health",
                    "GET /api/test/echo/{message}",
                    "GET /api/test/time",
                    "GET /api/test/info",
                    "POST /api/test/simple-test"
                }
            });
        }
    }

    // Simple DTO for testing
    public class TestRequest
    {
        public string Message { get; set; } = string.Empty;
        public int Number { get; set; }
    }
}