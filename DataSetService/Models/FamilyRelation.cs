namespace DataSetService.Models;

public class FamilyRelation
{
    public long Id { get; set; }
    public string? IndividualType { get; set; }  // "PAR" = parent, "CHIL" = child
    public long IndividualID { get; set; }
    public long? FamilyAncestor { get; set; }
}
