using Asp.Versioning;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services
    .AddApiVersioning(setup =>
    {
        setup.DefaultApiVersion = new ApiVersion(1, 0);
        setup.AssumeDefaultVersionWhenUnspecified = true;
        setup.ReportApiVersions = true;
        setup.ApiVersionReader = new HeaderApiVersionReader("Api-Version");
    })
    .AddMvc()
    .AddApiExplorer(setup =>
    {
        setup.GroupNameFormat = "'v'VVV";
    });

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseAuthorization();

app.MapControllers();

app.Run();
