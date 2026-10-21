package database

import (
	"fmt"
	"log"
	"os"
	"time"

	"github.com/glebarez/sqlite"
	"gorm.io/gorm"
	"gorm.io/gorm/logger"

	"github.com/rimzy2002/delivergate-backend-assessment/models"
)

var DB *gorm.DB

func Connect(databasePath string) error {
	databaseLogger := logger.New(
		log.New(os.Stdout, "\r\n", log.LstdFlags),
		logger.Config{
			SlowThreshold:             time.Second,
			LogLevel:                  logger.Warn,
			IgnoreRecordNotFoundError: true,
			ParameterizedQueries:      true,
			Colorful:                  false,
		},
	)

	db, err := gorm.Open(sqlite.Open(databasePath), &gorm.Config{
		Logger: databaseLogger,
	})
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
