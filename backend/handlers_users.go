package main

import (
	"database/sql"
	"encoding/json"
	"fmt"
	"math"
	"net/http"
	"time"

	"github.com/gin-gonic/gin"
	"github.com/google/uuid"
	"go.opentelemetry.io/otel"
	"go.opentelemetry.io/otel/attribute"
)

var tracer = otel.Tracer("permafrost-backend")

// @Summary Get users
// @Description Get a paginated list of users
// @Tags users
// @Accept json
// @Produce json
// @Param page query int false "Page number" default(1)
// @Param limit query int false "Items per page" default(20)
// @Param status query string false "Filter by status"
// @Param domain_controller_id query string false "Filter by domain controller"
// @Success 200 {object} PaginatedResponse
// @Failure 400 {object} ErrorResponse
// @Failure 500 {object} ErrorResponse
// @Router /api/v1/users [get]
func (s *Server) getUsers(c *gin.Context) {
	ctx, span := tracer.Start(c.Request.Context(), "getUsers")
	defer span.End()

	var params PaginationParams
	if err := c.ShouldBindQuery(&params); err != nil {
		span.RecordError(err)
		c.JSON(http.StatusBadRequest, ErrorResponse{
			Error:   "invalid_parameters",
			Message: err.Error(),
			Code:    http.StatusBadRequest,
		})
		return
	}

	status := c.Query("status")
	domainControllerID := c.Query("domain_controller_id")

	span.SetAttributes(
		attribute.Int("page", params.Page),
		attribute.Int("limit", params.Limit),
		attribute.String("status", status),
		attribute.String("domain_controller_id", domainControllerID),
	)

	// Build query
	query := `SELECT id, domain_controller_id, ad_object_guid, sam_account_name, 
			  user_principal_name, display_name, given_name, surname, email, 
			  department, title, manager_dn, status, last_logon, password_last_set, 
			  account_expires, created_at, updated_at, last_seen_at 
			  FROM users WHERE 1=1`
	countQuery := `SELECT COUNT(*) FROM users WHERE 1=1`
	args := []interface{}{}
	argIndex := 1

	if status != "" {
		query += fmt.Sprintf(" AND status = $%d", argIndex)
		countQuery += fmt.Sprintf(" AND status = $%d", argIndex)
		args = append(args, status)
		argIndex++
	}

	if domainControllerID != "" {
		query += fmt.Sprintf(" AND domain_controller_id = $%d", argIndex)
		countQuery += fmt.Sprintf(" AND domain_controller_id = $%d", argIndex)
		args = append(args, domainControllerID)
		argIndex++
	}

	// Get total count
	var total int64
	err := s.db.QueryRowContext(ctx, countQuery, args...).Scan(&total)
	if err != nil {
		span.RecordError(err)
		c.JSON(http.StatusInternalServerError, ErrorResponse{
			Error:   "database_error",
			Message: "Failed to count users",
			Code:    http.StatusInternalServerError,
		})
		return
	}

	// Add pagination
	offset := (params.Page - 1) * params.Limit
	query += fmt.Sprintf(" ORDER BY created_at DESC LIMIT $%d OFFSET $%d", argIndex, argIndex+1)
	args = append(args, params.Limit, offset)

	// Execute query
	rows, err := s.db.QueryContext(ctx, query, args...)
	if err != nil {
		span.RecordError(err)
		c.JSON(http.StatusInternalServerError, ErrorResponse{
			Error:   "database_error",
			Message: "Failed to fetch users",
			Code:    http.StatusInternalServerError,
		})
		return
	}
	defer rows.Close()

	var users []User
	for rows.Next() {
		var user User
		err := rows.Scan(
			&user.ID, &user.DomainControllerID, &user.ADObjectGUID, &user.SAMAccountName,
			&user.UserPrincipalName, &user.DisplayName, &user.GivenName, &user.Surname,
			&user.Email, &user.Department, &user.Title, &user.ManagerDN, &user.Status,
			&user.LastLogon, &user.PasswordLastSet, &user.AccountExpires,
			&user.CreatedAt, &user.UpdatedAt, &user.LastSeenAt,
		)
		if err != nil {
			span.RecordError(err)
			c.JSON(http.StatusInternalServerError, ErrorResponse{
				Error:   "database_error",
				Message: "Failed to scan user",
				Code:    http.StatusInternalServerError,
			})
			return
		}
		users = append(users, user)
	}

	totalPages := int(math.Ceil(float64(total) / float64(params.Limit)))

	response := PaginatedResponse{
		Data:       users,
		Page:       params.Page,
		Limit:      params.Limit,
		Total:      total,
		TotalPages: totalPages,
	}

	span.SetAttributes(attribute.Int64("total_users", total))
	c.JSON(http.StatusOK, response)
}

