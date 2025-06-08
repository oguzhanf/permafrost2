package main

import (
	"time"

	"github.com/google/uuid"
)

// UserStatus represents the status of a user account
type UserStatus string

const (
	UserStatusActive   UserStatus = "active"
	UserStatusDisabled UserStatus = "disabled"
	UserStatusLocked   UserStatus = "locked"
	UserStatusDeleted  UserStatus = "deleted"
)

// GroupType represents the type of a group
type GroupType string

const (
	GroupTypeSecurity     GroupType = "security"
	GroupTypeDistribution GroupType = "distribution"
	GroupTypeBuiltin      GroupType = "builtin"
)

// EventType represents the type of an event
type EventType string

const (
	EventUserCreated       EventType = "user_created"
	EventUserModified      EventType = "user_modified"
	EventUserDeleted       EventType = "user_deleted"
	EventGroupCreated      EventType = "group_created"
	EventGroupModified     EventType = "group_modified"
	EventGroupDeleted      EventType = "group_deleted"
	EventLogin             EventType = "login"
	EventLogout            EventType = "logout"
	EventPermissionChanged EventType = "permission_changed"
)

// User represents a user in the system
type User struct {
	ID                   uuid.UUID  `json:"id" db:"id"`
	DomainControllerID   string     `json:"domain_controller_id" db:"domain_controller_id"`
	ADObjectGUID         uuid.UUID  `json:"ad_object_guid" db:"ad_object_guid"`
	SAMAccountName       string     `json:"sam_account_name" db:"sam_account_name"`
	UserPrincipalName    *string    `json:"user_principal_name" db:"user_principal_name"`
	DisplayName          *string    `json:"display_name" db:"display_name"`
	GivenName            *string    `json:"given_name" db:"given_name"`
	Surname              *string    `json:"surname" db:"surname"`
	Email                *string    `json:"email" db:"email"`
	Department           *string    `json:"department" db:"department"`
	Title                *string    `json:"title" db:"title"`
	ManagerDN            *string    `json:"manager_dn" db:"manager_dn"`
	Status               UserStatus `json:"status" db:"status"`
	LastLogon            *time.Time `json:"last_logon" db:"last_logon"`
	PasswordLastSet      *time.Time `json:"password_last_set" db:"password_last_set"`
	AccountExpires       *time.Time `json:"account_expires" db:"account_expires"`
	CreatedAt            time.Time  `json:"created_at" db:"created_at"`
	UpdatedAt            time.Time  `json:"updated_at" db:"updated_at"`
	LastSeenAt           time.Time  `json:"last_seen_at" db:"last_seen_at"`
}

// Group represents a group in the system
type Group struct {
	ID                 uuid.UUID `json:"id" db:"id"`
	DomainControllerID string    `json:"domain_controller_id" db:"domain_controller_id"`
	ADObjectGUID       uuid.UUID `json:"ad_object_guid" db:"ad_object_guid"`
	SAMAccountName     string    `json:"sam_account_name" db:"sam_account_name"`
	DisplayName        *string   `json:"display_name" db:"display_name"`
	Description        *string   `json:"description" db:"description"`
	GroupType          GroupType `json:"group_type" db:"group_type"`
	DistinguishedName  string    `json:"distinguished_name" db:"distinguished_name"`
	CreatedAt          time.Time `json:"created_at" db:"created_at"`
	UpdatedAt          time.Time `json:"updated_at" db:"updated_at"`
	LastSeenAt         time.Time `json:"last_seen_at" db:"last_seen_at"`
}

// GroupMembership represents a user's membership in a group
type GroupMembership struct {
	ID        uuid.UUID `json:"id" db:"id"`
	UserID    uuid.UUID `json:"user_id" db:"user_id"`
	GroupID   uuid.UUID `json:"group_id" db:"group_id"`
	CreatedAt time.Time `json:"created_at" db:"created_at"`
}

// Event represents an audit event in the system
type Event struct {
	ID                 uuid.UUID              `json:"id" db:"id"`
	DomainControllerID string                 `json:"domain_controller_id" db:"domain_controller_id"`
	EventType          EventType              `json:"event_type" db:"event_type"`
	UserID             *uuid.UUID             `json:"user_id" db:"user_id"`
	GroupID            *uuid.UUID             `json:"group_id" db:"group_id"`
	EventData          map[string]interface{} `json:"event_data" db:"event_data"`
	SourceIP           *string                `json:"source_ip" db:"source_ip"`
	UserAgent          *string                `json:"user_agent" db:"user_agent"`
	OccurredAt         time.Time              `json:"occurred_at" db:"occurred_at"`
	CreatedAt          time.Time              `json:"created_at" db:"created_at"`
}

