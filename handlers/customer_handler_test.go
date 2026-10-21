package handlers

import (
	"net/http"
	"testing"

	"github.com/gofiber/fiber/v2"

	"github.com/rimzy2002/delivergate-backend-assessment/middleware"
	"github.com/rimzy2002/delivergate-backend-assessment/models"
)

func TestCustomerCRUDAndPermissions(t *testing.T) {
	db := newTestDB(t, &models.Customer{})

	app := fiber.New()
	handler := NewCustomerHandler(db)
	authRequired := middleware.Authenticate(handlerTestJWTSecret)

	customers := app.Group(
		"/api/customers",
		authRequired,
	)

	customers.Get("", handler.ListCustomers)
	customers.Get("/:id", handler.GetCustomer)

	customers.Post(
		"",
		middleware.RequireRoles(
			models.RoleOwner,
			models.RoleManager,
			models.RoleCashier,
		),
		handler.CreateCustomer,
	)

	customers.Patch(
		"/:id",
		middleware.RequireRoles(
			models.RoleOwner,
			models.RoleManager,
			models.RoleCashier,
		),
		handler.UpdateCustomer,
	)

	customers.Delete(
		"/:id",
		middleware.RequireRoles(models.RoleOwner),
		handler.DeleteCustomer,
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
		"/api/customers",
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
		"/api/customers",
		ownerToken,
		map[string]any{
			"name":    "Nimal Perera",
			"email":   "nimal@example.com",
			"phone":   "0771234567",
			"address": "Colombo",
		},
	)

	requireStatus(
		t,
		status,
		fiber.StatusCreated,
		responseBody,
	)

	var createResponse struct {
		Customer models.Customer `json:"customer"`
	}

	decodeJSON(t, responseBody, &createResponse)

	customerID := createResponse.Customer.ID

	if customerID == 0 {
		t.Fatal("expected customer ID")
	}

	status, responseBody = performJSONRequest(
		t,
		app,
		http.MethodPost,
		"/api/customers",
		ownerToken,
		map[string]any{
			"name":  "Duplicate Customer",
			"email": "NIMAL@EXAMPLE.COM",
			"phone": "0779999999",
		},
	)

	requireStatus(
		t,
		status,
		fiber.StatusConflict,
		responseBody,
	)

	status, responseBody = performJSONRequest(
		t,
		app,
		http.MethodPost,
		"/api/customers",
		ownerToken,
		map[string]any{
			"name":  "Invalid Email",
			"email": "wrong-email",
			"phone": "0779999999",
		},
	)

	requireStatus(
		t,
		status,
		fiber.StatusBadRequest,
		responseBody,
	)

	status, responseBody = performJSONRequest(
		t,
		app,
		http.MethodGet,
		"/api/customers?search=nimal",
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
		Customers []models.Customer `json:"customers"`
	}

	decodeJSON(t, responseBody, &listResponse)

	if len(listResponse.Customers) != 1 {
		t.Fatalf(
			"customer count = %d, want 1",
			len(listResponse.Customers),
		)
	}

	status, responseBody = performJSONRequest(
		t,
		app,
		http.MethodPatch,
		"/api/customers/"+formatTestID(customerID),
		cashierToken,
		map[string]any{
			"phone":   "0719876543",
			"address": "Kandy",
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
		"/api/customers",
		cashierToken,
		map[string]any{
			"name":    "Cashier Customer",
			"email":   "cashier.customer@example.com",
			"phone":   "0772222222",
			"address": "Galle",
		},
	)

	requireStatus(
		t,
		status,
		fiber.StatusCreated,
		responseBody,
	)

	var cashierCreateResponse struct {
		Customer models.Customer `json:"customer"`
	}

	decodeJSON(
		t,
		responseBody,
		&cashierCreateResponse,
	)

	status, responseBody = performJSONRequest(
		t,
		app,
		http.MethodDelete,
		"/api/customers/"+
			formatTestID(cashierCreateResponse.Customer.ID),
		cashierToken,
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
		http.MethodPost,
		"/api/customers",
		managerToken,
		map[string]any{
			"name":    "Manager Customer",
			"email":   "manager.customer@example.com",
			"phone":   "0773333333",
			"address": "Jaffna",
		},
	)

	requireStatus(
		t,
		status,
		fiber.StatusCreated,
		responseBody,
	)

	var managerCreateResponse struct {
		Customer models.Customer `json:"customer"`
	}

	decodeJSON(
		t,
		responseBody,
		&managerCreateResponse,
	)

	status, responseBody = performJSONRequest(
		t,
		app,
		http.MethodDelete,
		"/api/customers/"+
			formatTestID(managerCreateResponse.Customer.ID),
		managerToken,
		nil,
	)

	requireStatus(
		t,
		status,
		fiber.StatusForbidden,
		responseBody,
	)

	customerIDs := []uint{
		customerID,
		cashierCreateResponse.Customer.ID,
		managerCreateResponse.Customer.ID,
	}

	for _, id := range customerIDs {
		status, responseBody = performJSONRequest(
			t,
			app,
			http.MethodDelete,
			"/api/customers/"+formatTestID(id),
			ownerToken,
			nil,
		)

		requireStatus(
			t,
			status,
			fiber.StatusNoContent,
			responseBody,
		)
	}

	status, responseBody = performJSONRequest(
		t,
		app,
		http.MethodGet,
		"/api/customers/"+formatTestID(customerID),
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
