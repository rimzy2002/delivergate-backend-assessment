using System.Net;
using System.Text;
using System.Text.Json;
using Delivergate.Desktop.Services;
using Delivergate.Desktop.ViewModels;

internal static class AuthChecks
{
    private const string UserJson = "{\"id\":1,\"name\":\"Test Owner\",\"email\":\"owner@delivergate.local\",\"role\":\"owner\"}";
    private const string LoginJson = "{\"token\":\"test-token\",\"user\":" + UserJson + "}";
    private const string MeJson = "{\"user\":" + UserJson + "}";
    private static int _passed;

    private static void Assert(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }

    private static HttpResponseMessage Reply(string body, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static ApiClient Client(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) =>
        new(new HttpClient(new StubHandler(send)), "http://localhost:3000/");

    private static void Pass(string name)
    {
        Console.WriteLine($"PASS auth: {name}");
        _passed++;
    }

    public static async Task RunAsync(string[] args)
    {
        using (var api = Client((_, _) => throw new Exception("Must not send invalid form")))
        {
            var vm = new AuthViewModel(api);
            await vm.LoginAsync("");
            Assert(vm.Message == "Enter your email and password." && !vm.IsSignedIn, "Required fields");
            vm.Email = "invalid-email";
            await vm.LoginAsync("test-password");
            Assert(vm.Message.Contains("valid email"), "Email validation");
            Pass("local validation sends no request");
        }

        int requests = 0;
        using (var api = Client(async (request, _) =>
        {
            requests++;
            if (request.RequestUri!.AbsolutePath == "/api/auth/login")
            {
                Assert(request.Method == HttpMethod.Post && request.Headers.Authorization is null, "Login headers");
                using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
                Assert(json.RootElement.GetProperty("email").GetString() == "owner@delivergate.local", "Trim email");
                Assert(json.RootElement.GetProperty("password").GetString() == " test-password ", "Never trim password");
                return Reply(LoginJson);
            }
            Assert(request.RequestUri.AbsolutePath == "/api/auth/me", "Me URL");
            Assert(request.Headers.Authorization?.ToString() == "Bearer test-token", "Bearer header");
            return Reply(MeJson);
        }))
        {
            var vm = new AuthViewModel(api) { Email = " owner@delivergate.local " };
            await vm.LoginAsync(" test-password ");
            Assert(requests == 2 && vm.IsSignedIn && vm.UserRole == "owner", "Verified login");
            await vm.VerifySessionAsync();
            Assert(vm.Message == "Your session is valid.", "Verify session");
            vm.Logout();
            Assert(!vm.IsSignedIn && api.Session.Token is null && vm.UserEmail == "", "Logout clears identity/token");
            Pass("login contract, bearer header, verify, logout");
        }

        foreach (var scenario in new[]
        {
            ("wrong password", HttpStatusCode.Unauthorized, "{}", "Invalid email or password"),
            ("server error", HttpStatusCode.InternalServerError, "private server details", "HTTP 500"),
            ("bad JSON", HttpStatusCode.OK, "broken", "invalid JSON"),
            ("missing token", HttpStatusCode.OK, "{\"user\":" + UserJson + "}", "incomplete login"),
            ("unknown role", HttpStatusCode.OK, LoginJson.Replace("owner\"}", "admin\"}"), "incomplete login"),
            ("null response", HttpStatusCode.OK, "null", "incomplete login")
        })
        {
            using var api = Client((_, _) => Task.FromResult(Reply(scenario.Item3, scenario.Item2)));
            var vm = new AuthViewModel(api) { Email = "owner@delivergate.local" };
            await vm.LoginAsync("test-password");
            Assert(!vm.IsSignedIn && api.Session.Token is null && vm.CanInteract, scenario.Item1);
            Assert(vm.Message.Contains(scenario.Item4), vm.Message);
            Pass(scenario.Item1);
        }

        foreach (bool timeout in new[] { false, true })
        {
            using var api = Client((_, _) => timeout
                ? throw new TaskCanceledException() : throw new HttpRequestException());
            var vm = new AuthViewModel(api) { Email = "owner@delivergate.local" };
            await vm.LoginAsync("test-password");
            Assert(vm.Message.Contains(timeout ? "timed out" : "Cannot reach") && vm.CanInteract && !vm.IsSignedIn, "Network error");
            Pass(timeout ? "login timeout" : "login offline");
        }

        foreach (var status in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden })
        {
            bool reject = false;
            using var api = Client((request, _) => Task.FromResult(
                request.RequestUri!.AbsolutePath.EndsWith("login") ? Reply(LoginJson) :
                reject ? Reply("{}", status) : Reply(MeJson)));
            var vm = new AuthViewModel(api) { Email = "owner@delivergate.local" };
            await vm.LoginAsync("test-password");
            reject = true;
            await vm.VerifySessionAsync();
            Assert(vm.IsSignedIn == (status == HttpStatusCode.Forbidden), "401 clears session; 403 does not");
            Assert(vm.Message.Contains(status == HttpStatusCode.Unauthorized ? "expired" : "permission"), "Mapped status");
            Pass($"session HTTP {(int)status}");
        }

        using (var api = Client((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("login")
            ? Reply(LoginJson) : Reply("{}", HttpStatusCode.Unauthorized))))
        {
            var vm = new AuthViewModel(api) { Email = "owner@delivergate.local" };
            await vm.LoginAsync("test-password");
            Assert(!vm.IsSignedIn && api.Session.Token is null, "Do not save unverified token");
            Pass("login token rejected by /me");
        }

        var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        int loginCalls = 0;
        using (var api = Client((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("login")) { loginCalls++; return pending.Task; }
            return Task.FromResult(Reply(MeJson));
        }))
        {
            var vm = new AuthViewModel(api) { Email = "owner@delivergate.local" };
            Task first = vm.LoginAsync("test-password");
            Assert(vm.IsBusy && !vm.CanInteract, "Busy UI");
            await vm.LoginAsync("test-password");
            Assert(loginCalls == 1, "Duplicate login blocked");
            pending.SetResult(Reply(LoginJson));
            await first;
            Assert(vm.IsSignedIn && vm.CanInteract, "Completed login");
            Pass("busy state and duplicate login protection");
        }

