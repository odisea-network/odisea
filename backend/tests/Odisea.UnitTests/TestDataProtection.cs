using Microsoft.AspNetCore.DataProtection;

namespace Odisea.UnitTests;

/// One provider for the whole test process. EF caches the BookingDbContext model per
/// context type, and with it the protector captured by the document-number converter,
/// so every context in the run must share the provider that built the model.
public static class TestDataProtection
{
    public static readonly IDataProtectionProvider Provider = new EphemeralDataProtectionProvider();
}
