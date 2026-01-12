using BestelApp.Backend.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen(); // Temporarily disabled

// Register application services
builder.Services.AddSingleton<IApiKeyValidator, ApiKeyValidator>();
builder.Services.AddSingleton<IRabbitMQPublisher, RabbitMQPublisher>();
builder.Services.AddScoped<ISapService, SapService>();
builder.Services.AddScoped<ISalesforceService, SalesforceService>();

// Register HttpClientFactory for SAP and Salesforce
builder.Services.AddHttpClient("SAP", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient("Salesforce", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Register Salesforce Consumer as background service
builder.Services.AddHostedService<SalesforceConsumer>();

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
    //app.UseSwagger(); // Temporarily disabled
    //app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("MauiAppPolicy");
app.UseAuthorization();
app.MapControllers();

app.Logger.LogInformation("BestelApp Backend API starting...");
app.Logger.LogInformation("Salesforce Consumer running in background...");

app.Run();
