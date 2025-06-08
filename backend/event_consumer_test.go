package main

import (
	"context"
	"database/sql"
	"encoding/json"
	"testing"
	"time"

	"github.com/google/uuid"
	"github.com/nats-io/nats.go"
	_ "github.com/lib/pq"
)

func TestEventConsumer_HandleUserEvent(t *testing.T) {
	// Skip if no NATS server available
	nc, err := nats.Connect("nats://localhost:4222")
	if err != nil {
		t.Skip("NATS server not available, skipping test")
	}
	defer nc.Close()

	// Create mock server (without database for this test)
	server := &Server{
		nats: nc,
	}

	consumer := NewEventConsumer(nc, server)

	tests := []struct {
		name    string
		subject string
		data    map[string]interface{}
	}{
		{
			name:    "user created event",
			subject: "user.created",
			data: map[string]interface{}{
				"user_id":             uuid.New().String(),
				"sam_account_name":    "testuser",
				"domain_controller_id": "DC01",
			},
		},
		{
			name:    "user updated event",
			subject: "user.updated",
			data: map[string]interface{}{
				"user_id":             uuid.New().String(),
				"sam_account_name":    "testuser",
				"domain_controller_id": "DC01",
			},
		},
		{
			name:    "user deleted event",
			subject: "user.deleted",
			data: map[string]interface{}{
				"user_id": uuid.New().String(),
			},
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			// Convert data to JSON
			jsonData, err := json.Marshal(tt.data)
			if err != nil {
				t.Fatalf("Failed to marshal test data: %v", err)
			}

			// Create NATS message
			msg := &nats.Msg{
				Subject: tt.subject,
				Data:    jsonData,
			}

			// This should not panic
			consumer.handleUserEvent(msg)
		})
	}
}

func TestEventConsumer_HandleGroupEvent(t *testing.T) {
	// Skip if no NATS server available
	nc, err := nats.Connect("nats://localhost:4222")
	if err != nil {
		t.Skip("NATS server not available, skipping test")
	}
	defer nc.Close()

	// Create mock server (without database for this test)
	server := &Server{
		nats: nc,
	}

	consumer := NewEventConsumer(nc, server)

	tests := []struct {
		name    string
		subject string
		data    map[string]interface{}
	}{
		{
			name:    "group created event",
			subject: "group.created",
			data: map[string]interface{}{
				"group_id":            uuid.New().String(),
				"sam_account_name":    "testgroup",
				"domain_controller_id": "DC01",
			},
		},
		{
			name:    "group updated event",
			subject: "group.updated",
			data: map[string]interface{}{
				"group_id":            uuid.New().String(),
				"sam_account_name":    "testgroup",
				"domain_controller_id": "DC01",
			},
		},
		{
			name:    "group deleted event",
			subject: "group.deleted",
			data: map[string]interface{}{
				"group_id": uuid.New().String(),
			},
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			// Convert data to JSON
			jsonData, err := json.Marshal(tt.data)
			if err != nil {
				t.Fatalf("Failed to marshal test data: %v", err)
			}

			// Create NATS message
			msg := &nats.Msg{
				Subject: tt.subject,
				Data:    jsonData,
			}

			// This should not panic
			consumer.handleGroupEvent(msg)
		})
	}
}

func TestEventConsumer_HandleGeneralEvent(t *testing.T) {
	// Skip if no NATS server available
	nc, err := nats.Connect("nats://localhost:4222")
	if err != nil {
		t.Skip("NATS server not available, skipping test")
	}
	defer nc.Close()

	// Create mock server (without database for this test)
	server := &Server{
		nats: nc,
	}

	consumer := NewEventConsumer(nc, server)

	tests := []struct {
		name    string
		subject string
		data    map[string]interface{}
	}{
		{
			name:    "general event",
			subject: "event.created",
			data: map[string]interface{}{
				"event_id":            uuid.New().String(),
				"event_type":          "login",
				"domain_controller_id": "DC01",
				"occurred_at":         time.Now().Format(time.RFC3339),
			},
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			// Convert data to JSON
			jsonData, err := json.Marshal(tt.data)
			if err != nil {
				t.Fatalf("Failed to marshal test data: %v", err)
			}

			// Create NATS message
			msg := &nats.Msg{
				Subject: tt.subject,
				Data:    jsonData,
			}

			// This should not panic
			consumer.handleGeneralEvent(msg)
		})
	}
}

