using Microsoft.AspNetCore.Mvc;
using TestWebApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
// Add services to the container.

var app = builder.Build();

// Configure the HTTP request pipeline.

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

var group = app
    .MapGroup("weather-forecast")
    .WithGroupName("WeatherForecast");

group.MapGet("/", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecastRecord
        (
            DateTime.Now.AddDays(index),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
});

group.MapGet("with-name", ([FromQuery] int days) =>
{
    return new WeatherForecastRecord
        (
            DateTime.Now.AddDays(5),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        );
}).WithName("GetSomeWeather");

app.MapPost("/weather-forecast", (WeatherForecast forecast) => Results.Ok());
app.Run();