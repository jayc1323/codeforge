using CodeForge.Api.Execution;
using CodeForge.Api.Hubs;
using CodeForge.Core.Execution;
using CodeForge.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();
builder.Services.AddCodeForgeInfrastructure(builder.Configuration);
builder.Services.AddSingleton<IExecutionEventPublisher, SignalRExecutionEventPublisher>();

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

app.UseAuthorization();

app.MapControllers();
app.MapHub<ExecutionHub>("/hubs/executions");

app.Run();
