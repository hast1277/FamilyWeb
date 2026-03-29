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
        var val = dr.GetValue(i)?.ToString()?.Trim();
        if (string.IsNullOrEmpty(val)) return null;
        if (long.TryParse(val, out var result)) return result;
        // Handle multi-family values like "50- 49" or "61- 61" — take the first number
        var first = val.Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries)[0];
        return long.TryParse(first, out var firstResult) ? firstResult : null;
    }

    private static Person ToPerson(PersonReadModel source) => new Person
    {
        Id                = source.Id,
        SurName           = source.SurName,
        FirstName         = source.FirstName,
        Sex               = source.Sex,
        IsSpouseInFamily  = source.IsSpouseInFamily,
        IsChildInFamily   = source.IsChildInFamily,
        Notes             = source.Notes,
        Birthday          = source.Birthday,
        DeathDate         = source.DeathDate,
        Photo             = source.Photo,
        FamilyTrees       = source.FamilyTrees,
        RootId            = source.RootId,
        IsSpouseInFamily2 = source.IsSpouseInFamily2,
    };

    public List<Person> GetAllPersons()
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = PersonReadDefinition.SelectColumnsWithDeathDate + " ORDER BY p.SurName, p.FirstName";
        using var dr = cmd.ExecuteReader();
        var result = new List<Person>();
        while (dr.Read()) result.Add(ToPerson(PersonReadDefinition.Map(dr)));
        return result;
    }

    public Person? GetPerson(long id)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = PersonReadDefinition.SelectByIdWithDeathDate;
        cmd.Parameters.AddWithValue("@id", id.ToString());
        using var dr = cmd.ExecuteReader();
        return dr.Read() ? ToPerson(PersonReadDefinition.Map(dr)) : null;
    }

    public List<Person> GetParents(long personId)
    {
        var person = GetPerson(personId);
        if (person?.IsChildInFamily is null)
            return new List<Person>();

        return GetFamilyRelations(person.IsChildInFamily.Value)
            .Where(relation => relation.IndividualType == "PAR")
            .Select(relation => GetPerson(relation.IndividualID))
            .Where(parent => parent != null)
            .Select(parent => parent!)
            .DistinctBy(parent => parent.Id)
            .OrderBy(parent => GetParentSortKey(parent.Sex))
            .ThenBy(parent => parent.SurName)
            .ThenBy(parent => parent.FirstName)
            .ToList();
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

    private static int GetParentSortKey(string? sex) => sex switch
    {
        "F" => 0,
        "M" => 1,
        _ => 2,
    };

    // Returns all family members belonging to the given family ID.
    public List<FamilyRelation> GetFamilyRelations(long familyId)
    {
        using var conn = OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM \"Families\" WHERE ID = @familyId";
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
        cmd.CommandText = "SELECT * FROM \"Baptisms\" WHERE IndividualID = @personId";
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

