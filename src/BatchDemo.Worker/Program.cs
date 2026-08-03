using BatchDemo.Worker;
using BatchDemo.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddBatchDemoInfrastructure(builder.Configuration);
builder.Services.AddOptions<ProcessingWorkerOptions>().Bind(builder.Configuration.GetSection("Worker"))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
