using Microsoft.Data.Sqlite;
namespace DataSetService;

public class PersonService
{
    public List<Dictionary<string, object?>>? GetAllPersons()
    {
        // adjust the path if necessary
        var dbPath = "/var/lib/FamilyWeb/gullberg.sqlite";
        var builder = new SqliteConnectionStringBuilder { DataSource = dbPath };

        using var conn = new SqliteConnection(builder.ToString());
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Persons";

        using var reader = cmd.ExecuteReader();
        var cols = Enumerable.Range(0, reader.FieldCount)
                             .Select(i => reader.GetName(i))
                             .ToArray();

        var persons = new List<Dictionary<string, object?>>();

        while (reader.Read())
        {
            var row = new Dictionary<string, object?>();
            foreach (var col in cols)
            {
                row[col] = reader[col];
            }
            persons.Add(row);
        }

        return persons;
    }

}
