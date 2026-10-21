package middleware

import (
	"fmt"
	"strings"

	"github.com/gofiber/fiber/v2"
	"github.com/golang-jwt/jwt/v5"
)

func Authenticate(jwtSecret string) fiber.Handler {
	return func(c *fiber.Ctx) error {
		authorization := strings.Fields(c.Get("Authorization"))

		if len(authorization) != 2 ||
			!strings.EqualFold(authorization[0], "Bearer") {
			return unauthorized(c)
		}

		token, err := jwt.Parse(
			authorization[1],
			func(token *jwt.Token) (any, error) {
				if token.Method != jwt.SigningMethodHS256 {
					return nil, fmt.Errorf("unexpected signing method")
				}

				return []byte(jwtSecret), nil
			},
			jwt.WithValidMethods([]string{
				jwt.SigningMethodHS256.Alg(),
			}),
		)

		if err != nil || !token.Valid {
			return unauthorized(c)
		}

		claims, ok := token.Claims.(jwt.MapClaims)
		if !ok {
			return unauthorized(c)
		}

		userID, ok := claims["user_id"].(float64)
		if !ok || userID < 1 {
			return unauthorized(c)
		}

		role, ok := claims["role"].(string)
		if !ok || role == "" {
			return unauthorized(c)
		}

		c.Locals("user_id", uint(userID))
		c.Locals("role", role)

		return c.Next()
	}
}

func RequireRoles(allowedRoles ...string) fiber.Handler {
	allowed := make(map[string]struct{}, len(allowedRoles))
	for _, role := range allowedRoles {
		allowed[role] = struct{}{}
	}

	return func(c *fiber.Ctx) error {
		role, ok := c.Locals("role").(string)
		if !ok || role == "" {
			return unauthorized(c)
		}

		if _, ok := allowed[role]; !ok {
			return c.Status(fiber.StatusForbidden).JSON(fiber.Map{
				"error": "permission denied",
			})
		}

		return c.Next()
	}
}

func unauthorized(c *fiber.Ctx) error {
	return c.Status(fiber.StatusUnauthorized).JSON(fiber.Map{
		"error": "authentication required",
	})
}
