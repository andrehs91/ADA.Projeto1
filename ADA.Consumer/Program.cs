using ADA.Consumer;
using ADA.Consumer.Services;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddScoped<IConsumerService, ConsumerService>();

var host = builder.Build();
host.Run();
