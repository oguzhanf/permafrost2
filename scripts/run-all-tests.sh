#!/bin/bash

# Permafrost Complete Test Suite
# This script runs all tests: unit, integration, performance, and security

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
SKIP_UNIT_TESTS="${SKIP_UNIT_TESTS:-false}"
SKIP_INTEGRATION_TESTS="${SKIP_INTEGRATION_TESTS:-false}"
SKIP_PERFORMANCE_TESTS="${SKIP_PERFORMANCE_TESTS:-false}"
SKIP_SECURITY_TESTS="${SKIP_SECURITY_TESTS:-false}"
PARALLEL_TESTS="${PARALLEL_TESTS:-true}"

# Test results tracking
UNIT_TEST_RESULT=0
INTEGRATION_TEST_RESULT=0
PERFORMANCE_TEST_RESULT=0
SECURITY_TEST_RESULT=0

echo -e "${BLUE}🧪 Permafrost Complete Test Suite${NC}"
echo "=================================================="
echo "Skip Unit Tests: $SKIP_UNIT_TESTS"
echo "Skip Integration Tests: $SKIP_INTEGRATION_TESTS"
echo "Skip Performance Tests: $SKIP_PERFORMANCE_TESTS"
echo "Skip Security Tests: $SKIP_SECURITY_TESTS"
echo "Parallel Execution: $PARALLEL_TESTS"
echo "=================================================="

# Function to print test phase header
print_phase_header() {
    local phase="$1"
    echo ""
    echo -e "${BLUE}🔬 Phase: $phase${NC}"
    echo "=================================================="
}

# Function to print test result
print_test_result() {
    local test_name="$1"
    local result="$2"
    local duration="$3"
    
    if [ "$result" = "0" ]; then
        echo -e "${GREEN}✅ $test_name completed successfully${NC} (${duration}s)"
    else
        echo -e "${RED}❌ $test_name failed${NC} (${duration}s)"
    fi
}

# Function to run unit tests
run_unit_tests() {
    if [ "$SKIP_UNIT_TESTS" = "true" ]; then
        echo -e "${YELLOW}⏭️  Skipping unit tests${NC}"
        return 0
    fi
    
    print_phase_header "Unit Tests"
    
    local start_time=$(date +%s)
    
    # Backend unit tests
    echo -e "${BLUE}🔧 Running backend unit tests...${NC}"
    cd backend
    if go test -v -cover ./...; then
        echo -e "${GREEN}✅ Backend unit tests passed${NC}"
    else
        echo -e "${RED}❌ Backend unit tests failed${NC}"
        UNIT_TEST_RESULT=1
    fi
    cd ..
    
    # Frontend unit tests
    echo -e "${BLUE}🌐 Running frontend unit tests...${NC}"
    cd frontend
    if npm test -- --coverage --watchAll=false; then
        echo -e "${GREEN}✅ Frontend unit tests passed${NC}"
    else
        echo -e "${RED}❌ Frontend unit tests failed${NC}"
        UNIT_TEST_RESULT=1
    fi
    cd ..
    
    # Edge service unit tests
    echo -e "${BLUE}🏢 Running edge service unit tests...${NC}"
    cd edge-service
    if dotnet test --logger "console;verbosity=detailed" --collect:"XPlat Code Coverage"; then
        echo -e "${GREEN}✅ Edge service unit tests passed${NC}"
    else
        echo -e "${RED}❌ Edge service unit tests failed${NC}"
        UNIT_TEST_RESULT=1
    fi
    cd ..
    
    local end_time=$(date +%s)
    local duration=$((end_time - start_time))
    
    print_test_result "Unit Tests" "$UNIT_TEST_RESULT" "$duration"
    return $UNIT_TEST_RESULT
}

# Function to run integration tests
run_integration_tests() {
    if [ "$SKIP_INTEGRATION_TESTS" = "true" ]; then
        echo -e "${YELLOW}⏭️  Skipping integration tests${NC}"
        return 0
    fi
    
    print_phase_header "Integration Tests"
    
    local start_time=$(date +%s)
    
    # Start services if not running
    echo -e "${BLUE}🐳 Starting services for integration tests...${NC}"
    if ! docker-compose ps | grep -q "Up"; then
        docker-compose up -d
        sleep 30  # Wait for services to be ready
    fi
    
    # Run integration test suite
    if ./scripts/run-integration-tests.sh; then
        echo -e "${GREEN}✅ Integration tests passed${NC}"
    else
        echo -e "${RED}❌ Integration tests failed${NC}"
        INTEGRATION_TEST_RESULT=1
    fi
    
    local end_time=$(date +%s)
    local duration=$((end_time - start_time))
    
    print_test_result "Integration Tests" "$INTEGRATION_TEST_RESULT" "$duration"
    return $INTEGRATION_TEST_RESULT
}

