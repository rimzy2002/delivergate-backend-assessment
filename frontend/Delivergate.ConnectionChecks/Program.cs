using System.Net;
using System.Text;
using Delivergate.Desktop.Services;
using Delivergate.Desktop.ViewModels;

int passed = 0;

void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

HttpResponseMessage Reply(string json, HttpStatusCode status = HttpStatusCode.OK) =>
    new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

async Task Check(string name, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send,
    string expected)
{
    using var api = new ApiClient(new HttpClient(new StubHandler(send)), "http://localhost:3000/");
    var vm = new MainWindowViewModel(api, "http://localhost:3000/");
    var changes = new List<string?>();
    vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
    Assert(vm.CanCheckConnection, "Initially enabled");
    await vm.CheckConnectionAsync();
    Assert(vm.StatusMessage.Contains(expected), $"{name}: {vm.StatusMessage}");
    Assert(!vm.IsBusy && vm.CanCheckConnection, "Button re-enabled");
    Assert(changes.Contains("StatusMessage") && changes.Contains("CanCheckConnection"), "UI notifications");
    Console.WriteLine($"PASS {name}");
    passed++;
}

await Check("healthy response and request URL", (request, _) =>
{
    Assert(request.RequestUri?.AbsoluteUri == "http://localhost:3000/health", "Health URL");
    Assert(request.Method == HttpMethod.Get, "GET method");
    Assert(request.Headers.Accept.Any(x => x.MediaType == "application/json"), "JSON header");
    return Task.FromResult(Reply("{\"status\":\"ok\"}"));
}, "Connected.");
await Check("offline server", (_, _) => throw new HttpRequestException("offline"), "Cannot reach");
await Check("timeout response handling", (_, _) => throw new TaskCanceledException(), "timed out");
await Check("HTTP 404", (_, _) => Task.FromResult(Reply("{}", HttpStatusCode.NotFound)), "HTTP 404");
await Check("HTTP 500", (_, _) => Task.FromResult(Reply("{}", HttpStatusCode.InternalServerError)), "HTTP 500");
await Check("malformed JSON", (_, _) => Task.FromResult(Reply("not JSON")), "invalid JSON");
await Check("null JSON", (_, _) => Task.FromResult(Reply("null")), "not 'ok'");
await Check("missing health status", (_, _) => Task.FromResult(Reply("{}")), "not 'ok'");
await Check("unhealthy response", (_, _) => Task.FromResult(Reply("{\"status\":\"down\"}")), "not 'ok'");

var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
int calls = 0;
using (var api = new ApiClient(new HttpClient(new StubHandler((_, _) =>
{
    calls++;
    return pending.Task;
})), "http://localhost:3000/"))
{
    var vm = new MainWindowViewModel(api, "http://localhost:3000/");
    Task first = vm.CheckConnectionAsync();
    Assert(vm.IsBusy && !vm.CanCheckConnection, "Button disabled while pending");
    Assert(vm.StatusMessage == "Checking connection...", "Pending message");
    await vm.CheckConnectionAsync();
    Assert(calls == 1, "Duplicate request blocked");
    pending.SetResult(Reply("{\"status\":\"ok\"}"));
    await first;
    Assert(vm.CanCheckConnection, "Button re-enabled after pending request");
    Console.WriteLine("PASS busy state and duplicate-click protection");
    passed++;
}

int attempts = 0;
using (var api = new ApiClient(new HttpClient(new StubHandler((_, _) =>
{
    if (++attempts == 1) throw new HttpRequestException("offline");
    return Task.FromResult(Reply("{\"status\":\"ok\"}"));
})), "http://localhost:3000/"))
{
    var vm = new MainWindowViewModel(api, "http://localhost:3000/");
    await vm.CheckConnectionAsync();
    Assert(vm.StatusMessage.Contains("Cannot reach"), "Offline state");
    await vm.CheckConnectionAsync();
    Assert(vm.StatusMessage.StartsWith("Connected."), "Recovery without restarting");
    Console.WriteLine("PASS offline-to-online recovery");
    passed++;
}

if (args.Contains("--live"))
{
    using var api = new ApiClient("http://localhost:3000/");
    await api.CheckHealthAsync();
    Console.WriteLine("PASS live Go backend /health");
    passed++;
}

Console.WriteLine($"All {passed} connection checks passed.");

sealed class StubHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
}
