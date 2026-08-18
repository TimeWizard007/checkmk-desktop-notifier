namespace CheckmkDesktopNotifier.Core.Acknowledgements;

/// <summary>
/// Per-process Take capability. A 403 from Checkmk disables Take until the next successful
/// connection apply/reset. Monitoring remains read-only.
/// </summary>
public sealed class TakeSessionState
{
    private readonly object _gate = new();
    private bool _acknowledgeForbidden;

    public bool AcknowledgeForbidden
    {
        get
        {
            lock (_gate)
            {
                return _acknowledgeForbidden;
            }
        }
    }

    public event EventHandler? Changed;

    public void MarkAcknowledgeForbidden()
    {
        lock (_gate)
        {
            if (_acknowledgeForbidden)
            {
                return;
            }

            _acknowledgeForbidden = true;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Reset()
    {
        lock (_gate)
        {
            if (!_acknowledgeForbidden)
            {
                return;
            }

            _acknowledgeForbidden = false;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
