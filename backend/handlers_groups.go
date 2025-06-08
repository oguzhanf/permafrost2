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
	"go.opentelemetry.io/otel/attribute"
)

// @Summary Get groups
// @Description Get a paginated list of groups
// @Tags groups
// @Accept json
// @Produce json
// @Param page query int false "Page number" default(1)
// @Param limit query int false "Items per page" default(20)
// @Param group_type query string false "Filter by group type"
// @Param domain_controller_id query string false "Filter by domain controller"
// @Success 200 {object} PaginatedResponse
// @Failure 400 {object} ErrorResponse
// @Failure 500 {object} ErrorResponse
// @Router /api/v1/groups [get]
func (s *Server) getGroups(c *gin.Context) {
	ctx, span := tracer.Start(c.Request.Context(), "getGroups")
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

	groupType := c.Query("group_type")
	domainControllerID := c.Query("domain_controller_id")

	span.SetAttributes(
		attribute.Int("page", params.Page),
		attribute.Int("limit", params.Limit),
		attribute.String("group_type", groupType),
		attribute.String("domain_controller_id", domainControllerID),
	)

	// Build query
	query := `SELECT id, domain_controller_id, ad_object_guid, sam_account_name, 
			  display_name, description, group_type, distinguished_name,
			  created_at, updated_at, last_seen_at 
			  FROM groups WHERE 1=1`
	countQuery := `SELECT COUNT(*) FROM groups WHERE 1=1`
	args := []interface{}{}
	argIndex := 1

	if groupType != "" {
		query += fmt.Sprintf(" AND group_type = $%d", argIndex)
		countQuery += fmt.Sprintf(" AND group_type = $%d", argIndex)
		args = append(args, groupType)
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
			Message: "Failed to count groups",
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
			Message: "Failed to fetch groups",
			Code:    http.StatusInternalServerError,
		})
		return
	}
	defer rows.Close()

	var groups []Group
	for rows.Next() {
		var group Group
		err := rows.Scan(
			&group.ID, &group.DomainControllerID, &group.ADObjectGUID, &group.SAMAccountName,
			&group.DisplayName, &group.Description, &group.GroupType, &group.DistinguishedName,
			&group.CreatedAt, &group.UpdatedAt, &group.LastSeenAt,
		)
		if err != nil {
			span.RecordError(err)
			c.JSON(http.StatusInternalServerError, ErrorResponse{
				Error:   "database_error",
				Message: "Failed to scan group",
				Code:    http.StatusInternalServerError,
			})
			return
		}
		groups = append(groups, group)
	}

	totalPages := int(math.Ceil(float64(total) / float64(params.Limit)))

	response := PaginatedResponse{
		Data:       groups,
		Page:       params.Page,
		Limit:      params.Limit,
		Total:      total,
		TotalPages: totalPages,
	}

	span.SetAttributes(attribute.Int64("total_groups", total))
	c.JSON(http.StatusOK, response)
}

// @Summary Get group by ID
// @Description Get a specific group by ID
// @Tags groups
// @Accept json
// @Produce json
// @Param id path string true "Group ID"
// @Success 200 {object} Group
// @Failure 400 {object} ErrorResponse
// @Failure 404 {object} ErrorResponse
// @Failure 500 {object} ErrorResponse
// @Router /api/v1/groups/{id} [get]
func (s *Server) getGroup(c *gin.Context) {
	ctx, span := tracer.Start(c.Request.Context(), "getGroup")
	defer span.End()

	idStr := c.Param("id")
	groupID, err := uuid.Parse(idStr)
	if err != nil {
		span.RecordError(err)
		c.JSON(http.StatusBadRequest, ErrorResponse{
			Error:   "invalid_id",
			Message: "Invalid group ID format",
			Code:    http.StatusBadRequest,
		})
		return
	}

	span.SetAttributes(attribute.String("group_id", groupID.String()))

	query := `SELECT id, domain_controller_id, ad_object_guid, sam_account_name, 
			  display_name, description, group_type, distinguished_name,
			  created_at, updated_at, last_seen_at 
			  FROM groups WHERE id = $1`

	var group Group
	err = s.db.QueryRowContext(ctx, query, groupID).Scan(
		&group.ID, &group.DomainControllerID, &group.ADObjectGUID, &group.SAMAccountName,
		&group.DisplayName, &group.Description, &group.GroupType, &group.DistinguishedName,
		&group.CreatedAt, &group.UpdatedAt, &group.LastSeenAt,
	)

	if err == sql.ErrNoRows {
		c.JSON(http.StatusNotFound, ErrorResponse{
			Error:   "group_not_found",
			Message: "Group not found",
			Code:    http.StatusNotFound,
		})
		return
	}

	if err != nil {
		span.RecordError(err)
		c.JSON(http.StatusInternalServerError, ErrorResponse{
			Error:   "database_error",
			Message: "Failed to fetch group",
			Code:    http.StatusInternalServerError,
		})
		return
	}

	c.JSON(http.StatusOK, group)
}

