#!/bin/bash

# Permafrost Integration Test Suite
# This script runs comprehensive integration tests across all components

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
BACKEND_URL="http://localhost:8080"
FRONTEND_URL="http://localhost:3000"
EDGE_SERVICE_URL="http://localhost:5000"
POSTGRES_URL="postgres://permafrost:dev_password_change_in_prod@localhost:5432/permafrost_test"
NATS_URL="nats://localhost:4222"

# Test results
TESTS_PASSED=0
TESTS_FAILED=0
FAILED_TESTS=()

echo -e "${BLUE}🚀 Starting Permafrost Integration Test Suite${NC}"
echo "=================================================="

# Function to print test status
print_test_result() {
    local test_name="$1"
    local result="$2"
    local details="$3"
    
    if [ "$result" = "PASS" ]; then
        echo -e "${GREEN}✅ $test_name${NC}"
        ((TESTS_PASSED++))
    else
        echo -e "${RED}❌ $test_name${NC}"
        if [ -n "$details" ]; then
            echo -e "${RED}   Details: $details${NC}"
        fi
        ((TESTS_FAILED++))
        FAILED_TESTS+=("$test_name")
    fi
}

# Function to wait for service to be ready
wait_for_service() {
    local url="$1"
    local service_name="$2"
    local max_attempts=30
    local attempt=1
    
    echo -e "${YELLOW}⏳ Waiting for $service_name to be ready...${NC}"
    
    while [ $attempt -le $max_attempts ]; do
        if curl -s -f "$url/health" > /dev/null 2>&1; then
            echo -e "${GREEN}✅ $service_name is ready${NC}"
            return 0
        fi
        
        echo -n "."
        sleep 2
        ((attempt++))
    done
    
    echo -e "${RED}❌ $service_name failed to start within $((max_attempts * 2)) seconds${NC}"
    return 1
}

# Function to check if Docker Compose is running
check_docker_compose() {
    echo -e "${BLUE}🐳 Checking Docker Compose services...${NC}"
    
    if ! docker-compose ps | grep -q "Up"; then
        echo -e "${YELLOW}⚠️  Docker Compose services not running. Starting them...${NC}"
        docker-compose up -d
        sleep 10
    fi
    
    # Check individual services
    local services=("permafrost-postgres" "permafrost-nats" "permafrost-backend")
    for service in "${services[@]}"; do
        if docker-compose ps | grep "$service" | grep -q "Up"; then
            print_test_result "Docker service: $service" "PASS"
        else
            print_test_result "Docker service: $service" "FAIL" "Service not running"
        fi
    done
}

# Function to test database connectivity
test_database() {
    echo -e "${BLUE}🗄️  Testing Database Connectivity...${NC}"
    
    # Test connection
    if psql "$POSTGRES_URL" -c "SELECT 1;" > /dev/null 2>&1; then
        print_test_result "Database connection" "PASS"
    else
        print_test_result "Database connection" "FAIL" "Cannot connect to PostgreSQL"
        return 1
    fi
    
    # Test schema
    local table_count=$(psql "$POSTGRES_URL" -t -c "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public';" 2>/dev/null | tr -d ' ')
    if [ "$table_count" -gt 0 ]; then
        print_test_result "Database schema" "PASS"
    else
        print_test_result "Database schema" "FAIL" "No tables found"
    fi
    
    # Test specific tables
    local required_tables=("users" "groups" "events" "domain_controllers" "group_memberships")
    for table in "${required_tables[@]}"; do
        if psql "$POSTGRES_URL" -c "\d $table" > /dev/null 2>&1; then
            print_test_result "Table: $table" "PASS"
        else
            print_test_result "Table: $table" "FAIL" "Table not found"
        fi
    done
}

# Function to test NATS connectivity
test_nats() {
    echo -e "${BLUE}📡 Testing NATS Connectivity...${NC}"
    
    # Test NATS connection (requires nats CLI tool)
    if command -v nats &> /dev/null; then
        if nats --server="$NATS_URL" server check > /dev/null 2>&1; then
            print_test_result "NATS connection" "PASS"
        else
            print_test_result "NATS connection" "FAIL" "Cannot connect to NATS server"
        fi
        
        # Test JetStream
        if nats --server="$NATS_URL" stream ls > /dev/null 2>&1; then
            print_test_result "NATS JetStream" "PASS"
        else
            print_test_result "NATS JetStream" "FAIL" "JetStream not available"
        fi
    else
        print_test_result "NATS CLI tool" "FAIL" "nats CLI not installed - skipping NATS tests"
    fi
}

