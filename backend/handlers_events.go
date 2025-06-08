package main

import (
	"encoding/json"
	"fmt"
	"math"
	"net/http"
	"time"

	"github.com/gin-gonic/gin"
	"github.com/google/uuid"
	"go.opentelemetry.io/otel/attribute"
)

// @Summary Get events
// @Description Get a paginated list of events
// @Tags events
// @Accept json
// @Produce json
// @Param page query int false "Page number" default(1)
// @Param limit query int false "Items per page" default(20)
// @Param event_type query string false "Filter by event type"
// @Param user_id query string false "Filter by user ID"
// @Param group_id query string false "Filter by group ID"
// @Param domain_controller_id query string false "Filter by domain controller"
// @Param from query string false "Filter events from date (RFC3339)"
// @Param to query string false "Filter events to date (RFC3339)"
// @Success 200 {object} PaginatedResponse
// @Failure 400 {object} ErrorResponse
// @Failure 500 {object} ErrorResponse
// @Router /api/v1/events [get]
func (s *Server) getEvents(c *gin.Context) {
	ctx, span := tracer.Start(c.Request.Context(), "getEvents")
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

	eventType := c.Query("event_type")
	userIDStr := c.Query("user_id")
	groupIDStr := c.Query("group_id")
	domainControllerID := c.Query("domain_controller_id")
	fromStr := c.Query("from")
	toStr := c.Query("to")

	span.SetAttributes(
		attribute.Int("page", params.Page),
		attribute.Int("limit", params.Limit),
		attribute.String("event_type", eventType),
		attribute.String("user_id", userIDStr),
		attribute.String("group_id", groupIDStr),
		attribute.String("domain_controller_id", domainControllerID),
	)

	// Build query
	query := `SELECT id, domain_controller_id, event_type, user_id, group_id, 
			  event_data, source_ip, user_agent, occurred_at, created_at
			  FROM events WHERE 1=1`
	countQuery := `SELECT COUNT(*) FROM events WHERE 1=1`
	args := []interface{}{}
	argIndex := 1

	if eventType != "" {
		query += fmt.Sprintf(" AND event_type = $%d", argIndex)
		countQuery += fmt.Sprintf(" AND event_type = $%d", argIndex)
		args = append(args, eventType)
		argIndex++
	}

	if userIDStr != "" {
		userID, err := uuid.Parse(userIDStr)
		if err != nil {
			span.RecordError(err)
			c.JSON(http.StatusBadRequest, ErrorResponse{
				Error:   "invalid_user_id",
				Message: "Invalid user ID format",
				Code:    http.StatusBadRequest,
			})
			return
		}
		query += fmt.Sprintf(" AND user_id = $%d", argIndex)
		countQuery += fmt.Sprintf(" AND user_id = $%d", argIndex)
		args = append(args, userID)
		argIndex++
	}

	if groupIDStr != "" {
		groupID, err := uuid.Parse(groupIDStr)
		if err != nil {
			span.RecordError(err)
			c.JSON(http.StatusBadRequest, ErrorResponse{
				Error:   "invalid_group_id",
				Message: "Invalid group ID format",
				Code:    http.StatusBadRequest,
			})
			return
		}
		query += fmt.Sprintf(" AND group_id = $%d", argIndex)
		countQuery += fmt.Sprintf(" AND group_id = $%d", argIndex)
		args = append(args, groupID)
		argIndex++
	}

	if domainControllerID != "" {
		query += fmt.Sprintf(" AND domain_controller_id = $%d", argIndex)
		countQuery += fmt.Sprintf(" AND domain_controller_id = $%d", argIndex)
		args = append(args, domainControllerID)
		argIndex++
	}

	if fromStr != "" {
		fromTime, err := time.Parse(time.RFC3339, fromStr)
		if err != nil {
			span.RecordError(err)
			c.JSON(http.StatusBadRequest, ErrorResponse{
				Error:   "invalid_from_date",
				Message: "Invalid from date format, use RFC3339",
				Code:    http.StatusBadRequest,
			})
			return
		}
		query += fmt.Sprintf(" AND occurred_at >= $%d", argIndex)
		countQuery += fmt.Sprintf(" AND occurred_at >= $%d", argIndex)
		args = append(args, fromTime)
		argIndex++
	}

	if toStr != "" {
		toTime, err := time.Parse(time.RFC3339, toStr)
		if err != nil {
			span.RecordError(err)
			c.JSON(http.StatusBadRequest, ErrorResponse{
				Error:   "invalid_to_date",
				Message: "Invalid to date format, use RFC3339",
				Code:    http.StatusBadRequest,
			})
			return
		}
		query += fmt.Sprintf(" AND occurred_at <= $%d", argIndex)
		countQuery += fmt.Sprintf(" AND occurred_at <= $%d", argIndex)
		args = append(args, toTime)
		argIndex++
	}

	// Get total count
	var total int64
	err := s.db.QueryRowContext(ctx, countQuery, args...).Scan(&total)
	if err != nil {
		span.RecordError(err)
		c.JSON(http.StatusInternalServerError, ErrorResponse{
			Error:   "database_error",
			Message: "Failed to count events",
			Code:    http.StatusInternalServerError,
		})
		return
	}

	// Add pagination and ordering
	offset := (params.Page - 1) * params.Limit
	query += fmt.Sprintf(" ORDER BY occurred_at DESC LIMIT $%d OFFSET $%d", argIndex, argIndex+1)
	args = append(args, params.Limit, offset)

	// Execute query
	rows, err := s.db.QueryContext(ctx, query, args...)
	if err != nil {
		span.RecordError(err)
		c.JSON(http.StatusInternalServerError, ErrorResponse{
			Error:   "database_error",
			Message: "Failed to fetch events",
			Code:    http.StatusInternalServerError,
		})
		return
	}
	defer rows.Close()

	var events []Event
	for rows.Next() {
		var event Event
		var eventDataBytes []byte

		err := rows.Scan(
			&event.ID, &event.DomainControllerID, &event.EventType, &event.UserID, &event.GroupID,
			&eventDataBytes, &event.SourceIP, &event.UserAgent, &event.OccurredAt, &event.CreatedAt,
		)
		if err != nil {
			span.RecordError(err)
			c.JSON(http.StatusInternalServerError, ErrorResponse{
				Error:   "database_error",
				Message: "Failed to scan event",
				Code:    http.StatusInternalServerError,
			})
			return
		}

		// Parse event data JSON
		if eventDataBytes != nil {
			if err := json.Unmarshal(eventDataBytes, &event.EventData); err != nil {
				span.RecordError(err)
				// Log error but continue with empty event data
				event.EventData = make(map[string]interface{})
			}
		} else {
			event.EventData = make(map[string]interface{})
		}

		events = append(events, event)
	}

	totalPages := int(math.Ceil(float64(total) / float64(params.Limit)))

	response := PaginatedResponse{
		Data:       events,
		Page:       params.Page,
		Limit:      params.Limit,
		Total:      total,
		TotalPages: totalPages,
	}

	span.SetAttributes(attribute.Int64("total_events", total))
	c.JSON(http.StatusOK, response)
}

