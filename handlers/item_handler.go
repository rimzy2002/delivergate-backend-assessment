package handlers

import (
	"errors"
	"strings"

	"github.com/gofiber/fiber/v2"
	"gorm.io/gorm"

	"github.com/rimzy2002/delivergate-backend-assessment/models"
)

type ItemHandler struct {
	DB *gorm.DB
}

type createItemRequest struct {
	Name        string  `json:"name"`
	Description string  `json:"description"`
	CategoryID  uint    `json:"category_id"`
	Price       float64 `json:"price"`
	Quantity    int     `json:"quantity"`
}

type updateItemRequest struct {
	Name        *string  `json:"name"`
	Description *string  `json:"description"`
	CategoryID  *uint    `json:"category_id"`
	Price       *float64 `json:"price"`
	Quantity    *int     `json:"quantity"`
}

func NewItemHandler(db *gorm.DB) *ItemHandler {
	return &ItemHandler{
		DB: db,
	}
}

func (h *ItemHandler) ListCategories(c *fiber.Ctx) error {
	var categories []models.Category

	if err := h.DB.Order("name ASC").Find(&categories).Error; err != nil {
		return c.Status(fiber.StatusInternalServerError).JSON(fiber.Map{
			"error": "could not retrieve categories",
		})
	}

	return c.Status(fiber.StatusOK).JSON(fiber.Map{
		"categories": categories,
	})
}

func (h *ItemHandler) CreateItem(c *fiber.Ctx) error {
	var request createItemRequest

	if err := c.BodyParser(&request); err != nil {
		return c.Status(fiber.StatusBadRequest).JSON(fiber.Map{
			"error": "invalid request body",
		})
	}

	request.Name = strings.TrimSpace(request.Name)
	request.Description = strings.TrimSpace(request.Description)

	if request.Name == "" {
		return c.Status(fiber.StatusBadRequest).JSON(fiber.Map{
			"error": "item name is required",
		})
	}

	if request.CategoryID == 0 {
		return c.Status(fiber.StatusBadRequest).JSON(fiber.Map{
			"error": "category_id is required",
		})
	}

	if request.Price <= 0 {
		return c.Status(fiber.StatusBadRequest).JSON(fiber.Map{
			"error": "price must be greater than zero",
		})
	}

	if request.Quantity < 0 {
		return c.Status(fiber.StatusBadRequest).JSON(fiber.Map{
			"error": "quantity cannot be negative",
		})
	}

	var category models.Category

	result := h.DB.First(&category, request.CategoryID)
	if errors.Is(result.Error, gorm.ErrRecordNotFound) {
		return c.Status(fiber.StatusBadRequest).JSON(fiber.Map{
			"error": "category does not exist",
		})
	}

	if result.Error != nil {
		return c.Status(fiber.StatusInternalServerError).JSON(fiber.Map{
			"error": "could not validate category",
		})
	}

	item := models.Item{
		Name:        request.Name,
		Description: request.Description,
		CategoryID:  request.CategoryID,
		Price:       request.Price,
		Quantity:    request.Quantity,
	}

	if err := h.DB.Create(&item).Error; err != nil {
		return c.Status(fiber.StatusInternalServerError).JSON(fiber.Map{
			"error": "could not create item",
		})
	}

	if err := h.DB.Preload("Category").First(&item, item.ID).Error; err != nil {
		return c.Status(fiber.StatusInternalServerError).JSON(fiber.Map{
			"error": "item was created but could not be retrieved",
		})
	}

	return c.Status(fiber.StatusCreated).JSON(fiber.Map{
		"message": "item created successfully",
		"item":    item,
	})
}

func (h *ItemHandler) ListItems(c *fiber.Ctx) error {
	var items []models.Item

	query := h.DB.Preload("Category").Order("id DESC")

	search := strings.TrimSpace(c.Query("search"))
	if search != "" {
		query = query.Where(
			"LOWER(name) LIKE ?",
			"%"+strings.ToLower(search)+"%",
		)
	}

	if err := query.Find(&items).Error; err != nil {
		return c.Status(fiber.StatusInternalServerError).JSON(fiber.Map{
			"error": "could not retrieve items",
		})
	}

	return c.Status(fiber.StatusOK).JSON(fiber.Map{
		"items": items,
	})
}

func (h *ItemHandler) GetItem(c *fiber.Ctx) error {
	itemID, err := c.ParamsInt("id")
	if err != nil || itemID < 1 {
		return c.Status(fiber.StatusBadRequest).JSON(fiber.Map{
			"error": "invalid item ID",
		})
	}

	var item models.Item

	result := h.DB.
		Preload("Category").
		First(&item, uint(itemID))

	if errors.Is(result.Error, gorm.ErrRecordNotFound) {
		return c.Status(fiber.StatusNotFound).JSON(fiber.Map{
			"error": "item not found",
		})
	}

	if result.Error != nil {
		return c.Status(fiber.StatusInternalServerError).JSON(fiber.Map{
			"error": "could not retrieve item",
		})
	}

	return c.Status(fiber.StatusOK).JSON(fiber.Map{
		"item": item,
	})
}

