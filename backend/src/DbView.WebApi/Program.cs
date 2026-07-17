using FastEndpoints;
using FastEndpoints.Swagger;
using System.Text.Json;
using DbView.WebApi.Features.Backup;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddControllers();

builder.Services.RegisterFastEndpoint(builder.Configuration);
builder.Services.AddMediator();
builder.Services.AddEntityMapper();

builder.Services.AddDatabase(autoSync: true);

//builder.Services.AddHostedService<BackupSchedulerService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
app.UseStaticFiles();
app.UseDefaultFiles();

app.UseCors();
app.UseAuthentication().UseAuthorization();

app.MapFallbackToFile("index.html");

app.UseFastEndpoints(c =>
{
    c.Endpoints.RoutePrefix = "api";
    c.Versioning.PrependToRoute = true;
    c.Serializer.Options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    c.Endpoints.Configurator = ep =>
    {
        ep.DontAutoSendResponse();
        ep.PostProcessor<GlobalResponseProcessor>(FastEndpoints.Order.Before);
    };
    c.Errors.ResponseBuilder = (failures, ctx, statusCode) =>
    {
        return null;
    };
});

app.MapControllers();
app.UseSwaggerGen();
if (app.Environment.IsDevelopment())
{
    app.MapGet("/", async context =>
    {
        context.Response.Redirect("/index.html");
        await Task.CompletedTask;
    });
}

app.Run();