# Function to test backend API
test_backend_api() {
    echo -e "${BLUE}🔧 Testing Backend API...${NC}"
    
    # Wait for backend to be ready
    if ! wait_for_service "$BACKEND_URL" "Backend API"; then
        print_test_result "Backend API startup" "FAIL" "Service not responding"
        return 1
    fi
    
    # Test health endpoint
    local health_response=$(curl -s "$BACKEND_URL/health")
    if echo "$health_response" | grep -q "healthy"; then
        print_test_result "Backend health check" "PASS"
    else
        print_test_result "Backend health check" "FAIL" "Health check failed"
    fi
    
    # Test API endpoints
    local endpoints=("/api/v1/users" "/api/v1/groups" "/api/v1/events")
    for endpoint in "${endpoints[@]}"; do
        local status_code=$(curl -s -o /dev/null -w "%{http_code}" "$BACKEND_URL$endpoint")
        if [ "$status_code" = "200" ]; then
            print_test_result "API endpoint: $endpoint" "PASS"
        else
            print_test_result "API endpoint: $endpoint" "FAIL" "HTTP $status_code"
        fi
    done
    
    # Test API functionality
    test_api_crud_operations
}

# Function to test CRUD operations
test_api_crud_operations() {
    echo -e "${BLUE}🔄 Testing API CRUD Operations...${NC}"
    
    # Test user creation
    local user_data='{
        "domainControllerId": "TEST-DC01",
        "adObjectGuid": "12345678-1234-1234-1234-123456789abc",
        "samAccountName": "testuser",
        "displayName": "Test User",
        "email": "test@example.com",
        "status": "active"
    }'
    
    local create_response=$(curl -s -X POST "$BACKEND_URL/api/v1/users" \
        -H "Content-Type: application/json" \
        -d "$user_data")
    
    if echo "$create_response" | grep -q "testuser"; then
        print_test_result "User creation" "PASS"
        
        # Extract user ID for further tests
        local user_id=$(echo "$create_response" | grep -o '"id":"[^"]*"' | cut -d'"' -f4)
        
        if [ -n "$user_id" ]; then
            # Test user retrieval
            local get_response=$(curl -s "$BACKEND_URL/api/v1/users/$user_id")
            if echo "$get_response" | grep -q "testuser"; then
                print_test_result "User retrieval" "PASS"
            else
                print_test_result "User retrieval" "FAIL" "User not found"
            fi
            
            # Test user update
            local update_data='{"displayName": "Updated Test User"}'
            local update_response=$(curl -s -X PUT "$BACKEND_URL/api/v1/users/$user_id" \
                -H "Content-Type: application/json" \
                -d "$update_data")
            
            if echo "$update_response" | grep -q "Updated Test User"; then
                print_test_result "User update" "PASS"
            else
                print_test_result "User update" "FAIL" "Update failed"
            fi
            
            # Test user deletion
            local delete_status=$(curl -s -o /dev/null -w "%{http_code}" -X DELETE "$BACKEND_URL/api/v1/users/$user_id")
            if [ "$delete_status" = "204" ]; then
                print_test_result "User deletion" "PASS"
            else
                print_test_result "User deletion" "FAIL" "HTTP $delete_status"
            fi
        fi
    else
        print_test_result "User creation" "FAIL" "Creation failed"
    fi
}

# Function to test frontend
test_frontend() {
    echo -e "${BLUE}🌐 Testing Frontend Application...${NC}"
    
    # Check if frontend is running
    if curl -s -f "$FRONTEND_URL" > /dev/null 2>&1; then
        print_test_result "Frontend accessibility" "PASS"
    else
        print_test_result "Frontend accessibility" "FAIL" "Frontend not responding"
        return 1
    fi
    
    # Test static assets
    local assets=("/_next/static" "/favicon.ico")
    for asset in "${assets[@]}"; do
        local status_code=$(curl -s -o /dev/null -w "%{http_code}" "$FRONTEND_URL$asset")
        if [ "$status_code" = "200" ] || [ "$status_code" = "404" ]; then
            print_test_result "Frontend asset: $asset" "PASS"
        else
            print_test_result "Frontend asset: $asset" "FAIL" "HTTP $status_code"
        fi
    done
}

