package database

import (
	"fmt"

	"golang.org/x/crypto/bcrypt"

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
		if err := createUserIfMissing(user); err != nil {
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

func createUserIfMissing(input seedUser) error {
	var existingCount int64

	if err := DB.Model(&models.User{}).
		Where("email = ?", input.Email).
		Count(&existingCount).Error; err != nil {
		return fmt.Errorf("check user %s: %w", input.Email, err)
	}

	if existingCount > 0 {
		return nil
	}

	passwordHash, err := bcrypt.GenerateFromPassword(
		[]byte(input.Password),
		bcrypt.DefaultCost,
	)
	if err != nil {
		return fmt.Errorf("hash password for %s: %w", input.Email, err)
	}

	user := models.User{
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
