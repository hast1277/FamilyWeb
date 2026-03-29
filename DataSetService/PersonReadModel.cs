using System.Globalization;
using Microsoft.Data.Sqlite;

namespace DataSetService;

internal sealed record PersonReadModel(
    long Id,
    string? SurName,
    string? FirstName,
    string? Sex,
    long? IsSpouseInFamily,
    long? IsChildInFamily,
    string? Notes,
    string? Birthday,
    string? DeathDate,
    string Photo,
    string? FamilyTrees,
    long? RootId,
    long? IsSpouseInFamily2);

internal static class PersonReadDefinition
{
    internal const string SelectColumnsWithDeathDate = @"
        SELECT p.ID,
               p.SurName,
               p.FirstName,
               p.Sex,
               p.IsSpouseInFamily,
               p.IsChildInFamily,
               p.Notes,
               p.Birthday,
               (
                   SELECT d.DeathDate
                   FROM Deaths d
                   WHERE d.IndividualID = p.ID
                   ORDER BY d.ID
                   LIMIT 1
               ) AS DeathDate,
               p.Photo,
               p.FamilyTrees,
               p.RootID,
               p.IsSpouseInFamily2
        FROM Persons p";

    internal const string SelectByIdWithDeathDate = SelectColumnsWithDeathDate + " WHERE p.ID = @id";

    internal static PersonReadModel Map(SqliteDataReader dr)
    {
        var photo = dr.IsDBNull(9) || string.IsNullOrWhiteSpace(dr.GetString(9)) ? "EmptyPhoto.png" : dr.GetString(9);

        return new PersonReadModel(
            Id: ToLong(dr, 0),
            SurName: dr.IsDBNull(1) ? null : dr.GetString(1),
            FirstName: dr.IsDBNull(2) ? null : dr.GetString(2),
            Sex: dr.IsDBNull(3) ? null : dr.GetString(3),
            IsSpouseInFamily: ToLongNullable(dr, 4),
            IsChildInFamily: ToLongNullable(dr, 5),
            Notes: dr.IsDBNull(6) ? null : dr.GetString(6),
            Birthday: dr.IsDBNull(7) ? null : dr.GetString(7),
            DeathDate: dr.IsDBNull(8) ? null : dr.GetString(8),
            Photo: photo,
            FamilyTrees: dr.IsDBNull(10) ? null : dr.GetString(10),
            RootId: ToLongNullable(dr, 11),
            IsSpouseInFamily2: ToLongNullable(dr, 12));
    }

    private static long ToLong(SqliteDataReader dr, int i)
    {
        if (dr.IsDBNull(i)) return 0;
        var val = dr.GetValue(i)?.ToString();
        return string.IsNullOrWhiteSpace(val) ? 0 : Convert.ToInt64(val, CultureInfo.InvariantCulture);
    }

    private static long? ToLongNullable(SqliteDataReader dr, int i)
    {
        if (dr.IsDBNull(i)) return null;
        var val = dr.GetValue(i)?.ToString()?.Trim();
        if (string.IsNullOrEmpty(val)) return null;
        if (long.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)) return result;

        // Handle values like "50- 49" and keep compatibility with legacy data.
        var first = val.Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries)[0];
        return long.TryParse(first, NumberStyles.Integer, CultureInfo.InvariantCulture, out var firstResult)
            ? firstResult
            : null;
    }
}
