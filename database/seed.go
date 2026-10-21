package database

import (
	"errors"
	"fmt"

	"golang.org/x/crypto/bcrypt"
	"gorm.io/gorm"

	"github.com/rimzy2002/delivergate-backend-assessment/models"
)

type seedUser struct {
	Name     string
	Email    string
	Password string
	Role     string
}

func SeedInitialData(
	ownerPassword string,
	managerPassword string,
	cashierPassword string,
) error {
	users := []seedUser{
		{
			Name:     "Shop Owner",
			Email:    "owner@delivergate.local",
			Password: ownerPassword,
			Role:     models.RoleOwner,
		},
		{
			Name:     "Shop Manager",
			Email:    "manager@delivergate.local",
			Password: managerPassword,
			Role:     models.RoleManager,
		},
		{
			Name:     "Shop Cashier",
			Email:    "cashier@delivergate.local",
			Password: cashierPassword,
			Role:     models.RoleCashier,
		},
	}

	for _, user := range users {
		if len(user.Password) < 8 {
			return fmt.Errorf("password for %s must contain at least 8 characters", user.Email)
		}
	}

	if err := seedCategories(); err != nil {
		return err
	}

	for _, user := range users {
		if err := createOrUpdateUser(user); err != nil {
			return err
		}
	}

	return nil
}

func seedCategories() error {
	categoryNames := []string{
		"Chocolate",
		"Vanilla",
		"Strawberry",
	}

	for _, name := range categoryNames {
		category := models.Category{Name: name}

		if err := DB.Where("name = ?", name).
			FirstOrCreate(&category).Error; err != nil {
			return fmt.Errorf("seed category %s: %w", name, err)
		}
	}

	return nil
}

func createOrUpdateUser(input seedUser) error {
	var user models.User

	result := DB.Where("email = ?", input.Email).First(&user)
	if result.Error != nil && !errors.Is(result.Error, gorm.ErrRecordNotFound) {
		return fmt.Errorf("find user %s: %w", input.Email, result.Error)
	}

	if errors.Is(result.Error, gorm.ErrRecordNotFound) {
		passwordHash, err := bcrypt.GenerateFromPassword(
			[]byte(input.Password),
			bcrypt.DefaultCost,
		)
		if err != nil {
			return fmt.Errorf("hash password for %s: %w", input.Email, err)
		}

		user = models.User{
			Name:         input.Name,
			Email:        input.Email,
			PasswordHash: string(passwordHash),
			Role:         input.Role,
		}

		if err := DB.Create(&user).Error; err != nil {
			return fmt.Errorf("create user %s: %w", input.Email, err)
		}

		return nil
	}

	updates := map[string]any{
		"name": input.Name,
		"role": input.Role,
	}

	if bcrypt.CompareHashAndPassword(
		[]byte(user.PasswordHash),
		[]byte(input.Password),
	) != nil {
		passwordHash, err := bcrypt.GenerateFromPassword(
			[]byte(input.Password),
			bcrypt.DefaultCost,
		)
		if err != nil {
			return fmt.Errorf("hash password for %s: %w", input.Email, err)
		}

		updates["password_hash"] = string(passwordHash)
	}

	if err := DB.Model(&user).Updates(updates).Error; err != nil {
		return fmt.Errorf("update user %s: %w", input.Email, err)
	}

	return nil
}
