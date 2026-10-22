using System.Net;
using System.Text;
using Delivergate.Desktop.Models;
using Delivergate.Desktop.Services;
using Delivergate.Desktop.ViewModels;

internal static class InventoryChecks
{
    private const string Categories = "{\"categories\":[{\"id\":1,\"name\":\"Vanilla\"},{\"id\":2,\"name\":\"Chocolate\"}]}";
    private const string Items = """
        {"items":[
          {"id":3,"name":"Vanilla cone","description":"Classic cone","category_id":1,"category":{"id":1,"name":"Vanilla"},"price":125.50,"quantity":4},
          {"id":2,"name":"Chocolate cone","description":"Chocolate","category_id":2,"category":{"id":2,"name":"Chocolate"},"price":150,"quantity":0},
          {"id":1,"name":"Vanilla tub","description":"Tub","category_id":1,"category":{"id":1,"name":"Vanilla"},"price":300,"quantity":8}
        ]}
        """;

    private static HttpResponseMessage Reply(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static void SignIn(ApiClient api, string role = "owner", int id = 1) =>
        api.Session.Set("test-token-" + id, new AuthUser { Id = id, Name = "Test", Email = "test@example.com", Role = role });

    public static async Task RunAsync()
    {
        int passed = 0;
        void Check(bool value, string name)
        {
            if (!value) throw new Exception(name);
            Console.WriteLine($"PASS inventory: {name}");
            passed++;
        }

        int calls = 0;
        using (var api = new ApiClient(new HttpClient(new StubHandler((request, _) =>
        {
            calls++;
            if (request.Headers.Authorization?.ToString() != "Bearer test-token-1") throw new Exception("Missing token");
            if (request.Method != HttpMethod.Get) throw new Exception("Read-only requests required");
            return Task.FromResult(Reply(request.RequestUri!.AbsolutePath == "/api/categories" ? Categories : Items));
        })), "http://localhost:3000/"))
        {
            var vm = new InventoryViewModel(api);
            await vm.LoadAsync();
            Check(calls == 0 && !vm.CanLoad, "signed-out load blocked");
            SignIn(api, "cashier");
            await vm.LoadAsync();
            Check(vm.HasLoaded && vm.Items.Count == 3 && vm.CanFilter && calls == 2, "cashier loads real-shaped API responses");
            Check(vm.Items[0].CategoryId == 1 && vm.Items[0].Price == 125.50m && vm.Items[0].CategoryName == "Vanilla", "snake-case category and decimal mapping");
            Check(vm.Categories[0].Id == 0 && vm.Categories[1].Name == "Chocolate", "All categories and sorted choices");
            vm.SearchText = " CONE ";
            Check(vm.Items.Count == 2, "trimmed case-insensitive name search");
            vm.SelectedCategoryId = 2;
            Check(vm.Items.Count == 1 && vm.Items[0].Id == 2 && vm.Items[0].Quantity == 0, "combined category/search and zero stock");
            vm.SelectedItem = vm.Items[0];
            vm.SearchText = "not-present";
            Check(vm.Items.Count == 0 && vm.SelectedItem is null && vm.Message.Contains("No items match"), "empty filtered state clears selection");
            vm.ClearFilters();
            Check(vm.Items.Count == 3 && vm.SearchText == "" && vm.SelectedCategoryId == 0 && calls == 2, "local filtering sends no extra requests");
            await vm.LoadAsync();
            Check(calls == 4 && vm.Items.Count == 3, "Refresh reloads both endpoints");
            api.Session.Clear();
            Check(vm.Items.Count == 0 && vm.Categories.Count == 0 && !vm.HasLoaded && !vm.CanLoad, "logout clears inventory and categories");
        }

        foreach (var scenario in new[]
        {
            ("empty database", "{\"items\":[]}", HttpStatusCode.OK, "No inventory items", true),
            ("HTTP 500", "{}", HttpStatusCode.InternalServerError, "HTTP 500", false),
            ("HTTP 403", "{}", HttpStatusCode.Forbidden, "permission", false),
            ("invalid JSON", "not-json", HttpStatusCode.OK, "invalid inventory JSON", false),
            ("missing items wrapper", "{}", HttpStatusCode.OK, "invalid inventory data", false),
            ("null items wrapper", "{\"items\":null}", HttpStatusCode.OK, "invalid inventory data", false)
        })
        {
            using var api = new ApiClient(new HttpClient(new StubHandler((request, _) => Task.FromResult(
                request.RequestUri!.AbsolutePath.EndsWith("categories") ? Reply(Categories) : Reply(scenario.Item2, scenario.Item3)))), "http://localhost:3000/");
            SignIn(api);
            var vm = new InventoryViewModel(api);
            await vm.LoadAsync();
            Check(vm.HasLoaded == scenario.Item5 && vm.Items.Count == 0 && vm.CanLoad &&
                vm.Message.Contains(scenario.Item4), scenario.Item1);
        }

        foreach (bool timeout in new[] { false, true })
        {
            bool fail = true;
            using var api = new ApiClient(new HttpClient(new StubHandler((request, _) =>
            {
                if (fail) { if (timeout) throw new TaskCanceledException(); throw new HttpRequestException(); }
                return Task.FromResult(Reply(request.RequestUri!.AbsolutePath.EndsWith("categories") ? Categories : Items));
            })), "http://localhost:3000/");
            SignIn(api);
            var vm = new InventoryViewModel(api);
            await vm.LoadAsync();
            Check(vm.CanLoad && !vm.HasLoaded && vm.Message.Contains(timeout ? "timed out" : "Cannot reach"), timeout ? "timeout handled" : "offline handled");
            fail = false;
            await vm.LoadAsync();
            Check(vm.HasLoaded && vm.Items.Count == 3, timeout ? "timeout recovery" : "offline recovery");
        }

        using (var api = new ApiClient(new HttpClient(new StubHandler((_, _) => Task.FromResult(Reply("{}", HttpStatusCode.Unauthorized)))), "http://localhost:3000/"))
        {
            SignIn(api);
            var root = new MainWindowViewModel(api, "http://localhost:3000/");
            root.Workspace.Navigate(WorkspacePage.Inventory);
            await root.Inventory.LoadAsync();
            Check(!api.Session.IsSignedIn && root.Workspace.SelectedPage is null && root.Inventory.Items.Count == 0 &&
                root.Auth.Message.Contains("session ended"), "401 clears session, navigation, data and explains login return");
        }

        var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        int delayedCalls = 0;
        using (var api = new ApiClient(new HttpClient(new StubHandler((_, _) => { delayedCalls++; return pending.Task; })), "http://localhost:3000/"))
        {
            SignIn(api);
            var vm = new InventoryViewModel(api);
            Task first = vm.LoadAsync();
            await vm.LoadAsync();
            Check(vm.IsBusy && !vm.CanLoad && delayedCalls == 1, "duplicate refresh blocked");
            api.Session.Clear();
            SignIn(api, "manager", 2);
            pending.SetResult(Reply(Categories));
            await first;
            Check(api.Session.User?.Id == 2 && !vm.HasLoaded && vm.Items.Count == 0 && vm.CanLoad && delayedCalls == 1,
                "late response cannot restore previous user's inventory");
        }

        Console.WriteLine($"All {passed} inventory checks passed.");
    }
}
