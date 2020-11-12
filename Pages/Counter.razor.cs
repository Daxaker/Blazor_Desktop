using Microsoft.AspNetCore.Components;

namespace Blazor_Desktop.Pages
{
	public partial class Counter
	{
		[Inject] private FetchData.WeatherForecast weather { get; set; }
		[Inject] private GraphicEngine gEngine { get; set; }
		System.Threading.Timer timer;
		private string base64string;
		protected override void OnInitialized()
		{
			base64string = gEngine.GetImage();
			timer = new System.Threading.Timer(async _ =>
			{
				base64string = gEngine.GetImage();
				//currentCount++ ;
				await InvokeAsync(StateHasChanged);
			}, null, 0, 33);
		}
		
		private int currentCount = 0;

		private void IncrementCount()
		{
			currentCount++;
		}
	}
}