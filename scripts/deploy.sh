#!/bin/bash

# Permafrost Deployment Script
# This script automates the deployment of Permafrost to Kubernetes

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
ENVIRONMENT="${ENVIRONMENT:-production}"
NAMESPACE="${NAMESPACE:-permafrost}"
DOCKER_REGISTRY="${DOCKER_REGISTRY:-your-registry.com}"
IMAGE_TAG="${IMAGE_TAG:-$(git rev-parse --short HEAD)}"
KUBECTL_CONTEXT="${KUBECTL_CONTEXT:-}"

echo -e "${BLUE}🚀 Starting Permafrost Deployment${NC}"
echo "=================================================="
echo "Environment: $ENVIRONMENT"
echo "Namespace: $NAMESPACE"
echo "Docker Registry: $DOCKER_REGISTRY"
echo "Image Tag: $IMAGE_TAG"
echo "Kubectl Context: ${KUBECTL_CONTEXT:-default}"
echo "=================================================="

# Function to check prerequisites
check_prerequisites() {
    echo -e "${BLUE}🔧 Checking prerequisites...${NC}"
    
    local missing_tools=()
    
    if ! command -v kubectl &> /dev/null; then
        missing_tools+=("kubectl")
    fi
    
    if ! command -v docker &> /dev/null; then
        missing_tools+=("docker")
    fi
    
    if ! command -v git &> /dev/null; then
        missing_tools+=("git")
    fi
    
    if [ ${#missing_tools[@]} -gt 0 ]; then
        echo -e "${RED}❌ Missing required tools:${NC}"
        for tool in "${missing_tools[@]}"; do
            echo -e "${RED}  - $tool${NC}"
        done
        exit 1
    fi
    
    # Check if we're in a git repository
    if ! git rev-parse --git-dir > /dev/null 2>&1; then
        echo -e "${RED}❌ Not in a git repository${NC}"
        exit 1
    fi
    
    # Check if kubectl can connect to cluster
    if ! kubectl cluster-info > /dev/null 2>&1; then
        echo -e "${RED}❌ Cannot connect to Kubernetes cluster${NC}"
        exit 1
    fi
    
    echo -e "${GREEN}✅ All prerequisites met${NC}"
}

# Function to set kubectl context
set_kubectl_context() {
    if [ -n "$KUBECTL_CONTEXT" ]; then
        echo -e "${BLUE}🔧 Setting kubectl context to $KUBECTL_CONTEXT...${NC}"
        kubectl config use-context "$KUBECTL_CONTEXT"
    fi
}

# Function to build Docker images
build_images() {
    echo -e "${BLUE}🐳 Building Docker images...${NC}"
    
    # Build backend image
    echo "Building backend image..."
    docker build -t "$DOCKER_REGISTRY/permafrost-backend:$IMAGE_TAG" ./backend
    docker build -t "$DOCKER_REGISTRY/permafrost-backend:latest" ./backend
    
    # Build frontend image
    echo "Building frontend image..."
    docker build -t "$DOCKER_REGISTRY/permafrost-frontend:$IMAGE_TAG" ./frontend
    docker build -t "$DOCKER_REGISTRY/permafrost-frontend:latest" ./frontend
    
    echo -e "${GREEN}✅ Docker images built successfully${NC}"
}

# Function to push Docker images
push_images() {
    echo -e "${BLUE}📤 Pushing Docker images to registry...${NC}"
    
    # Push backend images
    docker push "$DOCKER_REGISTRY/permafrost-backend:$IMAGE_TAG"
    docker push "$DOCKER_REGISTRY/permafrost-backend:latest"
    
    # Push frontend images
    docker push "$DOCKER_REGISTRY/permafrost-frontend:$IMAGE_TAG"
    docker push "$DOCKER_REGISTRY/permafrost-frontend:latest"
    
    echo -e "${GREEN}✅ Docker images pushed successfully${NC}"
}

# Function to create namespace
create_namespace() {
    echo -e "${BLUE}🏗️  Creating namespace...${NC}"
    
    if kubectl get namespace "$NAMESPACE" > /dev/null 2>&1; then
        echo -e "${YELLOW}⚠️  Namespace $NAMESPACE already exists${NC}"
    else
        kubectl apply -f k8s/namespace.yaml
        echo -e "${GREEN}✅ Namespace created${NC}"
    fi
}

# Function to apply secrets
apply_secrets() {
    echo -e "${BLUE}🔐 Applying secrets...${NC}"
    
    # Check if secrets file exists
    if [ ! -f "k8s/secrets.yaml" ]; then
        echo -e "${RED}❌ Secrets file not found: k8s/secrets.yaml${NC}"
        echo "Please create the secrets file with your actual values"
        exit 1
    fi
    
    # Validate that secrets are not using default values
    if grep -q "eW91ci1henVyZS1jbGllbnQtaWQ=" k8s/secrets.yaml; then
        echo -e "${YELLOW}⚠️  Warning: Using default Azure AD client ID${NC}"
        echo "Please update k8s/secrets.yaml with your actual Azure AD configuration"
    fi
    
    kubectl apply -f k8s/secrets.yaml
    echo -e "${GREEN}✅ Secrets applied${NC}"
}

# Function to apply configuration
apply_config() {
    echo -e "${BLUE}⚙️  Applying configuration...${NC}"
    
    kubectl apply -f k8s/configmap.yaml
    echo -e "${GREEN}✅ Configuration applied${NC}"
}

# Function to deploy database
deploy_database() {
    echo -e "${BLUE}🗄️  Deploying PostgreSQL database...${NC}"
    
    kubectl apply -f k8s/postgres.yaml
    
    # Wait for database to be ready
    echo "Waiting for database to be ready..."
    kubectl wait --for=condition=ready pod -l app.kubernetes.io/component=database -n "$NAMESPACE" --timeout=300s
    
    echo -e "${GREEN}✅ Database deployed and ready${NC}"
}

# Function to deploy NATS
deploy_nats() {
    echo -e "${BLUE}📡 Deploying NATS messaging...${NC}"
    
    kubectl apply -f k8s/nats.yaml
    
    # Wait for NATS to be ready
    echo "Waiting for NATS to be ready..."
    kubectl wait --for=condition=ready pod -l app.kubernetes.io/component=messaging -n "$NAMESPACE" --timeout=300s
    
    echo -e "${GREEN}✅ NATS deployed and ready${NC}"
}

# Function to deploy backend
deploy_backend() {
    echo -e "${BLUE}🔧 Deploying backend application...${NC}"
    
    # Update image tags in deployment
    sed -i.bak "s|permafrost-backend:1.0.0|$DOCKER_REGISTRY/permafrost-backend:$IMAGE_TAG|g" k8s/backend.yaml
    
    kubectl apply -f k8s/backend.yaml
    
    # Wait for backend to be ready
    echo "Waiting for backend to be ready..."
    kubectl wait --for=condition=ready pod -l app.kubernetes.io/component=backend -n "$NAMESPACE" --timeout=300s
    
    # Restore original file
    mv k8s/backend.yaml.bak k8s/backend.yaml
    
    echo -e "${GREEN}✅ Backend deployed and ready${NC}"
}

# Function to deploy frontend
deploy_frontend() {
    echo -e "${BLUE}🌐 Deploying frontend application...${NC}"
    
    # Update image tags in deployment
    sed -i.bak "s|permafrost-frontend:1.0.0|$DOCKER_REGISTRY/permafrost-frontend:$IMAGE_TAG|g" k8s/frontend.yaml
    
    kubectl apply -f k8s/frontend.yaml
    
    # Wait for frontend to be ready
    echo "Waiting for frontend to be ready..."
    kubectl wait --for=condition=ready pod -l app.kubernetes.io/component=frontend -n "$NAMESPACE" --timeout=300s
    
    # Restore original file
    mv k8s/frontend.yaml.bak k8s/frontend.yaml
    
    echo -e "${GREEN}✅ Frontend deployed and ready${NC}"
}

# Function to deploy ingress
deploy_ingress() {
    echo -e "${BLUE}🌍 Deploying ingress...${NC}"
    
    kubectl apply -f k8s/ingress.yaml
    
    echo -e "${GREEN}✅ Ingress deployed${NC}"
}

# Function to run health checks
run_health_checks() {
    echo -e "${BLUE}🏥 Running health checks...${NC}"
    
    # Check backend health
    echo "Checking backend health..."
    local backend_pod=$(kubectl get pods -l app.kubernetes.io/component=backend -n "$NAMESPACE" -o jsonpath='{.items[0].metadata.name}')
    if kubectl exec -n "$NAMESPACE" "$backend_pod" -- curl -f http://localhost:8080/health > /dev/null 2>&1; then
        echo -e "${GREEN}✅ Backend health check passed${NC}"
    else
        echo -e "${RED}❌ Backend health check failed${NC}"
    fi
    
    # Check frontend health
    echo "Checking frontend health..."
    local frontend_pod=$(kubectl get pods -l app.kubernetes.io/component=frontend -n "$NAMESPACE" -o jsonpath='{.items[0].metadata.name}')
    if kubectl exec -n "$NAMESPACE" "$frontend_pod" -- curl -f http://localhost:3000 > /dev/null 2>&1; then
        echo -e "${GREEN}✅ Frontend health check passed${NC}"
    else
        echo -e "${RED}❌ Frontend health check failed${NC}"
    fi
    
    # Check database connectivity
    echo "Checking database connectivity..."
    local postgres_pod=$(kubectl get pods -l app.kubernetes.io/component=database -n "$NAMESPACE" -o jsonpath='{.items[0].metadata.name}')
    if kubectl exec -n "$NAMESPACE" "$postgres_pod" -- pg_isready -U permafrost > /dev/null 2>&1; then
        echo -e "${GREEN}✅ Database connectivity check passed${NC}"
    else
        echo -e "${RED}❌ Database connectivity check failed${NC}"
    fi
}

# Function to display deployment status
show_deployment_status() {
    echo -e "${BLUE}📊 Deployment Status${NC}"
    echo "=================================================="
    
    echo "Pods:"
    kubectl get pods -n "$NAMESPACE" -o wide
    
    echo ""
    echo "Services:"
    kubectl get services -n "$NAMESPACE"
    
    echo ""
    echo "Ingress:"
    kubectl get ingress -n "$NAMESPACE"
    
    echo ""
    echo "Persistent Volumes:"
    kubectl get pvc -n "$NAMESPACE"
}

# Function to show access information
show_access_info() {
    echo -e "${BLUE}🌐 Access Information${NC}"
    echo "=================================================="
    
    # Get ingress IP
    local ingress_ip=$(kubectl get ingress permafrost-ingress -n "$NAMESPACE" -o jsonpath='{.status.loadBalancer.ingress[0].ip}' 2>/dev/null || echo "pending")
    
    if [ "$ingress_ip" != "pending" ] && [ -n "$ingress_ip" ]; then
        echo "Frontend URL: https://permafrost.yourdomain.com"
        echo "Backend API: https://api.permafrost.yourdomain.com"
        echo "Ingress IP: $ingress_ip"
        echo ""
        echo "Make sure to update your DNS records:"
        echo "  permafrost.yourdomain.com -> $ingress_ip"
        echo "  api.permafrost.yourdomain.com -> $ingress_ip"
    else
        echo "Ingress IP is still pending. Check status with:"
        echo "  kubectl get ingress -n $NAMESPACE"
    fi
    
    echo ""
    echo "Port forwarding for local access:"
    echo "  Frontend: kubectl port-forward -n $NAMESPACE svc/permafrost-frontend 3000:3000"
    echo "  Backend:  kubectl port-forward -n $NAMESPACE svc/permafrost-backend 8080:8080"
    echo "  NATS:     kubectl port-forward -n $NAMESPACE svc/permafrost-nats 4222:4222"
}

# Function to cleanup on failure
cleanup_on_failure() {
    echo -e "${RED}❌ Deployment failed. Cleaning up...${NC}"
    
    # Optionally rollback or cleanup resources
    # kubectl delete namespace "$NAMESPACE" --ignore-not-found=true
    
    exit 1
}

# Trap to handle failures
trap cleanup_on_failure ERR

# Main deployment function
main() {
    echo -e "${BLUE}Starting deployment at $(date)${NC}"
    echo ""
    
    check_prerequisites
    set_kubectl_context
    
    # Build and push images
    build_images
    push_images
    
    # Deploy infrastructure
    create_namespace
    apply_secrets
    apply_config
    
    # Deploy services in order
    deploy_database
    deploy_nats
    deploy_backend
    deploy_frontend
    deploy_ingress
    
    # Verify deployment
    run_health_checks
    show_deployment_status
    show_access_info
    
    echo ""
    echo -e "${GREEN}🎉 Deployment completed successfully!${NC}"
    echo ""
    echo "Next steps:"
    echo "1. Update DNS records to point to the ingress IP"
    echo "2. Configure Azure AD application with the correct redirect URIs"
    echo "3. Update secrets with production values"
    echo "4. Set up monitoring and alerting"
    echo "5. Configure backup procedures"
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --environment)
            ENVIRONMENT="$2"
            shift 2
            ;;
        --namespace)
            NAMESPACE="$2"
            shift 2
            ;;
        --registry)
            DOCKER_REGISTRY="$2"
            shift 2
            ;;
        --tag)
            IMAGE_TAG="$2"
            shift 2
            ;;
        --context)
            KUBECTL_CONTEXT="$2"
            shift 2
            ;;
        --help)
            echo "Usage: $0 [options]"
            echo "Options:"
            echo "  --environment ENV    Deployment environment (default: production)"
            echo "  --namespace NS       Kubernetes namespace (default: permafrost)"
            echo "  --registry REG       Docker registry (default: your-registry.com)"
            echo "  --tag TAG           Image tag (default: git commit hash)"
            echo "  --context CTX       Kubectl context"
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
