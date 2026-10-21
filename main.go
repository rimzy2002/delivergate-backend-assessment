package main

import (
	"log"

	"github.com/gofiber/fiber/v2"
	"github.com/rimzy2002/delivergate-backend-assessment/config"
	"github.com/rimzy2002/delivergate-backend-assessment/database"
	"github.com/rimzy2002/delivergate-backend-assessment/handlers"
	"github.com/rimzy2002/delivergate-backend-assessment/middleware"
	"github.com/rimzy2002/delivergate-backend-assessment/models"
)

func main() {
	cfg := config.Load()

	if len(cfg.JWTSecret) < 32 {
		log.Fatal("JWT_SECRET must contain at least 32 characters")
	}

	if err := database.Connect(cfg.DatabasePath); err != nil {
		log.Fatal(err)
	}

	if err := database.SeedInitialData(
		cfg.OwnerPassword,
		cfg.ManagerPassword,
		cfg.CashierPassword,
	); err != nil {
		log.Fatal(err)
	}

	app := fiber.New()

	authHandler := handlers.NewAuthHandler(database.DB, cfg.JWTSecret)
	itemHandler := handlers.NewItemHandler(database.DB)
	customerHandler := handlers.NewCustomerHandler(database.DB)
	app.Post("/api/auth/login", authHandler.Login)

	authRequired := middleware.Authenticate(cfg.JWTSecret)
	app.Get("/api/auth/me", authRequired, authHandler.Me)

	app.Get(
		"/api/categories",
		authRequired,
		itemHandler.ListCategories,
	)

	items := app.Group("/api/items", authRequired)

	items.Get("", itemHandler.ListItems)
	items.Get("/:id", itemHandler.GetItem)

	items.Post(
		"",
		middleware.RequireRoles(
			models.RoleOwner,
			models.RoleManager,
		),
		itemHandler.CreateItem,
	)

	items.Patch(
		"/:id",
		middleware.RequireRoles(
			models.RoleOwner,
			models.RoleManager,
			models.RoleCashier,
		),
		itemHandler.UpdateItem,
	)

	items.Delete(
		"/:id",
		middleware.RequireRoles(models.RoleOwner),
		itemHandler.DeleteItem,
	)

	customers := app.Group("/api/customers", authRequired)

	customers.Get("", customerHandler.ListCustomers)
	customers.Get("/:id", customerHandler.GetCustomer)

	customers.Post(
		"",
		middleware.RequireRoles(
			models.RoleOwner,
			models.RoleManager,
			models.RoleCashier,
		),
		customerHandler.CreateCustomer,
	)

	customers.Patch(
		"/:id",
		middleware.RequireRoles(
			models.RoleOwner,
			models.RoleManager,
			models.RoleCashier,
		),
		customerHandler.UpdateCustomer,
	)

	customers.Delete(
		"/:id",
		middleware.RequireRoles(models.RoleOwner),
		customerHandler.DeleteCustomer,
	)

	app.Get("/health", func(c *fiber.Ctx) error {
		return c.Status(fiber.StatusOK).JSON(fiber.Map{
			"status": "ok",
		})
	})

	log.Fatal(app.Listen(":" + cfg.Port))
}