// @Summary Create group
// @Description Create a new group
// @Tags groups
// @Accept json
// @Produce json
// @Param group body CreateGroupRequest true "Group data"
// @Success 201 {object} Group
// @Failure 400 {object} ErrorResponse
// @Failure 409 {object} ErrorResponse
// @Failure 500 {object} ErrorResponse
// @Router /api/v1/groups [post]
func (s *Server) createGroup(c *gin.Context) {
	ctx, span := tracer.Start(c.Request.Context(), "createGroup")
	defer span.End()

	var req CreateGroupRequest
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

	// Set default group type if not provided
	if req.GroupType == "" {
		req.GroupType = GroupTypeSecurity
	}

	now := time.Now()
	groupID := uuid.New()

	query := `INSERT INTO groups (id, domain_controller_id, ad_object_guid, sam_account_name, 
			  display_name, description, group_type, distinguished_name,
			  created_at, updated_at, last_seen_at) 
			  VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11)
			  RETURNING id, created_at, updated_at`

	var createdGroup Group
	err := s.db.QueryRowContext(ctx, query,
		groupID, req.DomainControllerID, req.ADObjectGUID, req.SAMAccountName,
		req.DisplayName, req.Description, req.GroupType, req.DistinguishedName,
		now, now, now,
	).Scan(&createdGroup.ID, &createdGroup.CreatedAt, &createdGroup.UpdatedAt)

	if err != nil {
		span.RecordError(err)
		if err.Error() == `pq: duplicate key value violates unique constraint "groups_ad_object_guid_key"` {
			c.JSON(http.StatusConflict, ErrorResponse{
				Error:   "group_exists",
				Message: "Group with this AD Object GUID already exists",
				Code:    http.StatusConflict,
			})
			return
		}
		c.JSON(http.StatusInternalServerError, ErrorResponse{
			Error:   "database_error",
			Message: "Failed to create group",
			Code:    http.StatusInternalServerError,
		})
		return
	}

	// Fill in the rest of the group data
	createdGroup.DomainControllerID = req.DomainControllerID
	createdGroup.ADObjectGUID = req.ADObjectGUID
	createdGroup.SAMAccountName = req.SAMAccountName
	createdGroup.DisplayName = req.DisplayName
	createdGroup.Description = req.Description
	createdGroup.GroupType = req.GroupType
	createdGroup.DistinguishedName = req.DistinguishedName
	createdGroup.LastSeenAt = now

	// Publish event to NATS
	eventData := map[string]interface{}{
		"group_id":            createdGroup.ID,
		"sam_account_name":    createdGroup.SAMAccountName,
		"domain_controller_id": createdGroup.DomainControllerID,
	}
	eventJSON, _ := json.Marshal(eventData)
	s.nats.Publish("group.created", eventJSON)

	span.SetAttributes(attribute.String("created_group_id", createdGroup.ID.String()))
	c.JSON(http.StatusCreated, createdGroup)
}

