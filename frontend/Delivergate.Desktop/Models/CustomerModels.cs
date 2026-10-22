namespace Delivergate.Desktop.Models;

public sealed class Customer
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string Email { get; init; } = "";
    public string Phone { get; init; } = "";
    public string Address { get; init; } = "";
}

public sealed record CustomerInput(string Name, string Email, string Phone, string Address);

public sealed class CustomersResponse
{
    public List<Customer>? Customers { get; init; }
}

public sealed class CustomerResponse
{
    public Customer? Customer { get; init; }
}
