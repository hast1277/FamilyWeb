using Microsoft.AspNetCore.Components;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FamilyWebBlazorServer.Pages
{
    public partial class Persons : ComponentBase
    {
        protected List<Dictionary<string, object?>>? persons;

        protected override async Task OnInitializedAsync()
        {
            // adjust the path if necessary
            var dbPath = "/home/stefan-hall/gullberg.sqlite";
            var builder = new SqliteConnectionStringBuilder { DataSource = dbPath };

            using var conn = new SqliteConnection(builder.ToString());
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM Persons";

            using var reader = await cmd.ExecuteReaderAsync();
            var cols = Enumerable.Range(0, reader.FieldCount)
                                 .Select(i => reader.GetName(i))
                                 .ToArray();

            persons = new List<Dictionary<string, object?>>();

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                foreach (var col in cols)
                {
                    row[col] = reader[col];
                }
                persons.Add(row);
            }
        }
    }
}