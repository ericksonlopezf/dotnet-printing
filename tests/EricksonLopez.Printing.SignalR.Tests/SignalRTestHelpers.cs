// Copyright © Erickson Lopez. MIT License.
using System.Threading;

namespace EricksonLopez.Printing.SignalR.Tests;

internal sealed class DisallowingSynchronizationContext : SynchronizationContext
{
    public bool WasViolated { get; private set; }

    public override void Post(SendOrPostCallback d, object? state)
    {
        WasViolated = true;
        d(state);
    }

    public override void Send(SendOrPostCallback d, object? state)
    {
        WasViolated = true;
        d(state);
    }
}
