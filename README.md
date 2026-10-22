# Delivergate Backend Assessment

This repository contains my solution for the Delivergate Backend Engineer
Intern technical assessment. It is a REST API for a small ice cream shop and
covers authentication, role-based access, inventory management, and customer
management.

## What the API can do

- Authenticate users with email and password
- Issue JWT access tokens
- Restrict actions based on the user's role
- List the available product categories
- Create, view, search, update, and delete inventory items
- Create, view, search, update, and delete customers
- Store data in a local SQLite database
- Seed the initial users and categories on first startup

## Technology used

- Go
- Fiber
- GORM
- SQLite
- JWT
- bcrypt

## Roles and permissions

| Action | Owner | Manager | Cashier |
| --- | :---: | :---: | :---: |
| View categories and inventory | Yes | Yes | Yes |
| Create inventory items | Yes | Yes | No |
| Update inventory items | Yes | Yes | Yes |
| Delete inventory items | Yes | No | No |
| View customers | Yes | Yes | Yes |
| Create and update customers | Yes | Yes | Yes |
| Delete customers | Yes | No | No |

## Getting started

### 1. Clone the repository

```powershell
git clone https://github.com/rimzy2002/delivergate-backend-assessment.git
cd delivergate-backend-assessment
```

### 2. Create the environment file

Copy the example file and keep the new `.env` file private:

```powershell
Copy-Item .env.example .env
```

Open `.env` and replace the example values with your local development
values. `JWT_SECRET` must contain at least 32 characters.

```env
PORT=3000
DATABASE_PATH=delivergate.db
JWT_SECRET=replace_with_a_random_secret_of_at_least_32_characters

OWNER_PASSWORD=replace_with_owner_password
MANAGER_PASSWORD=replace_with_manager_password
CASHIER_PASSWORD=replace_with_cashier_password
```

### 3. Download the dependencies

```powershell
go mod download
```

### 4. Start the API

```powershell
go run .
```

The API will be available at `http://localhost:3000`. You can confirm that it
is running by opening `http://localhost:3000/health`.

The SQLite database, tables, categories, and demo users are created
automatically when the application starts.

## Demo users

| Role | Email | Password |
| --- | --- | --- |
| Owner | `owner@delivergate.local` | Value of `OWNER_PASSWORD` in `.env` |
| Manager | `manager@delivergate.local` | Value of `MANAGER_PASSWORD` in `.env` |
| Cashier | `cashier@delivergate.local` | Value of `CASHIER_PASSWORD` in `.env` |

Example login request:

```http
POST /api/auth/login
Content-Type: application/json
```

```json
{
  "email": "owner@delivergate.local",
  "password": "your local owner password"
}
```

Use the returned token on protected routes:

```http
Authorization: Bearer YOUR_TOKEN
```

## API endpoints

### General and authentication

| Method | Endpoint | Description |
| --- | --- | --- |
| `GET` | `/health` | Check whether the API is running |
| `POST` | `/api/auth/login` | Log in and receive a JWT |
| `GET` | `/api/auth/me` | Return the authenticated user |
| `GET` | `/api/categories` | List all categories |

### Inventory

| Method | Endpoint | Description |
| --- | --- | --- |
| `GET` | `/api/items` | List items; use `?search=value` to search by name |
| `GET` | `/api/items/:id` | Get one item |
| `POST` | `/api/items` | Create an item |
| `PATCH` | `/api/items/:id` | Update an item |
| `DELETE` | `/api/items/:id` | Delete an item |

### Customers

| Method | Endpoint | Description |
| --- | --- | --- |
| `GET` | `/api/customers` | List customers; use `?search=value` to search |
| `GET` | `/api/customers/:id` | Get one customer |
| `POST` | `/api/customers` | Create a customer |
| `PATCH` | `/api/customers/:id` | Update a customer |
| `DELETE` | `/api/customers/:id` | Delete a customer |

## Testing with Postman

The [`postman`](postman) directory contains the collection and a sanitized
local environment.

1. Start the API with `go run .`.
2. Import both JSON files from the `postman` directory.
3. Select the **Delivergate Local** environment.
4. Enter the three password values from your private `.env` file.
5. Run **Delivergate Backend API** in its saved order.

The collection covers all 28 requests, including successful operations,
permission checks, search requests, and cleanup. Passwords and generated JWTs
are intentionally empty in the exported environment.

## Automated tests

Run the Go test suite with:

```powershell
go test ./...
```

The tests cover login, JWT authentication, role permissions, inventory CRUD,
customer CRUD, validation, and search behaviour.

## Database diagram

The [`docs`](docs) directory contains the ER diagram in three formats:

- `delivergate-er-diagram.dbml` - editable source
- `delivergate-er-diagram.png` - image preview
- `delivergate-er-diagram.pdf` - submission-ready document

The database has four tables: `users`, `categories`, `items`, and `customers`.
A category can contain many items through `items.category_id`.

## Project structure

```text
config/       Environment configuration
database/     Database connection and seed data
handlers/     HTTP request handlers and handler tests
middleware/   JWT authentication, role checks, and tests
models/       GORM database models
docs/         ER diagram files
postman/      Postman collection and local environment
frontend/     Optional C# WPF desktop client (in progress)
main.go       Application routes and startup
```

## Desktop frontend (in progress)

The optional Windows desktop client is in [`frontend`](frontend/README.md).
F1 provides a simple WPF window and configurable API address. Login and CRUD
screens are planned for the following stages; they are not implemented yet.

To run the current window, install the .NET 10 SDK on Windows and use:

```powershell
cd frontend
dotnet run --project .\Delivergate.Desktop\Delivergate.Desktop.csproj
```

## Security notes

The `.env` file, SQLite database files, generated tokens, and executable files
must stay out of Git. The repository includes only `.env.example`, with safe
placeholder values. Do not commit real passwords, JWT secrets, access tokens,
or GitHub credentials.
