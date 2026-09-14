using DeliveryHub.Worker;

var app = WorkerHostFactory.Build(urls: "http://localhost:5000");

app.Run();
