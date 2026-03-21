using Microsoft.Data.Sqlite;
using DataSetService.Models;

namespace DataSetService;

public class PersonService
{
    private readonly string _dbPath;

    public PersonService(string dbPath = "/var/lib/FamilyWeb/FamilyDB.sqlite")
    {
        _dbPath = dbPath;
    }

    private SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString());
        conn.Open();
        return conn;
    }

    private static long ToLong(SqliteDataReader dr, int i)
    {
        if (dr.IsDBNull(i)) return 0;
        var val = dr.GetValue(i)?.ToString();
        return string.IsNullOrEmpty(val) ? 0 : Convert.ToInt64(val);
    }

    private static long? ToLongNullable(SqliteDataReader dr, int i)
    {
        if (dr.IsDBNull(i)) return null;
        var val = dr.GetValue(i)?.ToString();
        return string.IsNullOrEmpty(val) ? null : Convert.ToInt64(val);
    }

    private static Person MapPerson(SqliteDataReader dr) => new Person
    {
        Id                = ToLong(dr, 0),
        SurName           = dr.IsDBNull(1) ? null : dr.GetString(1),
        FirstName         = dr.IsDBNull(2) ? null : dr.GetString(2),
        Sex               = dr.IsDBNull(3) ? null : dr.GetString(3),
        IsSpouseInFamily  = ToLongNullable(dr, 4),
        IsChildInFamily   = ToLongNullable(dr, 5),
        Notes             = dr.IsDBNull(6) ? null : dr.GetString(6),
        Birthday          = dr.IsDBNull(7) ? null : dr.GetString(7),
        Photo             = dr.IsDBNull(8) || string.IsNullOrEmpty(dr.GetString(8)) ? "EmptyPhoto.png" : dr.GetString(8),
        FamilyTrees       = dr.IsDBNull(9) ? null : dr.GetString(9),
        RootId            = ToLongNullable(dr, 10),
        IsSpouseInFamily2 = ToLongNullable(dr, 11),
    };

    public List<Person> GetAllPersons()
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Persons ORDER BY SurName, FirstName";
        using var dr = cmd.ExecuteReader();
        var result = new List<Person>();
        while (dr.Read()) result.Add(MapPerson(dr));
        return result;
    }

    public Person? GetPerson(long id)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Persons WHERE ID = @id";
        cmd.Parameters.AddWithValue("@id", id.ToString());
        using var dr = cmd.ExecuteReader();
        return dr.Read() ? MapPerson(dr) : null;
    }

    public void UpdatePersonPhoto(long id, string photoFileName)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Persons SET Photo = @photo WHERE ID = @id";
        cmd.Parameters.AddWithValue("@photo", photoFileName);
        cmd.Parameters.AddWithValue("@id", id.ToString());
        cmd.ExecuteNonQuery();
    }

    // Returns all family members belonging to the given family ID.
    public List<FamilyRelation> GetFamilyRelations(long familyId)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM \"dbo.Families\" WHERE ID = @familyId";
        cmd.Parameters.AddWithValue("@familyId", familyId.ToString());
        using var dr = cmd.ExecuteReader();
        var result = new List<FamilyRelation>();
        while (dr.Read())
        {
            result.Add(new FamilyRelation
            {
                Id             = ToLong(dr, 0),
                IndividualType = dr.IsDBNull(1) ? null : dr.GetString(1),
                IndividualID   = ToLong(dr, 2),
                FamilyAncestor = ToLongNullable(dr, 3),
            });
        }
        return result;
    }

    public Baptism? GetBaptism(long personId)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM \"dbo.Baptisms\" WHERE IndividualID = @personId";
        cmd.Parameters.AddWithValue("@personId", personId.ToString());
        using var dr = cmd.ExecuteReader();
        if (!dr.Read()) return null;
        return new Baptism
        {
            Id           = dr.IsDBNull(0) ? 0 : dr.GetInt64(0),
            IndividualID = dr.IsDBNull(1) ? 0 : dr.GetInt64(1),
            BaptismDate  = dr.IsDBNull(2) ? null : dr.GetString(2),
            BaptismPlace = dr.IsDBNull(3) ? null : dr.GetString(3),
            GodParent1   = dr.IsDBNull(4) ? null : dr.GetString(4),
            GodParent2   = dr.IsDBNull(5) ? null : dr.GetString(5),
            Notes        = dr.IsDBNull(6) ? null : dr.GetString(6),
        };
    }

    public List<Person> BuildFamilyTree(long rootPersonId)
    {
        var result = new List<Person>();
        var visitedFamilies = new HashSet<long>();
        BuildTreeRecursive(rootPersonId, result, visitedFamilies);
        return result;
    }

    private void BuildTreeRecursive(long rootPersonId, List<Person> result, HashSet<long> visitedFamilies)
    {
        var rootPerson = GetPerson(rootPersonId);
        if (rootPerson?.IsSpouseInFamily == null) return;

        long familyId = rootPerson.IsSpouseInFamily.Value;
        if (!visitedFamilies.Add(familyId)) return;

        var members = GetFamilyRelations(familyId);
        var childrenToRecurse = new List<long>();

        foreach (var member in members)
        {
            bool isAncestor = member.FamilyAncestor == 1;
            bool isChild    = member.IndividualType == "CHIL";

            if (!isAncestor && !isChild) continue;

            var person = GetPerson(member.IndividualID);
            if (person == null) continue;

            // Attach spouse (the other PAR in this family that is not an ancestor)
            if (person.IsSpouseInFamily != null)
            {
                var spouseRelation = GetFamilyRelations(person.IsSpouseInFamily.Value)
                    .FirstOrDefault(r => r.IndividualType == "PAR" && r.FamilyAncestor != 1 && r.IndividualID != person.Id);
                if (spouseRelation != null)
                    person.Spouse = GetPerson(spouseRelation.IndividualID);
            }

            // Mark the subtree root for tree rendering, queue child for recursion
            if (!isAncestor && member.IndividualType != "PAR")
            {
                person.RootId = rootPersonId;
                if (person.IsSpouseInFamily != null)
                    childrenToRecurse.Add(person.Id);
            }

            result.Add(person);
        }

        foreach (var childId in childrenToRecurse)
            BuildTreeRecursive(childId, result, visitedFamilies);
    }
}

