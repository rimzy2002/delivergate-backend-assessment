using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Delivergate.Desktop.Models;
using Delivergate.Desktop.Services;
using Delivergate.Desktop.ViewModels;

internal static class CustomerChecks
{
    private static HttpResponseMessage Reply(object body, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = JsonContent.Create(body) };
    private static void SignIn(ApiClient api, string role = "owner", int id = 1) =>
        api.Session.Set("test-token-" + id, new AuthUser { Id = id, Name = "Test", Role = role });

    public static async Task RunAsync(string[] args)
    {
        int passed = 0;
        void Check(bool ok, string name)
        {
            if (!ok) throw new Exception(name);
            Console.WriteLine($"PASS customers: {name}");
            passed++;
        }
        foreach (string role in new[] { "owner", "manager", "cashier" })
        {
            var data = new List<Customer>
            {
                new() { Id = 1, Name = "Alice", Email = "alice@example.com", Phone = "0771234567", Address = "Colombo" },
                new() { Id = 2, Name = "Bob", Email = "bob@example.com", Phone = "+94 112345678", Address = "Kandy" }
            };
            int calls = 0, writes = 0;
            using var api = new ApiClient(new HttpClient(new StubHandler(async (request, _) =>
            {
                calls++;
                if (request.Headers.Authorization?.ToString() != "Bearer test-token-1") throw new Exception("Customer bearer token missing");
                if (request.Method == HttpMethod.Get) return Reply(new { customers = data });
                writes++;
                if (request.Method == HttpMethod.Delete)
                {
                    int deleteId = int.Parse(request.RequestUri!.Segments.Last());
                    data.RemoveAll(customer => customer.Id == deleteId);
                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                }
                CustomerInput input = (await request.Content!.ReadFromJsonAsync<CustomerInput>())!;
                int id = request.Method == HttpMethod.Post ? 3 : int.Parse(request.RequestUri!.Segments.Last());
                if (data.Any(customer => customer.Id != id && customer.Email.Equals(input.Email, StringComparison.OrdinalIgnoreCase)))
                    return Reply(new { error = "duplicate" }, HttpStatusCode.Conflict);
                var saved = new Customer { Id = id, Name = input.Name, Email = input.Email, Phone = input.Phone, Address = input.Address };
                data.RemoveAll(customer => customer.Id == id);
                data.Add(saved);
                return Reply(new { customer = saved }, request.Method == HttpMethod.Post ? HttpStatusCode.Created : HttpStatusCode.OK);
            })), "http://localhost:3000/");
            var vm = new CustomersViewModel(api);
            await vm.LoadAsync();
            Check(calls == 0 && !vm.CanLoad, $"{role} signed-out access blocked");
            SignIn(api, role);
            await vm.LoadAsync();
            Check(vm.HasLoaded && vm.Customers.Count == 2 && vm.CanCreate && !vm.CanEdit, $"{role} listing and initial actions");
            vm.SearchText = " ALI ";
            Check(vm.Customers.Count == 1 && vm.Customers[0].Id == 1, $"{role} case-insensitive name search");
            vm.SearchText = "bob@";
            Check(vm.Customers.Count == 1 && vm.Customers[0].Id == 2, $"{role} email search");
            vm.SearchText = "077";
            Check(vm.Customers.Count == 1 && calls == 1, $"{role} phone search runs locally");
            vm.SelectedCustomer = vm.Customers[0];
            vm.SearchText = "no-match";
            Check(vm.Customers.Count == 0 && vm.SelectedCustomer is null && vm.Message.Contains("No customers match"), $"{role} no results clears selection");
            vm.ClearSearch();
            vm.SelectedCustomer = vm.Customers[0];
            Check(vm.CanEdit && vm.CanDelete == (role == "owner"), $"{role} selected permissions");
            vm.BeginEdit();
            Check(vm.EditName == "Alice" && vm.EditAddress == "Colombo" && !vm.CanLoad, $"{role} prefilled draft");
            vm.EditName = "Unsaved";
            vm.CancelEdit();
            Check(writes == 0 && vm.Customers[0].Name == "Alice", $"{role} cancel preserves record");
            vm.BeginCreate();
            await vm.SaveAsync();
            Check(writes == 0 && vm.Message.Contains("name"), $"{role} required name");
            vm.EditName = "Carol"; vm.EditEmail = "bad-email"; vm.EditPhone = "0770000000";
            await vm.SaveAsync();
            Check(writes == 0 && vm.Message.Contains("email"), $"{role} email validation");
            vm.EditEmail = "carol@example.com"; vm.EditPhone = " ";
            await vm.SaveAsync();
            Check(writes == 0 && vm.Message.Contains("phone"), $"{role} required phone");
            vm.EditPhone = new string('1', 31);
            await vm.SaveAsync();
            Check(writes == 0, $"{role} phone length");
            vm.EditPhone = "0770000000"; vm.EditAddress = new string('a', 301);
            await vm.SaveAsync();
            Check(writes == 0, $"{role} address length");
            vm.EditAddress = ""; vm.EditEmail = "ALICE@example.com";
            await vm.SaveAsync();
            Check(writes == 1 && vm.IsEditing && vm.EditName == "Carol" && vm.Message.Contains("already exists"), $"{role} duplicate-email conflict preserves draft");
            vm.EditName = " Carol "; vm.EditEmail = " CAROL@example.com "; vm.EditPhone = " 0770000000 ";
            await vm.SaveAsync();
            Check(!vm.IsEditing && vm.Customers.Count == 3 && vm.SelectedCustomer?.Email == "carol@example.com" &&
                vm.SelectedCustomer.Name == "Carol" && vm.SelectedCustomer.Phone == "0770000000", $"{role} create and normalization");
            vm.BeginEdit(); vm.EditAddress = "New address"; await vm.SaveAsync();
            Check(vm.SelectedCustomer?.Address == "New address" && vm.Message.Contains("updated"), $"{role} edit persists response");
            vm.BeginEdit(); vm.EditAddress = ""; await vm.SaveAsync();
            Check(vm.SelectedCustomer?.Address == "", $"{role} can clear optional address");
            int previousWrites = writes;
            await vm.DeleteAsync(3, false);
            Check(writes == previousWrites, $"{role} delete requires confirmation");
            await vm.DeleteAsync(999, true);
            Check(writes == previousWrites, $"{role} wrong delete target blocked");
            await vm.DeleteAsync(3, true);
            Check(writes == previousWrites + (role == "owner" ? 1 : 0) && vm.Customers.Count == (role == "owner" ? 2 : 3), $"{role} delete role guard and 204");
            api.Session.Clear();
            Check(vm.Customers.Count == 0 && vm.EditEmail == "" && vm.SelectedCustomer is null && !vm.CanCreate, $"{role} logout removes personal data");
        }

        foreach (int status in new[] { 200, 401, 403, 500, 0, -1, -2 })
        {
            using var api = new ApiClient(new HttpClient(new StubHandler((_, _) =>
            {
                if (status == 0) throw new HttpRequestException();
                if (status == -1) throw new TaskCanceledException();
                if (status == -2) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = new StringContent("bad JSON", Encoding.UTF8, "application/json") });
                return Task.FromResult(Reply(new { customers = Array.Empty<Customer>() }, (HttpStatusCode)status));
            })), "http://localhost:3000/");
            SignIn(api);
            var vm = new CustomersViewModel(api);
            await vm.LoadAsync();
            Check(!vm.IsBusy && vm.Customers.Count == 0 &&
                (status == 200 ? vm.HasLoaded && vm.Message.Contains("No customers") : !vm.HasLoaded) &&
                (status != 401 || !api.Session.IsSignedIn), $"load error/empty state {status}");
        }

        var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        int pendingWrites = 0;
        using (var api = new ApiClient(new HttpClient(new StubHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Get) return Task.FromResult(Reply(new { customers = Array.Empty<Customer>() }));
            pendingWrites++; return pending.Task;
        })), "http://localhost:3000/"))
        {
            SignIn(api);
            var vm = new CustomersViewModel(api);
            await vm.LoadAsync(); vm.BeginCreate();
            vm.EditName = "Test"; vm.EditEmail = "test@example.com"; vm.EditPhone = "123";
            Task first = vm.SaveAsync(); await vm.SaveAsync();
            Check(pendingWrites == 1 && vm.IsBusy && !vm.CanSave, "duplicate customer save blocked");
            api.Session.Clear(); SignIn(api, "cashier", 2);
            pending.SetResult(Reply(new { customer = new Customer { Id = 3, Name = "Test", Email = "test@example.com", Phone = "123" } }));
            await first;
            Check(vm.Customers.Count == 0 && !vm.IsEditing && vm.EditEmail == "" && api.Session.User?.Id == 2, "late customer save cannot leak to new account");
        }

        foreach (int status in new[] { 400, 401, 403, 404, 409, 500, 0 })
        {
            using var api = new ApiClient(new HttpClient(new StubHandler((request, _) =>
            {
                if (request.Method == HttpMethod.Get) return Task.FromResult(Reply(new { customers = Array.Empty<Customer>() }));
                if (status == 0) throw new TaskCanceledException();
                return Task.FromResult(Reply(new { }, (HttpStatusCode)status));
            })), "http://localhost:3000/");
            SignIn(api);
            var vm = new CustomersViewModel(api);
            await vm.LoadAsync(); vm.BeginCreate(); vm.EditName = "Test"; vm.EditEmail = "test@example.com"; vm.EditPhone = "123";
            await vm.SaveAsync();
            Check(status == 401 ? !api.Session.IsSignedIn && vm.EditEmail == "" :
                status is 0 or 500 ? vm.RequiresRefresh && !vm.CanCreate && vm.CanLoad :
                vm.IsEditing && vm.EditEmail == "test@example.com" && vm.CanSave, $"customer write error state {status}");
        }

        foreach (object invalid in new object[] { new { }, new { customers = (object?)null }, new { customers = new[] { new { id = 0 } } } })
        {
            using var api = new ApiClient(new HttpClient(new StubHandler((_, _) => Task.FromResult(Reply(invalid)))), "http://localhost:3000/");
            SignIn(api);
            var vm = new CustomersViewModel(api);
            await vm.LoadAsync();
            Check(!vm.HasLoaded && vm.CanLoad && vm.Message.Contains("invalid customer data"), "invalid customer wrapper/row is not treated as empty");
        }

        bool offline = false;
        using (var api = new ApiClient(new HttpClient(new StubHandler((_, _) =>
        {
            if (offline) throw new HttpRequestException();
            return Task.FromResult(Reply(new { customers = new[] { new Customer { Id = 1, Name = "Test", Email = "test@example.com", Phone = "123" } } }));
        })), "http://localhost:3000/"))
        {
            SignIn(api);
            var vm = new CustomersViewModel(api);
            await vm.LoadAsync();
            offline = true;
            await vm.LoadAsync();
            Check(!vm.HasLoaded && vm.Customers.Count == 0 && vm.CanLoad, "failed refresh clears stale personal data");
            offline = false;
            await vm.LoadAsync();
            Check(vm.HasLoaded && vm.Customers.Count == 1, "customer load recovers after network error");
            api.Session.Set("unknown-token", new AuthUser { Id = 1, Role = "unknown" });
            Check(!vm.CanLoad && !vm.CanCreate && vm.Customers.Count == 0, "unknown customer role denied and data cleared");
        }

        if (args.Contains("--live-customers"))
        {
            string url = Environment.GetEnvironmentVariable("F6_TEST_API_URL") ?? throw new Exception("Use isolated backend for customer CRUD.");
            using var owner = new ApiClient(url);
            await owner.LoginAsync("owner@delivergate.local", Environment.GetEnvironmentVariable("OWNER_PASSWORD")!);
            foreach (string role in new[] { "owner", "manager", "cashier" })
            {
                using var client = new ApiClient(url);
                await client.LoginAsync(role + "@delivergate.local", Environment.GetEnvironmentVariable(role.ToUpperInvariant() + "_PASSWORD")!);
                string email = role + "-" + Guid.NewGuid().ToString("N") + "@example.com";
                Customer created = await client.SaveCustomerAsync(null, new CustomerInput("F7 Test", email, "+94 770000000", "Test address"));
                try
                {
                    Check((await client.GetCustomersAsync()).Any(customer => customer.Id == created.Id), $"live {role} create/list");
                    Customer updated = await client.SaveCustomerAsync(created.Id, new CustomerInput("Edited test", email, "0770000001", ""));
                    Check(updated.Name == "Edited test" && updated.Address == "", $"live {role} edit and clear address");
                    bool duplicate = false;
                    try { await client.SaveCustomerAsync(null, new CustomerInput("Duplicate", email.ToUpperInvariant(), "123", "")); }
                    catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Conflict) { duplicate = true; }
                    Check(duplicate, $"live {role} case-insensitive duplicate email rejected");
                    if (role != "owner")
                    {
                        bool denied = false;
                        try { await client.DeleteCustomerAsync(created.Id); }
                        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Forbidden) { denied = true; }
                        Check(denied, $"live backend blocks {role} delete");
                    }
                }
                finally { await owner.DeleteCustomerAsync(created.Id); }
                Check(!(await owner.GetCustomersAsync()).Any(customer => customer.Id == created.Id), $"live owner delete after {role} tests");
            }
        }
        Console.WriteLine($"All {passed} customer checks passed.");
    }
}
