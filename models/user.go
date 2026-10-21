package models

import "time"

const (
	RoleOwner   = "owner"
	RoleManager = "manager"
	RoleCashier = "cashier"
)

type User struct {
	ID           uint      `gorm:"primaryKey" json:"id"`
	Name         string    `gorm:"size:100;not null" json:"name"`
	Email        string    `gorm:"size:150;uniqueIndex;not null" json:"email"`
	PasswordHash string    `gorm:"not null" json:"-"`
	Role         string    `gorm:"size:20;not null;check:role IN ('owner','manager','cashier')" json:"role"`
	CreatedAt    time.Time `json:"created_at"`
	UpdatedAt    time.Time `json:"updated_at"`
}
