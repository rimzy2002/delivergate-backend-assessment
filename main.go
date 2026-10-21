package main

import (
	"log"

	"github.com/gofiber/fiber/v2"
	"github.com/rimzy2002/delivergate-backend-assessment/config"
	"github.com/rimzy2002/delivergate-backend-assessment/database"
	"github.com/rimzy2002/delivergate-backend-assessment/handlers"
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
	app.Post("/api/auth/login", authHandler.Login)

	app.Get("/health", func(c *fiber.Ctx) error {
		return c.Status(fiber.StatusOK).JSON(fiber.Map{
			"status": "ok",
		})
	})

	log.Fatal(app.Listen(":" + cfg.Port))
}
