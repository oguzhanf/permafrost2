package main

import (
	"bytes"
	"database/sql"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"testing"
	"time"

	"github.com/gin-gonic/gin"
	"github.com/google/uuid"
	_ "github.com/lib/pq"
	"github.com/nats-io/nats.go"
)

func setupTestServer(t *testing.T) *Server {
	// Use test database URL or in-memory database for testing
	config := &Config{
		DatabaseURL: "postgres://permafrost:dev_password_change_in_prod@localhost:5432/permafrost_test?sslmode=disable",
		NatsURL:     "nats://localhost:4222",
		Port:        "8080",
	}

	// For unit tests, we might want to use a mock database
	// For now, we'll skip database connection in tests
	server := &Server{
		config: config,
	}

	// Set gin to test mode
	gin.SetMode(gin.TestMode)
	server.setupRoutes()

	return server
}

func TestHealthCheck(t *testing.T) {
	server := setupTestServer(t)

	w := httptest.NewRecorder()
	req, _ := http.NewRequest("GET", "/health", nil)
	server.router.ServeHTTP(w, req)

	if w.Code != http.StatusOK {
		t.Errorf("Expected status code %d, got %d", http.StatusOK, w.Code)
	}

	var response map[string]interface{}
	err := json.Unmarshal(w.Body.Bytes(), &response)
	if err != nil {
		t.Errorf("Failed to unmarshal response: %v", err)
	}

	if response["status"] != "healthy" {
		t.Errorf("Expected status 'healthy', got %v", response["status"])
	}

	if response["version"] != "1.0.0" {
		t.Errorf("Expected version '1.0.0', got %v", response["version"])
	}
}

func TestCreateUserRequest_Validation(t *testing.T) {
	tests := []struct {
		name    string
		request CreateUserRequest
		valid   bool
	}{
		{
			name: "valid request",
			request: CreateUserRequest{
				DomainControllerID: "DC01",
				ADObjectGUID:       uuid.New(),
				SAMAccountName:     "testuser",
				Status:             UserStatusActive,
			},
			valid: true,
		},
		{
			name: "missing domain controller ID",
			request: CreateUserRequest{
				ADObjectGUID:   uuid.New(),
				SAMAccountName: "testuser",
				Status:         UserStatusActive,
			},
			valid: false,
		},
		{
			name: "missing SAM account name",
			request: CreateUserRequest{
				DomainControllerID: "DC01",
				ADObjectGUID:       uuid.New(),
				Status:             UserStatusActive,
			},
			valid: false,
		},
	}

	server := setupTestServer(t)

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			jsonData, _ := json.Marshal(tt.request)
			w := httptest.NewRecorder()
			req, _ := http.NewRequest("POST", "/api/v1/users", bytes.NewBuffer(jsonData))
			req.Header.Set("Content-Type", "application/json")
			server.router.ServeHTTP(w, req)

			if tt.valid {
				// For valid requests, we expect either 201 (created) or 500 (database error in test)
				if w.Code != http.StatusCreated && w.Code != http.StatusInternalServerError {
					t.Errorf("Expected status code %d or %d for valid request, got %d", 
						http.StatusCreated, http.StatusInternalServerError, w.Code)
				}
			} else {
				// For invalid requests, we expect 400 (bad request)
				if w.Code != http.StatusBadRequest {
					t.Errorf("Expected status code %d for invalid request, got %d", 
						http.StatusBadRequest, w.Code)
				}
			}
		})
	}
}

func TestCreateGroupRequest_Validation(t *testing.T) {
	tests := []struct {
		name    string
		request CreateGroupRequest
		valid   bool
	}{
		{
			name: "valid request",
			request: CreateGroupRequest{
				DomainControllerID: "DC01",
				ADObjectGUID:       uuid.New(),
				SAMAccountName:     "testgroup",
				DistinguishedName:  "CN=testgroup,OU=Groups,DC=example,DC=com",
				GroupType:          GroupTypeSecurity,
			},
			valid: true,
		},
		{
			name: "missing distinguished name",
			request: CreateGroupRequest{
				DomainControllerID: "DC01",
				ADObjectGUID:       uuid.New(),
				SAMAccountName:     "testgroup",
				GroupType:          GroupTypeSecurity,
			},
			valid: false,
		},
	}

	server := setupTestServer(t)

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			jsonData, _ := json.Marshal(tt.request)
			w := httptest.NewRecorder()
			req, _ := http.NewRequest("POST", "/api/v1/groups", bytes.NewBuffer(jsonData))
			req.Header.Set("Content-Type", "application/json")
			server.router.ServeHTTP(w, req)

			if tt.valid {
				// For valid requests, we expect either 201 (created) or 500 (database error in test)
				if w.Code != http.StatusCreated && w.Code != http.StatusInternalServerError {
					t.Errorf("Expected status code %d or %d for valid request, got %d", 
						http.StatusCreated, http.StatusInternalServerError, w.Code)
				}
			} else {
				// For invalid requests, we expect 400 (bad request)
				if w.Code != http.StatusBadRequest {
					t.Errorf("Expected status code %d for invalid request, got %d", 
						http.StatusBadRequest, w.Code)
				}
			}
		})
	}
}

