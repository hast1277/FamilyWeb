namespace DataSetService.Models;

public class Person
{
    public long Id { get; set; }
    public string? SurName { get; set; }
    public string? FirstName { get; set; }
    public string? Sex { get; set; }
    public long? IsSpouseInFamily { get; set; }
    public long? IsChildInFamily { get; set; }
    public string? Notes { get; set; }
    public string? Birthday { get; set; }
    public string? Photo { get; set; }
    public string? FamilyTrees { get; set; }
    public long? RootId { get; set; }
    public long? IsSpouseInFamily2 { get; set; }
    public Person? Spouse { get; set; }
}
