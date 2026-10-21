package handlers

import (
	"errors"
	"net/mail"
	"strings"

	"github.com/gofiber/fiber/v2"
	"gorm.io/gorm"

	"github.com/rimzy2002/delivergate-backend-assessment/models"
)

type CustomerHandler struct {
	DB *gorm.DB
}

type createCustomerRequest struct {
	Name    string `json:"name"`
	Email   string `json:"email"`
	Phone   string `json:"phone"`
	Address string `json:"address"`
}

type updateCustomerRequest struct {
	Name    *string `json:"name"`
	Email   *string `json:"email"`
	Phone   *string `json:"phone"`
	Address *string `json:"address"`
}

func NewCustomerHandler(db *gorm.DB) *CustomerHandler {
	return &CustomerHandler{
		DB: db,
	}
}

func (h *CustomerHandler) CreateCustomer(c *fiber.Ctx) error {
	var request createCustomerRequest

	if err := c.BodyParser(&request); err != nil {
		return c.Status(fiber.StatusBadRequest).JSON(fiber.Map{
			"error": "invalid request body",
		})
	}

	request.Name = strings.TrimSpace(request.Name)
	request.Phone = strings.TrimSpace(request.Phone)
	request.Address = strings.TrimSpace(request.Address)

	if request.Name == "" {
		return c.Status(fiber.StatusBadRequest).JSON(fiber.Map{
			"error": "customer name is required",
		})
	}

	email, valid := normalizeEmail(request.Email)
	if !valid {
		return c.Status(fiber.StatusBadRequest).JSON(fiber.Map{
			"error": "a valid email is required",
		})
	}

	if request.Phone == "" {
		return c.Status(fiber.StatusBadRequest).JSON(fiber.Map{
			"error": "phone number is required",
		})
	}

	var existingCustomer models.Customer

	result := h.DB.
		Where("LOWER(email) = ?", email).
		First(&existingCustomer)

	if result.Error == nil {
		return c.Status(fiber.StatusConflict).JSON(fiber.Map{
			"error": "a customer with this email already exists",
		})
	}

	if !errors.Is(result.Error, gorm.ErrRecordNotFound) {
		return c.Status(fiber.StatusInternalServerError).JSON(fiber.Map{
			"error": "could not validate customer email",
		})
	}

	customer := models.Customer{
		Name:    request.Name,
		Email:   email,
		Phone:   request.Phone,
		Address: request.Address,
	}

	if err := h.DB.Create(&customer).Error; err != nil {
		return c.Status(fiber.StatusInternalServerError).JSON(fiber.Map{
			"error": "could not create customer",
		})
	}

	return c.Status(fiber.StatusCreated).JSON(fiber.Map{
		"message":  "customer created successfully",
		"customer": customer,
	})
}

func (h *CustomerHandler) ListCustomers(c *fiber.Ctx) error {
	var customers []models.Customer

	query := h.DB.Order("id DESC")

	search := strings.TrimSpace(c.Query("search"))
	if search != "" {
		searchPattern := "%" + strings.ToLower(search) + "%"

		query = query.Where(
			"LOWER(name) LIKE ? OR LOWER(email) LIKE ? OR phone LIKE ?",
			searchPattern,
			searchPattern,
			"%"+search+"%",
		)
	}

	if err := query.Find(&customers).Error; err != nil {
		return c.Status(fiber.StatusInternalServerError).JSON(fiber.Map{
			"error": "could not retrieve customers",
		})
	}

	return c.Status(fiber.StatusOK).JSON(fiber.Map{
		"customers": customers,
	})
}

func (h *CustomerHandler) GetCustomer(c *fiber.Ctx) error {
	customerID, err := c.ParamsInt("id")
	if err != nil || customerID < 1 {
		return c.Status(fiber.StatusBadRequest).JSON(fiber.Map{
			"error": "invalid customer ID",
		})
	}

	var customer models.Customer

	result := h.DB.First(&customer, uint(customerID))

	if errors.Is(result.Error, gorm.ErrRecordNotFound) {
		return c.Status(fiber.StatusNotFound).JSON(fiber.Map{
			"error": "customer not found",
		})
	}

	if result.Error != nil {
		return c.Status(fiber.StatusInternalServerError).JSON(fiber.Map{
			"error": "could not retrieve customer",
		})
	}

	return c.Status(fiber.StatusOK).JSON(fiber.Map{
		"customer": customer,
	})
}

