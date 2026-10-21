package middleware

import (
	"net/http/httptest"
	"testing"
	"time"

	"github.com/gofiber/fiber/v2"
	"github.com/golang-jwt/jwt/v5"

	"github.com/rimzy2002/delivergate-backend-assessment/models"
)

const testJWTSecret = "test-secret-with-at-least-32-characters"

func TestAuthenticate(t *testing.T) {
	tests := []struct {
		name       string
		token      string
		wantStatus int
	}{
		{
			name:       "missing token",
			wantStatus: fiber.StatusUnauthorized,
		},
		{
			name: "valid token",
			token: createTestToken(
				t,
				models.RoleOwner,
				time.Now().Add(time.Hour),
			),
			wantStatus: fiber.StatusOK,
		},
		{
			name: "expired token",
			token: createTestToken(
				t,
				models.RoleOwner,
				time.Now().Add(-time.Hour),
			),
			wantStatus: fiber.StatusUnauthorized,
		},
	}

	for _, test := range tests {
		t.Run(test.name, func(t *testing.T) {
			app := fiber.New()
			app.Get("/protected", Authenticate(testJWTSecret), func(c *fiber.Ctx) error {
				return c.SendStatus(fiber.StatusOK)
			})

			request := httptest.NewRequest("GET", "/protected", nil)
			if test.token != "" {
				request.Header.Set("Authorization", "Bearer "+test.token)
			}

			response, err := app.Test(request)
			if err != nil {
				t.Fatalf("send request: %v", err)
			}

			if response.StatusCode != test.wantStatus {
				t.Fatalf("status = %d, want %d", response.StatusCode, test.wantStatus)
			}
		})
	}
}

func TestRequireRoles(t *testing.T) {
	tests := []struct {
		name       string
		role       string
		wantStatus int
	}{
		{
			name:       "owner is allowed",
			role:       models.RoleOwner,
			wantStatus: fiber.StatusOK,
		},
		{
			name:       "manager is forbidden",
			role:       models.RoleManager,
			wantStatus: fiber.StatusForbidden,
		},
	}

	for _, test := range tests {
		t.Run(test.name, func(t *testing.T) {
			app := fiber.New()
			app.Get(
				"/owner-only",
				Authenticate(testJWTSecret),
				RequireRoles(models.RoleOwner),
				func(c *fiber.Ctx) error {
					return c.SendStatus(fiber.StatusOK)
				},
			)

			request := httptest.NewRequest("GET", "/owner-only", nil)
			request.Header.Set(
				"Authorization",
				"Bearer "+createTestToken(t, test.role, time.Now().Add(time.Hour)),
			)

			response, err := app.Test(request)
			if err != nil {
				t.Fatalf("send request: %v", err)
			}

			if response.StatusCode != test.wantStatus {
				t.Fatalf("status = %d, want %d", response.StatusCode, test.wantStatus)
			}
		})
	}
}

func createTestToken(t *testing.T, role string, expiresAt time.Time) string {
	t.Helper()

	token := jwt.NewWithClaims(jwt.SigningMethodHS256, jwt.MapClaims{
		"user_id": 1,
		"role":    role,
		"exp":     expiresAt.Unix(),
	})

	signedToken, err := token.SignedString([]byte(testJWTSecret))
	if err != nil {
		t.Fatalf("sign token: %v", err)
	}

	return signedToken
}
