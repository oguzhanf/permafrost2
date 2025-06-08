package main

import (
	"context"
	"database/sql"
	"fmt"
	"log"
	"net/http"
	"os"
	"os/signal"
	"syscall"
	"time"

	"github.com/gin-gonic/gin"
	"github.com/google/uuid"
	_ "github.com/lib/pq"
	"github.com/nats-io/nats.go"
	swaggerFiles "github.com/swaggo/files"
	ginSwagger "github.com/swaggo/gin-swagger"
	"go.opentelemetry.io/otel"
	"go.opentelemetry.io/otel/exporters/jaeger"
	"go.opentelemetry.io/otel/sdk/resource"
	tracesdk "go.opentelemetry.io/otel/sdk/trace"
	semconv "go.opentelemetry.io/otel/semconv/v1.17.0"
)

// @title Permafrost Identity Management API
// @version 1.0
// @description A REST API for managing Active Directory identity data
// @termsOfService http://swagger.io/terms/

// @contact.name API Support
// @contact.url http://www.swagger.io/support
// @contact.email support@swagger.io

// @license.name MIT
// @license.url https://opensource.org/licenses/MIT

// @host localhost:8080
// @BasePath /api/v1

// @securityDefinitions.apikey BearerAuth
// @in header
// @name Authorization

type Config struct {
	DatabaseURL string
	NatsURL     string
	Port        string
	JaegerURL   string
}

type Server struct {
	db     *sql.DB
	nats   *nats.Conn
	config *Config
	router *gin.Engine
}

func main() {
	config := &Config{
		DatabaseURL: getEnv("DATABASE_URL", "postgres://permafrost:dev_password_change_in_prod@localhost:5432/permafrost?sslmode=disable"),
		NatsURL:     getEnv("NATS_URL", "nats://localhost:4222"),
		Port:        getEnv("PORT", "8080"),
		JaegerURL:   getEnv("JAEGER_URL", "http://localhost:14268/api/traces"),
	}

	// Initialize OpenTelemetry
	tp, err := initTracer(config.JaegerURL)
	if err != nil {
		log.Printf("Failed to initialize tracer: %v", err)
	} else {
		defer func() {
			if err := tp.Shutdown(context.Background()); err != nil {
				log.Printf("Error shutting down tracer provider: %v", err)
			}
		}()
	}

	server, err := NewServer(config)
	if err != nil {
		log.Fatalf("Failed to create server: %v", err)
	}
	defer server.Close()

	// Start server
	srv := &http.Server{
		Addr:    ":" + config.Port,
		Handler: server.router,
	}

	// Graceful shutdown
	go func() {
		if err := srv.ListenAndServe(); err != nil && err != http.ErrServerClosed {
			log.Fatalf("Failed to start server: %v", err)
		}
	}()

	log.Printf("Server started on port %s", config.Port)

	// Wait for interrupt signal
	quit := make(chan os.Signal, 1)
	signal.Notify(quit, syscall.SIGINT, syscall.SIGTERM)
	<-quit

	log.Println("Shutting down server...")

	ctx, cancel := context.WithTimeout(context.Background(), 30*time.Second)
	defer cancel()

	if err := srv.Shutdown(ctx); err != nil {
		log.Fatalf("Server forced to shutdown: %v", err)
	}

	log.Println("Server exited")
}

func NewServer(config *Config) (*Server, error) {
	// Connect to database
	db, err := sql.Open("postgres", config.DatabaseURL)
	if err != nil {
		return nil, fmt.Errorf("failed to connect to database: %w", err)
	}

	if err := db.Ping(); err != nil {
		return nil, fmt.Errorf("failed to ping database: %w", err)
	}

	// Connect to NATS
	nc, err := nats.Connect(config.NatsURL)
	if err != nil {
		return nil, fmt.Errorf("failed to connect to NATS: %w", err)
	}

	server := &Server{
		db:     db,
		nats:   nc,
		config: config,
	}

	server.setupRoutes()

	// Start event consumer
	eventConsumer := NewEventConsumer(nc, server)
	go func() {
		if err := eventConsumer.Start(context.Background()); err != nil {
			log.Printf("Failed to start event consumer: %v", err)
		}
	}()

	return server, nil
}

func (s *Server) setupRoutes() {
	s.router = gin.Default()

	// Health check
	s.router.GET("/health", s.healthCheck)

	// API routes
	v1 := s.router.Group("/api/v1")
	{
		users := v1.Group("/users")
		{
			users.GET("", s.getUsers)
			users.POST("", s.createUser)
			users.GET("/:id", s.getUser)
			users.PUT("/:id", s.updateUser)
			users.DELETE("/:id", s.deleteUser)
		}

		groups := v1.Group("/groups")
		{
			groups.GET("", s.getGroups)
			groups.POST("", s.createGroup)
			groups.GET("/:id", s.getGroup)
			groups.PUT("/:id", s.updateGroup)
			groups.DELETE("/:id", s.deleteGroup)
		}

		events := v1.Group("/events")
		{
			events.GET("", s.getEvents)
			events.POST("", s.createEvent)
		}
	}

	// Swagger documentation
	s.router.GET("/swagger/*any", ginSwagger.WrapHandler(swaggerFiles.Handler))
}

func (s *Server) Close() {
	if s.db != nil {
		s.db.Close()
	}
	if s.nats != nil {
		s.nats.Close()
	}
}

// @Summary Health check
// @Description Check if the service is healthy
// @Tags health
// @Produce json
// @Success 200 {object} map[string]interface{}
// @Router /health [get]
func (s *Server) healthCheck(c *gin.Context) {
	c.JSON(http.StatusOK, gin.H{
		"status":    "healthy",
		"timestamp": time.Now().UTC(),
		"version":   "1.0.0",
	})
}

func initTracer(jaegerURL string) (*tracesdk.TracerProvider, error) {
	exp, err := jaeger.New(jaeger.WithCollectorEndpoint(jaeger.WithEndpoint(jaegerURL)))
	if err != nil {
		return nil, err
	}

	tp := tracesdk.NewTracerProvider(
		tracesdk.WithBatcher(exp),
		tracesdk.WithResource(resource.NewWithAttributes(
			semconv.SchemaURL,
			semconv.ServiceName("permafrost-backend"),
			semconv.ServiceVersion("1.0.0"),
		)),
	)

	otel.SetTracerProvider(tp)
	return tp, nil
}

func getEnv(key, defaultValue string) string {
	if value := os.Getenv(key); value != "" {
		return value
	}
	return defaultValue
}