func TestEventConsumer_CreateAuditEvent(t *testing.T) {
	// Skip if no database available
	db, err := sql.Open("postgres", "postgres://permafrost:dev_password_change_in_prod@localhost:5432/permafrost_test?sslmode=disable")
	if err != nil {
		t.Skip("Database not available, skipping test")
	}
	defer db.Close()

	if err := db.Ping(); err != nil {
		t.Skip("Database not accessible, skipping test")
	}

	// Create mock server with database
	server := &Server{
		db: db,
	}

	consumer := NewEventConsumer(nil, server)

	tests := []struct {
		name      string
		eventType string
		eventData map[string]interface{}
	}{
		{
			name:      "user created audit event",
			eventType: "user_created",
			eventData: map[string]interface{}{
				"user_id":             uuid.New().String(),
				"sam_account_name":    "testuser",
				"domain_controller_id": "DC01",
			},
		},
		{
			name:      "group modified audit event",
			eventType: "group_modified",
			eventData: map[string]interface{}{
				"group_id":            uuid.New().String(),
				"sam_account_name":    "testgroup",
				"domain_controller_id": "DC01",
			},
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			ctx := context.Background()
			
			// This should not panic and should create an audit event
			consumer.createAuditEvent(ctx, tt.eventType, tt.eventData)

			// Verify the event was created (optional, requires database setup)
			// You could add a query here to check if the event was inserted
		})
	}
}

func TestEventConsumer_InvalidJSON(t *testing.T) {
	// Skip if no NATS server available
	nc, err := nats.Connect("nats://localhost:4222")
	if err != nil {
		t.Skip("NATS server not available, skipping test")
	}
	defer nc.Close()

	// Create mock server
	server := &Server{
		nats: nc,
	}

	consumer := NewEventConsumer(nc, server)

	// Test with invalid JSON
	msg := &nats.Msg{
		Subject: "user.created",
		Data:    []byte("invalid json"),
	}

	// This should not panic, just log an error
	consumer.handleUserEvent(msg)
	consumer.handleGroupEvent(msg)
	consumer.handleGeneralEvent(msg)
}

func TestEventConsumer_MissingFields(t *testing.T) {
	// Skip if no NATS server available
	nc, err := nats.Connect("nats://localhost:4222")
	if err != nil {
		t.Skip("NATS server not available, skipping test")
	}
	defer nc.Close()

	// Create mock server
	server := &Server{
		nats: nc,
	}

	consumer := NewEventConsumer(nc, server)

	tests := []struct {
		name    string
		subject string
		data    map[string]interface{}
	}{
		{
			name:    "user event missing user_id",
			subject: "user.created",
			data: map[string]interface{}{
				"sam_account_name": "testuser",
			},
		},
		{
			name:    "group event missing group_id",
			subject: "group.created",
			data: map[string]interface{}{
				"sam_account_name": "testgroup",
			},
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			// Convert data to JSON
			jsonData, err := json.Marshal(tt.data)
			if err != nil {
				t.Fatalf("Failed to marshal test data: %v", err)
			}

			// Create NATS message
			msg := &nats.Msg{
				Subject: tt.subject,
				Data:    jsonData,
			}

			// This should not panic, just log an error
			if tt.subject == "user.created" {
				consumer.handleUserEvent(msg)
			} else {
				consumer.handleGroupEvent(msg)
			}
		})
	}
}
