package models

import "time"

type Customer struct {
	ID        uint      `gorm:"primaryKey" json:"id"`
	Name      string    `gorm:"size:150;not null" json:"name"`
	Email     string    `gorm:"size:150;uniqueIndex;not null" json:"email"`
	Phone     string    `gorm:"size:30;not null" json:"phone"`
	Address   string    `gorm:"size:300" json:"address"`
	CreatedAt time.Time `json:"created_at"`
	UpdatedAt time.Time `json:"updated_at"`
}