// @Summary Create event
// @Description Create a new event
// @Tags events
// @Accept json
// @Produce json
// @Param event body CreateEventRequest true "Event data"
// @Success 201 {object} Event
// @Failure 400 {object} ErrorResponse
// @Failure 500 {object} ErrorResponse
// @Router /api/v1/events [post]
func (s *Server) createEvent(c *gin.Context) {
	ctx, span := tracer.Start(c.Request.Context(), "createEvent")
	defer span.End()

	var req CreateEventRequest
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
		attribute.String("event_type", string(req.EventType)),
		attribute.String("domain_controller_id", req.DomainControllerID),
	)

	now := time.Now()
	eventID := uuid.New()

	// Serialize event data
	var eventDataBytes []byte
	var err error
	if req.EventData != nil {
		eventDataBytes, err = json.Marshal(req.EventData)
		if err != nil {
			span.RecordError(err)
			c.JSON(http.StatusBadRequest, ErrorResponse{
				Error:   "invalid_event_data",
				Message: "Failed to serialize event data",
				Code:    http.StatusBadRequest,
			})
			return
		}
	}

	query := `INSERT INTO events (id, domain_controller_id, event_type, user_id, group_id, 
			  event_data, source_ip, user_agent, occurred_at, created_at) 
			  VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)
			  RETURNING id, created_at`

	var createdEvent Event
	err = s.db.QueryRowContext(ctx, query,
		eventID, req.DomainControllerID, req.EventType, req.UserID, req.GroupID,
		eventDataBytes, req.SourceIP, req.UserAgent, req.OccurredAt, now,
	).Scan(&createdEvent.ID, &createdEvent.CreatedAt)

	if err != nil {
		span.RecordError(err)
		c.JSON(http.StatusInternalServerError, ErrorResponse{
			Error:   "database_error",
			Message: "Failed to create event",
			Code:    http.StatusInternalServerError,
		})
		return
	}

	// Fill in the rest of the event data
	createdEvent.DomainControllerID = req.DomainControllerID
	createdEvent.EventType = req.EventType
	createdEvent.UserID = req.UserID
	createdEvent.GroupID = req.GroupID
	createdEvent.EventData = req.EventData
	createdEvent.SourceIP = req.SourceIP
	createdEvent.UserAgent = req.UserAgent
	createdEvent.OccurredAt = req.OccurredAt

	// Publish event to NATS
	eventData := map[string]interface{}{
		"event_id":            createdEvent.ID,
		"event_type":          createdEvent.EventType,
		"domain_controller_id": createdEvent.DomainControllerID,
		"occurred_at":         createdEvent.OccurredAt,
	}
	eventJSON, _ := json.Marshal(eventData)
	s.nats.Publish("event.created", eventJSON)

	span.SetAttributes(attribute.String("created_event_id", createdEvent.ID.String()))
	c.JSON(http.StatusCreated, createdEvent)
}