        using (var api = new ApiClient("http://localhost:3000/"))
        {
            Assert(!api.Session.IsSignedIn && api.Session.Token is null, "Fresh session");
            Pass("new application session starts signed out");
        }

        bool offline = false;
        using (var api = Client((request, _) =>
        {
            if (offline) throw new HttpRequestException();
            if (request.RequestUri!.AbsolutePath == "/health")
            {
                Assert(request.Headers.Authorization is null, "Health must not receive a token");
                return Task.FromResult(Reply("{\"status\":\"ok\"}"));
            }
            return Task.FromResult(Reply(request.RequestUri.AbsolutePath.EndsWith("login") ? LoginJson : MeJson));
        }))
        {
            var vm = new AuthViewModel(api) { Email = "owner@delivergate.local" };
            var changes = new List<string?>();
            vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
            await vm.LoginAsync("test-password");
            Assert(changes.Contains("IsSignedIn") && changes.Contains("UserRole"), "Authentication UI notifications");
            await api.CheckHealthAsync();
            offline = true;
            await vm.VerifySessionAsync();
            Assert(vm.IsSignedIn && vm.Message.Contains("Cannot reach"), "Network failure preserves session");
            offline = false;
            await vm.VerifySessionAsync();
            Assert(vm.Message == "Your session is valid.", "Recovery");
            Pass("session notifications, token isolation and network recovery");
        }

        using (var api = Client((request, _) => Task.FromResult(Reply(
            request.RequestUri!.AbsolutePath.EndsWith("login") ? LoginJson : MeJson.Replace("\"id\":1", "\"id\":2")))))
        {
            var vm = new AuthViewModel(api) { Email = "owner@delivergate.local" };
            await vm.LoginAsync("test-password");
            Assert(!vm.IsSignedIn && vm.Message.Contains("inconsistent"), "User mismatch must fail");
            Pass("inconsistent login identity rejected");
        }

        if (args.Contains("--live-auth"))
        {
            foreach (string role in new[] { "owner", "manager", "cashier" })
            {
                string password = Environment.GetEnvironmentVariable(role.ToUpperInvariant() + "_PASSWORD")
                    ?? throw new Exception("Supply role passwords through environment variables for live checks.");
                using var api = new ApiClient("http://localhost:3000/");
                await api.LoginAsync(role + "@delivergate.local", password);
                Assert(api.Session.User?.Role == role, "Real user role");
                await api.RefreshSessionAsync();
                api.Session.Clear();
                Assert(!api.Session.IsSignedIn, "Real session cleared");
                Pass($"live {role} login, /me and local logout");
            }
            using var invalidApi = new ApiClient("http://localhost:3000/");
            var invalidVm = new AuthViewModel(invalidApi) { Email = "owner@delivergate.local" };
            await invalidVm.LoginAsync("intentionally-incorrect-" + Guid.NewGuid());
            Assert(!invalidVm.IsSignedIn && invalidVm.Message.Contains("Invalid email or password"), "Real wrong password");
            Pass("live invalid credentials");
        }
        Console.WriteLine($"All {_passed} authentication checks passed.");
    }
}
