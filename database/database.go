package database

import (
	"fmt"

	"github.com/glebarez/sqlite"
	"gorm.io/gorm"

	"github.com/rimzy2002/delivergate-backend-assessment/models"
)

var DB *gorm.DB

func Connect(databasePath string) error {
	db, err := gorm.Open(sqlite.Open(databasePath), &gorm.Config{})
	if err != nil {
		return fmt.Errorf("connect to database: %w", err)
	}

	if err := db.AutoMigrate(
		&models.User{},
		&models.Category{},
		&models.Item{},
		&models.Customer{},
	); err != nil {
		return fmt.Errorf("migrate database: %w", err)
	}

	DB = db

	return nil
}
