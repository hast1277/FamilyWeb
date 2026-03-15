using Microsoft.AspNetCore.Components;
using DataSetService;
using DataSetService.Models;


namespace FamilyWebBlazorServer.Pages
{
    public partial class PersonDetails : ComponentBase
    {
        private const string PhotoBasePath = "/img/Family/";
        private const string PlaceholderPhotoFileName = "EmptyPhoto.png";
        private const string PlaceholderPhotoPath = $"{PhotoBasePath}{PlaceholderPhotoFileName}";

        [Parameter] public long Id { get; set; }

        private Person? person;
        private Baptism? baptism;
        private bool loaded;

        protected override void OnParametersSet()
        {
            loaded  = false;
            person  = PersonService.GetPerson(Id);
            baptism = PersonService.GetBaptism(Id);
            loaded  = true;
        }

        private static string GetPhotoUrl(Person person)
        {
            if (string.IsNullOrWhiteSpace(person.Photo))
                return PlaceholderPhotoPath;

            return $"{PhotoBasePath}{person.Photo}";
        }

        private static string GetPhotoAltText(Person person)
        {
            var fullName = string.Join(" ", new[] { person.FirstName, person.SurName }.Where(name => !string.IsNullOrWhiteSpace(name)));
            return string.IsNullOrWhiteSpace(fullName) ? "Foto saknas" : $"Foto på {fullName}";
        }
    }
}