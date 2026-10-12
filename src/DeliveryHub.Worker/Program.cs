using DeliveryHub.Worker;

// 5050 em vez de 5000: no macOS a 5000 é ocupada pelo AirPlay Receiver.
var app = WorkerHostFactory.Build(urls: "http://localhost:5050");

app.Run();
