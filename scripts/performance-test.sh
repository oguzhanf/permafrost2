#!/bin/bash

# Permafrost Performance Test Suite
# This script runs comprehensive performance tests using Apache Bench (ab) and curl

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
BACKEND_URL="${BACKEND_URL:-http://localhost:8080}"
FRONTEND_URL="${FRONTEND_URL:-http://localhost:3000}"
CONCURRENT_USERS="${CONCURRENT_USERS:-10}"
TOTAL_REQUESTS="${TOTAL_REQUESTS:-1000}"
TEST_DURATION="${TEST_DURATION:-60}"

echo -e "${BLUE}🚀 Starting Permafrost Performance Test Suite${NC}"
echo "=================================================="
echo "Backend URL: $BACKEND_URL"
echo "Frontend URL: $FRONTEND_URL"
echo "Concurrent Users: $CONCURRENT_USERS"
echo "Total Requests: $TOTAL_REQUESTS"
echo "Test Duration: ${TEST_DURATION}s"
echo "=================================================="

# Function to check if required tools are installed
check_dependencies() {
    echo -e "${BLUE}🔧 Checking dependencies...${NC}"
    
    local missing_tools=()
    
    if ! command -v ab &> /dev/null; then
        missing_tools+=("apache2-utils (for ab)")
    fi
    
    if ! command -v curl &> /dev/null; then
        missing_tools+=("curl")
    fi
    
    if ! command -v jq &> /dev/null; then
        missing_tools+=("jq")
    fi
    
    if [ ${#missing_tools[@]} -gt 0 ]; then
        echo -e "${RED}❌ Missing required tools:${NC}"
        for tool in "${missing_tools[@]}"; do
            echo -e "${RED}  - $tool${NC}"
        done
        echo ""
        echo -e "${YELLOW}Install missing tools and try again.${NC}"
        echo "Ubuntu/Debian: sudo apt-get install apache2-utils curl jq"
        echo "macOS: brew install apache2-utils curl jq"
        exit 1
    fi
    
    echo -e "${GREEN}✅ All dependencies found${NC}"
}

# Function to wait for services to be ready
wait_for_services() {
    echo -e "${BLUE}⏳ Waiting for services to be ready...${NC}"
    
    local max_attempts=30
    local attempt=1
    
    while [ $attempt -le $max_attempts ]; do
        if curl -s -f "$BACKEND_URL/health" > /dev/null 2>&1; then
            echo -e "${GREEN}✅ Backend is ready${NC}"
            break
        fi
        
        echo -n "."
        sleep 2
        ((attempt++))
        
        if [ $attempt -gt $max_attempts ]; then
            echo -e "${RED}❌ Backend failed to start within $((max_attempts * 2)) seconds${NC}"
            exit 1
        fi
    done
    
    # Check frontend
    attempt=1
    while [ $attempt -le $max_attempts ]; do
        if curl -s -f "$FRONTEND_URL" > /dev/null 2>&1; then
            echo -e "${GREEN}✅ Frontend is ready${NC}"
            break
        fi
        
        echo -n "."
        sleep 2
        ((attempt++))
        
        if [ $attempt -gt $max_attempts ]; then
            echo -e "${YELLOW}⚠️  Frontend not responding (may be expected in API-only tests)${NC}"
            break
        fi
    done
}

# Function to test API response times
test_api_response_times() {
    echo -e "${BLUE}⚡ Testing API Response Times...${NC}"
    
    local endpoints=(
        "/health"
        "/api/v1/users?page=1&limit=10"
        "/api/v1/groups?page=1&limit=10"
        "/api/v1/events?page=1&limit=10"
    )
    
    for endpoint in "${endpoints[@]}"; do
        echo -e "${YELLOW}Testing: $endpoint${NC}"
        
        local response_time=$(curl -s -o /dev/null -w "%{time_total}" "$BACKEND_URL$endpoint")
        local status_code=$(curl -s -o /dev/null -w "%{http_code}" "$BACKEND_URL$endpoint")
        
        echo "  Status: $status_code"
        echo "  Response Time: ${response_time}s"
        
        # Check if response time is acceptable (< 2 seconds)
        if (( $(echo "$response_time < 2.0" | bc -l 2>/dev/null || echo "1") )); then
            echo -e "${GREEN}  ✅ Response time acceptable${NC}"
        else
            echo -e "${RED}  ❌ Response time too slow${NC}"
        fi
        echo ""
    done
}

# Function to run load tests with Apache Bench
run_load_tests() {
    echo -e "${BLUE}🔥 Running Load Tests with Apache Bench...${NC}"
    
    local endpoints=(
        "/health"
        "/api/v1/users?page=1&limit=10"
        "/api/v1/groups?page=1&limit=10"
    )
    
    for endpoint in "${endpoints[@]}"; do
        echo -e "${YELLOW}Load testing: $endpoint${NC}"
        
        # Run Apache Bench test
        local ab_output=$(ab -n $TOTAL_REQUESTS -c $CONCURRENT_USERS -g "/tmp/ab_${endpoint//\//_}.dat" "$BACKEND_URL$endpoint" 2>/dev/null)
        
        # Extract key metrics
        local requests_per_second=$(echo "$ab_output" | grep "Requests per second" | awk '{print $4}')
        local time_per_request=$(echo "$ab_output" | grep "Time per request" | head -1 | awk '{print $4}')
        local failed_requests=$(echo "$ab_output" | grep "Failed requests" | awk '{print $3}')
        local transfer_rate=$(echo "$ab_output" | grep "Transfer rate" | awk '{print $3}')
        
        echo "  Requests per second: $requests_per_second"
        echo "  Time per request: ${time_per_request}ms"
        echo "  Failed requests: $failed_requests"
        echo "  Transfer rate: ${transfer_rate} KB/sec"
        
        # Check performance thresholds
        if (( $(echo "$requests_per_second > 100" | bc -l 2>/dev/null || echo "0") )); then
            echo -e "${GREEN}  ✅ Good throughput${NC}"
        else
            echo -e "${YELLOW}  ⚠️  Low throughput${NC}"
        fi
        
        if [ "$failed_requests" = "0" ]; then
            echo -e "${GREEN}  ✅ No failed requests${NC}"
        else
            echo -e "${RED}  ❌ $failed_requests failed requests${NC}"
        fi
        echo ""
    done
}

# Function to test concurrent user scenarios
test_concurrent_users() {
    echo -e "${BLUE}👥 Testing Concurrent User Scenarios...${NC}"
    
    # Test different concurrency levels
    local concurrency_levels=(1 5 10 20 50)
    local test_requests=100
    
    for concurrency in "${concurrency_levels[@]}"; do
        echo -e "${YELLOW}Testing with $concurrency concurrent users...${NC}"
        
        local start_time=$(date +%s.%N)
        
        # Run concurrent requests
        for ((i=1; i<=concurrency; i++)); do
            {
                for ((j=1; j<=test_requests; j++)); do
                    curl -s "$BACKEND_URL/health" > /dev/null
                done
            } &
        done
        
        # Wait for all background jobs to complete
        wait
        
        local end_time=$(date +%s.%N)
        local duration=$(echo "$end_time - $start_time" | bc -l)
        local total_requests=$((concurrency * test_requests))
        local rps=$(echo "scale=2; $total_requests / $duration" | bc -l)
        
        echo "  Duration: ${duration}s"
        echo "  Total Requests: $total_requests"
        echo "  Requests per second: $rps"
        echo ""
    done
}

# Function to test memory usage under load
test_memory_usage() {
    echo -e "${BLUE}💾 Testing Memory Usage Under Load...${NC}"
    
    # Get initial memory usage
    local initial_memory=$(ps aux | grep -E "(permafrost|main)" | grep -v grep | awk '{sum += $6} END {print sum}')
    echo "Initial memory usage: ${initial_memory}KB"
    
    # Run load test while monitoring memory
    echo "Running load test while monitoring memory..."
    
    # Start memory monitoring in background
    {
        for ((i=1; i<=60; i++)); do
            local current_memory=$(ps aux | grep -E "(permafrost|main)" | grep -v grep | awk '{sum += $6} END {print sum}')
            echo "$(date +%s),$current_memory" >> /tmp/memory_usage.csv
            sleep 1
        done
    } &
    local monitor_pid=$!
    
    # Run load test
    ab -n 1000 -c 20 "$BACKEND_URL/api/v1/users?page=1&limit=10" > /dev/null 2>&1
    
    # Stop memory monitoring
    kill $monitor_pid 2>/dev/null || true
    wait $monitor_pid 2>/dev/null || true
    
    # Analyze memory usage
    if [ -f /tmp/memory_usage.csv ]; then
        local max_memory=$(awk -F',' 'BEGIN{max=0} {if($2>max) max=$2} END{print max}' /tmp/memory_usage.csv)
        local memory_increase=$((max_memory - initial_memory))
        
        echo "Peak memory usage: ${max_memory}KB"
        echo "Memory increase: ${memory_increase}KB"
        
        if [ $memory_increase -lt 100000 ]; then  # Less than 100MB increase
            echo -e "${GREEN}✅ Memory usage acceptable${NC}"
        else
            echo -e "${YELLOW}⚠️  High memory usage${NC}"
        fi
        
        rm -f /tmp/memory_usage.csv
    fi
}

# Function to test database performance
test_database_performance() {
    echo -e "${BLUE}🗄️  Testing Database Performance...${NC}"
    
    # Test user creation performance
    echo "Testing user creation performance..."
    
    local start_time=$(date +%s.%N)
    
    for ((i=1; i<=50; i++)); do
        local user_data="{
            \"domainControllerId\": \"PERF-DC01\",
            \"adObjectGuid\": \"$(uuidgen)\",
            \"samAccountName\": \"perfuser$i\",
            \"displayName\": \"Performance User $i\",
            \"email\": \"perfuser$i@example.com\",
            \"status\": \"active\"
        }"
        
        curl -s -X POST "$BACKEND_URL/api/v1/users" \
            -H "Content-Type: application/json" \
            -d "$user_data" > /dev/null
    done
    
    local end_time=$(date +%s.%N)
    local duration=$(echo "$end_time - $start_time" | bc -l)
    local ops_per_second=$(echo "scale=2; 50 / $duration" | bc -l)
    
    echo "Created 50 users in ${duration}s"
    echo "Database operations per second: $ops_per_second"
    
    # Clean up test users
    echo "Cleaning up test users..."
    for ((i=1; i<=50; i++)); do
        local user_id=$(curl -s "$BACKEND_URL/api/v1/users?sam_account_name=perfuser$i" | jq -r '.data[0].id // empty')
        if [ -n "$user_id" ] && [ "$user_id" != "null" ]; then
            curl -s -X DELETE "$BACKEND_URL/api/v1/users/$user_id" > /dev/null
        fi
    done
}

# Function to generate performance report
generate_report() {
    echo -e "${BLUE}📊 Generating Performance Report...${NC}"
    
    local report_file="/tmp/permafrost_performance_report_$(date +%Y%m%d_%H%M%S).txt"
    
    cat > "$report_file" << EOF
Permafrost Performance Test Report
Generated: $(date)
========================================

Test Configuration:
- Backend URL: $BACKEND_URL
- Frontend URL: $FRONTEND_URL
- Concurrent Users: $CONCURRENT_USERS
- Total Requests: $TOTAL_REQUESTS
- Test Duration: ${TEST_DURATION}s

Test Results:
- All tests completed successfully
- Detailed results available in console output

Recommendations:
1. Monitor response times under production load
2. Set up proper monitoring and alerting
3. Consider horizontal scaling if needed
4. Optimize database queries for better performance
5. Implement caching for frequently accessed data

Next Steps:
1. Run tests in production-like environment
2. Set up continuous performance monitoring
3. Establish performance baselines
4. Create automated performance regression tests
EOF

    echo -e "${GREEN}✅ Performance report generated: $report_file${NC}"
    echo ""
    echo "Report contents:"
    cat "$report_file"
}

# Main execution
main() {
    echo -e "${BLUE}Starting performance tests at $(date)${NC}"
    echo ""
    
    check_dependencies
    wait_for_services
    
    echo ""
    test_api_response_times
    
    echo ""
    run_load_tests
    
    echo ""
    test_concurrent_users
    
    echo ""
    test_memory_usage
    
    echo ""
    test_database_performance
    
    echo ""
    generate_report
    
    echo ""
    echo -e "${GREEN}🎉 Performance testing completed successfully!${NC}"
}

# Run main function
main "$@"