# Function to run performance tests
run_performance_tests() {
    if [ "$SKIP_PERFORMANCE_TESTS" = "true" ]; then
        echo -e "${YELLOW}⏭️  Skipping performance tests${NC}"
        return 0
    fi
    
    print_phase_header "Performance Tests"
    
    local start_time=$(date +%s)
    
    # Run performance test suite
    if ./scripts/performance-test.sh; then
        echo -e "${GREEN}✅ Performance tests passed${NC}"
    else
        echo -e "${RED}❌ Performance tests failed${NC}"
        PERFORMANCE_TEST_RESULT=1
    fi
    
    local end_time=$(date +%s)
    local duration=$((end_time - start_time))
    
    print_test_result "Performance Tests" "$PERFORMANCE_TEST_RESULT" "$duration"
    return $PERFORMANCE_TEST_RESULT
}

# Function to run security tests
run_security_tests() {
    if [ "$SKIP_SECURITY_TESTS" = "true" ]; then
        echo -e "${YELLOW}⏭️  Skipping security tests${NC}"
        return 0
    fi
    
    print_phase_header "Security Tests"
    
    local start_time=$(date +%s)
    
    # Basic security checks
    echo -e "${BLUE}🔒 Running security checks...${NC}"
    
    # Check for hardcoded secrets
    echo "Checking for hardcoded secrets..."
    if grep -r -i "password\|secret\|key" --include="*.go" --include="*.ts" --include="*.tsx" --include="*.cs" . | grep -v "test" | grep -v "example" | grep -v "placeholder"; then
        echo -e "${YELLOW}⚠️  Potential hardcoded secrets found (review manually)${NC}"
    else
        echo -e "${GREEN}✅ No obvious hardcoded secrets found${NC}"
    fi
    
    # Check for SQL injection vulnerabilities
    echo "Checking for SQL injection vulnerabilities..."
    if grep -r "fmt.Sprintf.*SELECT\|fmt.Sprintf.*INSERT\|fmt.Sprintf.*UPDATE\|fmt.Sprintf.*DELETE" --include="*.go" .; then
        echo -e "${RED}❌ Potential SQL injection vulnerabilities found${NC}"
        SECURITY_TEST_RESULT=1
    else
        echo -e "${GREEN}✅ No obvious SQL injection vulnerabilities found${NC}"
    fi
    
    # Check for insecure HTTP usage
    echo "Checking for insecure HTTP usage..."
    if grep -r "http://" --include="*.go" --include="*.ts" --include="*.tsx" --include="*.cs" . | grep -v "localhost\|127.0.0.1\|test\|example"; then
        echo -e "${YELLOW}⚠️  HTTP usage found (should use HTTPS in production)${NC}"
    else
        echo -e "${GREEN}✅ No insecure HTTP usage found${NC}"
    fi
    
    # Check Docker security
    echo "Checking Docker security..."
    if grep -r "USER root\|--privileged" Dockerfile* docker-compose.yml 2>/dev/null; then
        echo -e "${YELLOW}⚠️  Potential Docker security issues found${NC}"
    else
        echo -e "${GREEN}✅ Docker security checks passed${NC}"
    fi
    
    # Check Kubernetes security
    echo "Checking Kubernetes security..."
    if grep -r "privileged: true\|runAsUser: 0" k8s/ 2>/dev/null; then
        echo -e "${YELLOW}⚠️  Potential Kubernetes security issues found${NC}"
    else
        echo -e "${GREEN}✅ Kubernetes security checks passed${NC}"
    fi
    
    local end_time=$(date +%s)
    local duration=$((end_time - start_time))
    
    print_test_result "Security Tests" "$SECURITY_TEST_RESULT" "$duration"
    return $SECURITY_TEST_RESULT
}

# Function to generate test report
generate_test_report() {
    echo -e "${BLUE}📊 Generating Test Report...${NC}"
    
    local report_file="/tmp/permafrost_test_report_$(date +%Y%m%d_%H%M%S).txt"
    local total_result=$((UNIT_TEST_RESULT + INTEGRATION_TEST_RESULT + PERFORMANCE_TEST_RESULT + SECURITY_TEST_RESULT))
    
    cat > "$report_file" << EOF
Permafrost Complete Test Report
Generated: $(date)
========================================

Test Results Summary:
- Unit Tests: $([ $UNIT_TEST_RESULT -eq 0 ] && echo "PASSED" || echo "FAILED")
- Integration Tests: $([ $INTEGRATION_TEST_RESULT -eq 0 ] && echo "PASSED" || echo "FAILED")
- Performance Tests: $([ $PERFORMANCE_TEST_RESULT -eq 0 ] && echo "PASSED" || echo "FAILED")
- Security Tests: $([ $SECURITY_TEST_RESULT -eq 0 ] && echo "PASSED" || echo "FAILED")

Overall Result: $([ $total_result -eq 0 ] && echo "PASSED" || echo "FAILED")

Test Configuration:
- Skip Unit Tests: $SKIP_UNIT_TESTS
- Skip Integration Tests: $SKIP_INTEGRATION_TESTS
- Skip Performance Tests: $SKIP_PERFORMANCE_TESTS
- Skip Security Tests: $SKIP_SECURITY_TESTS
- Parallel Execution: $PARALLEL_TESTS

Coverage Reports:
- Backend: backend/coverage.out
- Frontend: frontend/coverage/
- Edge Service: edge-service/TestResults/

Recommendations:
1. Review any failed tests and fix issues
2. Ensure test coverage is above 80%
3. Address any security warnings
4. Monitor performance metrics in production
5. Set up automated testing in CI/CD pipeline

Next Steps:
1. Fix any failing tests
2. Update documentation if needed
3. Deploy to staging environment
4. Run acceptance tests
5. Deploy to production
EOF

    echo -e "${GREEN}✅ Test report generated: $report_file${NC}"
    echo ""
    echo "Report contents:"
    cat "$report_file"
    
    return $total_result
}

