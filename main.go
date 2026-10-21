package main

import (
	"log"

	"github.com/gofiber/fiber/v2"

	"github.com/rimzy2002/delivergate-backend-assessment/config"
	"github.com/rimzy2002/delivergate-backend-assessment/database"
)

func main() {
	cfg := config.Load()

	if err := database.Connect(cfg.DatabasePath); err != nil {
		log.Fatal(err)
	}

	app := fiber.New()

	app.Get("/health", func(c *fiber.Ctx) error {
		return c.Status(fiber.StatusOK).JSON(fiber.Map{
			"status": "ok",
		})
	})

	log.Fatal(app.Listen(":" + cfg.Port))
}
