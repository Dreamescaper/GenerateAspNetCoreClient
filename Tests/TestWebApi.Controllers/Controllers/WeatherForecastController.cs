using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TestWebApi.Models;

namespace TestWebApi.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

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

    /// <summary>
    /// Get weather forecast by Id.
    /// </summary>
    /// <param name="id">some id.</param>
    /// <param name="cancellationToken">cancellation Token.</param>
    /// <returns><see cref="WeatherForecast"/> with matching id.</returns>
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

    [HttpGet("download")]
    public Task<FileContentResult> Download()
    {
        var bb = new byte[1];
        return Task.FromResult(File(bb, "application/pdf", "weather.pdf"));
    }

    [HttpPost("search")]
    public Task<WeatherForecast> Search(string name = "test")
    {
        return null;
    }

    [HttpPost("{id}/queryParams")]
    public Task<WeatherForecast> SomethingWithQueryParams(int id, int par1 = 2, [Required] string par2 = null, string par3 = null, string par4 = "1")
    {
        return null;
    }

    [HttpPatch("headerParams")]
    public async Task<ActionResult> WithHeaderParams([FromHeader(Name = "x-header-name")] string headerParam)
    {
        await Task.Delay(1);
        return Ok();
    }

    [HttpPost("form")]
    public async Task<ActionResult> WithFormParam([FromForm] SomeQueryModel formParam)
    {
        await Task.Delay(1);
        return Ok();
    }

    [HttpPost("form-with-file")]
    public async Task<ActionResult> WithFormWithFileParam([FromForm] FormModelWithFile formParam)
    {
        await Task.Delay(1);
        return Ok();
    }

    [HttpGet("record")]
    public async Task<ActionResult<RecordModel>> WithRecordModels([FromQuery] RecordModel record)
    {
        await Task.Delay(1);
        return Ok(record);
    }
}


public record FormModelWithFile(IFormFile File, string Title);