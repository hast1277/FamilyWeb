using Microsoft.AspNetCore.Components;
using DataSetService;


namespace FamilyWebBlazorServer.Pages
{
    public partial class Persons : ComponentBase
    {
        protected List<Dictionary<string, object?>>? persons;

        protected override async Task OnInitializedAsync()
        {
            var service = new PersonService();
            persons = service.GetAllPersons();
        }
    }
}