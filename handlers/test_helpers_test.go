package handlers

import (
	"bytes"
	"encoding/json"
	"fmt"
	"io"
	"net/http/httptest"
	"strconv"
	"strings"
	"testing"
	"time"

	"github.com/glebarez/sqlite"
	"github.com/gofiber/fiber/v2"
	"github.com/golang-jwt/jwt/v5"
	"gorm.io/gorm"
	"gorm.io/gorm/logger"
)

const handlerTestJWTSecret = "handler-test-secret-with-at-least-32-characters"

func newTestDB(t *testing.T, tables ...any) *gorm.DB {
	t.Helper()

	databaseName := strings.NewReplacer(
		"/", "_",
		" ", "_",
	).Replace(t.Name())

	dsn := fmt.Sprintf(
		"file:%s?mode=memory&cache=shared",
		databaseName,
	)

	db, err := gorm.Open(sqlite.Open(dsn), &gorm.Config{
		Logger: logger.Default.LogMode(logger.Silent),
	})
	if err != nil {
		t.Fatalf("connect test database: %v", err)
	}

	if err := db.AutoMigrate(tables...); err != nil {
		t.Fatalf("migrate test database: %v", err)
	}

	sqlDB, err := db.DB()
	if err != nil {
		t.Fatalf("retrieve SQL database: %v", err)
	}

	t.Cleanup(func() {
		_ = sqlDB.Close()
	})

	return db
}

func performJSONRequest(
	t *testing.T,
	app *fiber.App,
	method string,
	path string,
	token string,
	body any,
) (int, []byte) {
	t.Helper()

	var requestBody io.Reader

	if body != nil {
		payload, err := json.Marshal(body)
		if err != nil {
			t.Fatalf("encode request body: %v", err)
		}

		requestBody = bytes.NewReader(payload)
	}

	request := httptest.NewRequest(method, path, requestBody)

	if body != nil {
		request.Header.Set("Content-Type", "application/json")
	}

	if token != "" {
		request.Header.Set("Authorization", "Bearer "+token)
	}

	response, err := app.Test(request, -1)
	if err != nil {
		t.Fatalf("send request: %v", err)
	}
	defer response.Body.Close()

	responseBody, err := io.ReadAll(response.Body)
	if err != nil {
		t.Fatalf("read response body: %v", err)
	}

	return response.StatusCode, responseBody
}

func requireStatus(
	t *testing.T,
	actual int,
	expected int,
	responseBody []byte,
) {
	t.Helper()

	if actual != expected {
		t.Fatalf(
			"status = %d, want %d; body = %s",
			actual,
			expected,
			string(responseBody),
		)
	}
}

func decodeJSON(t *testing.T, data []byte, destination any) {
	t.Helper()

	if err := json.Unmarshal(data, destination); err != nil {
		t.Fatalf("decode JSON response: %v; body = %s", err, string(data))
	}
}

func createHandlerTestToken(
	t *testing.T,
	userID uint,
	role string,
) string {
	t.Helper()

	token := jwt.NewWithClaims(jwt.SigningMethodHS256, jwt.MapClaims{
		"user_id": userID,
		"role":    role,
		"exp":     time.Now().Add(time.Hour).Unix(),
	})

	signedToken, err := token.SignedString(
		[]byte(handlerTestJWTSecret),
	)
	if err != nil {
		t.Fatalf("sign test token: %v", err)
	}

	return signedToken
}

func formatTestID(id uint) string {
	return strconv.FormatUint(uint64(id), 10)
}
