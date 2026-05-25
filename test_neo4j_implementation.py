#!/usr/bin/env python3
"""
Test script to verify Neo4j service implementation for Task #110
"""

import os
import sys

def test_docker_compose_file():
    """Test that docker-compose.yml contains Neo4j service"""
    print("Testing docker-compose.yml...")
    
    try:
        with open('docker-compose.yml', 'r') as f:
            content = f.read()
        
        # Check for Neo4j service
        if 'neo4j:' in content:
            print("✅ Neo4j service defined")
        else:
            print("❌ Neo4j service not found")
            return False
            
        # Check for required Neo4j configuration
        required_configs = [
            'image: neo4j:5',
            'NEO4J_AUTH: neo4j/lexwolf123',
            '7474:7474',
            '7687:7687',
            'neo4j_data:/data'
        ]
        
        for config in required_configs:
            if config in content:
                print(f"✅ Found: {config}")
            else:
                print(f"❌ Missing: {config}")
                return False
                
        return True
    except Exception as e:
        print(f"❌ Error reading docker-compose.yml: {e}")
        return False

def test_env_example_file():
    """Test that .env.example contains Neo4j variables"""
    print("\nTesting .env.example...")
    
    try:
        with open('.env.example', 'r') as f:
            content = f.read()
            
        # Check for Neo4j environment variables
        required_vars = [
            'NEO4J_URI=neo4j://localhost:7687',
            'NEO4J_USER=neo4j',
            'NEO4J_PASSWORD=lexwolf123'
        ]
        
        for var in required_vars:
            if var in content:
                print(f"✅ Found in .env.example: {var}")
            else:
                print(f"❌ Missing in .env.example: {var}")
                return False
                
        return True
    except Exception as e:
        print(f"❌ Error reading .env.example: {e}")
        return False

def test_docker_compose_up():
    """Test that docker-compose can start (configuration test only)"""
    print("\nTesting docker-compose configuration...")
    
    try:
        import subprocess
        result = subprocess.run(["docker-compose", "config"], capture_output=True, text=True, timeout=30)
        if result.returncode == 0:
            print("✅ docker-compose.yml is valid")
            return True
        else:
            print("❌ docker-compose.yml is invalid")
            print(f"   Error: {result.stderr}")
            return False
    except Exception as e:
        print(f"❌ Error validating docker-compose config: {e}")
        return False

def main():
    """Main test function"""
    print("Neo4j Service Implementation Test for Task #110")
    print("=" * 55)
    
    # Change to project directory
    os.chdir("/data/.openclaw/workspace-codex/projects/skillforge")
    print(f"Working in: {os.getcwd()}")
    
    tests = [
        ("Docker Compose File", test_docker_compose_file),
        ("Environment Example File", test_env_example_file),
        ("Docker Compose Configuration", test_docker_compose_up)
    ]
    
    passed = 0
    total = len(tests)
    
    for test_name, test_func in tests:
        print(f"\n{test_name}:")
        if test_func():
            passed += 1
        else:
            print(f"  Failed: {test_name}")
    
    print("\n" + "=" * 55)
    print(f"Test Results: {passed}/{total} tests passed")
    
    if passed == total:
        print("\n🎉 Task #110: Neo4j service implementation is COMPLETE!")
        print("\nWhat's implemented:")
        print("  ✓ Neo4j service added to docker-compose.yml")
        print("  ✓ Image: neo4j:5")
        print("  ✓ Ports: 7474 (HTTP) and 7687 (Bolt)")
        print("  ✓ Volumes: neo4j_data for persistence")
        print("  ✓ Environment: NEO4J_AUTH=neo4j/lexwolf123")
        print("  ✓ .env.example in root with Neo4j variables")
        print("  ✓ NEO4J_URI, NEO4J_USER, NEO4J_PASSWORD added")
        print("\nTo test:")
        print("  docker-compose up -d")
        print("  Visit http://localhost:7474 in browser")
        return 0
    else:
        print("\n❌ Task #110: Neo4j service implementation needs attention!")
        return 1

if __name__ == "__main__":
    sys.exit(main())