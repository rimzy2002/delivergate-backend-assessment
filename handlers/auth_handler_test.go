package handlers

import (
	"net/http"
	"testing"

	"github.com/gofiber/fiber/v2"
	"golang.org/x/crypto/bcrypt"

	"github.com/rimzy2002/delivergate-backend-assessment/middleware"
	"github.com/rimzy2002/delivergate-backend-assessment/models"
)

func TestAuthHandlerLoginAndMe(t *testing.T) {
	db := newTestDB(t, &models.User{})

	password := "TestPassword123!"

	passwordHash, err := bcrypt.GenerateFromPassword(
		[]byte(password),
		bcrypt.DefaultCost,
	)
	if err != nil {
		t.Fatalf("hash password: %v", err)
	}

	user := models.User{
		Name:         "Test Owner",
		Email:        "owner@test.local",
		PasswordHash: string(passwordHash),
		Role:         models.RoleOwner,
	}

	if err := db.Create(&user).Error; err != nil {
		t.Fatalf("create test user: %v", err)
	}

	app := fiber.New()
	handler := NewAuthHandler(db, handlerTestJWTSecret)

	app.Post("/api/auth/login", handler.Login)
	app.Get(
		"/api/auth/me",
		middleware.Authenticate(handlerTestJWTSecret),
		handler.Me,
	)

	status, responseBody := performJSONRequest(
		t,
		app,
		http.MethodPost,
		"/api/auth/login",
		"",
		map[string]any{
			"email":    "OWNER@TEST.LOCAL",
			"password": password,
		},
	)

	requireStatus(
		t,
		status,
		fiber.StatusOK,
		responseBody,
	)

	var loginResponse struct {
		Token string `json:"token"`
		User  struct {
			ID    uint   `json:"id"`
			Name  string `json:"name"`
			Email string `json:"email"`
			Role  string `json:"role"`
		} `json:"user"`
	}

	decodeJSON(t, responseBody, &loginResponse)

	if loginResponse.Token == "" {
		t.Fatal("expected login token")
	}

	if loginResponse.User.Role != models.RoleOwner {
		t.Fatalf(
			"role = %q, want %q",
			loginResponse.User.Role,
			models.RoleOwner,
		)
	}

	status, responseBody = performJSONRequest(
		t,
		app,
		http.MethodGet,
		"/api/auth/me",
		loginResponse.Token,
		nil,
	)

	requireStatus(
		t,
		status,
		fiber.StatusOK,
		responseBody,
	)

	status, responseBody = performJSONRequest(
		t,
		app,
		http.MethodPost,
		"/api/auth/login",
		"",
		map[string]any{
			"email":    "owner@test.local",
			"password": "wrong-password",
		},
	)

	requireStatus(
		t,
		status,
		fiber.StatusUnauthorized,
		responseBody,
	)

	status, responseBody = performJSONRequest(
		t,
		app,
		http.MethodPost,
		"/api/auth/login",
		"",
		map[string]any{
			"email":    "",
			"password": "",
		},
	)

	requireStatus(
		t,
		status,
		fiber.StatusBadRequest,
		responseBody,
	)
}
