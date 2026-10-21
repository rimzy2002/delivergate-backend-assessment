package config

import (
	"os"

	"github.com/joho/godotenv"
)

type Config struct {
	Port            string
	DatabasePath    string
	JWTSecret       string
	OwnerPassword   string
	ManagerPassword string
	CashierPassword string
}

func Load() Config {
	_ = godotenv.Load()

	return Config{
		Port:            getEnv("PORT", "3000"),
		DatabasePath:    getEnv("DATABASE_PATH", "delivergate.db"),
		JWTSecret:       os.Getenv("JWT_SECRET"),
		OwnerPassword:   os.Getenv("OWNER_PASSWORD"),
		ManagerPassword: os.Getenv("MANAGER_PASSWORD"),
		CashierPassword: os.Getenv("CASHIER_PASSWORD"),
	}
}

func getEnv(key, fallback string) string {
	value := os.Getenv(key)
	if value == "" {
		return fallback
	}

	return value
}