// @Summary Get user by ID
// @Description Get a specific user by ID
// @Tags users
// @Accept json
// @Produce json
// @Param id path string true "User ID"
// @Success 200 {object} User
// @Failure 400 {object} ErrorResponse
// @Failure 404 {object} ErrorResponse
// @Failure 500 {object} ErrorResponse
// @Router /api/v1/users/{id} [get]
func (s *Server) getUser(c *gin.Context) {
	ctx, span := tracer.Start(c.Request.Context(), "getUser")
	defer span.End()

	idStr := c.Param("id")
	userID, err := uuid.Parse(idStr)
	if err != nil {
		span.RecordError(err)
		c.JSON(http.StatusBadRequest, ErrorResponse{
			Error:   "invalid_id",
			Message: "Invalid user ID format",
			Code:    http.StatusBadRequest,
		})
		return
	}

	span.SetAttributes(attribute.String("user_id", userID.String()))

	query := `SELECT id, domain_controller_id, ad_object_guid, sam_account_name, 
			  user_principal_name, display_name, given_name, surname, email, 
			  department, title, manager_dn, status, last_logon, password_last_set, 
			  account_expires, created_at, updated_at, last_seen_at 
			  FROM users WHERE id = $1`

	var user User
	err = s.db.QueryRowContext(ctx, query, userID).Scan(
		&user.ID, &user.DomainControllerID, &user.ADObjectGUID, &user.SAMAccountName,
		&user.UserPrincipalName, &user.DisplayName, &user.GivenName, &user.Surname,
		&user.Email, &user.Department, &user.Title, &user.ManagerDN, &user.Status,
		&user.LastLogon, &user.PasswordLastSet, &user.AccountExpires,
		&user.CreatedAt, &user.UpdatedAt, &user.LastSeenAt,
	)

	if err == sql.ErrNoRows {
		c.JSON(http.StatusNotFound, ErrorResponse{
			Error:   "user_not_found",
			Message: "User not found",
			Code:    http.StatusNotFound,
		})
		return
	}

	if err != nil {
		span.RecordError(err)
		c.JSON(http.StatusInternalServerError, ErrorResponse{
			Error:   "database_error",
			Message: "Failed to fetch user",
			Code:    http.StatusInternalServerError,
		})
		return
	}

	c.JSON(http.StatusOK, user)
}

