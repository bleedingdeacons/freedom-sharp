// Freedom from a console: sign in once, then every run is a start.
//
//   FREEDOM_BASE_URL         the site, e.g. https://stalwart-dev.local
//   FREEDOM_APPLICATION      the application's slug, e.g. register
//   FREEDOM_DEVICE_ID        this "device", e.g. cli-dev-01 (8+ characters)
//   FREEDOM_CALLBACK         optional; default http://127.0.0.1:53682/
//   FREEDOM_ALLOW_PLAINTEXT  "1" to allow an http site (local development)
//   FREEDOM_STATE            optional; where to keep state (default ./.freedom)
//
//   dotnet run --project example/Freedom-cli            sign in if needed, then sync
//   dotnet run --project example/Freedom-cli -- signout sign out and forget everything
using FreedomCli;
using Microsoft.Extensions.Logging;
using TheBleedingDeacons.Freedom.Client;

var baseUrl = Environment.GetEnvironmentVariable("FREEDOM_BASE_URL");
var application = Environment.GetEnvironmentVariable("FREEDOM_APPLICATION");
var deviceId = Environment.GetEnvironmentVariable("FREEDOM_DEVICE_ID") ?? "cli-" + Environment.MachineName.ToLowerInvariant();

if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(application))
{
	Console.Error.WriteLine("Set FREEDOM_BASE_URL and FREEDOM_APPLICATION.");
	return 2;
}

var options = new FreedomOptions
{
	BaseUrl = new Uri(baseUrl),
	Application = application,
	CallbackUri = new Uri(Environment.GetEnvironmentVariable("FREEDOM_CALLBACK") ?? "http://127.0.0.1:53682/"),
	AllowInsecureBaseUrl = Environment.GetEnvironmentVariable("FREEDOM_ALLOW_PLAINTEXT") == "1",
};

var stores = new FileStores(Environment.GetEnvironmentVariable("FREEDOM_STATE") ?? Path.Combine(Environment.CurrentDirectory, ".freedom"));
using var logging = LoggerFactory.Create(b => b.AddSimpleConsole(o => o.SingleLine = true).SetMinimumLevel(LogLevel.Information));
using var freedom = new FreedomClient(
	options,
	stores,
	stores,
	new LoopbackSignIn(),
	new CliDevice(deviceId),
	logger: logging.CreateLogger<FreedomClient>());

freedom.ConfigChanged += (_, e) =>
	Console.WriteLine($"Changed: [{string.Join(", ", e.Updated)}]  Removed: [{string.Join(", ", e.Removed)}]");

if (args.Length > 0 && args[0] == "signout")
{
	await freedom.SignOutAsync();
	Console.WriteLine("Signed out.");
	return 0;
}

var result = await freedom.SyncAsync();

if (result.Status is SyncStatus.NotEnrolled or SyncStatus.Revoked)
{
	var enrolment = await freedom.EnrolAsync();
	Console.WriteLine($"Sign-in: {enrolment.Status} — {enrolment.Message}");
	if (!enrolment.Succeeded)
	{
		return 1;
	}

	result = enrolment.Sync!;
}

Console.WriteLine($"{result.Status} [{string.Join(", ", result.Updated)}] — {result.Message}");
Console.WriteLine($"Verified at: {result.VerifiedAt?.ToString("u") ?? "never"}");

foreach (var value in freedom.Current.Values.Values.OrderBy(v => v.Key, StringComparer.Ordinal))
{
	// The secret's value is never printed; its length proves it opened.
	Console.WriteLine(value.IsSecret
		? $"  {value.Key} (v{value.Version}) = <secret, {value.Value.Length} characters>"
		: $"  {value.Key} (v{value.Version}) = {value.Value}");
}

return 0;
