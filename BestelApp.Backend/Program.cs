using BestelApp.Backend.Services;
using BestelApp.Backend.Consumers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
// builder.Services.AddSwaggerGen(); // Temporarily disabled

// Register application services
builder.Services.AddSingleton<IApiKeyValidator, ApiKeyValidator>();
builder.Services.AddSingleton<IRabbitMQPublisher, RabbitMQPublisher>();
builder.Services.AddScoped<ISapService, SapService>();
builder.Services.AddScoped<ISalesforceService, SalesforceService>();
builder.Services.AddScoped<ISapIdocService, SapIdocService>(); // ? VERPLAATST naar boven

// HttpClient configurations
builder.Services.AddHttpClient(); // Algemene HttpClient

// Specifieke HttpClient voor Salesforce met auth
builder.Services.AddHttpClient<ISalesforceService, SalesforceService>((serviceProvider, client) =>
{
    var config = serviceProvider.GetRequiredService<IConfiguration>();
    var accessToken = config["Salesforce:AccessToken"]
                      ?? Environment.GetEnvironmentVariable("SALESFORCE_ACCESS_TOKEN");

    if (!string.IsNullOrEmpty(accessToken))
    {
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
    }

    client.BaseAddress = new Uri(config["Salesforce:LoginUrl"] ?? "https://login.salesforce.com");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Specifieke HttpClient voor SAP
builder.Services.AddHttpClient<ISapIdocService, SapIdocService>((serviceProvider, client) =>
{
    var config = serviceProvider.GetRequiredService<IConfiguration>();
    var endpoint = config["SAP:Endpoint"]
                   ?? Environment.GetEnvironmentVariable("SAP_IDOC_ENDPOINT");

    if (!string.IsNullOrEmpty(endpoint))
    {
        client.BaseAddress = new Uri(endpoint);
    }

    client.Timeout = TimeSpan.FromSeconds(30);
});



// Background services (consumers)
builder.Services.AddHostedService<SalesforceConsumer>();
builder.Services.AddHostedService<SapIdocConsumer>();


// Add CORS for MAUI app
builder.Services.AddCors(options =>
{
    options.AddPolicy("MauiAppPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    // app.UseSwagger(); // Temporarily disabled
    // app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("MauiAppPolicy");
app.UseAuthorization();
app.MapControllers();

app.Logger.LogInformation("BestelApp Backend API starting...");
app.Logger.LogInformation("Salesforce Consumer running in background...");
app.Logger.LogInformation("SAP iDoc Consumer running in background...");

app.Run();