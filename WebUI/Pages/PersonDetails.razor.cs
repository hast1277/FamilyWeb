using System.IO;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using DataSetService;
using DataSetService.Models;

namespace FamilyWebBlazorServer.Pages
{
	public partial class PersonDetails : ComponentBase
	{
		private const string PhotoBasePath = "/img/Family/";
		private const string PlaceholderPhotoFileName = "EmptyPhoto.png";
		private const string PlaceholderPhotoPath = $"{PhotoBasePath}{PlaceholderPhotoFileName}";
		private const long MaxPhotoUploadSize = 5 * 1024 * 1024;
		private static readonly HashSet<string> AllowedPhotoExtensions = new(StringComparer.OrdinalIgnoreCase)
		{
			".jpg",
			".jpeg",
			".png",
			".gif",
			".webp"
		};

		[Parameter] public long Id { get; set; }
		[Parameter] public EventCallback<string?> OnPhotoUpdated { get; set; }
		[Inject] private PersonService PersonService { get; set; } = default!;
		[Inject] private IWebHostEnvironment HostEnvironment { get; set; } = default!;

		private Person? person;
		private Baptism? baptism;
		private bool loaded;
		private bool isUploading;
		private string? photoUploadMessage;
		private string PhotoInputId => $"person-details-photo-upload-{Id}";

		protected override void OnParametersSet()
		{
			loaded = false;
			person = PersonService.GetPerson(Id);
			baptism = PersonService.GetBaptism(Id);
			loaded = true;
			photoUploadMessage = null;
		}

		private async Task OnPhotoSelectedAsync(InputFileChangeEventArgs e)
		{
			if (person == null)
			{
				photoUploadMessage = "Ingen person är vald.";
				return;
			}

			var file = e.File;
			if (file == null)
			{
				photoUploadMessage = "Ingen fil valdes.";
				return;
			}

			var extension = Path.GetExtension(file.Name);
			if (string.IsNullOrWhiteSpace(extension) || !AllowedPhotoExtensions.Contains(extension))
			{
				photoUploadMessage = "Välj en bild i formatet JPG, PNG, GIF eller WebP.";
				return;
			}

			if (file.Size > MaxPhotoUploadSize)
			{
				photoUploadMessage = "Bilden är för stor. Maxstorlek är 5 MB.";
				return;
			}

			isUploading = true;
			photoUploadMessage = null;
			string? savedFilePath = null;

			try
			{
				var uploadsPath = Path.Combine(HostEnvironment.WebRootPath, "img", "Family");
				Directory.CreateDirectory(uploadsPath);

				var previousPhoto = person.Photo;
				var fileName = $"person-{person.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}{extension.ToLowerInvariant()}";
				var filePath = Path.Combine(uploadsPath, fileName);
				savedFilePath = filePath;

				await using (var uploadStream = file.OpenReadStream(MaxPhotoUploadSize))
				await using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
				{
					await uploadStream.CopyToAsync(fileStream);
				}

				PersonService.UpdatePersonPhoto(person.Id, fileName);
				person.Photo = fileName;

				DeletePreviousGeneratedPhoto(previousPhoto, uploadsPath, person.Id);
				photoUploadMessage = "Fotot har uppdaterats.";

				if (OnPhotoUpdated.HasDelegate)
				{
					await OnPhotoUpdated.InvokeAsync(fileName);
				}
			}
			catch (SqliteException ex) when (ex.SqliteErrorCode == 8)
			{
				TryDeleteFile(savedFilePath);
				photoUploadMessage = "Det gick inte att spara fotot eftersom databasen är skrivskyddad.";
			}
			catch
			{
				TryDeleteFile(savedFilePath);
				photoUploadMessage = "Det gick inte att ladda upp bilden.";
			}
			finally
			{
				isUploading = false;
			}
		}

		private static void TryDeleteFile(string? filePath)
		{
			if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
				return;

			File.Delete(filePath);
		}

		private static void DeletePreviousGeneratedPhoto(string? previousPhoto, string uploadsPath, long personId)
		{
			if (string.IsNullOrWhiteSpace(previousPhoto) ||
				string.Equals(previousPhoto, PlaceholderPhotoFileName, StringComparison.OrdinalIgnoreCase) ||
				!previousPhoto.StartsWith($"person-{personId}-", StringComparison.OrdinalIgnoreCase))
			{
				return;
			}

			var previousPhotoPath = Path.Combine(uploadsPath, previousPhoto);
			if (File.Exists(previousPhotoPath))
			{
				File.Delete(previousPhotoPath);
			}
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