func (h *ItemHandler) UpdateItem(c *fiber.Ctx) error {
	itemID, err := c.ParamsInt("id")
	if err != nil || itemID < 1 {
		return c.Status(fiber.StatusBadRequest).JSON(fiber.Map{
			"error": "invalid item ID",
		})
	}

	var item models.Item

	result := h.DB.First(&item, uint(itemID))
	if errors.Is(result.Error, gorm.ErrRecordNotFound) {
		return c.Status(fiber.StatusNotFound).JSON(fiber.Map{
			"error": "item not found",
		})
	}

	if result.Error != nil {
		return c.Status(fiber.StatusInternalServerError).JSON(fiber.Map{
			"error": "could not retrieve item",
		})
	}

	var request updateItemRequest

	if err := c.BodyParser(&request); err != nil {
		return c.Status(fiber.StatusBadRequest).JSON(fiber.Map{
			"error": "invalid request body",
		})
	}

	if request.Name == nil &&
		request.Description == nil &&
		request.CategoryID == nil &&
		request.Price == nil &&
		request.Quantity == nil {
		return c.Status(fiber.StatusBadRequest).JSON(fiber.Map{
			"error": "at least one field is required",
		})
	}

	if request.Name != nil {
		name := strings.TrimSpace(*request.Name)

		if name == "" {
			return c.Status(fiber.StatusBadRequest).JSON(fiber.Map{
				"error": "item name cannot be empty",
			})
		}

		item.Name = name
	}

	if request.Description != nil {
		item.Description = strings.TrimSpace(*request.Description)
	}

	if request.CategoryID != nil {
		if *request.CategoryID == 0 {
			return c.Status(fiber.StatusBadRequest).JSON(fiber.Map{
				"error": "invalid category ID",
			})
		}

		var category models.Category

		result := h.DB.First(&category, *request.CategoryID)
		if errors.Is(result.Error, gorm.ErrRecordNotFound) {
			return c.Status(fiber.StatusBadRequest).JSON(fiber.Map{
				"error": "category does not exist",
			})
		}

		if result.Error != nil {
			return c.Status(fiber.StatusInternalServerError).JSON(fiber.Map{
				"error": "could not validate category",
			})
		}

		item.CategoryID = *request.CategoryID
	}

	if request.Price != nil {
		if *request.Price <= 0 {
			return c.Status(fiber.StatusBadRequest).JSON(fiber.Map{
				"error": "price must be greater than zero",
			})
		}

		item.Price = *request.Price
	}

	if request.Quantity != nil {
		if *request.Quantity < 0 {
			return c.Status(fiber.StatusBadRequest).JSON(fiber.Map{
				"error": "quantity cannot be negative",
			})
		}

		item.Quantity = *request.Quantity
	}

	if err := h.DB.Save(&item).Error; err != nil {
		return c.Status(fiber.StatusInternalServerError).JSON(fiber.Map{
			"error": "could not update item",
		})
	}

	if err := h.DB.Preload("Category").First(&item, item.ID).Error; err != nil {
		return c.Status(fiber.StatusInternalServerError).JSON(fiber.Map{
			"error": "item was updated but could not be retrieved",
		})
	}

	return c.Status(fiber.StatusOK).JSON(fiber.Map{
		"message": "item updated successfully",
		"item":    item,
	})
}

func (h *ItemHandler) DeleteItem(c *fiber.Ctx) error {
	itemID, err := c.ParamsInt("id")
	if err != nil || itemID < 1 {
		return c.Status(fiber.StatusBadRequest).JSON(fiber.Map{
			"error": "invalid item ID",
		})
	}

	var item models.Item

	result := h.DB.First(&item, uint(itemID))
	if errors.Is(result.Error, gorm.ErrRecordNotFound) {
		return c.Status(fiber.StatusNotFound).JSON(fiber.Map{
			"error": "item not found",
		})
	}

	if result.Error != nil {
		return c.Status(fiber.StatusInternalServerError).JSON(fiber.Map{
			"error": "could not retrieve item",
		})
	}

	if err := h.DB.Delete(&item).Error; err != nil {
		return c.Status(fiber.StatusInternalServerError).JSON(fiber.Map{
			"error": "could not delete item",
		})
	}

	return c.SendStatus(fiber.StatusNoContent)
}
