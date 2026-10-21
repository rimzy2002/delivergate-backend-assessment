package models

import "time"

type Item struct {
	ID          uint      `gorm:"primaryKey" json:"id"`
	Name        string    `gorm:"size:150;not null;index" json:"name"`
	Description string    `gorm:"size:500" json:"description"`
	CategoryID  uint      `gorm:"not null;index" json:"category_id"`
	Category    Category  `gorm:"constraint:OnUpdate:CASCADE,OnDelete:RESTRICT;" json:"category"`
	Price       float64   `gorm:"not null;check:price > 0" json:"price"`
	Quantity    int       `gorm:"not null;check:quantity >= 0" json:"quantity"`
	CreatedAt   time.Time `json:"created_at"`
	UpdatedAt   time.Time `json:"updated_at"`
}