func (h *CustomerHandler) UpdateCustomer(c *fiber.Ctx) error {
	customerID, err := c.ParamsInt("id")
	if err != nil || customerID < 1 {
		return c.Status(fiber.StatusBadRequest).JSON(fiber.Map{
			"error": "invalid customer ID",
		})
	}

	var customer models.Customer

	result := h.DB.First(&customer, uint(customerID))

	if errors.Is(result.Error, gorm.ErrRecordNotFound) {
		return c.Status(fiber.StatusNotFound).JSON(fiber.Map{
			"error": "customer not found",
		})
	}

	if result.Error != nil {
		return c.Status(fiber.StatusInternalServerError).JSON(fiber.Map{
			"error": "could not retrieve customer",
		})
	}

	var request updateCustomerRequest

	if err := c.BodyParser(&request); err != nil {
		return c.Status(fiber.StatusBadRequest).JSON(fiber.Map{
			"error": "invalid request body",
		})
	}

	if request.Name == nil &&
		request.Email == nil &&
		request.Phone == nil &&
		request.Address == nil {
		return c.Status(fiber.StatusBadRequest).JSON(fiber.Map{
			"error": "at least one field is required",
		})
	}

	if request.Name != nil {
		name := strings.TrimSpace(*request.Name)

		if name == "" {
			return c.Status(fiber.StatusBadRequest).JSON(fiber.Map{
				"error": "customer name cannot be empty",
			})
		}

		customer.Name = name
	}

	if request.Email != nil {
		email, valid := normalizeEmail(*request.Email)
		if !valid {
			return c.Status(fiber.StatusBadRequest).JSON(fiber.Map{
				"error": "a valid email is required",
			})
		}

		var existingCustomer models.Customer

		result := h.DB.
			Where(
				"LOWER(email) = ? AND id <> ?",
				email,
				customer.ID,
			).
			First(&existingCustomer)

		if result.Error == nil {
			return c.Status(fiber.StatusConflict).JSON(fiber.Map{
				"error": "a customer with this email already exists",
			})
		}

		if !errors.Is(result.Error, gorm.ErrRecordNotFound) {
			return c.Status(fiber.StatusInternalServerError).JSON(fiber.Map{
				"error": "could not validate customer email",
			})
		}

		customer.Email = email
	}

	if request.Phone != nil {
		phone := strings.TrimSpace(*request.Phone)

		if phone == "" {
			return c.Status(fiber.StatusBadRequest).JSON(fiber.Map{
				"error": "phone number cannot be empty",
			})
		}

		customer.Phone = phone
	}

	if request.Address != nil {
		customer.Address = strings.TrimSpace(*request.Address)
	}

	if err := h.DB.Save(&customer).Error; err != nil {
		return c.Status(fiber.StatusInternalServerError).JSON(fiber.Map{
			"error": "could not update customer",
		})
	}

	return c.Status(fiber.StatusOK).JSON(fiber.Map{
		"message":  "customer updated successfully",
		"customer": customer,
	})
}

func (h *CustomerHandler) DeleteCustomer(c *fiber.Ctx) error {
	customerID, err := c.ParamsInt("id")
	if err != nil || customerID < 1 {
		return c.Status(fiber.StatusBadRequest).JSON(fiber.Map{
			"error": "invalid customer ID",
		})
	}

	var customer models.Customer

	result := h.DB.First(&customer, uint(customerID))

	if errors.Is(result.Error, gorm.ErrRecordNotFound) {
		return c.Status(fiber.StatusNotFound).JSON(fiber.Map{
			"error": "customer not found",
		})
	}

	if result.Error != nil {
		return c.Status(fiber.StatusInternalServerError).JSON(fiber.Map{
			"error": "could not retrieve customer",
		})
	}

	if err := h.DB.Delete(&customer).Error; err != nil {
		return c.Status(fiber.StatusInternalServerError).JSON(fiber.Map{
			"error": "could not delete customer",
		})
	}

	return c.SendStatus(fiber.StatusNoContent)
}

func normalizeEmail(value string) (string, bool) {
	email := strings.ToLower(strings.TrimSpace(value))

	parsedAddress, err := mail.ParseAddress(email)
	if err != nil || parsedAddress.Address != email {
		return "", false
	}

	return email, true
}
