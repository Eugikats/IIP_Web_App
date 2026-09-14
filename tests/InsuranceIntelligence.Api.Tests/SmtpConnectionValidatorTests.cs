using System.Net;
using System.Net.Sockets;
using System.Text;
using InsuranceIntelligence.Infrastructure.Communications;

namespace InsuranceIntelligence.Api.Tests;

/// <summary>
/// Exercises SmtpConnectionValidator's hand-rolled SMTP AUTH LOGIN handshake against a real TCP
/// loopback server scripted to speak just enough SMTP to answer it, the highest-risk code in the
/// email connection work, since it is not built on SmtpClient and was never exercised against a
/// real mail server in this session (blueprint §14 Phase 1 honest-status note).
/// </summary>
public sealed class SmtpConnectionValidatorTests
{
    [Fact]
    public async Task ValidateAsync_WithCorrectCredentials_Succeeds()
    {
        using var server = await FakeSmtpServer.StartAsync(expectedUsername: "mailer", expectedPassword: "correct-horse");
        var validator = new SmtpConnectionValidator();

        var result = await validator.ValidateAsync(Settings(server.Port, "mailer", "correct-horse"));

        Assert.True(result.Success, result.SafeError);
        await server.WaitForCompletionAsync();
    }

    [Fact]
    public async Task ValidateAsync_WithWrongPassword_ReportsRejectedCredentials()
    {
        using var server = await FakeSmtpServer.StartAsync(expectedUsername: "mailer", expectedPassword: "correct-horse");
        var validator = new SmtpConnectionValidator();

        var result = await validator.ValidateAsync(Settings(server.Port, "mailer", "wrong-password"));

        Assert.False(result.Success);
        Assert.Contains("rejected these credentials", result.SafeError);
        await server.WaitForCompletionAsync();
    }

    [Fact]
    public async Task ValidateAsync_WithMissingFields_FailsWithoutOpeningAConnection()
    {
        var validator = new SmtpConnectionValidator();

        var result = await validator.ValidateAsync(new Dictionary<string, string> { ["host"] = "smtp.example.com" });

        Assert.False(result.Success);
        Assert.Contains("port", result.SafeError, StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string> Settings(int port, string username, string password) => new()
    {
        ["host"] = "127.0.0.1",
        ["port"] = port.ToString(),
        ["username"] = username,
        ["password"] = password,
        ["fromAddress"] = "claims@example.ug",
        ["useSsl"] = "false", // plaintext loopback, the validator's non-TLS branch
    };

    /// <summary>Speaks just enough SMTP (greeting, EHLO, AUTH LOGIN) to answer the validator,
    /// not a real mail server, only a script of the exact exchange it performs.</summary>
    private sealed class FakeSmtpServer : IDisposable
    {
        private readonly TcpListener listener;
        private readonly Task serverTask;

        private FakeSmtpServer(TcpListener listener, Task serverTask)
        {
            this.listener = listener;
            this.serverTask = serverTask;
        }

        public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

        public static async Task<FakeSmtpServer> StartAsync(string expectedUsername, string expectedPassword)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var serverTask = RunAsync(listener, expectedUsername, expectedPassword);
            await Task.Yield();
            return new FakeSmtpServer(listener, serverTask);
        }

        public Task WaitForCompletionAsync() => serverTask.WaitAsync(TimeSpan.FromSeconds(5));

        private static async Task RunAsync(TcpListener listener, string expectedUsername, string expectedPassword)
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, leaveOpen: true);
            using var writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true, NewLine = "\r\n" };

            await writer.WriteLineAsync("220 fake-smtp.local ESMTP ready");
            await reader.ReadLineAsync(); // EHLO
            await writer.WriteLineAsync("250 fake-smtp.local greets you");
            await reader.ReadLineAsync(); // AUTH LOGIN
            await writer.WriteLineAsync("334 VXNlcm5hbWU6"); // "Username:"
            var usernameLine = await reader.ReadLineAsync();
            await writer.WriteLineAsync("334 UGFzc3dvcmQ6"); // "Password:"
            var passwordLine = await reader.ReadLineAsync();

            var username = Encoding.UTF8.GetString(Convert.FromBase64String(usernameLine ?? ""));
            var password = Encoding.UTF8.GetString(Convert.FromBase64String(passwordLine ?? ""));

            await writer.WriteLineAsync(
                username == expectedUsername && password == expectedPassword
                    ? "235 2.7.0 Authentication successful"
                    : "535 5.7.8 Authentication credentials invalid");

            await reader.ReadLineAsync(); // QUIT, if the client sends it before closing
        }

        public void Dispose() => listener.Stop();
    }
}
