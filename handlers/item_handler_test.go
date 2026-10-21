package handlers

import (
	"net/http"
	"testing"

	"github.com/gofiber/fiber/v2"

	"github.com/rimzy2002/delivergate-backend-assessment/middleware"
	"github.com/rimzy2002/delivergate-backend-assessment/models"
)

func TestItemCRUDAndPermissions(t *testing.T) {
	db := newTestDB(
		t,
		&models.Category{},
		&models.Item{},
	)

	category := models.Category{
		Name: "Chocolate",
	}

	if err := db.Create(&category).Error; err != nil {
		t.Fatalf("create test category: %v", err)
	}

	app := fiber.New()
	handler := NewItemHandler(db)
	authRequired := middleware.Authenticate(handlerTestJWTSecret)

	items := app.Group("/api/items", authRequired)

	items.Get("", handler.ListItems)
	items.Get("/:id", handler.GetItem)

	items.Post(
		"",
		middleware.RequireRoles(
			models.RoleOwner,
			models.RoleManager,
		),
		handler.CreateItem,
	)

	items.Patch(
		"/:id",
		middleware.RequireRoles(
			models.RoleOwner,
			models.RoleManager,
			models.RoleCashier,
		),
		handler.UpdateItem,
	)

	items.Delete(
		"/:id",
		middleware.RequireRoles(models.RoleOwner),
		handler.DeleteItem,
	)

	ownerToken := createHandlerTestToken(
		t,
		1,
		models.RoleOwner,
	)

	managerToken := createHandlerTestToken(
		t,
		2,
		models.RoleManager,
	)

	cashierToken := createHandlerTestToken(
		t,
		3,
		models.RoleCashier,
	)

	status, responseBody := performJSONRequest(
		t,
		app,
		http.MethodGet,
		"/api/items",
		"",
		nil,
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
		"/api/items",
		ownerToken,
		map[string]any{
			"name":        "Chocolate Cake",
			"description": "One kilogram cake",
			"category_id": category.ID,
			"price":       3500,
			"quantity":    10,
		},
	)

	requireStatus(
		t,
		status,
		fiber.StatusCreated,
		responseBody,
	)

	var createResponse struct {
		Item models.Item `json:"item"`
	}

	decodeJSON(t, responseBody, &createResponse)

	itemID := createResponse.Item.ID

	if itemID == 0 {
		t.Fatal("expected created item ID")
	}

	status, responseBody = performJSONRequest(
		t,
		app,
		http.MethodGet,
		"/api/items?search=chocolate",
		cashierToken,
		nil,
	)

	requireStatus(
		t,
		status,
		fiber.StatusOK,
		responseBody,
	)

	var listResponse struct {
		Items []models.Item `json:"items"`
	}

	decodeJSON(t, responseBody, &listResponse)

	if len(listResponse.Items) != 1 {
		t.Fatalf(
			"item count = %d, want 1",
			len(listResponse.Items),
		)
	}

	status, responseBody = performJSONRequest(
		t,
		app,
		http.MethodPatch,
		"/api/items/"+formatTestID(itemID),
		cashierToken,
		map[string]any{
			"quantity": 15,
		},
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
		"/api/items",
		cashierToken,
		map[string]any{
			"name":        "Forbidden Item",
			"description": "Cashier cannot create",
			"category_id": category.ID,
			"price":       100,
			"quantity":    1,
		},
	)

	requireStatus(
		t,
		status,
		fiber.StatusForbidden,
		responseBody,
	)

	status, responseBody = performJSONRequest(
		t,
		app,
		http.MethodPost,
		"/api/items",
		managerToken,
		map[string]any{
			"name":        "Manager Item",
			"description": "Manager can create",
			"category_id": category.ID,
			"price":       200,
			"quantity":    2,
		},
	)

	requireStatus(
		t,
		status,
		fiber.StatusCreated,
		responseBody,
	)

	status, responseBody = performJSONRequest(
		t,
		app,
		http.MethodDelete,
		"/api/items/"+formatTestID(itemID),
		managerToken,
		nil,
	)

	requireStatus(
		t,
		status,
		fiber.StatusForbidden,
		responseBody,
	)

	status, responseBody = performJSONRequest(
		t,
		app,
		http.MethodDelete,
		"/api/items/"+formatTestID(itemID),
		ownerToken,
		nil,
	)

	requireStatus(
		t,
		status,
		fiber.StatusNoContent,
		responseBody,
	)

	status, responseBody = performJSONRequest(
		t,
		app,
		http.MethodGet,
		"/api/items/"+formatTestID(itemID),
		ownerToken,
		nil,
	)

	requireStatus(
		t,
		status,
		fiber.StatusNotFound,
		responseBody,
	)
}