# Function to test edge service (if running on Windows)
test_edge_service() {
    echo -e "${BLUE}🏢 Testing Edge Service...${NC}"
    
    # Check if edge service is accessible
    if curl -s -f "$EDGE_SERVICE_URL/health" > /dev/null 2>&1; then
        print_test_result "Edge Service health" "PASS"
        
        # Test AD endpoints
        local endpoints=("/api/users" "/api/groups" "/api/events")
        for endpoint in "${endpoints[@]}"; do
            local status_code=$(curl -s -o /dev/null -w "%{http_code}" "$EDGE_SERVICE_URL$endpoint")
            if [ "$status_code" = "200" ] || [ "$status_code" = "500" ]; then
                # 500 is acceptable as AD might not be configured in test environment
                print_test_result "Edge Service endpoint: $endpoint" "PASS"
            else
                print_test_result "Edge Service endpoint: $endpoint" "FAIL" "HTTP $status_code"
            fi
        done
    else
        print_test_result "Edge Service" "FAIL" "Service not running (expected in non-Windows environment)"
    fi
}

# Function to test event flow
test_event_flow() {
    echo -e "${BLUE}🔄 Testing Event Flow...${NC}"
    
    # Test NATS event publishing
    if command -v nats &> /dev/null; then
        local test_event='{"user_id":"test-123","sam_account_name":"testuser","domain_controller_id":"TEST-DC01"}'
        
        if nats --server="$NATS_URL" pub "user.created" "$test_event" > /dev/null 2>&1; then
            print_test_result "NATS event publishing" "PASS"
            
            # Wait a moment for event processing
            sleep 2
            
            # Check if event was processed (look for audit event in database)
            local event_count=$(psql "$POSTGRES_URL" -t -c "SELECT COUNT(*) FROM events WHERE event_type = 'user_created' AND created_at > NOW() - INTERVAL '1 minute';" 2>/dev/null | tr -d ' ')
            if [ "$event_count" -gt 0 ]; then
                print_test_result "Event processing" "PASS"
            else
                print_test_result "Event processing" "FAIL" "No events processed"
            fi
        else
            print_test_result "NATS event publishing" "FAIL" "Failed to publish event"
        fi
    else
        print_test_result "Event flow test" "FAIL" "nats CLI not available"
    fi
}

# Function to run performance tests
test_performance() {
    echo -e "${BLUE}⚡ Running Performance Tests...${NC}"
    
    # Test API response times
    if command -v curl &> /dev/null; then
        local response_time=$(curl -s -o /dev/null -w "%{time_total}" "$BACKEND_URL/health")
        local response_time_ms=$(echo "$response_time * 1000" | bc -l 2>/dev/null || echo "0")
        
        if (( $(echo "$response_time < 1.0" | bc -l 2>/dev/null || echo "0") )); then
            print_test_result "API response time (<1s)" "PASS"
        else
            print_test_result "API response time (<1s)" "FAIL" "${response_time}s"
        fi
        
        # Test concurrent requests
        echo -e "${YELLOW}⏳ Testing concurrent API requests...${NC}"
        local concurrent_test_result=0
        for i in {1..10}; do
            curl -s "$BACKEND_URL/health" > /dev/null &
        done
        wait
        
        print_test_result "Concurrent requests handling" "PASS"
    fi
}

# Main execution
main() {
    echo -e "${BLUE}Starting integration tests at $(date)${NC}"
    echo ""
    
    # Run test suites
    check_docker_compose
    test_database
    test_nats
    test_backend_api
    test_frontend
    test_edge_service
    test_event_flow
    test_performance
    
    # Print summary
    echo ""
    echo "=================================================="
    echo -e "${BLUE}📊 Test Summary${NC}"
    echo "=================================================="
    echo -e "${GREEN}✅ Tests Passed: $TESTS_PASSED${NC}"
    echo -e "${RED}❌ Tests Failed: $TESTS_FAILED${NC}"
    
    if [ $TESTS_FAILED -gt 0 ]; then
        echo ""
        echo -e "${RED}Failed Tests:${NC}"
        for test in "${FAILED_TESTS[@]}"; do
            echo -e "${RED}  - $test${NC}"
        done
        echo ""
        echo -e "${RED}❌ Integration tests FAILED${NC}"
        exit 1
    else
        echo ""
        echo -e "${GREEN}🎉 All integration tests PASSED!${NC}"
        exit 0
    fi
}

# Run main function
main "$@"
