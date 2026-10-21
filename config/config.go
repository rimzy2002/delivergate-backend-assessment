package config

import (
	"os"

	"github.com/joho/godotenv"
)

type Config struct {
	Port         string
	DatabasePath string
	JWTSecret    string
}

func Load() Config {
	_ = godotenv.Load()

	return Config{
		Port:         getEnv("PORT", "3000"),
		DatabasePath: getEnv("DATABASE_PATH", "delivergate.db"),
		JWTSecret:    os.Getenv("JWT_SECRET"),
	}
}

func getEnv(key, fallback string) string {
	value := os.Getenv(key)
	if value == "" {
		return fallback
	}

	return value
}