# Function to cleanup test artifacts
cleanup_test_artifacts() {
    echo -e "${BLUE}🧹 Cleaning up test artifacts...${NC}"
    
    # Remove temporary files
    rm -f /tmp/ab_*.dat
    rm -f /tmp/memory_usage.csv
    
    # Clean up Docker if needed
    if [ "$1" = "full" ]; then
        echo "Stopping Docker Compose services..."
        docker-compose down
    fi
    
    echo -e "${GREEN}✅ Cleanup completed${NC}"
}

# Function to run tests in parallel
run_tests_parallel() {
    echo -e "${BLUE}🚀 Running tests in parallel...${NC}"
    
    # Start all test phases in background
    if [ "$SKIP_UNIT_TESTS" != "true" ]; then
        run_unit_tests &
        local unit_pid=$!
    fi
    
    if [ "$SKIP_INTEGRATION_TESTS" != "true" ]; then
        run_integration_tests &
        local integration_pid=$!
    fi
    
    if [ "$SKIP_PERFORMANCE_TESTS" != "true" ]; then
        run_performance_tests &
        local performance_pid=$!
    fi
    
    if [ "$SKIP_SECURITY_TESTS" != "true" ]; then
        run_security_tests &
        local security_pid=$!
    fi
    
    # Wait for all tests to complete
    if [ -n "$unit_pid" ]; then
        wait $unit_pid
        UNIT_TEST_RESULT=$?
    fi
    
    if [ -n "$integration_pid" ]; then
        wait $integration_pid
        INTEGRATION_TEST_RESULT=$?
    fi
    
    if [ -n "$performance_pid" ]; then
        wait $performance_pid
        PERFORMANCE_TEST_RESULT=$?
    fi
    
    if [ -n "$security_pid" ]; then
        wait $security_pid
        SECURITY_TEST_RESULT=$?
    fi
}

# Function to run tests sequentially
run_tests_sequential() {
    echo -e "${BLUE}🔄 Running tests sequentially...${NC}"
    
    run_unit_tests
    run_integration_tests
    run_performance_tests
    run_security_tests
}

# Main execution function
main() {
    local start_time=$(date +%s)
    
    echo -e "${BLUE}Starting complete test suite at $(date)${NC}"
    echo ""
    
    # Trap to cleanup on exit
    trap 'cleanup_test_artifacts' EXIT
    
    # Run tests based on configuration
    if [ "$PARALLEL_TESTS" = "true" ]; then
        run_tests_parallel
    else
        run_tests_sequential
    fi
    
    # Generate report
    generate_test_report
    local final_result=$?
    
    local end_time=$(date +%s)
    local total_duration=$((end_time - start_time))
    
    echo ""
    echo "=================================================="
    echo -e "${BLUE}📊 Final Results${NC}"
    echo "=================================================="
    echo "Total Duration: ${total_duration}s"
    
    if [ $final_result -eq 0 ]; then
        echo -e "${GREEN}🎉 All tests PASSED!${NC}"
        echo ""
        echo "✅ Ready for deployment"
    else
        echo -e "${RED}❌ Some tests FAILED!${NC}"
        echo ""
        echo "🔧 Please fix failing tests before deployment"
    fi
    
    exit $final_result
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --skip-unit)
            SKIP_UNIT_TESTS="true"
            shift
            ;;
        --skip-integration)
            SKIP_INTEGRATION_TESTS="true"
            shift
            ;;
        --skip-performance)
            SKIP_PERFORMANCE_TESTS="true"
            shift
            ;;
        --skip-security)
            SKIP_SECURITY_TESTS="true"
            shift
            ;;
        --sequential)
            PARALLEL_TESTS="false"
            shift
            ;;
        --help)
            echo "Usage: $0 [options]"
            echo "Options:"
            echo "  --skip-unit         Skip unit tests"
            echo "  --skip-integration  Skip integration tests"
            echo "  --skip-performance  Skip performance tests"
            echo "  --skip-security     Skip security tests"
            echo "  --sequential        Run tests sequentially instead of parallel"
            echo "  --help              Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

# Run main function
main "$@"
