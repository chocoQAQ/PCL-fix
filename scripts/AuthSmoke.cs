// Exercises the auth code in the standalone launcher. Interactive credentials
// stay in memory; only boolean outcomes are written to the report.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using PCL;
using MeloongCore;

[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]

internal static class AuthSmoke
{
    private static Assembly launcher;
    private static readonly Dictionary<string, Assembly> dependencies = new Dictionary<string, Assembly>();

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            launcher = Assembly.LoadFrom(Path.Combine(args[0], "Plain Craft Launcher 2.exe"));
            AppDomain.CurrentDomain.AssemblyResolve += ResolveEmbedded;
            Environment.Exit(Run(args[0], args[1]));
            return 0;
        }
        catch (Exception ex)
        {
            while (ex is TargetInvocationException && ex.InnerException != null) ex = ex.InnerException;
            Console.Error.WriteLine("FAIL: " + ex.GetType().Name + ": " + ex.Message);
            Environment.Exit(1);
            return 1;
        }
    }

    private static Assembly ResolveEmbedded(object sender, ResolveEventArgs args)
    {
        var name = new AssemblyName(args.Name).Name;
        var names = new[] { "NAudio", "Newtonsoft.Json", "Ookii.Dialogs.Wpf", "Imazen.WebP", "CacheCow.Common", "CacheCow.Client.FileStore", "CacheCow.Client", "ThrottleDebounce", "System.Net.Http.Formatting", "Microsoft.Win32.Registry", "PCLCS", "MeloongCore.Wpf", "MeloongCore" };
        if (!names.Contains(name)) return null;
        lock (dependencies)
        {
            Assembly loaded;
            if (dependencies.TryGetValue(name, out loaded)) return loaded;
            var bytes = new ResourceManager("PCL.Resources", launcher).GetObject(name) as byte[];
            if (bytes == null) return null;
            loaded = Assembly.Load(bytes);
            dependencies[name] = loaded;
            return loaded;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Run(string work, string mode)
    {
        var app = new System.Windows.Application();
        MeloongCore.Main.Init("PCLFixAuthTest");
        foreach (var setting in Settings.Entries.Values) setting.ValueCache[""] = setting.DefaultValue;
        ModBase.PathTemp = Path.Combine(work, "cache") + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(ModBase.PathTemp);
        Exception failure = null;
        var worker = new Thread(delegate()
        {
            try
            {
                if (mode == "interactive") Interactive(work);
                else if (mode == "probe") Probe();
                else Regression(work);
            }
            catch (Exception ex) { failure = ex; }
            finally { app.Dispatcher.BeginInvoke(new Action(delegate { app.Shutdown(); })); }
        });
        worker.Start();
        app.Run();
        worker.Join();
        if (failure != null) throw failure;
        return 0;
    }

    private static object Auth(string name, params object[] args)
    {
        return typeof(ModMicrosoftAuth).GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, args);
    }

    private static object Launch(string name, params object[] args)
    {
        return typeof(ModLaunch).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
    }

    private static Exception Unwrap(Exception ex)
    {
        while (ex is TargetInvocationException && ex.InnerException != null) ex = ex.InnerException;
        return ex;
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("Regression failed: " + name);
    }

    private static JObject FakeTokens()
    {
        return JObject.Parse("{\"access_token\":\"TEST_ACCESS\",\"refresh_token\":\"TEST_REFRESH\"}");
    }

    private static JObject Poll(int expires, Func<string, JObject> request, Func<bool> cancel, Action<int> wait, Func<double> elapsed)
    {
        return (JObject)Auth("MsPollDeviceCodeCore", JObject.Parse("{\"interval\":5,\"expires_in\":" + expires + ",\"device_code\":\"TEST+&CODE\"}"), cancel, request, wait, elapsed);
    }

    private static void Regression(string work)
    {
        Check(ModMicrosoftAuth.MsGetClientId() == "e1dc8448-c3a0-43df-8ebd-bb146c003941", "embedded client id");
        double seconds = 0;
        var times = new List<double>();
        Func<string, JObject> request = delegate(string body)
        {
            Check(body.Contains("device_code=TEST%2B%26CODE"), "form escaping");
            times.Add(seconds);
            if (times.Count <= 2)
                throw new HttpRequestCodeException("TEST", HttpStatusCode.BadRequest,
                    times.Count == 1 ? "{\"error\":\"authorization_pending\"}" : "{\"error\":\"slow_down\"}");
            return FakeTokens();
        };
        var tokens = Poll(100, request, delegate { return false; }, delegate(int ms) { seconds += ms / 1000.0; }, delegate { return seconds; });
        Check(times.Count == 3 && times[0] >= 5 && times[1] - times[0] >= 5 && times[2] - times[1] >= 10, "pending and slow_down intervals");
        Check((string)tokens["access_token"] == "TEST_ACCESS", "token result");
        seconds = 0;
        int calls = 0;
        try { Poll(2, delegate(string body) { calls++; return FakeTokens(); }, delegate { return false; }, delegate(int ms) { seconds += ms / 1000.0; }, delegate { return seconds; }); throw new Exception("Expiry did not fail"); }
        catch (TargetInvocationException ex) { Check(Unwrap(ex).Message.Contains("过期") && calls == 0, "expiry before poll"); }
        seconds = 0;
        try { Poll(100, delegate(string body) { calls++; return FakeTokens(); }, delegate { return true; }, delegate(int ms) { seconds += ms / 1000.0; }, delegate { return seconds; }); throw new Exception("Cancel did not fail"); }
        catch (TargetInvocationException ex) { Check(Unwrap(ex) is OperationCanceledException && calls == 0, "cancel before poll"); }
        seconds = 0;
        calls = 0;
        try { Poll(100, delegate(string body) { calls++; return new JObject(); }, delegate { return false; }, delegate(int ms) { seconds += ms / 1000.0; }, delegate { return seconds; }); throw new Exception("Missing tokens accepted"); }
        catch (TargetInvocationException ex) { Check(Unwrap(ex).Message.Contains("不完整") && calls == 1, "reject incomplete token response"); }
        Check(((Exception)Auth("MsOAuthFailure", "{\"error\":\"authorization_declined\"}")).Message.Contains("拒绝"), "denied consent");
        Check(((Exception)Auth("MsOAuthFailure", "{\"error\":\"invalid_client\",\"error_description\":\"AADSTS7000218\"}")).Message.Contains("公共客户端"), "public client configuration hint");
        Check(((Exception)Auth("MsMinecraftLoginFailure", 403, "{\"errorMessage\":\"Invalid app registration\"}")).Message.Contains("审核"), "Minecraft app approval hint");
        Check(!((Exception)Auth("MsMinecraftLoginFailure", 403, "")).Message.Contains("IP"), "no false IP diagnosis");
        Check(((Exception)Auth("MsMinecraftLoginFailure", 403, "ACCOUNT_SUSPENDED")).Message.Contains("封禁"), "suspension before generic 403");
        seconds = 0;
        calls = 0;
        Poll(100, delegate(string body)
        {
            calls++;
            if (calls <= 2) throw new WebException("TEST timeout", WebExceptionStatus.Timeout);
            return FakeTokens();
        }, delegate { return false; }, delegate(int ms) { seconds += ms / 1000.0; }, delegate { return seconds; });
        Check(calls == 3 && seconds >= 15, "transient timeout retry interval");
        seconds = 0;
        calls = 0;
        try { Poll(100, delegate(string body) { calls++; throw new HttpRequestCodeException("TEST", HttpStatusCode.ServiceUnavailable, ""); }, delegate { return false; }, delegate(int ms) { seconds += ms / 1000.0; }, delegate { return seconds; }); throw new Exception("Unbounded retries"); }
        catch (TargetInvocationException ex) { Check(calls == 4 && Unwrap(ex).Message.Contains("稍后"), "bounded service retry"); }
        File.WriteAllText(Path.Combine(work, "result.json"), new JObject(new JProperty("protocol_regression_passed", true), new JProperty("tested_at_utc", DateTime.UtcNow.ToString("o"))).ToString());
        Console.WriteLine("PASS: polling intervals, throttling, expiry, cancellation, token validation and approval errors.");
    }

    private static void Probe()
    {
        var data = ModMicrosoftAuth.MsRequestDeviceCode();
        Check(!string.IsNullOrEmpty((string)data["device_code"]), "device code accepted");
        Console.WriteLine("PASS: Microsoft accepted the registered client ID and issued a device code. No account was authorized.");
    }

    private static void Interactive(string work)
    {
        var data = ModMicrosoftAuth.MsRequestDeviceCode();
        Console.WriteLine("Open the official Microsoft page: " + (string)data["verification_uri"]);
        Console.WriteLine("Enter this user code: " + (string)data["user_code"]);
        Console.WriteLine("Check that the application is your PCL-fix registration before approving. Waiting for authorization...");
        var tokens = ModMicrosoftAuth.MsPollDeviceCode(data, delegate { return false; });
        Console.WriteLine("PASS: Microsoft account authorization.");
        var xbl = (string)Launch("MsLoginStep2", (string)tokens["access_token"]);
        var xsts = Launch("MsLoginStep3", xbl);
        Console.WriteLine("PASS: Xbox Live and XSTS authentication.");
        var minecraft = Launch("MsLoginStep4", xsts);
        var access = (string)minecraft.GetType().GetField("Item1").GetValue(minecraft);
        Console.WriteLine("PASS: Minecraft service authentication.");
        var entitlement = JObject.Parse(ModNet.NetRequestByClient("https://api.minecraftservices.com/entitlements/mcstore",
            Headers: new string[,] { { "Authorization", "Bearer " + access } }, RequireJson: true, MakeLog: false));
        Check(entitlement["items"] != null && entitlement["items"].Any(), "owned Minecraft entitlement");
        var profile = Launch("MsLoginStep6", access);
        Check(!string.IsNullOrWhiteSpace((string)profile.GetType().GetField("Item1").GetValue(profile)), "Minecraft profile");
        Console.WriteLine("PASS: Minecraft entitlement and player profile.");
        var refreshed = ModMicrosoftAuth.MsRefreshOAuthToken((string)tokens["refresh_token"]);
        var refreshedAccess = (string)refreshed.GetType().GetField("Item1").GetValue(refreshed);
        Check(refreshedAccess != "Relogin", "refresh token accepted");
        Launch("MsLoginStep2", refreshedAccess);
        Console.WriteLine("PASS: Refresh token login and renewed Xbox authentication.");
        File.WriteAllText(Path.Combine(work, "interactive-result.json"), new JObject(new JProperty("client_id", ModMicrosoftAuth.MsGetClientId()), new JProperty("microsoft", true), new JProperty("xbox", true), new JProperty("minecraft", true), new JProperty("entitlement", true), new JProperty("profile", true), new JProperty("refresh", true), new JProperty("tested_at_utc", DateTime.UtcNow.ToString("o"))).ToString());
    }
}
