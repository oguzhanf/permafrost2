package main

import (
	"context"
	"encoding/json"
	"log"
	"time"

	"github.com/nats-io/nats.go"
	"go.opentelemetry.io/otel"
	"go.opentelemetry.io/otel/attribute"
)

// EventConsumer handles NATS event consumption
type EventConsumer struct {
	nats   *nats.Conn
	server *Server
}

// NewEventConsumer creates a new event consumer
func NewEventConsumer(nc *nats.Conn, server *Server) *EventConsumer {
	return &EventConsumer{
		nats:   nc,
		server: server,
	}
}

// Start begins consuming events from NATS
func (ec *EventConsumer) Start(ctx context.Context) error {
	log.Println("Starting NATS event consumer...")

	// Subscribe to user events
	_, err := ec.nats.Subscribe("user.*", ec.handleUserEvent)
	if err != nil {
		return err
	}

	// Subscribe to group events
	_, err = ec.nats.Subscribe("group.*", ec.handleGroupEvent)
	if err != nil {
		return err
	}

	// Subscribe to general events
	_, err = ec.nats.Subscribe("event.*", ec.handleGeneralEvent)
	if err != nil {
		return err
	}

	log.Println("NATS event consumer started successfully")
	return nil
}

// handleUserEvent processes user-related events
func (ec *EventConsumer) handleUserEvent(msg *nats.Msg) {
	ctx, span := tracer.Start(context.Background(), "handleUserEvent")
	defer span.End()

	span.SetAttributes(
		attribute.String("subject", msg.Subject),
		attribute.Int("data_size", len(msg.Data)),
	)

	var eventData map[string]interface{}
	if err := json.Unmarshal(msg.Data, &eventData); err != nil {
		log.Printf("Failed to unmarshal user event: %v", err)
		span.RecordError(err)
		return
	}

	log.Printf("Received user event: %s - %v", msg.Subject, eventData)

	// Process based on event type
	switch msg.Subject {
	case "user.created":
		ec.handleUserCreated(ctx, eventData)
	case "user.updated":
		ec.handleUserUpdated(ctx, eventData)
	case "user.deleted":
		ec.handleUserDeleted(ctx, eventData)
	default:
		log.Printf("Unknown user event type: %s", msg.Subject)
	}
}

// handleGroupEvent processes group-related events
func (ec *EventConsumer) handleGroupEvent(msg *nats.Msg) {
	ctx, span := tracer.Start(context.Background(), "handleGroupEvent")
	defer span.End()

	span.SetAttributes(
		attribute.String("subject", msg.Subject),
		attribute.Int("data_size", len(msg.Data)),
	)

	var eventData map[string]interface{}
	if err := json.Unmarshal(msg.Data, &eventData); err != nil {
		log.Printf("Failed to unmarshal group event: %v", err)
		span.RecordError(err)
		return
	}

	log.Printf("Received group event: %s - %v", msg.Subject, eventData)

	// Process based on event type
	switch msg.Subject {
	case "group.created":
		ec.handleGroupCreated(ctx, eventData)
	case "group.updated":
		ec.handleGroupUpdated(ctx, eventData)
	case "group.deleted":
		ec.handleGroupDeleted(ctx, eventData)
	default:
		log.Printf("Unknown group event type: %s", msg.Subject)
	}
}

// handleGeneralEvent processes general system events
func (ec *EventConsumer) handleGeneralEvent(msg *nats.Msg) {
	ctx, span := tracer.Start(context.Background(), "handleGeneralEvent")
	defer span.End()

	span.SetAttributes(
		attribute.String("subject", msg.Subject),
		attribute.Int("data_size", len(msg.Data)),
	)

	var eventData map[string]interface{}
	if err := json.Unmarshal(msg.Data, &eventData); err != nil {
		log.Printf("Failed to unmarshal general event: %v", err)
		span.RecordError(err)
		return
	}

	log.Printf("Received general event: %s - %v", msg.Subject, eventData)

	// Create audit event in database
	ec.createAuditEvent(ctx, msg.Subject, eventData)
}

// handleUserCreated processes user creation events
func (ec *EventConsumer) handleUserCreated(ctx context.Context, eventData map[string]interface{}) {
	span := otel.Tracer("event-consumer").StartSpan(ctx, "handleUserCreated")
	defer span.End()

	// Extract user information
	userID, ok := eventData["user_id"].(string)
	if !ok {
		log.Printf("Invalid user_id in user.created event")
		return
	}

	samAccountName, _ := eventData["sam_account_name"].(string)
	domainControllerID, _ := eventData["domain_controller_id"].(string)

	span.SetAttributes(
		attribute.String("user_id", userID),
		attribute.String("sam_account_name", samAccountName),
		attribute.String("domain_controller_id", domainControllerID),
	)

	// Create audit event
	ec.createAuditEvent(ctx, "user_created", eventData)

	log.Printf("Processed user creation: %s (%s)", samAccountName, userID)
}

