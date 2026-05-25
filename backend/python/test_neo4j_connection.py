#!/usr/bin/env python3
"""
Test script to verify Neo4j connection
"""
import sys
import os

# Add the services directory to the path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'services'))

from neo4j_service import Neo4jService

def test_neo4j_connection():
    """Test actual Neo4j connection"""
    print("Testing Neo4j connection...")
    
    # Use default local Neo4j connection parameters
    uri = "neo4j://localhost:7687"
    user = "neo4j"
    password = "lexwolf123"
    
    try:
        # Test with context manager
        with Neo4jService(uri, user, password) as service:
            print("✅ Connected to Neo4j successfully")
            
            # Test health check
            if service.health_check():
                print("✅ Health check passed")
            else:
                print("❌ Health check failed")
                return False
                
            # Test simple query
            try:
                result = service.run_query("RETURN 1 AS result")
                if len(result) > 0 and result[0]["result"] == 1:
                    print("✅ Simple query executed successfully")
                else:
                    print("❌ Simple query returned unexpected result")
                    return False
            except Exception as e:
                print(f"❌ Failed to execute simple query: {e}")
                return False
                
        print("✅ Connection closed successfully")
        return True
        
    except Exception as e:
        print(f"❌ Failed to connect to Neo4j: {e}")
        return False

def main():
    """Main test function"""
    print("Neo4j Connection Test")
    print("=" * 25)
    
    if test_neo4j_connection():
        print("\n🎉 Neo4j connection test passed!")
        print("The Neo4jService implementation is working correctly.")
        return 0
    else:
        print("\n❌ Neo4j connection test failed.")
        print("Please ensure Neo4j is running and accessible.")
        return 1

if __name__ == "__main__":
    sys.exit(main())