// @Summary Create user
// @Description Create a new user
// @Tags users
// @Accept json
// @Produce json
// @Param user body CreateUserRequest true "User data"
// @Success 201 {object} User
// @Failure 400 {object} ErrorResponse
// @Failure 409 {object} ErrorResponse
// @Failure 500 {object} ErrorResponse
// @Router /api/v1/users [post]
func (s *Server) createUser(c *gin.Context) {
	ctx, span := tracer.Start(c.Request.Context(), "createUser")
	defer span.End()

	var req CreateUserRequest
	if err := c.ShouldBindJSON(&req); err != nil {
		span.RecordError(err)
		c.JSON(http.StatusBadRequest, ErrorResponse{
			Error:   "invalid_request",
			Message: err.Error(),
			Code:    http.StatusBadRequest,
		})
		return
	}

	span.SetAttributes(
		attribute.String("sam_account_name", req.SAMAccountName),
		attribute.String("domain_controller_id", req.DomainControllerID),
	)

	// Set default status if not provided
	if req.Status == "" {
		req.Status = UserStatusActive
	}

	now := time.Now()
	userID := uuid.New()

	query := `INSERT INTO users (id, domain_controller_id, ad_object_guid, sam_account_name, 
			  user_principal_name, display_name, given_name, surname, email, department, 
			  title, manager_dn, status, last_logon, password_last_set, account_expires, 
			  created_at, updated_at, last_seen_at) 
			  VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19)
			  RETURNING id, created_at, updated_at`

	var createdUser User
	err := s.db.QueryRowContext(ctx, query,
		userID, req.DomainControllerID, req.ADObjectGUID, req.SAMAccountName,
		req.UserPrincipalName, req.DisplayName, req.GivenName, req.Surname,
		req.Email, req.Department, req.Title, req.ManagerDN, req.Status,
		req.LastLogon, req.PasswordLastSet, req.AccountExpires,
		now, now, now,
	).Scan(&createdUser.ID, &createdUser.CreatedAt, &createdUser.UpdatedAt)

	if err != nil {
		span.RecordError(err)
		if err.Error() == `pq: duplicate key value violates unique constraint "users_ad_object_guid_key"` {
			c.JSON(http.StatusConflict, ErrorResponse{
				Error:   "user_exists",
				Message: "User with this AD Object GUID already exists",
				Code:    http.StatusConflict,
			})
			return
		}
		c.JSON(http.StatusInternalServerError, ErrorResponse{
			Error:   "database_error",
			Message: "Failed to create user",
			Code:    http.StatusInternalServerError,
		})
		return
	}

	// Fill in the rest of the user data
	createdUser.DomainControllerID = req.DomainControllerID
	createdUser.ADObjectGUID = req.ADObjectGUID
	createdUser.SAMAccountName = req.SAMAccountName
	createdUser.UserPrincipalName = req.UserPrincipalName
	createdUser.DisplayName = req.DisplayName
	createdUser.GivenName = req.GivenName
	createdUser.Surname = req.Surname
	createdUser.Email = req.Email
	createdUser.Department = req.Department
	createdUser.Title = req.Title
	createdUser.ManagerDN = req.ManagerDN
	createdUser.Status = req.Status
	createdUser.LastLogon = req.LastLogon
	createdUser.PasswordLastSet = req.PasswordLastSet
	createdUser.AccountExpires = req.AccountExpires
	createdUser.LastSeenAt = now

	// Publish event to NATS
	eventData := map[string]interface{}{
		"user_id":             createdUser.ID,
		"sam_account_name":    createdUser.SAMAccountName,
		"domain_controller_id": createdUser.DomainControllerID,
	}
	eventJSON, _ := json.Marshal(eventData)
	s.nats.Publish("user.created", eventJSON)

	span.SetAttributes(attribute.String("created_user_id", createdUser.ID.String()))
	c.JSON(http.StatusCreated, createdUser)
}

// @Summary Update user
// @Description Update an existing user
// @Tags users
// @Accept json
// @Produce json
// @Param id path string true "User ID"
// @Param user body UpdateUserRequest true "User data"
// @Success 200 {object} User
// @Failure 400 {object} ErrorResponse
// @Failure 404 {object} ErrorResponse
// @Failure 500 {object} ErrorResponse
// @Router /api/v1/users/{id} [put]
func (s *Server) updateUser(c *gin.Context) {
	ctx, span := tracer.Start(c.Request.Context(), "updateUser")
	defer span.End()

	idStr := c.Param("id")
	userID, err := uuid.Parse(idStr)
	if err != nil {
		span.RecordError(err)
		c.JSON(http.StatusBadRequest, ErrorResponse{
			Error:   "invalid_id",
			Message: "Invalid user ID format",
			Code:    http.StatusBadRequest,
		})
		return
	}

	var req UpdateUserRequest
	if err := c.ShouldBindJSON(&req); err != nil {
		span.RecordError(err)
		c.JSON(http.StatusBadRequest, ErrorResponse{
			Error:   "invalid_request",
			Message: err.Error(),
			Code:    http.StatusBadRequest,
		})
		return
	}

	span.SetAttributes(attribute.String("user_id", userID.String()))

	// Check if user exists
	var exists bool
	err = s.db.QueryRowContext(ctx, "SELECT EXISTS(SELECT 1 FROM users WHERE id = $1)", userID).Scan(&exists)
	if err != nil {
		span.RecordError(err)
		c.JSON(http.StatusInternalServerError, ErrorResponse{
			Error:   "database_error",
			Message: "Failed to check user existence",
			Code:    http.StatusInternalServerError,
		})
		return
	}

	if !exists {
		c.JSON(http.StatusNotFound, ErrorResponse{
			Error:   "user_not_found",
			Message: "User not found",
			Code:    http.StatusNotFound,
		})
		return
	}

	// Update user
	query := `UPDATE users SET
			  user_principal_name = $2, display_name = $3, given_name = $4, surname = $5,
			  email = $6, department = $7, title = $8, manager_dn = $9, status = $10,
			  last_logon = $11, password_last_set = $12, account_expires = $13,
			  updated_at = $14, last_seen_at = $15
			  WHERE id = $1`

	now := time.Now()
	_, err = s.db.ExecContext(ctx, query,
		userID, req.UserPrincipalName, req.DisplayName, req.GivenName, req.Surname,
		req.Email, req.Department, req.Title, req.ManagerDN, req.Status,
		req.LastLogon, req.PasswordLastSet, req.AccountExpires,
		now, now,
	)

	if err != nil {
		span.RecordError(err)
		c.JSON(http.StatusInternalServerError, ErrorResponse{
			Error:   "database_error",
			Message: "Failed to update user",
			Code:    http.StatusInternalServerError,
		})
		return
	}

	// Fetch updated user
	var updatedUser User
	selectQuery := `SELECT id, domain_controller_id, ad_object_guid, sam_account_name,
					user_principal_name, display_name, given_name, surname, email,
					department, title, manager_dn, status, last_logon, password_last_set,
					account_expires, created_at, updated_at, last_seen_at
					FROM users WHERE id = $1`

	err = s.db.QueryRowContext(ctx, selectQuery, userID).Scan(
		&updatedUser.ID, &updatedUser.DomainControllerID, &updatedUser.ADObjectGUID, &updatedUser.SAMAccountName,
		&updatedUser.UserPrincipalName, &updatedUser.DisplayName, &updatedUser.GivenName, &updatedUser.Surname,
		&updatedUser.Email, &updatedUser.Department, &updatedUser.Title, &updatedUser.ManagerDN, &updatedUser.Status,
		&updatedUser.LastLogon, &updatedUser.PasswordLastSet, &updatedUser.AccountExpires,
		&updatedUser.CreatedAt, &updatedUser.UpdatedAt, &updatedUser.LastSeenAt,
	)

	if err != nil {
		span.RecordError(err)
		c.JSON(http.StatusInternalServerError, ErrorResponse{
			Error:   "database_error",
			Message: "Failed to fetch updated user",
			Code:    http.StatusInternalServerError,
		})
		return
	}

	// Publish event to NATS
	eventData := map[string]interface{}{
		"user_id":             updatedUser.ID,
		"sam_account_name":    updatedUser.SAMAccountName,
		"domain_controller_id": updatedUser.DomainControllerID,
	}
	eventJSON, _ := json.Marshal(eventData)
	s.nats.Publish("user.updated", eventJSON)

	c.JSON(http.StatusOK, updatedUser)
}

