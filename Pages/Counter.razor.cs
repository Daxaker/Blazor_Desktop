using Microsoft.AspNetCore.Components;

namespace Blazor_Desktop.Pages
{
	public partial class Counter
	{
		[Inject] private FetchData.WeatherForecast weather { get; set; }
		System.Threading.Timer timer;
		protected override void OnInitialized()
		{
			
			timer = new System.Threading.Timer(async _ =>
			{
				currentCount++ ;
				await InvokeAsync(StateHasChanged);
			}, null, 0, 10);
		}
		
		private int currentCount = 0;

		private void IncrementCount()
		{
			currentCount++;
		}
	}
}