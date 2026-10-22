using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Delivergate.Desktop.Models;
using Delivergate.Desktop.Services;
using Delivergate.Desktop.ViewModels;

internal static class InventoryEditingChecks
{
    private const string Item = "{\"id\":1,\"name\":\"Cone\",\"description\":\"Original\",\"category_id\":1,\"category\":{\"id\":1,\"name\":\"Vanilla\"},\"price\":100,\"quantity\":5}";
    private static HttpResponseMessage Reply(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    public static async Task RunAsync(string[] args)
    {
        int passed = 0;
        void Check(bool ok, string name)
        {
            if (!ok) throw new Exception(name);
            Console.WriteLine($"PASS inventory editing: {name}");
            passed++;
        }

        foreach (string role in new[] { "owner", "manager", "cashier" })
        {
            int writes = 0;
            HttpMethod? lastMethod = null;
            using var api = new ApiClient(new HttpClient(new StubHandler(async (request, _) =>
            {
                if (request.Method == HttpMethod.Get)
                    return Reply(request.RequestUri!.AbsolutePath.EndsWith("categories")
                        ? "{\"categories\":[{\"id\":1,\"name\":\"Vanilla\"}]}" : "{\"items\":[" + Item + "]}");
                writes++;
                lastMethod = request.Method;
                Check(request.Headers.Authorization?.ToString() == "Bearer test-token", $"{role} write bearer header");
                if (request.Method == HttpMethod.Delete) return new HttpResponseMessage(HttpStatusCode.NoContent);
                using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
                Check(json.RootElement.GetProperty("category_id").GetInt32() == 1 &&
                    json.RootElement.GetProperty("price").ValueKind == JsonValueKind.Number, $"{role} numeric JSON and category_id");
                return Reply("{\"item\":" + Item + "}", request.Method == HttpMethod.Post ? HttpStatusCode.Created : HttpStatusCode.OK);
            })), "http://localhost:3000/");
            api.Session.Set("test-token", new AuthUser { Id = 1, Name = "Test", Role = role });
            var vm = new InventoryViewModel(api);
            await vm.LoadAsync();
            Check(vm.CanCreate == (role != "cashier") && !vm.CanEdit && !vm.CanDelete, $"{role} action availability before selection");
            vm.SelectedItem = vm.Items[0];
            Check(vm.CanEdit && vm.CanDelete == (role == "owner"), $"{role} edit/delete permissions");
            vm.BeginEdit();
            Check(vm.EditName == "Cone" && vm.EditPrice == "100" && !vm.CanLoad, $"{role} prefilled form locks refresh");
            vm.EditName = "";
            await vm.SaveAsync();
            Check(writes == 0 && vm.Message.Contains("name"), $"{role} invalid name blocked");
            vm.EditName = "Cone";
            foreach (string invalidPrice in new[] { "0", "-1", "abc", "NaN", "1,20" })
            {
                vm.EditPrice = invalidPrice;
                await vm.SaveAsync();
                Check(writes == 0 && vm.IsEditing, $"{role} rejects price {invalidPrice}");
            }
            vm.EditPrice = "125.50";
            vm.EditQuantity = "1.5";
            await vm.SaveAsync();
            Check(writes == 0, $"{role} fractional quantity blocked");
            vm.EditQuantity = "-1";
            await vm.SaveAsync();
            Check(writes == 0, $"{role} negative quantity blocked");
            vm.EditQuantity = "0";
            vm.EditCategoryId = 0;
            await vm.SaveAsync();
            Check(writes == 0, $"{role} invalid category blocked");
            vm.CancelEdit();
            Check(!vm.IsEditing && writes == 0 && vm.Items[0].Name == "Cone", $"{role} cancel never mutates original");
            vm.BeginEdit();
            await vm.SaveAsync();
            Check(writes == 1 && lastMethod == HttpMethod.Patch && !vm.IsEditing && vm.Message.Contains("updated"), $"{role} PATCH success");
            await vm.DeleteAsync(1, false);
            Check(writes == 1, $"{role} deletion requires confirmation");
            await vm.DeleteAsync(1, true);
            Check(writes == (role == "owner" ? 2 : 1) && vm.Items.Count == (role == "owner" ? 0 : 1), $"{role} guarded delete with 204");
            vm.BeginCreate();
            if (role == "cashier")
            {
                await vm.SaveAsync();
                Check(!vm.IsEditing && writes == 1, "cashier cannot start create even from code");
            }
            else
            {
                vm.EditName = "New cone"; vm.EditPrice = "150.50"; vm.EditQuantity = "0";
                await vm.SaveAsync();
                Check(lastMethod == HttpMethod.Post && vm.Message.Contains("created") && vm.Items.Count == 1, $"{role} POST success updates list");
            }
        }

        foreach (int errorCode in new[] { 400, 401, 403, 404, 500, 0 })
        {
            using var api = new ApiClient(new HttpClient(new StubHandler((request, _) =>
            {
                if (request.Method == HttpMethod.Get) return Task.FromResult(Reply(request.RequestUri!.AbsolutePath.EndsWith("categories")
                    ? "{\"categories\":[{\"id\":1,\"name\":\"Vanilla\"}]}" : "{\"items\":[" + Item + "]}"));
                if (errorCode == 0) throw new TaskCanceledException();
                return Task.FromResult(Reply("{}", (HttpStatusCode)errorCode));
            })), "http://localhost:3000/");
            api.Session.Set("test-token", new AuthUser { Id = 1, Name = "Test", Role = "owner" });
            var vm = new InventoryViewModel(api);
            await vm.LoadAsync(); vm.SelectedItem = vm.Items[0]; vm.BeginEdit();
            await vm.SaveAsync();
            if (errorCode == 401) Check(!api.Session.IsSignedIn && !vm.IsEditing && vm.Items.Count == 0, "write 401 clears draft and rows");
            else if (errorCode is 0 or 500)
            {
                Check(vm.RequiresRefresh && !vm.CanCreate && !vm.CanEdit && !vm.CanDelete && vm.CanLoad, $"uncertain result {errorCode} blocks repeated writes");
                await vm.LoadAsync();
                Check(!vm.RequiresRefresh && vm.CanCreate, $"refresh resolves uncertain result {errorCode}");
            }
            else Check(vm.IsEditing && vm.EditName == "Cone" && vm.CanSave && !vm.Message.Contains("password"), $"HTTP {errorCode} preserves draft and explains item error");
        }

        var pendingWrite = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        int pendingCalls = 0;
        using (var api = new ApiClient(new HttpClient(new StubHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Get) return Task.FromResult(Reply(request.RequestUri!.AbsolutePath.EndsWith("categories")
                ? "{\"categories\":[{\"id\":1,\"name\":\"Vanilla\"}]}" : "{\"items\":[" + Item + "]}"));
            pendingCalls++;
            return pendingWrite.Task;
        })), "http://localhost:3000/"))
        {
            api.Session.Set("test-token", new AuthUser { Id = 1, Name = "Owner", Role = "owner" });
            var vm = new InventoryViewModel(api);
            await vm.LoadAsync(); vm.SelectedItem = vm.Items[0]; vm.BeginEdit();
            vm.EditName = new string('a', 151);
            await vm.SaveAsync();
            Check(pendingCalls == 0, "overlong name blocked");
            vm.EditName = "Cone"; vm.EditDescription = new string('a', 501);
            await vm.SaveAsync();
            Check(pendingCalls == 0, "overlong description blocked");
            vm.EditDescription = "";
            Task first = vm.SaveAsync();
            await vm.SaveAsync();
            Check(pendingCalls == 1 && vm.IsBusy && !vm.CanCancelEdit && !vm.CanSave, "duplicate Save blocked while writing");
            api.Session.Clear();
            api.Session.Set("new-token", new AuthUser { Id = 2, Name = "Manager", Role = "manager" });
            pendingWrite.SetResult(Reply("{\"item\":" + Item + "}"));
            await first;
            Check(!vm.IsEditing && vm.Items.Count == 0 && !vm.HasLoaded && api.Session.User?.Id == 2,
                "late save cannot populate next account's screen");
        }

        if (args.Contains("--live-crud"))
        {
            string url = Environment.GetEnvironmentVariable("F6_TEST_API_URL") ?? throw new Exception("Use an isolated test backend for live CRUD.");
            using var owner = new ApiClient(url);
            await owner.LoginAsync("owner@delivergate.local", Environment.GetEnvironmentVariable("OWNER_PASSWORD")!);
            int categoryId = (await owner.GetCategoriesAsync())[0].Id;
            InventoryItem created = await owner.SaveItemAsync(null, new ItemInput("F6 test item", "Test only", categoryId, 125.50m, 0));
            try
            {
                Check((await owner.GetItemsAsync()).Any(item => item.Id == created.Id), "live owner create persisted");
                foreach (string role in new[] { "manager", "cashier" })
                {
                    using var client = new ApiClient(url);
                    await client.LoginAsync(role + "@delivergate.local", Environment.GetEnvironmentVariable(role.ToUpperInvariant() + "_PASSWORD")!);
                    InventoryItem updated = await client.SaveItemAsync(created.Id, new ItemInput("Edited by " + role, "Updated", categoryId, 175.25m, 2));
                    Check(updated.Name == "Edited by " + role && updated.Quantity == 2, $"live {role} edit");
                    bool rejected = false;
                    try { await client.DeleteItemAsync(created.Id); } catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Forbidden) { rejected = true; }
                    Check(rejected, $"live backend blocks {role} delete");
                    if (role == "cashier")
                    {
                        rejected = false;
                        try { await client.SaveItemAsync(null, new ItemInput("Forbidden", "", categoryId, 1m, 0)); }
                        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Forbidden) { rejected = true; }
                        Check(rejected, "live backend blocks cashier create");
                    }
                    else
                    {
                        InventoryItem managerItem = await client.SaveItemAsync(null, new ItemInput("Manager created", "", categoryId, 1m, 0));
                        await owner.DeleteItemAsync(managerItem.Id);
                        Check(true, "live manager create and owner cleanup");
                    }
                }
            }
            finally { await owner.DeleteItemAsync(created.Id); }
            Check(!(await owner.GetItemsAsync()).Any(item => item.Id == created.Id), "live owner delete persisted");
        }
        Console.WriteLine($"All {passed} inventory editing checks passed.");
    }
}
