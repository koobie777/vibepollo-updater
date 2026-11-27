using System.Text.Json;

namespace VibepolloUi;

static class SelfCheck
{
    public static int Run()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("Vibepollo Updater self-check");
        var fails = 0;
        fails += Step("script", () =>
        {
            var script = Engine.FindScript();
            return script is null ? "FAIL missing" : "PASS " + Path.GetFileName(script);
        });
        fails += Step("offline", () =>
        {
            var (code, stdout, stderr) = Engine.Run(new[] { "--self-check" }, 15000);
            if (code != 0)
                return "FAIL exit " + code + " " + Brief(stderr, stdout);
            using var doc = JsonDocument.Parse(stdout);
            var root = doc.RootElement;
            if (!root.TryGetProperty("ok", out var ok) || !ok.GetBoolean())
                return "FAIL not ok";
            if (root.GetProperty("schema").GetString() != "vibepollo.updater.self.v1")
                return "FAIL schema";
            return "PASS version=" + root.GetProperty("version").GetString()
                + " platform=" + root.GetProperty("platform").GetString()
                + " supported=" + root.GetProperty("supported").GetBoolean();
        });
        Console.WriteLine(fails == 0 ? "SELF-CHECK OK" : "SELF-CHECK FAILED " + fails);
        return fails == 0 ? 0 : 1;
    }

    static int Step(string name, Func<string> fn)
    {
        try
        {
            var msg = fn();
            var ok = msg.StartsWith("PASS", StringComparison.Ordinal);
            Console.WriteLine((ok ? "PASS" : "FAIL") + "  " + name + "  " + (msg.Length > 5 ? msg[5..] : msg));
            return ok ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine("FAIL  " + name + "  " + ex.Message);
            return 1;
        }
    }

    static string Brief(string stderr, string stdout)
    {
        var text = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
        text = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return text.Length > 180 ? text[..180] : text;
    }
}
