var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres");
var ordersDb = postgres.AddDatabase("ordersdb");

var pricing = builder.AddProject<Projects.Pricing_Api>("pricing-api");

builder.AddProject<Projects.Orders_Api>("orders-api")
    .WithReference(pricing)
    .WithReference(ordersDb)
    .WaitFor(ordersDb);

builder.Build().Run();