// @Summary Update group
// @Description Update an existing group
// @Tags groups
// @Accept json
// @Produce json
// @Param id path string true "Group ID"
// @Param group body UpdateGroupRequest true "Group data"
// @Success 200 {object} Group
// @Failure 400 {object} ErrorResponse
// @Failure 404 {object} ErrorResponse
// @Failure 500 {object} ErrorResponse
// @Router /api/v1/groups/{id} [put]
func (s *Server) updateGroup(c *gin.Context) {
	ctx, span := tracer.Start(c.Request.Context(), "updateGroup")
	defer span.End()

	idStr := c.Param("id")
	groupID, err := uuid.Parse(idStr)
	if err != nil {
		span.RecordError(err)
		c.JSON(http.StatusBadRequest, ErrorResponse{
			Error:   "invalid_id",
			Message: "Invalid group ID format",
			Code:    http.StatusBadRequest,
		})
		return
	}

	var req UpdateGroupRequest
	if err := c.ShouldBindJSON(&req); err != nil {
		span.RecordError(err)
		c.JSON(http.StatusBadRequest, ErrorResponse{
			Error:   "invalid_request",
			Message: err.Error(),
			Code:    http.StatusBadRequest,
		})
		return
	}

	span.SetAttributes(attribute.String("group_id", groupID.String()))

	// Check if group exists
	var exists bool
	err = s.db.QueryRowContext(ctx, "SELECT EXISTS(SELECT 1 FROM groups WHERE id = $1)", groupID).Scan(&exists)
	if err != nil {
		span.RecordError(err)
		c.JSON(http.StatusInternalServerError, ErrorResponse{
			Error:   "database_error",
			Message: "Failed to check group existence",
			Code:    http.StatusInternalServerError,
		})
		return
	}

	if !exists {
		c.JSON(http.StatusNotFound, ErrorResponse{
			Error:   "group_not_found",
			Message: "Group not found",
			Code:    http.StatusNotFound,
		})
		return
	}

	// Update group
	query := `UPDATE groups SET
			  display_name = $2, description = $3, group_type = $4, distinguished_name = $5,
			  updated_at = $6, last_seen_at = $7
			  WHERE id = $1`

	now := time.Now()
	_, err = s.db.ExecContext(ctx, query,
		groupID, req.DisplayName, req.Description, req.GroupType, req.DistinguishedName,
		now, now,
	)

	if err != nil {
		span.RecordError(err)
		c.JSON(http.StatusInternalServerError, ErrorResponse{
			Error:   "database_error",
			Message: "Failed to update group",
			Code:    http.StatusInternalServerError,
		})
		return
	}

	// Fetch updated group
	var updatedGroup Group
	selectQuery := `SELECT id, domain_controller_id, ad_object_guid, sam_account_name,
					display_name, description, group_type, distinguished_name,
					created_at, updated_at, last_seen_at
					FROM groups WHERE id = $1`

	err = s.db.QueryRowContext(ctx, selectQuery, groupID).Scan(
		&updatedGroup.ID, &updatedGroup.DomainControllerID, &updatedGroup.ADObjectGUID, &updatedGroup.SAMAccountName,
		&updatedGroup.DisplayName, &updatedGroup.Description, &updatedGroup.GroupType, &updatedGroup.DistinguishedName,
		&updatedGroup.CreatedAt, &updatedGroup.UpdatedAt, &updatedGroup.LastSeenAt,
	)

	if err != nil {
		span.RecordError(err)
		c.JSON(http.StatusInternalServerError, ErrorResponse{
			Error:   "database_error",
			Message: "Failed to fetch updated group",
			Code:    http.StatusInternalServerError,
		})
		return
	}

	// Publish event to NATS
	eventData := map[string]interface{}{
		"group_id":            updatedGroup.ID,
		"sam_account_name":    updatedGroup.SAMAccountName,
		"domain_controller_id": updatedGroup.DomainControllerID,
	}
	eventJSON, _ := json.Marshal(eventData)
	s.nats.Publish("group.updated", eventJSON)

	c.JSON(http.StatusOK, updatedGroup)
}

// @Summary Delete group
// @Description Delete a group
// @Tags groups
// @Accept json
// @Produce json
// @Param id path string true "Group ID"
// @Success 204
// @Failure 400 {object} ErrorResponse
// @Failure 404 {object} ErrorResponse
// @Failure 500 {object} ErrorResponse
// @Router /api/v1/groups/{id} [delete]
func (s *Server) deleteGroup(c *gin.Context) {
	ctx, span := tracer.Start(c.Request.Context(), "deleteGroup")
	defer span.End()

	idStr := c.Param("id")
	groupID, err := uuid.Parse(idStr)
	if err != nil {
		span.RecordError(err)
		c.JSON(http.StatusBadRequest, ErrorResponse{
			Error:   "invalid_id",
			Message: "Invalid group ID format",
			Code:    http.StatusBadRequest,
		})
		return
	}

	span.SetAttributes(attribute.String("group_id", groupID.String()))

	// Delete group (hard delete)
	query := `DELETE FROM groups WHERE id = $1`

	result, err := s.db.ExecContext(ctx, query, groupID)
	if err != nil {
		span.RecordError(err)
		c.JSON(http.StatusInternalServerError, ErrorResponse{
			Error:   "database_error",
			Message: "Failed to delete group",
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
			Error:   "group_not_found",
			Message: "Group not found",
			Code:    http.StatusNotFound,
		})
		return
	}

	// Publish event to NATS
	eventData := map[string]interface{}{
		"group_id": groupID,
	}
	eventJSON, _ := json.Marshal(eventData)
	s.nats.Publish("group.deleted", eventJSON)

	c.Status(http.StatusNoContent)
}
