using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ApiExplorerTest.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<WeatherForecastController> _logger;

        public WeatherForecastController(ILogger<WeatherForecastController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public IEnumerable<WeatherForecast> Get()
        {
            var rng = new Random();
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = rng.Next(-20, 55),
                Summary = Summaries[rng.Next(Summaries.Length)]
            })
            .ToArray();
        }

        [HttpGet("{id}")]
        public WeatherForecast Get(Guid id, CancellationToken cancellationToken)
        {
            var rng = new Random();
            return new WeatherForecast
            {
                Date = DateTime.Now.AddDays(6),
                TemperatureC = rng.Next(-20, 55),
                Summary = Summaries[rng.Next(Summaries.Length)]
            };
        }

        [HttpPost("create")]
        public Task<ActionResult<WeatherForecast>> Post(WeatherForecast weatherForecast)
        {
            return null;
        }

        [HttpPost("upload")]
        public Task Upload(IFormFile uploadedFile)
        {
            return null;
        }

        [HttpPost("search")]
        public Task<WeatherForecast> Search(string name = "test")
        {
            return null;
        }

        [HttpPost("{id}/queryParams")]
        public Task<WeatherForecast> SomethingWithQueryParams(int id = 1, string par1 = null, string par2 = "1")
        {
            return null;
        }
    }
}
