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
