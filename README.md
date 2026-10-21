# delivergate-backend-assessment

Backend Engineer Intern technical assessment — ice cream shop management API

## Local environment setup

Create a private `.env` file from the provided example:

```powershell
Copy-Item .env.example .env
```

The values in `.env.example` are development placeholders. You can use them
locally or replace them with your own strong development values before starting
the API:

```powershell
go run .
```

## Demo users

The application creates the following local demo users when it starts:

| Role | Email | Password source |
| --- | --- | --- |
| Owner | `owner@delivergate.local` | `OWNER_PASSWORD` in `.env` |
| Manager | `manager@delivergate.local` | `MANAGER_PASSWORD` in `.env` |
| Cashier | `cashier@delivergate.local` | `CASHIER_PASSWORD` in `.env` |

Use these credentials with:

```text
POST http://localhost:3000/api/auth/login
```

Example request body:

```json
{
  "email": "owner@delivergate.local",
  "password": "the value of OWNER_PASSWORD in your .env file"
}
```

## Security note

The `.env` file, SQLite database files, and executable files are ignored by
Git. Never commit real passwords, JWT secrets, access tokens, or GitHub
credentials. Only `.env.example` should be committed as the configuration
template.

## Postman API collection

The `postman` directory contains a complete collection and a sanitized local
environment. To run it:

1. Start the API with `go run .`.
2. Import both JSON files from the `postman` directory into Postman.
3. Select the `Delivergate Local` environment.
4. Set `owner_password`, `manager_password`, and `cashier_password` to the
   matching private values from your local `.env` file.
5. Run the `Delivergate Backend API` collection in its saved order.

The collection tests authentication, categories, inventory CRUD, customer
CRUD, role permissions, and cleanup. Passwords and generated tokens are not
stored in the exported files.

## ER diagram

The `docs` directory contains the database diagram in three formats:

- `delivergate-er-diagram.dbml` - editable DBML source
- `delivergate-er-diagram.png` - image for quick review
- `delivergate-er-diagram.pdf` - submission-ready PDF

The only database relationship is one category to many items through
`items.category_id`.