// handleUserUpdated processes user update events
func (ec *EventConsumer) handleUserUpdated(ctx context.Context, eventData map[string]interface{}) {
	span := otel.Tracer("event-consumer").StartSpan(ctx, "handleUserUpdated")
	defer span.End()

	userID, ok := eventData["user_id"].(string)
	if !ok {
		log.Printf("Invalid user_id in user.updated event")
		return
	}

	samAccountName, _ := eventData["sam_account_name"].(string)
	domainControllerID, _ := eventData["domain_controller_id"].(string)

	span.SetAttributes(
		attribute.String("user_id", userID),
		attribute.String("sam_account_name", samAccountName),
		attribute.String("domain_controller_id", domainControllerID),
	)

	// Create audit event
	ec.createAuditEvent(ctx, "user_modified", eventData)

	log.Printf("Processed user update: %s (%s)", samAccountName, userID)
}

// handleUserDeleted processes user deletion events
func (ec *EventConsumer) handleUserDeleted(ctx context.Context, eventData map[string]interface{}) {
	span := otel.Tracer("event-consumer").StartSpan(ctx, "handleUserDeleted")
	defer span.End()

	userID, ok := eventData["user_id"].(string)
	if !ok {
		log.Printf("Invalid user_id in user.deleted event")
		return
	}

	span.SetAttributes(attribute.String("user_id", userID))

	// Create audit event
	ec.createAuditEvent(ctx, "user_deleted", eventData)

	log.Printf("Processed user deletion: %s", userID)
}

// handleGroupCreated processes group creation events
func (ec *EventConsumer) handleGroupCreated(ctx context.Context, eventData map[string]interface{}) {
	span := otel.Tracer("event-consumer").StartSpan(ctx, "handleGroupCreated")
	defer span.End()

	groupID, ok := eventData["group_id"].(string)
	if !ok {
		log.Printf("Invalid group_id in group.created event")
		return
	}

	samAccountName, _ := eventData["sam_account_name"].(string)
	domainControllerID, _ := eventData["domain_controller_id"].(string)

	span.SetAttributes(
		attribute.String("group_id", groupID),
		attribute.String("sam_account_name", samAccountName),
		attribute.String("domain_controller_id", domainControllerID),
	)

	// Create audit event
	ec.createAuditEvent(ctx, "group_created", eventData)

	log.Printf("Processed group creation: %s (%s)", samAccountName, groupID)
}

// handleGroupUpdated processes group update events
func (ec *EventConsumer) handleGroupUpdated(ctx context.Context, eventData map[string]interface{}) {
	span := otel.Tracer("event-consumer").StartSpan(ctx, "handleGroupUpdated")
	defer span.End()

	groupID, ok := eventData["group_id"].(string)
	if !ok {
		log.Printf("Invalid group_id in group.updated event")
		return
	}

	samAccountName, _ := eventData["sam_account_name"].(string)
	domainControllerID, _ := eventData["domain_controller_id"].(string)

	span.SetAttributes(
		attribute.String("group_id", groupID),
		attribute.String("sam_account_name", samAccountName),
		attribute.String("domain_controller_id", domainControllerID),
	)

	// Create audit event
	ec.createAuditEvent(ctx, "group_modified", eventData)

	log.Printf("Processed group update: %s (%s)", samAccountName, groupID)
}

// handleGroupDeleted processes group deletion events
func (ec *EventConsumer) handleGroupDeleted(ctx context.Context, eventData map[string]interface{}) {
	span := otel.Tracer("event-consumer").StartSpan(ctx, "handleGroupDeleted")
	defer span.End()

	groupID, ok := eventData["group_id"].(string)
	if !ok {
		log.Printf("Invalid group_id in group.deleted event")
		return
	}

	span.SetAttributes(attribute.String("group_id", groupID))

	// Create audit event
	ec.createAuditEvent(ctx, "group_deleted", eventData)

	log.Printf("Processed group deletion: %s", groupID)
}

// createAuditEvent creates an audit event in the database
func (ec *EventConsumer) createAuditEvent(ctx context.Context, eventType string, eventData map[string]interface{}) {
	span := otel.Tracer("event-consumer").StartSpan(ctx, "createAuditEvent")
	defer span.End()

	// Extract common fields
	domainControllerID, _ := eventData["domain_controller_id"].(string)
	if domainControllerID == "" {
		domainControllerID = "unknown"
	}

	// Convert event data to JSON
	eventDataJSON, err := json.Marshal(eventData)
	if err != nil {
		log.Printf("Failed to marshal event data: %v", err)
		span.RecordError(err)
		return
	}

	// Insert into database
	query := `INSERT INTO events (id, domain_controller_id, event_type, event_data, occurred_at, created_at) 
			  VALUES (gen_random_uuid(), $1, $2, $3, $4, $5)`

	now := time.Now()
	_, err = ec.server.db.ExecContext(ctx, query, domainControllerID, eventType, eventDataJSON, now, now)
	if err != nil {
		log.Printf("Failed to create audit event: %v", err)
		span.RecordError(err)
		return
	}

	span.SetAttributes(
		attribute.String("event_type", eventType),
		attribute.String("domain_controller_id", domainControllerID),
	)

	log.Printf("Created audit event: %s", eventType)
}