// @Summary Delete user
// @Description Delete a user (soft delete by setting status to deleted)
// @Tags users
// @Accept json
// @Produce json
// @Param id path string true "User ID"
// @Success 204
// @Failure 400 {object} ErrorResponse
// @Failure 404 {object} ErrorResponse
// @Failure 500 {object} ErrorResponse
// @Router /api/v1/users/{id} [delete]
func (s *Server) deleteUser(c *gin.Context) {
	ctx, span := tracer.Start(c.Request.Context(), "deleteUser")
	defer span.End()

	idStr := c.Param("id")
	userID, err := uuid.Parse(idStr)
	if err != nil {
		span.RecordError(err)
		c.JSON(http.StatusBadRequest, ErrorResponse{
			Error:   "invalid_id",
			Message: "Invalid user ID format",
			Code:    http.StatusBadRequest,
		})
		return
	}

	span.SetAttributes(attribute.String("user_id", userID.String()))

	// Soft delete by updating status
	query := `UPDATE users SET status = $2, updated_at = $3 WHERE id = $1 AND status != $2`
	now := time.Now()

	result, err := s.db.ExecContext(ctx, query, userID, UserStatusDeleted, now)
	if err != nil {
		span.RecordError(err)
		c.JSON(http.StatusInternalServerError, ErrorResponse{
			Error:   "database_error",
			Message: "Failed to delete user",
			Code:    http.StatusInternalServerError,
		})
		return
	}

	rowsAffected, err := result.RowsAffected()
	if err != nil {
		span.RecordError(err)
		c.JSON(http.StatusInternalServerError, ErrorResponse{
			Error:   "database_error",
			Message: "Failed to check deletion result",
			Code:    http.StatusInternalServerError,
		})
		return
	}

	if rowsAffected == 0 {
		c.JSON(http.StatusNotFound, ErrorResponse{
			Error:   "user_not_found",
			Message: "User not found or already deleted",
			Code:    http.StatusNotFound,
		})
		return
	}

	// Publish event to NATS
	eventData := map[string]interface{}{
		"user_id": userID,
	}
	eventJSON, _ := json.Marshal(eventData)
	s.nats.Publish("user.deleted", eventJSON)

	c.Status(http.StatusNoContent)
}
