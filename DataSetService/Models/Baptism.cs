namespace DataSetService.Models;

public class Baptism
{
    public long Id { get; set; }
    public long IndividualID { get; set; }
    public string? BaptismDate { get; set; }
    public string? BaptismPlace { get; set; }
    public string? GodParent1 { get; set; }
    public string? GodParent2 { get; set; }
    public string? Notes { get; set; }
}
