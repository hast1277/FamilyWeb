using Microsoft.AspNetCore.Components;
using DataSetService;
using DataSetService.Models;


namespace FamilyWebBlazorServer.Pages
{
    public partial class Persons : ComponentBase
    {
        protected List<Person>? persons;

        protected override async Task OnInitializedAsync()
        {
            var service = new PersonService();
            persons = service.GetAllPersons();
        }
    }
}