// DomainController represents a domain controller in the system
type DomainController struct {
	ID            string     `json:"id" db:"id"`
	Hostname      string     `json:"hostname" db:"hostname"`
	DomainName    string     `json:"domain_name" db:"domain_name"`
	LastHeartbeat *time.Time `json:"last_heartbeat" db:"last_heartbeat"`
	Version       *string    `json:"version" db:"version"`
	Status        string     `json:"status" db:"status"`
	CreatedAt     time.Time  `json:"created_at" db:"created_at"`
	UpdatedAt     time.Time  `json:"updated_at" db:"updated_at"`
}

// CreateUserRequest represents the request to create a new user
type CreateUserRequest struct {
	DomainControllerID string     `json:"domain_controller_id" binding:"required"`
	ADObjectGUID       uuid.UUID  `json:"ad_object_guid" binding:"required"`
	SAMAccountName     string     `json:"sam_account_name" binding:"required"`
	UserPrincipalName  *string    `json:"user_principal_name"`
	DisplayName        *string    `json:"display_name"`
	GivenName          *string    `json:"given_name"`
	Surname            *string    `json:"surname"`
	Email              *string    `json:"email"`
	Department         *string    `json:"department"`
	Title              *string    `json:"title"`
	ManagerDN          *string    `json:"manager_dn"`
	Status             UserStatus `json:"status"`
	LastLogon          *time.Time `json:"last_logon"`
	PasswordLastSet    *time.Time `json:"password_last_set"`
	AccountExpires     *time.Time `json:"account_expires"`
}

// UpdateUserRequest represents the request to update a user
type UpdateUserRequest struct {
	UserPrincipalName *string    `json:"user_principal_name"`
	DisplayName       *string    `json:"display_name"`
	GivenName         *string    `json:"given_name"`
	Surname           *string    `json:"surname"`
	Email             *string    `json:"email"`
	Department        *string    `json:"department"`
	Title             *string    `json:"title"`
	ManagerDN         *string    `json:"manager_dn"`
	Status            UserStatus `json:"status"`
	LastLogon         *time.Time `json:"last_logon"`
	PasswordLastSet   *time.Time `json:"password_last_set"`
	AccountExpires    *time.Time `json:"account_expires"`
}

// CreateGroupRequest represents the request to create a new group
type CreateGroupRequest struct {
	DomainControllerID string    `json:"domain_controller_id" binding:"required"`
	ADObjectGUID       uuid.UUID `json:"ad_object_guid" binding:"required"`
	SAMAccountName     string    `json:"sam_account_name" binding:"required"`
	DisplayName        *string   `json:"display_name"`
	Description        *string   `json:"description"`
	GroupType          GroupType `json:"group_type"`
	DistinguishedName  string    `json:"distinguished_name" binding:"required"`
}

// UpdateGroupRequest represents the request to update a group
type UpdateGroupRequest struct {
	DisplayName       *string   `json:"display_name"`
	Description       *string   `json:"description"`
	GroupType         GroupType `json:"group_type"`
	DistinguishedName string    `json:"distinguished_name"`
}

// CreateEventRequest represents the request to create a new event
type CreateEventRequest struct {
	DomainControllerID string                 `json:"domain_controller_id" binding:"required"`
	EventType          EventType              `json:"event_type" binding:"required"`
	UserID             *uuid.UUID             `json:"user_id"`
	GroupID            *uuid.UUID             `json:"group_id"`
	EventData          map[string]interface{} `json:"event_data"`
	SourceIP           *string                `json:"source_ip"`
	UserAgent          *string                `json:"user_agent"`
	OccurredAt         time.Time              `json:"occurred_at" binding:"required"`
}

// PaginationParams represents pagination parameters
type PaginationParams struct {
	Page  int `form:"page,default=1" binding:"min=1"`
	Limit int `form:"limit,default=20" binding:"min=1,max=100"`
}

// PaginatedResponse represents a paginated response
type PaginatedResponse struct {
	Data       interface{} `json:"data"`
	Page       int         `json:"page"`
	Limit      int         `json:"limit"`
	Total      int64       `json:"total"`
	TotalPages int         `json:"total_pages"`
}

// ErrorResponse represents an error response
type ErrorResponse struct {
	Error   string `json:"error"`
	Message string `json:"message"`
	Code    int    `json:"code"`
}
