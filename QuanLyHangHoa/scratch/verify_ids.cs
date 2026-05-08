using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;

string connectionString = "Server=.\\SQLEXPRESS;Database=ProductManagementDb;Trusted_Connection=True;TrustServerCertificate=True;";

using (var connection = new SqlConnection(connectionString))
{
    connection.Open();
    
    Console.WriteLine("--- Products ---");
    using (var command = new SqlCommand("SELECT TOP 5 Id, DisplayName FROM Product", connection))
    using (var reader = command.ExecuteReader())
    {
        while (reader.Read())
        {
            Console.WriteLine($"{reader["Id"]}: {reader["DisplayName"]}");
        }
    }

    Console.WriteLine("\n--- StockInLines ---");
    using (var command = new SqlCommand("SELECT TOP 5 Id FROM StockInLine", connection))
    using (var reader = command.ExecuteReader())
    {
        while (reader.Read())
        {
            Console.WriteLine($"{reader["Id"]}");
        }
    }
}
