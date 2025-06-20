#!/bin/bash

echo "Testing VulnArena Logging System"
echo "================================="

# Test 1: Login as admin
echo "1. Logging in as admin..."
LOGIN_RESPONSE=$(curl -s -X POST http://localhost:5028/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}')

echo "Login response: $LOGIN_RESPONSE"

# Extract session token
SESSION_TOKEN=$(echo $LOGIN_RESPONSE | grep -o '"sessionToken":"[^"]*"' | cut -d'"' -f4)

if [ -z "$SESSION_TOKEN" ]; then
    echo "Failed to get session token"
    exit 1
fi

echo "Session token: $SESSION_TOKEN"

# Test 2: Access challenges to generate some logs
echo "2. Accessing challenges to generate logs..."
curl -s http://localhost:5028/api/challenges > /dev/null

# Test 3: Try to access logs with authentication
echo "3. Testing logs endpoint with authentication..."
LOGS_RESPONSE=$(curl -s http://localhost:5028/api/logs \
  -H "Authorization: Bearer $SESSION_TOKEN")

echo "Logs response: $LOGS_RESPONSE"

# Test 4: Test logs statistics
echo "4. Testing logs statistics..."
STATS_RESPONSE=$(curl -s http://localhost:5028/api/logs/statistics \
  -H "Authorization: Bearer $SESSION_TOKEN")

echo "Statistics response: $STATS_RESPONSE"

echo "Test completed!" 