// .NET Aspire AppHost Program.cs
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure dependencies orchestrated in containers
var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .AddDatabase("appdb");

var rabbitmq = builder.AddRabbitMQ("messaging");

var redis = builder.AddRedis("cache");

// .NET Microservice
builder.AddProject<mygherkinservice_ApiService>("apiservice")
    .WithReference(postgres)
    .WithReference(rabbitmq)
    .WithReference(redis);

builder.Build().Run();
