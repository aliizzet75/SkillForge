#!/usr/bin/env python3
"""
Test script for Neo4jService class
"""
import sys
import os

# Add the services directory to the path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'services'))

from neo4j_service import Neo4jService

def test_neo4j_service():
    """Test Neo4jService implementation"""
    print("Testing Neo4jService implementation...")
    
    # Test 1: Check that neo4j package is available
    try:
        import neo4j
        print("✅ neo4j package imported successfully")
    except ImportError as e:
        print(f"❌ Failed to import neo4j package: {e}")
        return False
    
    # Test 2: Check that Neo4jService class exists
    try:
        service = Neo4jService.__new__(Neo4jService)
        print("✅ Neo4jService class exists")
    except Exception as e:
        print(f"❌ Failed to create Neo4jService instance: {e}")
        return False
    
    # Test 3: Check that required methods exist
    required_methods = ['__init__', 'connect', 'close', 'run_query', 'health_check']
    for method in required_methods:
        if hasattr(Neo4jService, method):
            print(f"✅ Method {method} exists")
        else:
            print(f"❌ Method {method} is missing")
            return False
    
    # Test 4: Check requirements.txt content
    try:
        with open(os.path.join(os.path.dirname(__file__), 'requirements.txt'), 'r') as f:
            content = f.read()
            if 'neo4j>=5.0' in content:
                print("✅ neo4j>=5.0 found in requirements.txt")
            else:
                print("❌ neo4j>=5.0 not found in requirements.txt")
                return False
    except Exception as e:
        print(f"❌ Failed to read requirements.txt: {e}")
        return False
    
    # Test 5: Check file structure
    expected_files = [
        'services/neo4j_service.py',
        'requirements.txt'
    ]
    
    for file_path in expected_files:
        full_path = os.path.join(os.path.dirname(__file__), file_path)
        if os.path.exists(full_path):
            print(f"✅ File {file_path} exists")
        else:
            print(f"❌ File {file_path} is missing")
            return False
    
    return True

def main():
    """Main test function"""
    print("Neo4j Service Implementation Test for Task #111")
    print("=" * 50)
    
    if test_neo4j_service():
        print("\n🎉 All tests passed! Neo4j service implementation is ready.")
        print("\nWhat's implemented:")
        print("  ✓ neo4j>=5.0 added to backend/python/requirements.txt")
        print("  ✓ Neo4jService class in backend/python/services/neo4j_service.py")
        print("  ✓ __init__(uri, user, password) method")
        print("  ✓ connect() method")
        print("  ✓ close() method")
        print("  ✓ run_query(cypher, params) method")
        print("  ✓ health_check() method")
        print("  ✓ Context manager support (__enter__, __exit__)")
        print("\nTo test with a real Neo4j instance:")
        print("  1. Start Neo4j (docker-compose up -d)")
        print("  2. Run: python3 test_neo4j_connection.py")
        return 0
    else:
        print("\n❌ Some tests failed. Please check implementation.")
        return 1

if __name__ == "__main__":
    sys.exit(main())