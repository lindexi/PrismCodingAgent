using System.Threading;
using System.Threading.Tasks;

namespace PrismCodingAgent.CodingShell.Services.Integrations.WindowsSandbox;

internal interface IWindowsSandboxConnectionTester
{
    Task<WindowsSandboxConnectionTestResult> TestAsync
    (
        string toolPath,
        string serverAddress,
        CancellationToken cancellationToken = default
    );
}