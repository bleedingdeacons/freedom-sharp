using System.Diagnostics;
using System.Net;
using System.Text;
using System.Web;
using TheBleedingDeacons.Freedom.Client.Abstractions;

namespace FreedomCli;

/// <summary>
/// A browser sign-in for a desktop or console: open the system browser and
/// listen on the loopback address for the redirect.
/// </summary>
/// <remarks>
/// The application must have "Allow a loopback callback" ticked in the Freedom
/// admin — development only. The callback is <c>http://127.0.0.1:PORT/</c>,
/// port 1024 or above.
/// </remarks>
internal sealed class LoopbackSignIn : IFreedomSignIn
{
	public async Task<BrowserResult> AuthenticateAsync(Uri authorizationUrl, Uri callbackUri, CancellationToken cancellationToken)
	{
		using var listener = new HttpListener();
		listener.Prefixes.Add(callbackUri.GetLeftPart(UriPartial.Authority) + "/");
		listener.Start();

		Console.WriteLine("Opening the browser to sign in. If it does not open, visit:");
		Console.WriteLine(authorizationUrl);
		Process.Start(new ProcessStartInfo(authorizationUrl.ToString()) { UseShellExecute = true });

		var context = await listener.GetContextAsync().WaitAsync(TimeSpan.FromMinutes(5), cancellationToken);
		var query = HttpUtility.ParseQueryString(context.Request.Url?.Query ?? string.Empty);

		var page = Encoding.UTF8.GetBytes("<html><body><p>Signed in. You can close this window.</p></body></html>");
		context.Response.ContentType = "text/html";
		await context.Response.OutputStream.WriteAsync(page, cancellationToken);
		context.Response.Close();

		var code = query["code"];
		return string.IsNullOrEmpty(code) ? BrowserResult.Failed(query["error"] ?? "verification") : BrowserResult.Succeeded(code);
	}
}