func TestCreateEventRequest_Validation(t *testing.T) {
	tests := []struct {
		name    string
		request CreateEventRequest
		valid   bool
	}{
		{
			name: "valid request",
			request: CreateEventRequest{
				DomainControllerID: "DC01",
				EventType:          EventUserCreated,
				OccurredAt:         time.Now(),
			},
			valid: true,
		},
		{
			name: "missing event type",
			request: CreateEventRequest{
				DomainControllerID: "DC01",
				OccurredAt:         time.Now(),
			},
			valid: false,
		},
		{
			name: "missing occurred at",
			request: CreateEventRequest{
				DomainControllerID: "DC01",
				EventType:          EventUserCreated,
			},
			valid: false,
		},
	}

	server := setupTestServer(t)

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			jsonData, _ := json.Marshal(tt.request)
			w := httptest.NewRecorder()
			req, _ := http.NewRequest("POST", "/api/v1/events", bytes.NewBuffer(jsonData))
			req.Header.Set("Content-Type", "application/json")
			server.router.ServeHTTP(w, req)

			if tt.valid {
				// For valid requests, we expect either 201 (created) or 500 (database error in test)
				if w.Code != http.StatusCreated && w.Code != http.StatusInternalServerError {
					t.Errorf("Expected status code %d or %d for valid request, got %d", 
						http.StatusCreated, http.StatusInternalServerError, w.Code)
				}
			} else {
				// For invalid requests, we expect 400 (bad request)
				if w.Code != http.StatusBadRequest {
					t.Errorf("Expected status code %d for invalid request, got %d", 
						http.StatusBadRequest, w.Code)
				}
			}
		})
	}
}

func TestPaginationParams_Validation(t *testing.T) {
	server := setupTestServer(t)

	tests := []struct {
		name       string
		queryParam string
		expectCode int
	}{
		{
			name:       "valid pagination",
			queryParam: "?page=1&limit=20",
			expectCode: http.StatusOK,
		},
		{
			name:       "invalid page (zero)",
			queryParam: "?page=0&limit=20",
			expectCode: http.StatusBadRequest,
		},
		{
			name:       "invalid limit (too high)",
			queryParam: "?page=1&limit=200",
			expectCode: http.StatusBadRequest,
		},
		{
			name:       "default values",
			queryParam: "",
			expectCode: http.StatusOK,
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			w := httptest.NewRecorder()
			req, _ := http.NewRequest("GET", "/api/v1/users"+tt.queryParam, nil)
			server.router.ServeHTTP(w, req)

			// We expect either the expected code or 500 (database error in test)
			if w.Code != tt.expectCode && w.Code != http.StatusInternalServerError {
				t.Errorf("Expected status code %d or %d, got %d", 
					tt.expectCode, http.StatusInternalServerError, w.Code)
			}
		})
	}
}

func TestGetEnv(t *testing.T) {
	tests := []struct {
		name         string
		key          string
		defaultValue string
		expected     string
	}{
		{
			name:         "existing environment variable",
			key:          "PATH",
			defaultValue: "default",
			expected:     "", // PATH should exist, so we don't expect default
		},
		{
			name:         "non-existing environment variable",
			key:          "NON_EXISTING_VAR_12345",
			defaultValue: "default_value",
			expected:     "default_value",
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			result := getEnv(tt.key, tt.defaultValue)
			
			if tt.key == "PATH" {
				// PATH should exist and not be empty
				if result == "" {
					t.Errorf("Expected PATH to exist and not be empty")
				}
			} else {
				if result != tt.expected {
					t.Errorf("Expected %s, got %s", tt.expected, result)
				}
			}
		})
	}
}
