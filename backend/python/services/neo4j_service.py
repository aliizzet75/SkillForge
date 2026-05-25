"""
Neo4j Service for connecting to Neo4j database
"""
from neo4j import GraphDatabase
import logging

class Neo4jService:
    """Service class for Neo4j database operations"""
    
    def __init__(self, uri, user, password):
        """
        Initialize Neo4j service
        
        Args:
            uri (str): Neo4j connection URI
            user (str): Username for authentication
            password (str): Password for authentication
        """
        self._uri = uri
        self._user = user
        self._password = password
        self._driver = None
        
    def connect(self):
        """
        Establish connection to Neo4j database
        
        Returns:
            bool: True if connection successful, False otherwise
        """
        try:
            self._driver = GraphDatabase.driver(self._uri, auth=(self._user, self._password))
            return True
        except Exception as e:
            logging.error(f"Failed to connect to Neo4j: {e}")
            return False
    
    def close(self):
        """
        Close the Neo4j database connection
        """
        if self._driver:
            self._driver.close()
            self._driver = None
    
    def run_query(self, cypher, params=None):
        """
        Run a Cypher query against the Neo4j database
        
        Args:
            cypher (str): Cypher query string
            params (dict, optional): Parameters for the query
            
        Returns:
            list: Query results
        """
        if not self._driver:
            raise Exception("No active connection to Neo4j database")
        
        try:
            with self._driver.session() as session:
                result = session.run(cypher, parameters=params)
                return [record.data() for record in result]
        except Exception as e:
            logging.error(f"Failed to execute query: {e}")
            raise
    
    def health_check(self):
        """
        Perform a health check on the Neo4j connection
        
        Returns:
            bool: True if healthy, False otherwise
        """
        try:
            result = self.run_query("RETURN 1 AS result")
            return len(result) > 0 and result[0]["result"] == 1
        except Exception as e:
            logging.error(f"Health check failed: {e}")
            return False
    
    def __enter__(self):
        """Context manager entry"""
        self.connect()
        return self
    
    def __exit__(self, exc_type, exc_val, exc_tb):
        """Context manager exit"""
        self.close()